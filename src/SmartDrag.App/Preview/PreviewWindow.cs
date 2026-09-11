using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Interop;
using System.Windows.Input;
using System.Windows.Data;
using System.IO;
using System.Globalization;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using SmartDrag.Actions;
using SmartDrag.Core.Actions;
using SmartDrag.Core.Completion;
using SmartDrag.Core.Jobs;
using SmartDrag.Core.Output;
using SmartDrag.Core.Payload;
using SmartDrag.Core.Primitives;
using SmartDrag.Core.Settings;
using SmartDrag.Imaging;
using SmartDrag.Infrastructure;
using SmartDrag.Infrastructure.Recovery;
using SmartDrag.Infrastructure.Preferences;
using SmartDrag.Overlay;
using SmartDrag.Orchestration;
using SmartDrag.Presentation;
using SmartDrag.Runtime;
using SmartDrag.Windows.Artifacts;
using SmartDrag.Windows.Completion;
using SmartDrag.Windows.Imaging;

namespace SmartDrag.App.Preview;

/// <summary>
/// Interactive visual/functional preview. It intentionally uses an in-window WPF drop surface instead of global
/// WinEvent/OLE activation, so it is safe to iterate on UI and image actions while G1/G2 evidence is pending.
/// </summary>
public sealed class PreviewWindow : Window
{
    private enum PreviewLanguage
    {
        Russian,
        English
    }

    private readonly WpfOverlayService _overlay;
    private readonly WindowsWicImageCodec _codec;
    private readonly ImageSafetyLimits _safetyLimits;
    private readonly IJobQueue _jobQueue;
    private readonly CompletionCoordinator _completionCoordinator;
    private readonly MvpPresentationCoordinator _presentation;
    private readonly DragInteractionCoordinator _interaction;
    private readonly AppCapabilities _capabilities = new()
    {
        ImagingAvailable = true,
        WebpEncodingAvailable = false
    };
    private readonly UserSettings _settings = new();
    private readonly PreviewPreferencesStore _preferencesStore = PreviewPreferencesStore.CreateDefault();
    private readonly PreviewHistoryStore _historyStore = PreviewHistoryStore.CreateDefault();
    private readonly List<PreviewHistoryEntry> _persistedHistory = new();
    private readonly Border _dropZone;
    private readonly Image _thumbnail;
    private readonly TextBlock _fileName;
    private readonly TextBlock _metadata;
    private readonly TextBlock _status;
    private readonly ProgressBar _progress;
    private readonly Button _cancelButton;
    private readonly TextBlock _result;
    private readonly WrapPanel _resultActions;
    private readonly Button _copyButton;
    private readonly Button _openButton;
    private readonly Button _deleteButton;
    private readonly Button _dismissButton;
    private readonly Button _clearHistoryButton;
    private readonly ListBox _historyList;
    private readonly TextBlock _safetyProfile;
    private readonly TextBlock _capabilityHint;
    private readonly Button _selfCheckButton;
    private readonly Button _openSelfCheckReportButton;
    private readonly Button _resetButton;
    private readonly Button _chooseButton;
    private readonly Button _languageButton;
    private readonly Button _themeButton;
    private readonly Button _emptyIcon;
    private readonly TextBlock _emptyTitle;
    private readonly TextBlock _formatHint;
    private readonly TextBlock _headerSubtitle;
    private readonly TextBlock _headerTitle;
    private readonly TextBlock _historyTitle;
    private readonly TextBlock _historyHint;
    private readonly TextBlock _safetyTitle;
    private readonly TextBlock _safetyHint;
    private readonly TextBlock _howTitle;
    private readonly TextBlock _footer;
    private readonly Border _surface;
    private readonly Border _historyCard;
    private readonly Border _safetyCard;
    private readonly Border _howItWorksCard;
    private readonly StackPanel _headerCopy;
    private readonly Grid _contentGrid;
    private readonly StackPanel _leftColumn;
    private readonly StackPanel _sidebar;
    private readonly TextBlock[] _stepTitles;
    private readonly TextBlock[] _stepDetails;
    private readonly PreviewPreferences _initialPreferences;
    private readonly IPreviewFilePicker _filePicker;
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private PreviewLanguage _language = PreviewLanguage.Russian;
    private bool _darkTheme;
    private string? _outputPath;
    private string? _currentPath;
    private PayloadQualificationResult? _authoritativeQualification;
    private JobId? _activeJobId;
    private JobId? _activeCompletionJobId;
    private bool _inputBusy;
    private bool _pickerBusy;
    private bool _selfCheckRunning;
    private bool _busy;
    private bool _dropHover;
    private bool _runtimeDisposed;
    private bool _closing;
    private bool _compactLayout;
    private CancellationTokenSource? _inputCancellation;
    private DateTimeOffset? _historyClearedAt;
    private string? _selfCheckReportPath;

    public PreviewWindow(IPreviewFilePicker? filePicker = null)
    {
        var savedPreferences = _preferencesStore.Load();
        _initialPreferences = savedPreferences;
        _language = string.Equals(savedPreferences.Language, "en", StringComparison.OrdinalIgnoreCase)
            ? PreviewLanguage.English
            : PreviewLanguage.Russian;
        _darkTheme = savedPreferences.DarkTheme;
        var historyState = _historyStore.LoadState();
        _historyClearedAt = historyState.ClearedAt;
        _persistedHistory.AddRange(historyState.Entries);
        Title = "SmartDrag · Preview";
        Width = PreviewWindowPlacementPolicy.DefaultWidth;
        Height = PreviewWindowPlacementPolicy.DefaultHeight;
        MinWidth = PreviewWindowPlacementPolicy.MinimumWidth;
        MinHeight = PreviewWindowPlacementPolicy.MinimumHeight;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        UseLayoutRounding = true;
        SnapsToDevicePixels = true;
        Background = new SolidColorBrush(Color.FromRgb(245, 245, 247));
        Foreground = new SolidColorBrush(Color.FromRgb(29, 29, 31));
        FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI");
        TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
        TextOptions.SetTextRenderingMode(this, TextRenderingMode.ClearType);
        AllowDrop = true;

        _overlay = new WpfOverlayService(Dispatcher);
        _overlay.ActionInvoked += OnActionInvoked;
        _codec = new WindowsWicImageCodec();
        _safetyLimits = new ImageSafetyLimits
        {
            MaxSourceBytes = 100L * 1024 * 1024,
            MaxDecodedPixels = 64L * 1024 * 1024,
            MaxDimension = 12_000
        };
        var guardedProcessor = new GuardedImageProcessor(_codec, _codec, _safetyLimits);
        var artifactIdentity = new WindowsGeneratedArtifactIdentityService();
        var outputManager = new PhysicalOutputManager(
            new InMemoryOutputRecoveryJournal(),
            artifactIdentity);
        var handlers = new IActionHandler[]
        {
            new CompressImageActionHandler(guardedProcessor, outputManager),
            new ConvertImageToWebPActionHandler(guardedProcessor, outputManager),
            new RemoveImageMetadataActionHandler(guardedProcessor, outputManager)
        };
        var registry = new ActionRegistry(BuiltInActionDefinitions.Mvp);
        _jobQueue = new SequentialJobQueue(new ActionExecutor(handlers));
        var completionOwnerHwnd = new WindowInteropHelper(this).EnsureHandle();
        var completionCommands = new CompletionCommandExecutor(
            _jobQueue,
            new WindowsCompletionPlatformService(completionOwnerHwnd),
            artifactIdentity);
        _completionCoordinator = new CompletionCoordinator(_jobQueue, completionCommands.Capabilities);
        _presentation = new MvpPresentationCoordinator(
            _jobQueue,
            _completionCoordinator,
            completionCommands,
            registry);
        _interaction = new DragInteractionCoordinator(
            new DragWorkflowOrchestrator(registry),
            new CommittedActionDispatcher(_jobQueue),
            _overlay);
        _presentation.SnapshotChanged += OnPresentationSnapshotChanged;

        _fileName = new TextBlock
        {
            Text = "Drop one JPEG or PNG here",
            FontSize = 18,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(29, 29, 31)),
            HorizontalAlignment = HorizontalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        _thumbnail = new Image
        {
            Width = 184,
            Height = 112,
            Stretch = Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 12),
            Visibility = Visibility.Collapsed
        };
        _metadata = new TextBlock
        {
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.FromRgb(110, 110, 115)),
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 4, 0, 0)
        };
        _status = new TextBlock
        {
            Text = "Local-only preview · source files are never overwritten",
            FontSize = 13,
            Foreground = new SolidColorBrush(Color.FromRgb(110, 110, 115)),
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 8, 0, 0)
        };
        _progress = new ProgressBar
        {
            Height = 5,
            IsIndeterminate = true,
            Visibility = Visibility.Collapsed,
            Foreground = new SolidColorBrush(Color.FromRgb(0, 122, 255)),
            Background = new SolidColorBrush(Color.FromRgb(229, 229, 234)),
            Margin = new Thickness(0, 18, 0, 0)
        };
        _cancelButton = new Button
        {
            Content = "Cancel",
            Height = 30,
            Width = 100,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 10, 0, 0),
            Padding = new Thickness(12, 0, 12, 0),
            Foreground = new SolidColorBrush(Color.FromRgb(255, 59, 48)),
            Background = new SolidColorBrush(Color.FromRgb(255, 242, 241)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(255, 205, 200)),
            BorderThickness = new Thickness(1),
            Template = CreateRoundedButtonTemplate(),
            Cursor = System.Windows.Input.Cursors.Hand,
            Visibility = Visibility.Collapsed
        };
        _cancelButton.Click += OnCancelClicked;
        _result = new TextBlock
        {
            FontSize = 13,
            Foreground = new SolidColorBrush(Color.FromRgb(52, 199, 89)),
            TextWrapping = TextWrapping.Wrap,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 14, 0, 0)
        };
        _resultActions = new WrapPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 12, 0, 0),
            Visibility = Visibility.Collapsed
        };
        _copyButton = CreateResultButton("Copy path", CopyOutputPath);
        _openButton = CreateResultButton("Open folder", OpenOutputFolder);
        _deleteButton = CreateResultButton("Delete output", DeleteOutput);
        _dismissButton = CreateResultButton("Dismiss", DismissCompletion);
        _clearHistoryButton = CreateResultButton("Clear history", ClearHistory);
        _clearHistoryButton.MinWidth = 110;
        _clearHistoryButton.Height = 28;
        _clearHistoryButton.Margin = new Thickness(0, 8, 0, 0);
        _clearHistoryButton.HorizontalAlignment = HorizontalAlignment.Right;
        _resultActions.Children.Add(_copyButton);
        _resultActions.Children.Add(_openButton);
        _resultActions.Children.Add(_deleteButton);
        _resultActions.Children.Add(_dismissButton);

        SetAutomationName(_status, "Current status");
        System.Windows.Automation.AutomationProperties.SetLiveSetting(
            _status,
            System.Windows.Automation.AutomationLiveSetting.Polite);
        SetAutomationName(_result, "Operation result");
        System.Windows.Automation.AutomationProperties.SetLiveSetting(
            _result,
            System.Windows.Automation.AutomationLiveSetting.Polite);
        SetAutomationName(_progress, "Processing progress");
        System.Windows.Automation.AutomationProperties.SetLiveSetting(
            _progress,
            System.Windows.Automation.AutomationLiveSetting.Polite);

        _historyList = new ListBox
        {
            Height = 116,
            Margin = new Thickness(0, 18, 0, 0),
            Background = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(210, 210, 215)),
            BorderThickness = new Thickness(1),
            Foreground = new SolidColorBrush(Color.FromRgb(29, 29, 31)),
            FontSize = 12,
            Padding = new Thickness(8)
        };
        SetAutomationName(_historyList, "Recent operations");
        _safetyProfile = new TextBlock
        {
            Text = BuildSafetyProfile(),
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.FromRgb(110, 110, 115)),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 12, 0, 0)
        };
        _capabilityHint = new TextBlock
        {
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.FromRgb(184, 112, 0)),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 8, 0, 0)
        };
        _selfCheckButton = new Button
        {
            Content = "Run self-check",
            Height = 30,
            Width = 142,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 10, 0, 0),
            Padding = new Thickness(12, 0, 12, 0),
            Foreground = Brushes.White,
            Background = new SolidColorBrush(Color.FromRgb(0, 122, 255)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0, 122, 255)),
            BorderThickness = new Thickness(1),
            Template = CreateRoundedButtonTemplate(),
            Cursor = System.Windows.Input.Cursors.Hand
        };
        _selfCheckButton.Click += OnSelfCheckClicked;
        _openSelfCheckReportButton = CreateResultButton("Open report folder", OpenSelfCheckReportFolder);
        _openSelfCheckReportButton.MinWidth = 142;
        _openSelfCheckReportButton.Margin = new Thickness(4, 8, 4, 0);
        _openSelfCheckReportButton.Visibility = Visibility.Collapsed;
        _resetButton = CreateResultButton("Reset local data", OnResetLocalDataClicked);
        _resetButton.MinWidth = 160;
        _resetButton.Margin = new Thickness(4, 10, 4, 0);
        _languageButton = CreateToolbarButton("EN", OnLanguageButtonClicked, "Switch interface language");
        _themeButton = CreateToolbarButton("Dark", OnThemeButtonClicked, "Switch between light and dark appearance");
        _chooseButton = new Button
        {
            Content = "Choose image",
            Height = 36,
            MinWidth = 132,
            IsDefault = true,
            Padding = new Thickness(16, 0, 16, 0),
            Foreground = Brushes.White,
            Background = new SolidColorBrush(Color.FromRgb(0, 122, 255)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0, 122, 255)),
            BorderThickness = new Thickness(1),
            Template = CreateRoundedButtonTemplate(),
            Cursor = System.Windows.Input.Cursors.Hand,
            ToolTip = "Select one JPEG or PNG without dragging"
        };
        _chooseButton.Click += OnChooseButtonClicked;
        _emptyIcon = new Button
        {
            Width = 72,
            Height = 72,
            Background = new SolidColorBrush(Color.FromRgb(242, 247, 255)),
            BorderBrush = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0),
            Template = CreateCircleButtonTemplate(),
            Cursor = System.Windows.Input.Cursors.Hand,
            ToolTip = "Choose one JPEG or PNG",
            Content = "+",
            FontSize = 34,
            FontWeight = FontWeights.Light,
            Foreground = new SolidColorBrush(Color.FromRgb(0, 122, 255)),
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        _emptyIcon.Click += OnChooseButtonClicked;
        SetAutomationHelpText(_emptyIcon, T("Opens the image picker", "Открывает выбор изображения"));
        _emptyTitle = new TextBlock
        {
            Text = "Add an image to get started",
            FontSize = 22,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(29, 29, 31)),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 10, 0, 0)
        };
        _formatHint = new TextBlock
        {
            Text = "JPEG or PNG · one file at a time",
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.FromRgb(110, 110, 115)),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 7, 0, 0)
        };

        _dropZone = new Border
        {
            BorderBrush = new SolidColorBrush(Color.FromRgb(210, 210, 215)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(28),
            Background = Brushes.White,
            Padding = new Thickness(30),
            Margin = new Thickness(0),
            MinHeight = 350,
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = Colors.Black,
                Opacity = 0.10,
                BlurRadius = 24,
                ShadowDepth = 4
            },
            Child = new StackPanel
            {
                Children =
                {
                    _thumbnail,
                    _emptyIcon,
                    _emptyTitle,
                    _fileName,
                    _formatHint,
                    _metadata,
                    _status,
                    _progress,
                    _cancelButton,
                    _result,
                    _resultActions
                }
            }
        };
        _dropZone.AllowDrop = true;
        SetAutomationName(_dropZone, "Image drop area");
        _dropZone.DragOver += OnDragOver;
        _dropZone.DragEnter += OnDragEnter;
        _dropZone.DragLeave += OnDragLeave;
        _dropZone.Drop += OnDrop;

        var cardShadow = new System.Windows.Media.Effects.DropShadowEffect
        {
            Color = Colors.Black,
            Opacity = 0.08,
            BlurRadius = 20,
            ShadowDepth = 3
        };
        _historyTitle = new TextBlock
        {
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(29, 29, 31))
        };
        _historyHint = new TextBlock
        {
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.FromRgb(110, 110, 115)),
            Margin = new Thickness(0, 4, 0, 0)
        };
        _historyCard = new Border
        {
            Background = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(210, 210, 215)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(20),
            Padding = new Thickness(20),
            Effect = cardShadow,
            Child = new StackPanel
            {
                Children =
                {
                    _historyTitle,
                    _historyHint,
                    _clearHistoryButton,
                    _historyList
                }
            }
        };
        _safetyTitle = new TextBlock
        {
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(29, 29, 31))
        };
        _safetyHint = new TextBlock
        {
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.FromRgb(110, 110, 115)),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 4, 0, 0)
        };
        _safetyCard = new Border
        {
            Background = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(210, 210, 215)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(20),
            Padding = new Thickness(20),
            Margin = new Thickness(0, 16, 0, 0),
            Effect = cardShadow,
            Child = new StackPanel
            {
                Children =
                {
                    _safetyTitle,
                    _safetyHint,
                    _safetyProfile,
                    _capabilityHint,
                    _selfCheckButton,
                    _openSelfCheckReportButton,
                    _resetButton
                }
            }
        };
        _howTitle = new TextBlock
        {
            FontSize = 15,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(29, 29, 31))
        };
        _stepTitles = new TextBlock[3];
        _stepDetails = new TextBlock[3];
        var step1 = CreateStep("1", "Add", "Drop or select one image", 0, _stepTitles, _stepDetails);
        var step2 = CreateStep("2", "Choose", "Pick one action", 1, _stepTitles, _stepDetails);
        var step3 = CreateStep("3", "Get", "A new file is created", 2, _stepTitles, _stepDetails);
        _howItWorksCard = new Border
        {
            Background = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(210, 210, 215)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(20),
            Padding = new Thickness(18, 14, 18, 14),
            Margin = new Thickness(0, 16, 0, 0),
            Effect = cardShadow,
            Child = new StackPanel
            {
                Children =
                {
                    _howTitle,
                    new Grid
                    {
                        Margin = new Thickness(0, 12, 0, 0),
                        ColumnDefinitions =
                        {
                            new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                            new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                            new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }
                        },
                        Children =
                        {
                            step1,
                            step2,
                            step3
                        }
                    }
                }
            }
        };
        _leftColumn = new StackPanel { Children = { _dropZone, _howItWorksCard } };
        _sidebar = new StackPanel { Children = { _historyCard, _safetyCard }, Margin = new Thickness(18, 0, 0, 0) };
        _contentGrid = new Grid
        {
            Margin = new Thickness(0, 28, 0, 0),
            VerticalAlignment = VerticalAlignment.Top
        };
        Grid.SetColumn(_leftColumn, 0);
        Grid.SetColumn(_sidebar, 1);
        _contentGrid.Children.Add(_leftColumn);
        _contentGrid.Children.Add(_sidebar);

        var rootGrid = new Grid();
        rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var header = new Grid();
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _headerSubtitle = new TextBlock
        {
            FontSize = 16,
            Foreground = new SolidColorBrush(Color.FromRgb(110, 110, 115)),
            Margin = new Thickness(0, 5, 0, 0)
        };
        _headerTitle = new TextBlock
        {
            Text = "SmartDrag",
            FontSize = 36,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(29, 29, 31))
        };
        var titleBlock = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                _headerTitle,
                _headerSubtitle
            }
        };
        _headerCopy = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { CreateLogoMark(), titleBlock }
        };
        Grid.SetColumn(_headerCopy, 0);
        var headerActions = new StackPanel { Orientation = Orientation.Horizontal };
        headerActions.Children.Add(_languageButton);
        headerActions.Children.Add(_themeButton);
        headerActions.Children.Add(_chooseButton);
        Grid.SetColumn(headerActions, 1);
        header.Children.Add(_headerCopy);
        header.Children.Add(headerActions);
        _footer = new TextBlock
        {
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.FromRgb(110, 110, 115)),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 18, 0, 0)
        };
        Grid.SetRow(header, 0);
        Grid.SetRow(_contentGrid, 1);
        Grid.SetRow(_footer, 2);
        rootGrid.Children.Add(header);
        var contentScroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            CanContentScroll = false,
            Focusable = false,
            Padding = new Thickness(0, 0, 8, 0),
            Content = _contentGrid
        };
        Grid.SetRow(contentScroll, 1);
        rootGrid.Children.Add(contentScroll);
        rootGrid.Children.Add(_footer);
        _surface = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(245, 245, 247)),
            Padding = new Thickness(38),
            Child = rootGrid
        };
        Content = _surface;
        RestoreWindowPlacement(_initialPreferences);
        _filePicker = filePicker ?? new WpfPreviewFilePicker(this);
        ConfigureKeyboardNavigation();
        ApplyLanguage();
        ApplyTheme();
        SizeChanged += (_, _) => ApplyResponsiveLayout();

        Closed += (_, _) =>
        {
            _closing = true;
            _lifetimeCancellation.Cancel();
            _inputCancellation?.Cancel();
            _presentation.SnapshotChanged -= OnPresentationSnapshotChanged;
            _presentation.Dispose();
            _completionCoordinator.Dispose();
            SafeTaskRunner.FireAndForget(DisposeQueueAsync, "preview-queue-dispose");
            _overlay.Dispose();
            _lifetimeCancellation.Dispose();
        };
        Closing += (_, _) =>
        {
            _closing = true;
            _lifetimeCancellation.Cancel();
            _inputCancellation?.Cancel();
            SaveWindowPlacement();
            PreviewProcessLog.TryAppend(PreviewProcessLog.DefaultPath, "preview-closing", "user-requested");
        };
        PreviewKeyDown += OnPreviewKeyDown;
        Loaded += (_, _) =>
        {
            ApplyResponsiveLayout();
            ResetPreview();
        };
    }

    private void ApplyResponsiveLayout()
    {
        var compact = ActualWidth > 0 && ActualWidth < 1020;
        if (_compactLayout == compact && _contentGrid.ColumnDefinitions.Count > 0)
        {
            return;
        }

        _compactLayout = compact;
        _contentGrid.ColumnDefinitions.Clear();
        _contentGrid.RowDefinitions.Clear();

        if (compact)
        {
            _contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            _contentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            _contentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid.SetColumn(_leftColumn, 0);
            Grid.SetColumn(_sidebar, 0);
            Grid.SetRow(_leftColumn, 0);
            Grid.SetRow(_sidebar, 1);
            _sidebar.Margin = new Thickness(0, 16, 0, 0);
            return;
        }

        _contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.45, GridUnitType.Star) });
        _contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.95, GridUnitType.Star) });
        _contentGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(_leftColumn, 0);
        Grid.SetColumn(_sidebar, 1);
        Grid.SetRow(_leftColumn, 0);
        Grid.SetRow(_sidebar, 0);
        _sidebar.Margin = new Thickness(18, 0, 0, 0);
    }

    private void ConfigureKeyboardNavigation()
    {
        KeyboardNavigation.SetTabNavigation(this, KeyboardNavigationMode.Continue);
        var controls = new Control[]
        {
            _languageButton,
            _themeButton,
            _chooseButton,
            _emptyIcon,
            _copyButton,
            _openButton,
            _deleteButton,
            _dismissButton,
            _clearHistoryButton,
            _historyList,
            _selfCheckButton,
            _openSelfCheckReportButton,
            _resetButton,
            _cancelButton
        };
        for (var index = 0; index < controls.Length; index++)
        {
            KeyboardNavigation.SetTabIndex(controls[index], index);
            controls[index].IsTabStop = true;
        }
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        var canAccept = PreviewDropPolicy.CanAccept(
            e.Data.GetDataPresent(DataFormats.FileDrop),
            _inputBusy,
            _pickerBusy,
            _interaction.ActiveDragSessionId is not null,
            _activeJobId is not null || _busy);
        e.Effects = canAccept ? DragDropEffects.Copy : DragDropEffects.None;
        if (canAccept)
        {
            SetDropHover(true);
        }
        else
        {
            SetDropHover(false);
        }
        e.Handled = true;
    }

    private void OnDragEnter(object sender, DragEventArgs e)
    {
        var canAccept = PreviewDropPolicy.CanAccept(
            e.Data.GetDataPresent(DataFormats.FileDrop),
            _inputBusy,
            _pickerBusy,
            _interaction.ActiveDragSessionId is not null,
            _activeJobId is not null || _busy);
        if (canAccept)
        {
            SetDropHover(true);
            _status.Text = T("Release to inspect the image", "Отпустите файл, чтобы проверить изображение");
            _status.Foreground = ToBrush(PreviewThemePalette.For(_darkTheme).Accent);
        }
        else
        {
            SetDropHover(false);
        }

        e.Handled = true;
    }

    private void OnDragLeave(object sender, DragEventArgs e)
    {
        SetDropHover(false);
        if (_currentPath is null && _activeJobId is null)
        {
            SetStatus(T("Local-only preview · source files are never overwritten", "Локальный предпросмотр · исходные файлы не изменяются"), error: false);
        }

        e.Handled = true;
    }

    private async void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        var keyboardState = new PreviewKeyboardState(
            HasActiveOperation: _activeJobId is not null,
            HasCompletion: _activeCompletionJobId is not null,
            HasDragSession: _interaction.ActiveDragSessionId is not null,
            CanDeleteOutput: _deleteButton.Visibility == Visibility.Visible && _deleteButton.IsEnabled);
        var intent = PreviewKeyboardPolicy.Resolve(
            ToPreviewKeyboardKey(e.Key),
            ToPreviewKeyboardModifiers(Keyboard.Modifiers),
            keyboardState);
        if (intent == PreviewKeyboardIntent.None)
        {
            return;
        }

        e.Handled = true;
        try
        {
            switch (intent)
            {
                case PreviewKeyboardIntent.CancelOperation:
                    if (_activeJobId is { } jobId && _presentation.TryCancelVisibleOperation(jobId))
                    {
                        _cancelButton.IsEnabled = false;
                        _status.Text = T("Cancelling…", "Отмена…");
                    }
                    return;
                case PreviewKeyboardIntent.DismissCompletion:
                    DismissCompletion(this, new RoutedEventArgs());
                    return;
                case PreviewKeyboardIntent.CancelDrag:
                    if (_interaction.ActiveDragSessionId is { } sessionId)
                    {
                        await _interaction.EndNativeDragAsync(sessionId, cancelled: true, cancellationToken: CancellationToken.None);
                    }
                    return;
                case PreviewKeyboardIntent.ResetPreview:
                    ResetPreview();
                    return;
                case PreviewKeyboardIntent.PasteFiles:
                    try
                    {
                        if (!Clipboard.ContainsFileDropList())
                        {
                            SetStatus(T("The clipboard does not contain a file", "В буфере обмена нет файла"), error: true);
                            return;
                        }

                        var paths = Clipboard.GetFileDropList().Cast<string>().ToArray();
                        await HandlePathsAsync(paths, PayloadEvidenceSource.OleDataObject, "Clipboard paste");
                    }
                    catch (ExternalException)
                    {
                        SetStatus(T("Clipboard is busy — try again", "Буфер обмена занят — попробуйте ещё раз"), error: true);
                    }
                    catch (InvalidOperationException)
                    {
                        SetStatus(T("The clipboard could not be read safely", "Не удалось безопасно прочитать буфер обмена"), error: true);
                    }
                    return;
                case PreviewKeyboardIntent.ChooseImage:
                    OnChooseButtonClicked(this, new RoutedEventArgs());
                    return;
                case PreviewKeyboardIntent.ToggleLanguage:
                    OnLanguageButtonClicked(this, new RoutedEventArgs());
                    return;
                case PreviewKeyboardIntent.ToggleTheme:
                    OnThemeButtonClicked(this, new RoutedEventArgs());
                    return;
                case PreviewKeyboardIntent.ClearHistory:
                    ClearHistory(this, new RoutedEventArgs());
                    return;
                case PreviewKeyboardIntent.DeleteOutput:
                    DeleteOutput(this, new RoutedEventArgs());
                    return;
            }
        }
        catch (Exception ex)
        {
            SetStatus(T("The keyboard action could not be completed safely", "Действие с клавиатуры безопасно завершилось ошибкой"), error: true);
            PreviewProcessLog.TryAppend(PreviewProcessLog.DefaultPath, "keyboard-callback-failed", ex.ToString());
        }
    }

    private static PreviewKeyboardKey ToPreviewKeyboardKey(Key key) => key switch
    {
        Key.Escape => PreviewKeyboardKey.Escape,
        Key.Delete => PreviewKeyboardKey.Delete,
        Key.V => PreviewKeyboardKey.V,
        Key.O => PreviewKeyboardKey.O,
        Key.L => PreviewKeyboardKey.L,
        Key.T => PreviewKeyboardKey.T,
        Key.H => PreviewKeyboardKey.H,
        _ => PreviewKeyboardKey.Other
    };

    private static PreviewKeyboardModifiers ToPreviewKeyboardModifiers(ModifierKeys modifiers)
    {
        var result = PreviewKeyboardModifiers.None;
        if (modifiers.HasFlag(ModifierKeys.Control))
        {
            result |= PreviewKeyboardModifiers.Control;
        }

        if (modifiers.HasFlag(ModifierKeys.Shift))
        {
            result |= PreviewKeyboardModifiers.Shift;
        }

        if (modifiers.HasFlag(ModifierKeys.Alt))
        {
            result |= PreviewKeyboardModifiers.Alt;
        }

        return result;
    }

    private async void OnDrop(object sender, DragEventArgs e)
    {
        try
        {
            e.Handled = true;
            SetDropHover(false);
            if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                return;
            }

            var paths = e.Data.GetData(DataFormats.FileDrop) as string[] ?? Array.Empty<string>();
            await HandlePathsAsync(paths, PayloadEvidenceSource.OleDataObject, "WPF preview drop");
        }
        catch (Exception ex)
        {
            SetStatus(T("The dropped file could not be read safely", "Не удалось безопасно прочитать перетащенный файл"), error: true);
            PreviewProcessLog.TryAppend(PreviewProcessLog.DefaultPath, "drop-callback-failed", ex.ToString());
        }
    }

    private async void OnChooseButtonClicked(object? sender, RoutedEventArgs e)
    {
        if (!PreviewFilePickerPolicy.CanPickImage(
                _inputBusy || _pickerBusy,
                _interaction.ActiveDragSessionId is not null,
                _activeJobId is not null || _busy))
        {
            SetStatus(T("Finish the current operation before choosing another image", "Сначала завершите текущую операцию"), error: true);
            return;
        }

        _pickerBusy = true;
        ApplyInputAvailability();
        string? selectedPath;
        try
        {
            selectedPath = _filePicker.PickImage(new PreviewFilePickerOptions
            {
                Title = T("Choose an image for SmartDrag", "Выберите изображение для SmartDrag"),
                Filter = T("JPEG or PNG image|*.jpg;*.jpeg;*.png|All files|*.*", "Изображение JPEG или PNG|*.jpg;*.jpeg;*.png|Все файлы|*.*"),
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)
            });
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or ExternalException)
        {
            SetStatus(T("The image picker could not be opened", "Не удалось открыть выбор изображения"), error: true);
            PreviewProcessLog.TryAppend(PreviewProcessLog.DefaultPath, "image-picker-failed", ex.ToString());
            return;
        }
        finally
        {
            _pickerBusy = false;
            ApplyInputAvailability();
        }

        if (IsClosing)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(selectedPath))
        {
            return;
        }

        await HandlePathsAsync(
            new[] { selectedPath },
            PayloadEvidenceSource.LocalFilePicker,
            "Local file picker");
    }

    private async Task HandlePathsAsync(
        IReadOnlyList<string> paths,
        PayloadEvidenceSource evidenceSource,
        string evidenceDescription)
    {
        var inputDecision = PreviewInputPolicy.Decide(
            _inputBusy,
            _interaction.ActiveDragSessionId is not null,
            _presentation.Current.Completion is not null,
            paths.Count);
        if (inputDecision == PreviewInputDecision.IgnoreBusy)
        {
            return;
        }

        _inputBusy = true;
        ApplyInputAvailability();
        CancellationTokenSource? inputCancellation = null;
        try
        {
        inputCancellation = CancellationTokenSource.CreateLinkedTokenSource(_lifetimeCancellation.Token);
        _inputCancellation = inputCancellation;
        var inputCancellationToken = inputCancellation.Token;
        if (inputDecision == PreviewInputDecision.FinishActiveDrag)
        {
            SetStatus(T("Choose an action from the current panel first", "Сначала выберите действие в панели"), error: true);
            return;
        }

        if (inputDecision == PreviewInputDecision.DismissVisibleCompletion
            && _presentation.Current.Completion is { } visibleCompletion)
        {
            var dismissed = await _presentation.ExecuteVisibleCompletionCommandAsync(
                visibleCompletion.JobId,
                CompletionCommand.Dismiss,
                CancellationToken.None);
            if (!dismissed.Success && _presentation.Current.Completion is not null)
            {
                SetStatus(T("Finish the current result first", "Сначала закройте текущий результат"), error: true);
                return;
            }
        }

        ClearSelectedImageState();
        _resultActions.Visibility = Visibility.Collapsed;
        _result.Text = string.Empty;

        if (inputDecision == PreviewInputDecision.RejectPayload || paths.Count != 1)
        {
            SetStatus(T("Drop exactly one file.", "Перетащите ровно один файл."), error: true);
            return;
        }

        var path = paths[0];
        var snapshot = new PhysicalFilePayloadSnapshotFactory().Create(
            paths,
            evidenceSource,
            evidenceDescription);
        var qualification = MvpPayloadQualifier.Qualify(snapshot);
        if (!qualification.IsAuthoritative || qualification.Payload is null)
        {
            _resultActions.Visibility = Visibility.Collapsed;
            await _overlay.HideAsync(SmartDrag.Core.Overlay.OverlayHideReason.Suppressed, CancellationToken.None);
            SetStatus(T("This preview accepts one existing JPEG or PNG file.", "Предпросмотр принимает один существующий JPEG или PNG."), error: true);
            _result.Text = LocalizePayloadRejection(qualification.Reason);
            return;
        }

        _progress.Visibility = Visibility.Visible;
        _status.Text = T("Inspecting image…", "Проверяем изображение…");
        _status.Foreground = SecondaryTextBrush();
        _metadata.Text = string.Empty;
        _result.Text = string.Empty;
        _resultActions.Visibility = Visibility.Collapsed;
        _thumbnail.Visibility = Visibility.Collapsed;
        _thumbnail.Source = null;

        ImageInspectionResult inspection;
        try
        {
            inspection = await _codec.InspectAsync(path, inputCancellationToken);
        }
        catch (OperationCanceledException) when (inputCancellation?.IsCancellationRequested == true)
        {
            return;
        }
        catch (Exception)
        {
            _progress.Visibility = Visibility.Collapsed;
            ClearSelectedImageState();
            await _overlay.HideAsync(SmartDrag.Core.Overlay.OverlayHideReason.Suppressed, CancellationToken.None);
            SetStatus(T("Image inspection failed safely", "Проверка изображения безопасно завершилась ошибкой"), error: true);
            _result.Text = T("No action was started.", "Действие не запущено.");
            return;
        }

        _progress.Visibility = Visibility.Collapsed;
        var guard = MvpImageExecutionGuard.Evaluate(inspection, _safetyLimits);
        if (!guard.Allowed)
        {
            ClearSelectedImageState();
            await _overlay.HideAsync(SmartDrag.Core.Overlay.OverlayHideReason.Suppressed, CancellationToken.None);
            SetStatus(T("This image cannot be processed in preview", "Это изображение нельзя обработать в предпросмотре"), error: true);
            _result.Text = PreviewFailurePolicy.ImageRejected(_language == PreviewLanguage.Russian);
            return;
        }

        _currentPath = path;
        _authoritativeQualification = qualification;
        _thumbnail.Source = TryLoadThumbnail(path);
        _thumbnail.Visibility = _thumbnail.Source is null ? Visibility.Collapsed : Visibility.Visible;
        SetEmptyState(false);
        _fileName.Text = Path.GetFileName(qualification.Payload.Files[0].FullPath);
        _metadata.Text = BuildMetadataSummary(inspection);
        _status.Text = T("Choose an action from the floating panel", "Выберите действие во всплывающей панели");
        _result.Text = string.Empty;
        _dropZone.BorderBrush = ToBrush(PreviewThemePalette.For(_darkTheme).Accent);

        var dragSessionId = Guid.NewGuid();
        var point = PointToScreen(new Point(Math.Max(0, (ActualWidth - 320) / 2), 235));
        try
        {
            var preparation = await _interaction.TryPresentAsync(
                dragSessionId,
                qualification,
                _capabilities,
                _settings,
                new SmartDrag.Core.Overlay.OverlayPlacement(point.X, point.Y, 320, 205),
                inputCancellationToken);
            if (!preparation.IsPrepared)
            {
                ClearSelectedImageState();
                SetStatus(T("No action is available for this file", "Для этого файла нет доступного действия"), error: true);
                _result.Text = PreviewFailurePolicy.ActionPanelUnavailable(_language == PreviewLanguage.Russian);
            }
        }
        catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested || inputCancellation?.IsCancellationRequested == true)
        {
            return;
        }
        catch (Exception)
        {
            ClearSelectedImageState();
            SetStatus(T("Could not show the action panel", "Не удалось показать панель действий"), error: true);
            _result.Text = T("No action was started.", "Действие не запущено.");
        }
        }
        catch (Exception)
        {
            ClearSelectedImageState();
            _resultActions.Visibility = Visibility.Collapsed;
            _progress.Visibility = Visibility.Collapsed;
            SetStatus(T("The file could not be read safely", "Не удалось безопасно прочитать файл"), error: true);
            _result.Text = T("No action was started.", "Действие не запущено.");
            SafeTaskRunner.FireAndForget(
                () => _overlay.HideAsync(SmartDrag.Core.Overlay.OverlayHideReason.Suppressed, CancellationToken.None),
                "preview-overlay-hide-after-input-error");
        }
        finally
        {
            if (ReferenceEquals(_inputCancellation, inputCancellation))
            {
                _inputCancellation = null;
            }

            inputCancellation?.Dispose();
            _inputBusy = false;
            ApplyInputAvailability();
        }
    }

    private async void OnActionInvoked(object? sender, OverlayActionInvokedEventArgs args)
    {
        if (_busy || _currentPath is null || _authoritativeQualification is null)
        {
            return;
        }

        _busy = true;
        ApplyInputAvailability();
        _resultActions.Visibility = Visibility.Collapsed;
        _result.Text = string.Empty;
        try
        {
            var dispatch = await _interaction.TryCommitMvpAsync(
                args.DragSessionId,
                args.ActionId,
                _authoritativeQualification,
                _capabilities,
                _settings,
                _lifetimeCancellation.Token);
            if (IsClosing)
            {
                return;
            }

            if (dispatch.Status == ActionDispatchStatus.Accepted)
            {
                _currentPath = null;
                _authoritativeQualification = null;
                _status.Text = T("Queued — preparing the result", "В очереди — готовим результат");
                _status.Foreground = SecondaryTextBrush();
            }
            else
            {
                _busy = false;
                ApplyInputAvailability();
                SetStatus(T("Action was not started", "Действие не запущено"), error: true);
                _result.Text = PreviewFailurePolicy.ActionNotStarted(_language == PreviewLanguage.Russian);
            }
        }
        catch (OperationCanceledException) when (IsClosing)
        {
            // Closing the preview is an expected cancellation boundary, not a failed action.
        }
        catch (Exception)
        {
            _busy = false;
            ApplyInputAvailability();
            _status.Text = T("Action failed safely", "Действие безопасно завершилось ошибкой");
            _result.Foreground = ErrorTextBrush();
            _result.Text = T("No source changes were made.", "Исходный файл не изменён.");
        }
    }

    private void OnPresentationSnapshotChanged(object? sender, PresentationSnapshotChangedEventArgs args)
    {
        if (_closing)
        {
            return;
        }

        if (!Dispatcher.CheckAccess())
        {
            SafeTaskRunner.FireAndForget(
                () => Dispatcher.BeginInvoke(() => RenderPresentation(args.Snapshot)).Task,
                "preview-render-dispatch");
            return;
        }

        RenderPresentation(args.Snapshot);
    }

    private void RenderPresentation(SmartDragPresentationSnapshot snapshot)
    {
        RefreshHistory();
        if (snapshot.Operation is { } operation)
        {
            ClearCompletionVisualState();
            _busy = false;
            _progress.Visibility = Visibility.Visible;
            _cancelButton.Visibility = operation.CanCancel ? Visibility.Visible : Visibility.Collapsed;
            _cancelButton.IsEnabled = operation.CanCancel;
            _activeJobId = operation.JobId;
            ApplyInputAvailability();
            _status.Foreground = SecondaryTextBrush();
            _status.Text = FormatOperationStatus(operation);
            return;
        }

        _progress.Visibility = Visibility.Collapsed;
        _cancelButton.Visibility = Visibility.Collapsed;
        _activeJobId = null;
        ApplyInputAvailability();
        _activeCompletionJobId = null;
        if (snapshot.Completion is not { } completion)
        {
            _busy = false;
            ClearCompletionVisualState();
            if (_currentPath is null)
            {
                SetEmptyState(true);
                _fileName.Text = T("Drop one JPEG or PNG here", "Перетащите сюда один JPEG или PNG");
                _status.Text = T("Ready for another file", "Готово для следующего файла");
                _status.Foreground = SecondaryTextBrush();
            }
            return;
        }

        _busy = false;
        _activeCompletionJobId = completion.JobId;
        _status.Text = LocalizeCompletionTitle(completion.Title);
        _status.Foreground = completion.Tone switch
        {
            PresentationTone.Error => ErrorTextBrush(),
            PresentationTone.Neutral => SecondaryTextBrush(),
            _ => SuccessTextBrush()
        };
        _result.Foreground = _status.Foreground;
        _result.Text = LocalizeCompletionDetail(completion.Detail);
        _outputPath = null;
        _resultActions.Visibility = Visibility.Collapsed;
        _copyButton.Visibility = Visibility.Collapsed;
        _openButton.Visibility = Visibility.Collapsed;
        _deleteButton.Visibility = Visibility.Collapsed;
        var outputPath = _jobQueue.TryGetSnapshot(completion.JobId, out var terminal)
            && terminal.OutputPaths.Count == 1
            ? terminal.OutputPaths[0]
            : null;
        var outputAvailable = !string.IsNullOrWhiteSpace(outputPath) && File.Exists(outputPath);
        var actionState = PreviewResultActionPolicy.Resolve(
            completion.Commands,
            outputAvailable,
            completion.OutputWasDeleted);
        _dismissButton.Visibility = actionState.ShowDismiss ? Visibility.Visible : Visibility.Collapsed;
        if (completion.OutputWasDeleted)
        {
            _result.Text = LocalizeCompletionDetail(completion.Detail);
            _resultActions.Visibility = actionState.ShowActionRow ? Visibility.Visible : Visibility.Collapsed;
        }
        else if (outputPath is not null)
        {
            if (!outputAvailable)
            {
                _result.Text = $"{T("Result unavailable", "Результат недоступен")}\n{LocalizeCompletionDetail(completion.Detail)}";
                _resultActions.Visibility = actionState.ShowActionRow ? Visibility.Visible : Visibility.Collapsed;
                return;
            }

            _outputPath = outputPath;
            _result.Text = $"{T("Created", "Создано")}: {Path.GetFileName(_outputPath)}\n{LocalizeCompletionDetail(completion.Detail)}";
            _resultActions.Visibility = actionState.ShowActionRow ? Visibility.Visible : Visibility.Collapsed;
            _copyButton.Visibility = actionState.ShowCopyPath ? Visibility.Visible : Visibility.Collapsed;
            _openButton.Visibility = actionState.ShowOpenFolder ? Visibility.Visible : Visibility.Collapsed;
            _deleteButton.Visibility = actionState.ShowDeleteOutput ? Visibility.Visible : Visibility.Collapsed;
        }
        else
        {
            _resultActions.Visibility = actionState.ShowActionRow ? Visibility.Visible : Visibility.Collapsed;
        }

        if (!string.IsNullOrWhiteSpace(completion.CommandError))
        {
            var failedCommand = completion.Commands.FirstOrDefault(command => command != CompletionCommand.Dismiss);
            var hasFailedCommand = completion.Commands.Any(command => command != CompletionCommand.Dismiss);
            var safeCommandError = PreviewCommandErrorPolicy.Localize(
                hasFailedCommand ? failedCommand : null,
                completion.CommandError,
                _language == PreviewLanguage.Russian);
            if (!string.IsNullOrWhiteSpace(safeCommandError))
            {
                _result.Text += $"\n{safeCommandError}";
            }
        }
    }

    private void RefreshHistory()
    {
        _historyList.Items.Clear();
        var snapshots = _jobQueue.GetSnapshots()
            .Where(snapshot => _historyClearedAt is null
                || (snapshot.FinishedAt ?? snapshot.EnqueuedAt) > _historyClearedAt.Value)
            .OrderByDescending(snapshot => snapshot.EnqueuedAt)
            .ThenByDescending(snapshot => snapshot.Id.Value)
            .ToArray();

        var deletedHistoryIds = _persistedHistory
            .Where(entry => entry.OutputDeleted)
            .Select(entry => entry.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var currentEntries = snapshots
            .Select(ToHistoryEntry)
            .Select(entry => deletedHistoryIds.Contains(entry.Id)
                ? entry with { OutputDeleted = true }
                : entry)
            .ToArray();
        var currentIds = currentEntries
            .Select(entry => entry.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var mergedPersisted = currentEntries
            .Where(entry => IsTerminalState(entry.State))
            .Concat(_persistedHistory.Where(entry => !currentIds.Contains(entry.Id)))
            .GroupBy(entry => entry.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(entry => entry.OccurredAt).First())
            .OrderByDescending(entry => entry.OccurredAt)
            .Take(PreviewHistoryStore.MaximumEntries)
            .ToArray();
        _persistedHistory.Clear();
        _persistedHistory.AddRange(mergedPersisted);
        _historyStore.TrySave(_persistedHistory, _historyClearedAt);

        var displayEntries = currentEntries
            .Concat(_persistedHistory.Where(entry => !currentIds.Contains(entry.Id)))
            .GroupBy(entry => entry.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(entry => entry.OccurredAt).First())
            .OrderByDescending(entry => entry.OccurredAt)
            .Take(PreviewHistoryStore.MaximumEntries)
            .ToArray();
        if (displayEntries.Length == 0)
        {
            _historyList.Items.Add(new TextBlock
            {
                Text = T("No operations yet", "Операций пока нет"),
                Foreground = SecondaryTextBrush(),
                FontSize = 11,
                Margin = new Thickness(3, 4, 3, 4)
            });
            return;
        }

        foreach (var entry in displayEntries)
            _historyList.Items.Add(CreateHistoryRow(entry));
    }

    private PreviewHistoryEntry ToHistoryEntry(JobSnapshot snapshot)
    {
        var source = snapshot.InputPaths.Count == 1
            ? Path.GetFileName(snapshot.InputPaths[0])
            : $"{snapshot.InputPaths.Count} files";
        var output = snapshot.OutputPaths.Count == 1
            ? Path.GetFileName(snapshot.OutputPaths[0])
            : null;
        var outputAvailable = snapshot.OutputPaths.Count != 1 || File.Exists(snapshot.OutputPaths[0]);
        return new PreviewHistoryEntry
        {
            Id = snapshot.Id.Value.ToString("N"),
            State = snapshot.State.ToString(),
            ActionId = snapshot.ActionId.Value,
            SourceName = source,
            OutputName = output,
            OutputAvailable = outputAvailable,
            OccurredAt = snapshot.FinishedAt ?? snapshot.EnqueuedAt
        };
    }

    private Border CreateHistoryRow(PreviewHistoryEntry entry)
    {
        var output = entry.OutputDeleted
            ? T(" · output deleted", " · результат удалён")
            : !entry.OutputAvailable && !string.IsNullOrWhiteSpace(entry.OutputName)
                ? T(" · result unavailable", " · результат недоступен")
            : string.IsNullOrWhiteSpace(entry.OutputName) ? string.Empty : $" → {PreviewHistoryDisplayPolicy.SafeName(entry.OutputName)}";
        var palette = PreviewThemePalette.For(_darkTheme);
        var primary = ToBrush(palette.TextPrimary);
        var secondary = SecondaryTextBrush();
        var accent = HistoryAccentBrush(entry.State);
        var safeSourceName = PreviewHistoryDisplayPolicy.SafeName(entry.SourceName);
        var row = new Border
        {
            Background = ToBrush(palette.SurfaceElevated),
            BorderBrush = accent,
            BorderThickness = new Thickness(3, 0, 0, 0),
            CornerRadius = new CornerRadius(9),
            Padding = new Thickness(9, 6, 8, 6),
            Margin = new Thickness(0, 0, 0, 5),
            ToolTip = T(
                $"{entry.OccurredAt.ToLocalTime().ToString("g", CultureInfo.CurrentCulture)} · {safeSourceName}",
                $"{entry.OccurredAt.ToLocalTime().ToString("g", CultureInfo.CurrentCulture)} · {safeSourceName}"),
            Child = new StackPanel
            {
                Children =
                {
                    new TextBlock
                    {
                        Text = $"{PreviewHistoryDisplayPolicy.LocalizeState(entry.State, _language == PreviewLanguage.Russian)} · {PreviewHistoryDisplayPolicy.LocalizeAction(entry.ActionId, _language == PreviewLanguage.Russian)}",
                        Foreground = primary,
                        FontSize = 11,
                        FontWeight = FontWeights.SemiBold,
                        TextTrimming = TextTrimming.CharacterEllipsis
                    },
                    new TextBlock
                    {
                        Text = $"{LocalizeSourceName(PreviewHistoryDisplayPolicy.SafeName(entry.SourceName))}{output}",
                        Foreground = secondary,
                        FontSize = 11,
                        Margin = new Thickness(0, 2, 0, 0),
                        TextTrimming = TextTrimming.CharacterEllipsis
                    },
                    new TextBlock
                    {
                        Text = FormatHistoryTime(entry.OccurredAt),
                        Foreground = secondary,
                        FontSize = 10,
                        Margin = new Thickness(0, 2, 0, 0)
                    }
                }
            }
        };
        return row;
    }

    private SolidColorBrush HistoryAccentBrush(string state) => state switch
    {
        nameof(JobState.Completed) => SuccessTextBrush(),
        nameof(JobState.Failed) => ErrorTextBrush(),
        nameof(JobState.Cancelled) => ToBrush(PreviewThemePalette.For(_darkTheme).Warning),
        nameof(JobState.Running) or nameof(JobState.Cancelling) => ToBrush(PreviewThemePalette.For(_darkTheme).Accent),
        _ => SecondaryTextBrush()
    };

    private string FormatHistoryTime(DateTimeOffset occurredAt)
    {
        var age = DateTimeOffset.UtcNow - occurredAt.ToUniversalTime();
        if (age < TimeSpan.FromMinutes(1))
        {
            return T("Just now", "Только что");
        }

        if (age < TimeSpan.FromHours(1))
        {
            var minutes = Math.Max(1, (int)age.TotalMinutes);
            return T($"{minutes} min ago", $"{minutes} мин назад");
        }

        if (age < TimeSpan.FromDays(1))
        {
            var hours = Math.Max(1, (int)age.TotalHours);
            return T($"{hours} h ago", $"{hours} ч назад");
        }

        return occurredAt.ToLocalTime().ToString(
            _language == PreviewLanguage.Russian ? "dd.MM.yyyy HH:mm" : "MMM d, yyyy HH:mm",
            CultureInfo.InvariantCulture);
    }

    private string LocalizeSourceName(string sourceName)
    {
        if (_language == PreviewLanguage.Russian && sourceName.EndsWith(" files", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(sourceName[..^6], out var count))
        {
            return $"Файлов: {count}";
        }

        return sourceName;
    }

    private static bool IsTerminalState(string state) => state is
        nameof(JobState.Completed) or nameof(JobState.Failed) or nameof(JobState.Cancelled);

    private async void DismissCompletion(object? sender, RoutedEventArgs e)
    {
        if (_activeCompletionJobId is not { } jobId)
        {
            return;
        }

        _resultActions.IsEnabled = false;
        try
        {
            var result = await _presentation.ExecuteVisibleCompletionCommandAsync(
                jobId,
                CompletionCommand.Dismiss,
                _lifetimeCancellation.Token);
            if (IsClosing)
            {
                return;
            }

            if (result.Success)
            {
                var current = _presentation.Current;
                if (current.Completion is null)
                {
                    _activeCompletionJobId = null;
                    _outputPath = null;
                    _resultActions.Visibility = Visibility.Collapsed;
                    _status.Text = T("Ready for another file", "Готово для следующего файла");
                    _status.Foreground = SecondaryTextBrush();
                    _result.Text = string.Empty;
                }
                else
                {
                    RenderPresentation(current);
                }
            }
            else
            {
                SetStatus(
                    PreviewCommandErrorPolicy.Localize(
                        CompletionCommand.Dismiss,
                        result.Error?.UserMessage,
                        _language == PreviewLanguage.Russian)
                    ?? T("The completion could not be dismissed", "Не удалось закрыть результат"),
                    error: true);
            }
        }
        catch (OperationCanceledException) when (IsClosing)
        {
            // Window shutdown cancels completion commands without surfacing a false error.
        }
        finally
        {
            _resultActions.IsEnabled = true;
        }
    }

    private async Task DisposeQueueAsync()
    {
        if (_runtimeDisposed)
        {
            return;
        }

        _runtimeDisposed = true;
        await _jobQueue.DisposeAsync();
    }

    private void ClearSelectedImageState()
    {
        _currentPath = null;
        _authoritativeQualification = null;
        _fileName.Text = T("Drop one JPEG or PNG here", "Перетащите сюда один JPEG или PNG");
        _metadata.Text = string.Empty;
        _thumbnail.Source = null;
        _thumbnail.Visibility = Visibility.Collapsed;
        _dropZone.BorderBrush = ToBrush(PreviewThemePalette.For(_darkTheme).SeparatorStrong);
        SetEmptyState(true);
    }

    private void ResetPreview()
    {
        ClearSelectedImageState();
        _outputPath = null;
        _authoritativeQualification = null;
        _thumbnail.Source = null;
        _thumbnail.Visibility = Visibility.Collapsed;
        _activeCompletionJobId = null;
        _status.Text = T("Local-only preview · source files are never overwritten", "Локальный предпросмотр · исходные файлы не изменяются");
        _status.Foreground = SecondaryTextBrush();
        _result.Text = string.Empty;
        _resultActions.Visibility = Visibility.Collapsed;
        _copyButton.Visibility = Visibility.Collapsed;
        _openButton.Visibility = Visibility.Collapsed;
        _deleteButton.Visibility = Visibility.Collapsed;
        _dismissButton.Visibility = Visibility.Visible;
        _cancelButton.Visibility = Visibility.Collapsed;
        _activeJobId = null;
        SetEmptyState(true);
        SetDropHover(false);
        ApplyInputAvailability();
        RefreshHistory();
    }

    private void ClearCompletionVisualState()
    {
        _outputPath = null;
        _resultActions.Visibility = Visibility.Collapsed;
        _copyButton.Visibility = Visibility.Collapsed;
        _openButton.Visibility = Visibility.Collapsed;
        _deleteButton.Visibility = Visibility.Collapsed;
        _dismissButton.Visibility = Visibility.Collapsed;
        _result.Text = string.Empty;
    }

    private void SetEmptyState(bool visible)
    {
        var state = visible ? Visibility.Visible : Visibility.Collapsed;
        _emptyIcon.Visibility = state;
        _emptyTitle.Visibility = state;
        _formatHint.Visibility = state;
    }

    private void SetDropHover(bool active)
    {
        if (_dropHover == active)
        {
            return;
        }

        _dropHover = active;
        var palette = PreviewThemePalette.For(_darkTheme);
        _dropZone.Background = active ? ToBrush(palette.DropZoneHover) : ToBrush(palette.SurfaceCard);
        _dropZone.BorderBrush = active
            ? ToBrush(palette.Accent)
            : ToBrush(palette.SeparatorStrong);
    }

    private void SetStatus(string text, bool error)
    {
        _status.Text = text;
        _status.Foreground = error ? ErrorTextBrush() : SecondaryTextBrush();
    }

    private void ApplyInputAvailability()
    {
        var canPick = PreviewFilePickerPolicy.CanPickImage(
            _inputBusy || _pickerBusy,
            _interaction.ActiveDragSessionId is not null,
            _activeJobId is not null || _busy);
        _chooseButton.IsEnabled = canPick;
        _emptyIcon.IsEnabled = canPick;
        _selfCheckButton.IsEnabled = PreviewDiagnosticsPolicy.CanRunSelfCheck(
            _inputBusy,
            _pickerBusy,
            _interaction.ActiveDragSessionId is not null,
            _activeJobId is not null || _busy,
            _activeCompletionJobId is not null,
            _selfCheckRunning);
        SetAutomationHelpText(
            _chooseButton,
            canPick
                ? T("Opens the image picker", "Открывает выбор изображения")
                : T("Finish the current operation before choosing an image", "Сначала завершите текущую операцию"));
        SetAutomationHelpText(
            _emptyIcon,
            canPick
                ? T("Opens the image picker", "Открывает выбор изображения")
                : T("Finish the current operation before choosing an image", "Сначала завершите текущую операцию"));
    }

    private void OnCancelClicked(object? sender, RoutedEventArgs e)
    {
        if (_activeJobId is not { } jobId)
        {
            return;
        }

        if (_presentation.TryCancelVisibleOperation(jobId))
        {
            _cancelButton.IsEnabled = false;
            _status.Text = T("Cancelling…", "Отмена…");
        }
    }

    private async void OnSelfCheckClicked(object? sender, RoutedEventArgs e)
    {
        if (!PreviewDiagnosticsPolicy.CanRunSelfCheck(
                _inputBusy,
                _pickerBusy,
                _interaction.ActiveDragSessionId is not null,
                _activeJobId is not null || _busy,
                _activeCompletionJobId is not null,
                _selfCheckRunning))
        {
            SetStatus(T("Finish the current operation before running self-check", "Сначала завершите текущую операцию перед самопроверкой"), error: true);
            return;
        }

        _selfCheckRunning = true;
        _selfCheckButton.IsEnabled = false;
        _openSelfCheckReportButton.Visibility = Visibility.Collapsed;
        _selfCheckReportPath = null;
        _progress.Visibility = Visibility.Visible;
        SetStatus(T("Running local self-check…", "Запускаем локальную самопроверку…"), error: false);
        try
        {
            var reportPath = Path.Combine("artifacts", "preview-self-check", "ui-latest.json");
            var exitCode = await PreviewSelfCheck.RunAsync(reportPath);
            if (IsClosing)
            {
                return;
            }

            _selfCheckReportPath = Path.GetFullPath(reportPath);
            _openSelfCheckReportButton.Visibility = File.Exists(_selfCheckReportPath)
                ? Visibility.Visible
                : Visibility.Collapsed;
            if (PreviewDiagnosticsPolicy.TryReadSummary(_selfCheckReportPath, out var summary))
            {
                _result.Foreground = exitCode == 0 && summary.Passed ? SuccessTextBrush() : ErrorTextBrush();
                _result.Text = $"{PreviewDiagnosticsPolicy.FormatSummary(summary, _language == PreviewLanguage.Russian)}\n"
                    + T("Report: artifacts/preview-self-check/ui-latest.json", "Отчёт: artifacts/preview-self-check/ui-latest.json");
            }
            else
            {
                _result.Foreground = ErrorTextBrush();
                _result.Text = T(
                    "Self-check found a problem.\nOpen the JSON report for details.",
                    "Самопроверка нашла проблему.\nОткройте JSON-отчёт для подробностей.");
            }
        }
        catch (Exception ex)
        {
            PreviewProcessLog.TryAppend(PreviewProcessLog.DefaultPath, "preview-self-check-failed", ex.ToString());
            if (IsClosing)
            {
                return;
            }

            _selfCheckReportPath = Path.GetFullPath(Path.Combine("artifacts", "preview-self-check", "ui-latest.json"));
            _openSelfCheckReportButton.Visibility = File.Exists(_selfCheckReportPath)
                ? Visibility.Visible
                : Visibility.Collapsed;
            _result.Foreground = ErrorTextBrush();
            _result.Text = T(
                "Self-check failed safely. Open the JSON report for details.",
                "Самопроверка безопасно завершилась ошибкой. Откройте JSON-отчёт для подробностей.");
        }
        finally
        {
            if (!IsClosing)
            {
                _progress.Visibility = Visibility.Collapsed;
                _selfCheckRunning = false;
                ApplyInputAvailability();
                if (_activeJobId is { } jobId && _jobQueue.TryGetSnapshot(jobId, out _))
                {
                    RenderPresentation(_presentation.Current);
                }
            }
        }
    }

    private void OpenSelfCheckReportFolder(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_selfCheckReportPath))
        {
            return;
        }

        var directory = Path.GetDirectoryName(_selfCheckReportPath);
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            SetStatus(T("The self-check report folder is unavailable", "Папка отчёта самопроверки недоступна"), error: true);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"\"{directory}\"",
                UseShellExecute = true
            });
            SetStatus(T("Self-check report folder opened", "Папка отчёта самопроверки открыта"), error: false);
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            PreviewProcessLog.TryAppend(PreviewProcessLog.DefaultPath, "preview-self-check-report-open-failed", ex.ToString());
            SetStatus(T("Could not open the self-check report folder", "Не удалось открыть папку отчёта самопроверки"), error: true);
        }
    }

    private bool IsClosing => _closing || _lifetimeCancellation.IsCancellationRequested;

    private void OnResetLocalDataClicked(object? sender, RoutedEventArgs e)
    {
        var confirmation = MessageBox.Show(
            T(
                "Reset saved language, theme, and preview history? Image files will not be deleted.",
                "Сбросить сохранённые язык, тему и историю preview? Файлы изображений не будут удалены."),
            T("Reset SmartDrag preview", "Сбросить preview SmartDrag"),
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (confirmation != MessageBoxResult.Yes)
        {
            return;
        }

        _historyClearedAt = DateTimeOffset.UtcNow;
        _persistedHistory.Clear();
        _historyStore.TrySave(Array.Empty<PreviewHistoryEntry>(), _historyClearedAt);
        _selfCheckReportPath = null;
        _openSelfCheckReportButton.Visibility = Visibility.Collapsed;
        _language = PreviewLanguage.Russian;
        _darkTheme = false;
        SavePreferences();
        ApplyLanguage();
        ApplyTheme();
        ResetPreview();
    }

    private static string FormatBytes(long? bytes)
    {
        if (bytes is null)
        {
            return "size unknown";
        }

        var value = bytes.Value;
        var units = new[] { "B", "KB", "MB", "GB" };
        var unit = 0;
        var amount = (double)value;
        while (amount >= 1024 && unit < units.Length - 1)
        {
            amount /= 1024;
            unit++;
        }

        return unit == 0 ? $"{value:N0} {units[unit]}" : $"{amount:0.##} {units[unit]}";
    }

    private static string BuildMetadataSummary(ImageInspectionResult inspection)
    {
        var facts = new List<string>
        {
            inspection.Format.ToString().ToUpperInvariant(),
            $"{inspection.Width}×{inspection.Height}",
            FormatBytes(inspection.SourceSizeBytes)
        };

        if (inspection.HasAlpha) facts.Add("alpha");
        if (inspection.HasExif) facts.Add("EXIF");
        if (inspection.HasXmp) facts.Add("XMP");
        if (inspection.HasIptc) facts.Add("IPTC");
        if (inspection.HasIccProfile) facts.Add("ICC");
        if (inspection.ExifOrientation is > 1) facts.Add($"orientation {inspection.ExifOrientation}");
        return string.Join("  ·  ", facts);
    }

    private string BuildSafetyProfile() => T(
        $"Original stays untouched  ·  JPEG/PNG only  ·  max {FormatBytes(_safetyLimits.MaxSourceBytes)}  ·  max {_safetyLimits.MaxDimension:N0}px  ·  max {_safetyLimits.MaxDecodedPixels / 1_000_000d:0.#} MP",
        $"Оригинал не изменяется  ·  только JPEG/PNG  ·  максимум {FormatBytes(_safetyLimits.MaxSourceBytes)}  ·  максимум {_safetyLimits.MaxDimension:N0} px  ·  максимум {_safetyLimits.MaxDecodedPixels / 1_000_000d:0.#} Мп");

    private string BuildCapabilityHint() => _capabilities.WebpEncodingAvailable
        ? T(
            "Available now: Compress, Remove Metadata, and WebP conversion.",
            "Сейчас доступны: Сжать, Удалить метаданные и конвертация в WebP.")
        : T(
            "Available now: Compress and Remove Metadata. WebP appears after G3 encoder validation.",
            "Сейчас доступны: Сжать и Удалить метаданные. WebP появится после проверки кодека на G3.");

    private static BitmapImage? TryLoadThumbnail(string path)
    {
        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(Path.GetFullPath(path), UriKind.Absolute);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.DecodePixelWidth = 368;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or InvalidOperationException)
        {
            return null;
        }
    }

    private Button CreateResultButton(string label, RoutedEventHandler handler)
    {
        var button = new Button
        {
            Content = label,
            Height = 32,
            MinWidth = 106,
            Margin = new Thickness(4, 0, 4, 0),
            Padding = new Thickness(12, 0, 12, 0),
            Foreground = new SolidColorBrush(Color.FromRgb(29, 29, 31)),
            Background = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(210, 210, 215)),
            BorderThickness = new Thickness(1),
            Template = CreateRoundedButtonTemplate(),
            Cursor = System.Windows.Input.Cursors.Hand
        };
        button.Click += handler;
        return button;
    }

    private static ControlTemplate CreateRoundedButtonTemplate()
    {
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(10));
        border.SetBinding(Border.BackgroundProperty, new Binding(nameof(Control.Background))
        {
            RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent)
        });
        border.SetBinding(Border.BorderBrushProperty, new Binding(nameof(Control.BorderBrush))
        {
            RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent)
        });
        border.SetBinding(Border.BorderThicknessProperty, new Binding(nameof(Control.BorderThickness))
        {
            RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent)
        });

        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Center);
        presenter.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);
        presenter.SetBinding(ContentPresenter.ContentProperty, new Binding(nameof(ContentControl.Content))
        {
            RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent)
        });
        presenter.SetBinding(ContentPresenter.ContentTemplateProperty, new Binding(nameof(ContentControl.ContentTemplate))
        {
            RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent)
        });
        border.AppendChild(presenter);
        var template = new ControlTemplate(typeof(Button)) { VisualTree = border };
        var focusTrigger = new Trigger
        {
            Property = UIElement.IsKeyboardFocusedProperty,
            Value = true
        };
        focusTrigger.Setters.Add(new Setter(Border.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(0, 122, 255))));
        focusTrigger.Setters.Add(new Setter(Border.BorderThicknessProperty, new Thickness(2)));
        template.Triggers.Add(focusTrigger);
        var hoverTrigger = new Trigger
        {
            Property = UIElement.IsMouseOverProperty,
            Value = true
        };
        hoverTrigger.Setters.Add(new Setter(UIElement.OpacityProperty, 0.92));
        template.Triggers.Add(hoverTrigger);
        var pressedTrigger = new Trigger
        {
            Property = Button.IsPressedProperty,
            Value = true
        };
        pressedTrigger.Setters.Add(new Setter(UIElement.OpacityProperty, 0.82));
        template.Triggers.Add(pressedTrigger);
        var disabledTrigger = new Trigger
        {
            Property = UIElement.IsEnabledProperty,
            Value = false
        };
        disabledTrigger.Setters.Add(new Setter(UIElement.OpacityProperty, 0.52));
        template.Triggers.Add(disabledTrigger);
        return template;
    }

    private static ControlTemplate CreateCircleButtonTemplate()
    {
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(36));
        border.SetBinding(Border.BackgroundProperty, new Binding(nameof(Control.Background))
        {
            RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent)
        });
        border.SetBinding(Border.BorderBrushProperty, new Binding(nameof(Control.BorderBrush))
        {
            RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent)
        });
        border.SetBinding(Border.BorderThicknessProperty, new Binding(nameof(Control.BorderThickness))
        {
            RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent)
        });

        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Center);
        presenter.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);
        presenter.SetBinding(ContentPresenter.ContentProperty, new Binding(nameof(ContentControl.Content))
        {
            RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent)
        });
        presenter.SetBinding(ContentPresenter.ContentTemplateProperty, new Binding(nameof(ContentControl.ContentTemplate))
        {
            RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent)
        });
        presenter.SetBinding(ContentPresenter.ContentStringFormatProperty, new Binding(nameof(ContentControl.ContentStringFormat))
        {
            RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent)
        });
        border.AppendChild(presenter);
        var template = new ControlTemplate(typeof(Button)) { VisualTree = border };
        var focusTrigger = new Trigger
        {
            Property = UIElement.IsKeyboardFocusedProperty,
            Value = true
        };
        focusTrigger.Setters.Add(new Setter(Border.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(0, 122, 255))));
        focusTrigger.Setters.Add(new Setter(Border.BorderThicknessProperty, new Thickness(2)));
        template.Triggers.Add(focusTrigger);
        var hoverTrigger = new Trigger
        {
            Property = UIElement.IsMouseOverProperty,
            Value = true
        };
        hoverTrigger.Setters.Add(new Setter(UIElement.OpacityProperty, 0.92));
        template.Triggers.Add(hoverTrigger);
        var pressedTrigger = new Trigger
        {
            Property = Button.IsPressedProperty,
            Value = true
        };
        pressedTrigger.Setters.Add(new Setter(UIElement.OpacityProperty, 0.82));
        template.Triggers.Add(pressedTrigger);
        var disabledTrigger = new Trigger
        {
            Property = UIElement.IsEnabledProperty,
            Value = false
        };
        disabledTrigger.Setters.Add(new Setter(UIElement.OpacityProperty, 0.52));
        template.Triggers.Add(disabledTrigger);
        return template;
    }

    private static UIElement CreateStep(
        string number,
        string title,
        string detail,
        int column,
        TextBlock[] titles,
        TextBlock[] details)
    {
        var step = new StackPanel { Margin = new Thickness(0, 0, 10, 0) };
        step.Children.Add(new Border
        {
            Width = 22,
            Height = 22,
            CornerRadius = new CornerRadius(11),
            Background = new SolidColorBrush(Color.FromRgb(0, 122, 255)),
            HorizontalAlignment = HorizontalAlignment.Left,
            Child = new TextBlock
            {
                Text = number,
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                TextAlignment = TextAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        });
        var titleText = new TextBlock
        {
            Text = title,
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(29, 29, 31)),
            Margin = new Thickness(0, 6, 0, 0)
        };
        titles[column] = titleText;
        step.Children.Add(titleText);
        var detailText = new TextBlock
        {
            Text = detail,
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.FromRgb(110, 110, 115)),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 2, 0, 0)
        };
        details[column] = detailText;
        step.Children.Add(detailText);
        Grid.SetColumn(step, column);
        return step;
    }

    private static UIElement CreateLogoMark()
    {
        var canvas = new Canvas { Width = 42, Height = 42 };
        canvas.Children.Add(new System.Windows.Shapes.Line
        {
            X1 = 9,
            Y1 = 30,
            X2 = 20,
            Y2 = 19,
            Stroke = Brushes.White,
            StrokeThickness = 3.5,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round
        });
        canvas.Children.Add(new System.Windows.Shapes.Line
        {
            X1 = 20,
            Y1 = 19,
            X2 = 20,
            Y2 = 26,
            Stroke = Brushes.White,
            StrokeThickness = 3.5,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round
        });
        canvas.Children.Add(new System.Windows.Shapes.Line
        {
            X1 = 20,
            Y1 = 26,
            X2 = 28,
            Y2 = 18,
            Stroke = Brushes.White,
            StrokeThickness = 3.5,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round
        });
        canvas.Children.Add(new System.Windows.Shapes.Line
        {
            X1 = 28,
            Y1 = 18,
            X2 = 34,
            Y2 = 12,
            Stroke = Brushes.White,
            StrokeThickness = 3.5,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round
        });
        canvas.Children.Add(new System.Windows.Shapes.Line
        {
            X1 = 27,
            Y1 = 12,
            X2 = 34,
            Y2 = 12,
            Stroke = Brushes.White,
            StrokeThickness = 3.5,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round
        });
        canvas.Children.Add(new System.Windows.Shapes.Line
        {
            X1 = 34,
            Y1 = 12,
            X2 = 34,
            Y2 = 19,
            Stroke = Brushes.White,
            StrokeThickness = 3.5,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round
        });
        return new Border
        {
            Width = 42,
            Height = 42,
            CornerRadius = new CornerRadius(13),
            Background = new SolidColorBrush(Color.FromRgb(0, 122, 255)),
            Margin = new Thickness(0, 0, 12, 0),
            Child = canvas,
            ToolTip = "SmartDrag"
        };
    }

    private Button CreateToolbarButton(string content, RoutedEventHandler handler, string toolTip)
    {
        var button = new Button
        {
            Content = content,
            Height = 36,
            MinWidth = 48,
            Margin = new Thickness(0, 0, 8, 0),
            Padding = new Thickness(12, 0, 12, 0),
            Background = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(210, 210, 215)),
            BorderThickness = new Thickness(1),
            Foreground = new SolidColorBrush(Color.FromRgb(29, 29, 31)),
            Template = CreateRoundedButtonTemplate(),
            Cursor = System.Windows.Input.Cursors.Hand,
            ToolTip = toolTip
        };
        button.Click += handler;
        return button;
    }

    private void OnLanguageButtonClicked(object? sender, RoutedEventArgs e)
    {
        _language = _language == PreviewLanguage.Russian ? PreviewLanguage.English : PreviewLanguage.Russian;
        SavePreferences();
        ApplyLanguage();
    }

    private void OnThemeButtonClicked(object? sender, RoutedEventArgs e)
    {
        _darkTheme = !_darkTheme;
        SavePreferences();
        ApplyTheme();
        ApplyLanguage();
    }

    private void SavePreferences() => _preferencesStore.TrySave(new PreviewPreferences
    {
        Language = _language == PreviewLanguage.Russian ? "ru" : "en",
        DarkTheme = _darkTheme,
        WindowLeft = TryGetFinite(Left),
        WindowTop = TryGetFinite(Top),
        WindowWidth = TryGetFinite(Width),
        WindowHeight = TryGetFinite(Height)
    });

    private void SaveWindowPlacement() => SavePreferences();

    private void RestoreWindowPlacement(PreviewPreferences preferences)
    {
        var workArea = SystemParameters.WorkArea;
        var persisted = preferences.WindowLeft is { } left
            && preferences.WindowTop is { } top
            && preferences.WindowWidth is { } width
            && preferences.WindowHeight is { } height
            ? new PreviewWindowBounds(left, top, width, height)
            : (PreviewWindowBounds?)null;
        var placement = PreviewWindowPlacementPolicy.Resolve(
            persisted,
            new RectD(workArea.Left, workArea.Top, workArea.Right, workArea.Bottom));
        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = placement.Left;
        Top = placement.Top;
        Width = placement.Width;
        Height = placement.Height;
    }

    private static double? TryGetFinite(double value) => double.IsFinite(value) ? value : null;

    private string T(string english, string russian) =>
        _language == PreviewLanguage.Russian ? russian : english;

    private SolidColorBrush SecondaryTextBrush() => _darkTheme
        ? ToBrush(PreviewThemePalette.Dark.TextSecondary)
        : ToBrush(PreviewThemePalette.Light.TextSecondary);

    private SolidColorBrush ErrorTextBrush() => _darkTheme
        ? ToBrush(PreviewThemePalette.Dark.Error)
        : ToBrush(PreviewThemePalette.Light.Error);

    private SolidColorBrush SuccessTextBrush() => _darkTheme
        ? ToBrush(PreviewThemePalette.Dark.Success)
        : ToBrush(PreviewThemePalette.Light.Success);

    private static SolidColorBrush ToBrush(ThemeColor color) =>
        new(Color.FromRgb(color.Red, color.Green, color.Blue));

    private static void SetAutomationName(DependencyObject element, string name) =>
        System.Windows.Automation.AutomationProperties.SetName(element, name);

    private static void SetAutomationHelpText(DependencyObject element, string text) =>
        System.Windows.Automation.AutomationProperties.SetHelpText(element, text);

    private string LocalizeOperationStatus(OperationPresentationState state, string fallback) => state switch
    {
        OperationPresentationState.Queued => T("Waiting…", "Ожидание…"),
        OperationPresentationState.Running => T("Working…", "Выполняется…"),
        OperationPresentationState.Cancelling => T("Cancelling…", "Отмена…"),
        _ => fallback
    };

    private string FormatOperationStatus(OperationPresentationModel operation)
    {
        var status = $"{LocalizeActionLabel(operation.ActionLabel)} · {LocalizeOperationStatus(operation.State, operation.StatusText)}";
        if (!string.IsNullOrWhiteSpace(operation.SourceDisplayName))
        {
            status += $" · {operation.SourceDisplayName}";
        }

        if (operation.QueuedBehindCount > 0)
        {
            status += T(
                $" · {operation.QueuedBehindCount} more queued",
                $" · ещё {operation.QueuedBehindCount} в очереди");
        }

        return status;
    }

    private string LocalizeActionLabel(string label) => label switch
    {
        "Compress" => T("Compress", "Сжать"),
        "Convert to WebP" => T("Convert to WebP", "Конвертировать в WebP"),
        "Remove Metadata" => T("Remove Metadata", "Удалить метаданные"),
        _ => label
    };

    private string LocalizeCompletionTitle(string title) => title switch
    {
        "Compressed" => T("Compressed", "Сжато"),
        "Converted to WebP" => T("Converted to WebP", "Конвертировано в WebP"),
        "Metadata removed" => T("Metadata removed", "Метаданные удалены"),
        "Cancelled" => T("Cancelled", "Отменено"),
        "Couldn’t finish" => T("Couldn’t finish", "Не удалось завершить"),
        "Generated file deleted" => T("Generated file deleted", "Созданный файл удалён"),
        _ => title
    };

    private string LocalizeCompletionDetail(string detail)
    {
        if (_language == PreviewLanguage.English)
        {
            return detail;
        }

        if (detail.StartsWith("Saved ", StringComparison.Ordinal))
        {
            return "Сэкономлено " + detail[6..];
        }

        return detail switch
        {
            "A new file was created. The original was kept." => "Создан новый файл. Оригинал сохранён.",
            "The original file was not changed." => "Исходный файл не изменён.",
            _ => detail
        };
    }

    private string LocalizeJobState(JobState state) => state switch
    {
        JobState.Queued => T("Queued", "В очереди"),
        JobState.Running => T("Running", "Выполняется"),
        JobState.Cancelling => T("Cancelling", "Отмена"),
        JobState.Completed => T("Completed", "Готово"),
        JobState.Cancelled => T("Cancelled", "Отменено"),
        JobState.Failed => T("Failed", "Ошибка"),
        _ => state.ToString()
    };

    private string LocalizePayloadRejection(PayloadRejectionReason reason) => reason switch
    {
        PayloadRejectionReason.NoFiles => T("No file was found in the drop.", "В перетаскивании не найден файл."),
        PayloadRejectionReason.MultipleFiles => T("Drop one file at a time.", "Перетаскивайте по одному файлу."),
        PayloadRejectionReason.SourceMissing => T("The file is no longer available.", "Файл больше недоступен."),
        PayloadRejectionReason.DirectoryNotSupported => T("Folders are not supported in this preview.", "Папки не поддерживаются в этом preview."),
        PayloadRejectionReason.UnsupportedExtension => T("Only JPEG and PNG images are supported.", "Поддерживаются только изображения JPEG и PNG."),
        PayloadRejectionReason.PathUnavailable => T("The file location could not be verified.", "Не удалось проверить расположение файла."),
        _ => T("The file could not be verified safely.", "Не удалось безопасно проверить файл.")
    };

    private void ApplyLanguage()
    {
        _overlay.Language = _language == PreviewLanguage.Russian ? "ru" : "en";
        Title = $"SmartDrag · {T("Preview", "Предпросмотр")}";
        _headerSubtitle.Text = T(
            "Simple local image actions, explained as you go.",
            "Простые локальные действия с изображением — всё понятно по шагам.");
        _languageButton.Content = _language == PreviewLanguage.Russian ? "EN" : "RU";
        _languageButton.ToolTip = T("Switch to Russian", "Переключить на английский");
        _themeButton.Content = _darkTheme ? T("Light", "Светлая") : T("Dark", "Тёмная");
        _themeButton.ToolTip = _darkTheme
            ? T("Switch to light appearance", "Включить светлую тему")
            : T("Switch to dark appearance", "Включить тёмную тему");
        _chooseButton.Content = T("Choose image", "Выбрать изображение");
        _chooseButton.ToolTip = T("Select one JPEG or PNG without dragging", "Выбрать один JPEG или PNG без перетаскивания");
        _cancelButton.Content = T("Cancel", "Отменить");
        _copyButton.Content = T("Copy path", "Скопировать путь");
        _openButton.Content = T("Open folder", "Открыть папку");
        _deleteButton.Content = T("Delete output", "Удалить результат");
        _dismissButton.Content = T("Dismiss", "Закрыть");
        _selfCheckButton.Content = T("Run self-check", "Запустить самопроверку");
        _openSelfCheckReportButton.Content = T("Open report folder", "Открыть папку отчёта");
        _openSelfCheckReportButton.ToolTip = T(
            "Open the folder containing the latest self-check JSON report",
            "Открыть папку с последним JSON-отчётом самопроверки");
        _resetButton.Content = T("Reset local data", "Сбросить локальные данные");
        _resetButton.ToolTip = T(
            "Reset saved language, theme, and preview history. Image files are not deleted.",
            "Сбросить язык, тему и историю preview. Файлы изображений не удаляются.");
        _emptyTitle.Text = T("Add an image to get started", "Добавьте изображение, чтобы начать");
        _formatHint.Text = T("JPEG or PNG · one file at a time", "JPEG или PNG · по одному файлу");
        _historyTitle.Text = T("Recent operations", "Последние операции");
        _historyHint.Text = T("Your latest six results appear here", "Здесь появятся последние шесть результатов");
        _clearHistoryButton.Content = T("Clear history", "Очистить историю");
        _clearHistoryButton.ToolTip = T(
            "Hide recent operations. Image files will not be deleted.",
            "Скрыть последние операции. Файлы изображений не удаляются.");
        _safetyTitle.Text = T("Safety & scope", "Безопасность и возможности");
        _safetyHint.Text = T(
            "SmartDrag creates a new file and never edits the original.",
            "SmartDrag создаёт новый файл и никогда не изменяет оригинал.");
        _howTitle.Text = T("How it works", "Как это работает");
        _stepTitles[0].Text = T("Add", "Добавьте");
        _stepDetails[0].Text = T("Drop or select one image", "Перетащите или выберите одно изображение");
        _stepTitles[1].Text = T("Choose", "Выберите");
        _stepDetails[1].Text = T("Pick one action", "Выберите действие");
        _stepTitles[2].Text = T("Get", "Получите");
        _stepDetails[2].Text = T("A new file is created", "Будет создан новый файл");
        _footer.Text = T(
            "Esc: cancel or dismiss  ·  Delete: remove result  ·  Ctrl+V: paste  ·  Ctrl+O: choose  ·  Ctrl+L: language  ·  Ctrl+Shift+T: theme  ·  Ctrl+Shift+H: clear history",
            "Esc: отмена или закрытие  ·  Delete: удалить результат  ·  Ctrl+V: вставить  ·  Ctrl+O: выбрать  ·  Ctrl+L: язык  ·  Ctrl+Shift+T: тема  ·  Ctrl+Shift+H: очистить историю");
        SetAutomationName(_dropZone, T("Image drop area. Drop one JPEG or PNG here.", "Область для файла. Перетащите сюда JPEG или PNG."));
        SetAutomationName(_chooseButton, T("Choose an image", "Выбрать изображение"));
        SetAutomationName(_emptyIcon, T("Choose an image", "Выбрать изображение"));
        SetAutomationHelpText(_chooseButton, T("Opens the image picker", "Открывает выбор изображения"));
        SetAutomationHelpText(_emptyIcon, T("Opens the image picker", "Открывает выбор изображения"));
        SetAutomationName(_languageButton, T("Switch interface to Russian", "Переключить интерфейс на английский"));
        SetAutomationName(_themeButton, _darkTheme
            ? T("Switch to light appearance", "Включить светлую тему")
            : T("Switch to dark appearance", "Включить тёмную тему"));
        SetAutomationName(_cancelButton, T("Cancel current operation", "Отменить текущую операцию"));
        SetAutomationName(_copyButton, T("Copy generated file path", "Скопировать путь к созданному файлу"));
        SetAutomationName(_openButton, T("Open generated file folder", "Открыть папку созданного файла"));
        SetAutomationName(_deleteButton, T("Delete generated file", "Удалить созданный файл"));
        SetAutomationName(_dismissButton, T("Dismiss completion result", "Закрыть результат операции"));
        SetAutomationName(_selfCheckButton, T("Run local preview self-check", "Запустить локальную самопроверку preview"));
        SetAutomationName(_openSelfCheckReportButton, T("Open self-check report folder", "Открыть папку отчёта самопроверки"));
        SetAutomationName(_resetButton, T("Reset saved preview data", "Сбросить сохранённые данные preview"));
        SetAutomationName(_historyList, T("Recent operations history", "История последних операций"));
        SetAutomationName(_clearHistoryButton, T("Clear recent operations", "Очистить последние операции"));
        SetAutomationName(_status, T("Current status", "Текущий статус"));
        SetAutomationName(_result, T("Operation result", "Результат операции"));
        SetAutomationName(_progress, T("Processing progress", "Прогресс обработки"));
        _safetyProfile.Text = BuildSafetyProfile();
        _capabilityHint.Text = BuildCapabilityHint();
        SetAutomationName(_capabilityHint, T(
            "Available actions: Compress and Remove Metadata. WebP is pending G3 encoder validation.",
            "Доступные действия: Сжать и Удалить метаданные. WebP ожидает проверки кодека на G3."));
        RefreshHistory();
        if (_currentPath is null && _activeJobId is null && _activeCompletionJobId is null)
        {
            _fileName.Text = T("Drop one JPEG or PNG here", "Перетащите сюда один JPEG или PNG");
            _status.Text = T(
                "Local-only preview · source files are never overwritten",
                "Локальный предпросмотр · исходные файлы не изменяются");
        }
    }

    private void ApplyTheme()
    {
        _overlay.IsDarkTheme = _darkTheme;
        var palette = PreviewThemePalette.For(_darkTheme);
        var background = ToBrush(palette.WindowBackground);
        var surface = ToBrush(palette.WindowBackground);
        var card = ToBrush(palette.SurfaceCard);
        var control = ToBrush(palette.SurfaceElevated);
        var primary = ToBrush(palette.TextPrimary);
        var secondary = ToBrush(palette.TextSecondary);
        var border = ToBrush(palette.Separator);
        var accent = ToBrush(palette.Accent);

        Background = background;
        Foreground = primary;
        _surface.Background = surface;
        _dropZone.Background = _dropHover
            ? ToBrush(palette.DropZoneHover)
            : card;
        _dropZone.BorderBrush = _dropHover ? accent : ToBrush(palette.SeparatorStrong);
        _historyCard.Background = card;
        _safetyCard.Background = card;
        _howItWorksCard.Background = card;
        _historyCard.BorderBrush = border;
        _safetyCard.BorderBrush = border;
        _howItWorksCard.BorderBrush = border;
        _historyList.Background = control;
        _historyList.BorderBrush = border;
        _historyList.Foreground = primary;
        _safetyProfile.Foreground = secondary;
        _capabilityHint.Foreground = ToBrush(palette.Warning);
        _fileName.Foreground = primary;
        _metadata.Foreground = secondary;
        _emptyTitle.Foreground = primary;
        _formatHint.Foreground = secondary;
        _headerSubtitle.Foreground = secondary;
        _headerTitle.Foreground = primary;
        _historyTitle.Foreground = primary;
        _historyHint.Foreground = secondary;
        _safetyTitle.Foreground = primary;
        _safetyHint.Foreground = secondary;
        _howTitle.Foreground = primary;
        _footer.Foreground = secondary;
        foreach (var title in _stepTitles) title.Foreground = primary;
        foreach (var detail in _stepDetails) detail.Foreground = secondary;
        _emptyIcon.Background = ToBrush(palette.DropZoneHover);
        _emptyIcon.Foreground = accent;
        foreach (var button in new[] { _languageButton, _themeButton, _copyButton, _openButton, _deleteButton, _dismissButton, _clearHistoryButton, _selfCheckButton, _openSelfCheckReportButton, _resetButton })
        {
            button.Background = control;
            button.BorderBrush = border;
            button.Foreground = primary;
        }
        _chooseButton.Background = accent;
        _chooseButton.BorderBrush = accent;
        _selfCheckButton.Background = accent;
        _selfCheckButton.BorderBrush = accent;
        _cancelButton.Background = ToBrush(palette.CancelSurface);
        _cancelButton.BorderBrush = ToBrush(palette.CancelBorder);
        _cancelButton.Foreground = ToBrush(palette.Error);
        _progress.Foreground = accent;
        _progress.Background = ToBrush(palette.Separator);
        if (_presentation.Current.Completion is { } completion)
        {
            var completionBrush = completion.Tone switch
            {
                PresentationTone.Error => ErrorTextBrush(),
                PresentationTone.Neutral => SecondaryTextBrush(),
                _ => SuccessTextBrush()
            };
            _status.Foreground = completionBrush;
            _result.Foreground = completionBrush;
        }
        else
        {
            _status.Foreground = SecondaryTextBrush();
        }
        RefreshHistory();
    }

    private async void CopyOutputPath(object? sender, RoutedEventArgs e)
    {
        if (_activeCompletionJobId is not { } jobId || string.IsNullOrWhiteSpace(_outputPath))
        {
            return;
        }

        try
        {
            _resultActions.IsEnabled = false;
            var result = await _presentation.ExecuteVisibleCompletionCommandAsync(
                jobId,
                CompletionCommand.CopyResultPath,
                CancellationToken.None);
            if (result.Success)
            {
                SetStatus(T("Path copied to clipboard", "Путь скопирован в буфер обмена"), error: false);
            }
            else
            {
                SetStatus(
                    PreviewCommandErrorPolicy.Localize(
                        CompletionCommand.CopyResultPath,
                        result.Error?.UserMessage,
                        _language == PreviewLanguage.Russian)
                    ?? T("Path could not be copied", "Не удалось скопировать путь"),
                    error: true);
            }
        }
        catch (Exception ex) when (ex is ExternalException or InvalidOperationException)
        {
            SetStatus(T("Clipboard is busy — path was not copied", "Буфер обмена занят — путь не скопирован"), error: true);
        }
        finally
        {
            _resultActions.IsEnabled = true;
        }
    }

    private async void OpenOutputFolder(object? sender, RoutedEventArgs e)
    {
        if (_activeCompletionJobId is not { } jobId || string.IsNullOrWhiteSpace(_outputPath))
        {
            return;
        }

        try
        {
            _resultActions.IsEnabled = false;
            var result = await _presentation.ExecuteVisibleCompletionCommandAsync(
                jobId,
                CompletionCommand.OpenContainingFolder,
                CancellationToken.None);
            SetStatus(
                result.Success
                    ? T("Output folder opened", "Папка с результатом открыта")
                    : PreviewCommandErrorPolicy.Localize(
                        CompletionCommand.OpenContainingFolder,
                        result.Error?.UserMessage,
                        _language == PreviewLanguage.Russian)
                        ?? T("Output folder could not be opened", "Не удалось открыть папку с результатом"),
                error: !result.Success);
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
        {
            SetStatus(T("Could not open the output folder", "Не удалось открыть папку с результатом"), error: true);
        }
        finally
        {
            _resultActions.IsEnabled = true;
        }
    }

    private async void DeleteOutput(object? sender, RoutedEventArgs e)
    {
        if (_activeCompletionJobId is not { } jobId || string.IsNullOrWhiteSpace(_outputPath))
        {
            return;
        }

        var confirmation = MessageBox.Show(
            T(
                "Delete the generated file? The original file will not be changed.",
                "Удалить созданный файл? Исходный файл не будет изменён."),
            T("Delete generated file", "Удалить созданный файл"),
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (confirmation != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            _resultActions.IsEnabled = false;
            var result = await _presentation.ExecuteVisibleCompletionCommandAsync(
                jobId,
                CompletionCommand.DeleteGeneratedOutput,
                CancellationToken.None);
            if (result.Success)
            {
                MarkHistoryOutputDeleted(jobId);
                RenderPresentation(_presentation.Current);
            }
            else
            {
                SetStatus(
                    PreviewCommandErrorPolicy.Localize(
                        CompletionCommand.DeleteGeneratedOutput,
                        result.Error?.UserMessage,
                        _language == PreviewLanguage.Russian)
                    ?? T("The generated file was not deleted", "Созданный файл не удалён"),
                    error: true);
            }
        }
        catch (Exception)
        {
            SetStatus(T("The generated file was not deleted", "Созданный файл не удалён"), error: true);
        }
        finally
        {
            _resultActions.IsEnabled = true;
        }
    }

    private void MarkHistoryOutputDeleted(JobId jobId)
    {
        var id = jobId.Value.ToString("N");
        var index = _persistedHistory.FindIndex(entry =>
            string.Equals(entry.Id, id, StringComparison.OrdinalIgnoreCase));
        if (index >= 0)
        {
            _persistedHistory[index] = _persistedHistory[index] with { OutputDeleted = true };
        }
        else if (_jobQueue.TryGetSnapshot(jobId, out var snapshot))
        {
            _persistedHistory.Add(ToHistoryEntry(snapshot) with { OutputDeleted = true });
        }

        _historyStore.TrySave(_persistedHistory, _historyClearedAt);
    }

    private void ClearHistory(object? sender, RoutedEventArgs e)
    {
        var confirmation = MessageBox.Show(
            T(
                "Hide recent operations? Image files will not be deleted.",
                "Скрыть последние операции? Файлы изображений не будут удалены."),
            T("Clear preview history", "Очистить историю preview"),
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (confirmation != MessageBoxResult.Yes)
        {
            return;
        }

        _historyClearedAt = DateTimeOffset.UtcNow;
        _persistedHistory.Clear();
        _historyStore.TrySave(Array.Empty<PreviewHistoryEntry>(), _historyClearedAt);
        RefreshHistory();
    }
}
