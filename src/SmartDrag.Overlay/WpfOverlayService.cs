using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Data;
using System.Windows.Threading;
using SmartDrag.Core.Overlay;
using SmartDrag.Core.Primitives;

namespace SmartDrag.Overlay;

/// <summary>
/// WPF visual adapter for the transient drag action panel. The window is deliberately non-activating and does not
/// register its own OLE drop target; the native boundary may register the same HWND with its guarded IDropTarget.
/// </summary>
public sealed class WpfOverlayService : IActionOverlayService, IDisposable
{
    private readonly Dispatcher _dispatcher;
    private OverlayWindow? _window;
    private string _language = "en";
    private bool _darkTheme;
    private int _disposed;

    public WpfOverlayService(Dispatcher? dispatcher = null)
    {
        _dispatcher = dispatcher ?? Dispatcher.CurrentDispatcher;
    }

    public event EventHandler<OverlayActionInvokedEventArgs>? ActionInvoked;

    public nint WindowHandle => _window?.Handle ?? nint.Zero;

    public string Language
    {
        get => _language;
        set
        {
            _language = string.Equals(value, "ru", StringComparison.OrdinalIgnoreCase) ? "ru" : "en";
            if (_window is not null && _dispatcher.CheckAccess())
            {
                _window.SetLanguage(_language);
            }
        }
    }

    public bool IsDarkTheme
    {
        get => _darkTheme;
        set
        {
            _darkTheme = value;
            if (_window is not null && _dispatcher.CheckAccess())
            {
                _window.SetTheme(_darkTheme);
            }
        }
    }

    public Task ShowAsync(OverlayModel model, OverlayPlacement placement, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(model);
        return InvokeAsync(() =>
        {
            var window = EnsureWindow();
            window.SetLanguage(_language);
            window.SetTheme(_darkTheme);
            window.SetModel(model);
            window.SetPlacement(placement);
            window.ShowWithoutActivation();
        }, cancellationToken);
    }

    public Task UpdateAsync(OverlayModel model, OverlayPlacement placement, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(model);
        return InvokeAsync(() =>
        {
            var window = EnsureWindow();
            window.SetLanguage(_language);
            window.SetTheme(_darkTheme);
            window.SetModel(model);
            window.SetPlacement(placement);
            if (!window.IsVisible)
            {
                window.ShowWithoutActivation();
            }
        }, cancellationToken);
    }

    public Task HideAsync(OverlayHideReason reason, CancellationToken cancellationToken) =>
        InvokeAsync(() => _window?.Hide(), cancellationToken);

    public bool TryGetActionAtScreenPoint(Guid dragSessionId, int x, int y, out ActionId actionId)
    {
        actionId = default;
        if (_window is null || !_dispatcher.CheckAccess())
        {
            return false;
        }

        return _window.TryGetActionAtScreenPoint(new Point(x, y), dragSessionId, out actionId);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        if (_dispatcher.CheckAccess())
        {
            _window?.Close();
            _window = null;
        }
        else
        {
            try
            {
                _ = _dispatcher.InvokeAsync(() =>
                {
                    _window?.Close();
                    _window = null;
                }, DispatcherPriority.Send);
            }
            catch (InvalidOperationException)
            {
                // Dispatcher shutdown is already disposing the UI thread; there is no live overlay to close.
            }
        }
    }

    private OverlayWindow EnsureWindow()
    {
        ThrowIfDisposed();
        return _window ??= new OverlayWindow(HandleActionInvoked);
    }

    private void HandleActionInvoked(object? sender, OverlayActionInvokedEventArgs args) =>
        ActionInvoked?.Invoke(this, args);

    private Task InvokeAsync(Action action, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();
        if (_dispatcher.CheckAccess())
        {
            action();
            return Task.CompletedTask;
        }

        return _dispatcher.InvokeAsync(action, DispatcherPriority.Send, cancellationToken).Task;
    }

    private void ThrowIfDisposed()
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            throw new ObjectDisposedException(nameof(WpfOverlayService));
        }
    }
}

public interface IActionOverlayService : IOverlayService
{
    event EventHandler<OverlayActionInvokedEventArgs>? ActionInvoked;
    nint WindowHandle { get; }
    bool TryGetActionAtScreenPoint(Guid dragSessionId, int x, int y, out ActionId actionId);
}

public sealed class OverlayActionInvokedEventArgs(Guid dragSessionId, ActionId actionId) : EventArgs
{
    public Guid DragSessionId { get; } = dragSessionId;
    public ActionId ActionId { get; } = actionId;
}

internal sealed class OverlayWindow : Window
{
    private const int SwShownoactivate = 4;
    private const int GwlExstyle = -20;
    private const long WsExNoActivate = 0x08000000;
    private const long WsExToolWindow = 0x00000080;
    private const long WsExTopmost = 0x00000008;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpShowWindow = 0x0040;
    private static readonly nint HwndTopmost = new(-1);

    private readonly EventHandler<OverlayActionInvokedEventArgs> _actionInvoked;
    private readonly Border _card;
    private readonly Border _brandMark;
    private readonly TextBlock _payloadLabel;
    private readonly StackPanel _actions;
    private readonly EventHandler _sourceInitializedHandler;
    private readonly Dictionary<ActionId, Button> _actionButtons = new();
    private string _language = "en";
    private bool _darkTheme;
    private Guid _dragSessionId;

    public OverlayWindow(EventHandler<OverlayActionInvokedEventArgs> actionInvoked)
    {
        _actionInvoked = actionInvoked;
        _sourceInitializedHandler = (_, _) => ConfigureNativeWindow();
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI");
        TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
        TextOptions.SetTextRenderingMode(this, TextRenderingMode.ClearType);
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = true;
        Focusable = false;
        SizeToContent = SizeToContent.WidthAndHeight;
        WindowStartupLocation = WindowStartupLocation.Manual;
        IsTabStop = false;

        _payloadLabel = new TextBlock
        {
            Foreground = new SolidColorBrush(Color.FromRgb(110, 110, 115)),
            FontSize = 12,
            Margin = new Thickness(8, 0, 4, 8),
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        _payloadLabel.IsHitTestVisible = false;
        _brandMark = new Border
        {
            Width = 24,
            Height = 24,
            CornerRadius = new CornerRadius(7),
            Background = new SolidColorBrush(Color.FromRgb(0, 122, 255)),
            Child = new TextBlock
            {
                Text = "↗",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                TextAlignment = TextAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
        _brandMark.IsHitTestVisible = false;
        _actions = new StackPanel { Orientation = Orientation.Vertical };
        _card = new Border
        {
            Background = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(210, 210, 215)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(16),
            Padding = new Thickness(10),
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                BlurRadius = 22,
                ShadowDepth = 5,
                Opacity = 0.16,
                Color = Colors.Black
            },
            Child = new StackPanel
            {
                Children =
                {
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Children = { _brandMark, _payloadLabel }
                    },
                    _actions
                }
            }
        };
        Content = _card;

        SourceInitialized += _sourceInitializedHandler;
        Deactivated += (_, _) =>
        {
            // A passive overlay must never keep keyboard focus if another application activates itself.
            if (IsVisible)
            {
                SetNoActivateStyle();
            }
        };
    }

    public nint Handle => new WindowInteropHelper(this).Handle;

    public void SetModel(OverlayModel model)
    {
        _dragSessionId = model.DragSessionId;
        _payloadLabel.Text = model.PayloadLabel;
        _actions.Children.Clear();
        _actionButtons.Clear();

        foreach (var action in model.Actions)
        {
            var button = new Button
            {
                Tag = action.ActionId,
                Content = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = IconFor(action.IconId),
                            FontSize = 16,
                            Width = 28,
                            Foreground = new SolidColorBrush(Color.FromRgb(0, 122, 255)),
                            VerticalAlignment = VerticalAlignment.Center,
                            IsHitTestVisible = false
                        },
                        new TextBlock
                        {
                            Text = action.Label,
                            FontSize = 13,
                            Foreground = new SolidColorBrush(Color.FromRgb(29, 29, 31)),
                            VerticalAlignment = VerticalAlignment.Center,
                            IsHitTestVisible = false
                        }
                    }
                },
                IsEnabled = action.IsEnabled,
                Focusable = false,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Height = 42,
                Margin = new Thickness(0, 2, 0, 2),
                Padding = new Thickness(10, 0, 10, 0),
                Background = new SolidColorBrush(Color.FromRgb(245, 245, 247)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(210, 210, 215)),
                BorderThickness = new Thickness(1),
                Template = CreateRoundedButtonTemplate(),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            System.Windows.Automation.AutomationProperties.SetName(button, action.Label);
            System.Windows.Automation.AutomationProperties.SetHelpText(button, action.Label);
            button.MouseEnter += (_, _) => button.Background = _darkTheme
                ? new SolidColorBrush(Color.FromRgb(70, 70, 74))
                : new SolidColorBrush(Color.FromRgb(232, 241, 255));
            button.MouseLeave += (_, _) => button.Background = _darkTheme
                ? new SolidColorBrush(Color.FromRgb(58, 58, 60))
                : new SolidColorBrush(Color.FromRgb(245, 245, 247));
            button.Click += (_, _) =>
            {
                if (button.Tag is ActionId actionId)
                {
                    _actionInvoked(this, new OverlayActionInvokedEventArgs(_dragSessionId, actionId));
                }
            };
            _actions.Children.Add(button);
            _actionButtons[action.ActionId] = button;
        }

        SetLanguage(_language);
        SetTheme(_darkTheme);
    }

    public void SetLanguage(string language)
    {
        _language = string.Equals(language, "ru", StringComparison.OrdinalIgnoreCase) ? "ru" : "en";
        foreach (var button in _actions.Children.OfType<Button>())
        {
            if (button.Tag is not ActionId actionId || button.Content is not StackPanel content)
            {
                continue;
            }

            var label = content.Children.OfType<TextBlock>().LastOrDefault();
            if (label is not null)
            {
                label.Text = LocalizeActionLabel(actionId);
                System.Windows.Automation.AutomationProperties.SetName(button, label.Text);
                System.Windows.Automation.AutomationProperties.SetHelpText(button, label.Text);
            }
        }
    }

    public void SetTheme(bool darkTheme)
    {
        _darkTheme = darkTheme;
        var card = darkTheme ? new SolidColorBrush(Color.FromRgb(44, 44, 46)) : Brushes.White;
        var control = darkTheme ? new SolidColorBrush(Color.FromRgb(58, 58, 60)) : new SolidColorBrush(Color.FromRgb(245, 245, 247));
        var primary = darkTheme ? new SolidColorBrush(Color.FromRgb(245, 245, 247)) : new SolidColorBrush(Color.FromRgb(29, 29, 31));
        var secondary = darkTheme ? new SolidColorBrush(Color.FromRgb(174, 174, 178)) : new SolidColorBrush(Color.FromRgb(110, 110, 115));
        var border = darkTheme ? new SolidColorBrush(Color.FromRgb(72, 72, 74)) : new SolidColorBrush(Color.FromRgb(210, 210, 215));
        var accent = darkTheme ? new SolidColorBrush(Color.FromRgb(10, 132, 255)) : new SolidColorBrush(Color.FromRgb(0, 122, 255));
        _card.Background = card;
        _card.BorderBrush = border;
        _brandMark.Background = accent;
        _payloadLabel.Foreground = secondary;
        foreach (var button in _actions.Children.OfType<Button>())
        {
            button.Background = control;
            button.BorderBrush = border;
            if (button.Content is StackPanel content)
            {
                var blocks = content.Children.OfType<TextBlock>().ToArray();
                if (blocks.Length > 0) blocks[^1].Foreground = primary;
                if (blocks.Length > 1) blocks[0].Foreground = accent;
            }
        }
    }

    public void SetPlacement(OverlayPlacement placement)
    {
        Left = placement.X;
        Top = placement.Y;
        Width = placement.Width;
        Height = placement.Height;
    }

    public void ShowWithoutActivation()
    {
        if (!IsVisible)
        {
            Show();
        }

        ConfigureNativeWindow();
        NativeMethods.ShowWindow(Handle, SwShownoactivate);
        NativeMethods.SetWindowPos(
            Handle,
            HwndTopmost,
            (int)Math.Round(Left),
            (int)Math.Round(Top),
            (int)Math.Round(Width),
            (int)Math.Round(Height),
            SwpNoActivate | SwpShowWindow);
    }

    public bool TryGetActionAtScreenPoint(Point screenPoint, Guid dragSessionId, out ActionId actionId)
    {
        actionId = default;
        if (dragSessionId != _dragSessionId || !IsVisible)
        {
            return false;
        }

        Point local;
        try
        {
            local = PointFromScreen(screenPoint);
        }
        catch
        {
            return false;
        }

        var targets = _actionButtons.Select(pair =>
        {
            var button = pair.Value;
            try
            {
                var topLeft = button.TranslatePoint(new Point(0, 0), this);
                var width = button.ActualWidth > 0 ? button.ActualWidth : button.RenderSize.Width;
                var height = button.ActualHeight > 0 ? button.ActualHeight : button.RenderSize.Height;
                return new OverlayHitTarget(
                    pair.Key,
                    new RectD(topLeft.X, topLeft.Y, topLeft.X + width, topLeft.Y + height),
                    button.IsEnabled && button.IsHitTestVisible);
            }
            catch (InvalidOperationException)
            {
                return new OverlayHitTarget(pair.Key, new RectD(double.NaN, double.NaN, double.NaN, double.NaN), false);
            }
        }).ToArray();
        var resolved = OverlayHitTestPolicy.Resolve(
            new PointD(local.X, local.Y),
            targets);
        if (resolved is { } candidate)
        {
            actionId = candidate;
            return true;
        }

        return false;
    }

    protected override void OnClosed(EventArgs e)
    {
        SourceInitialized -= _sourceInitializedHandler;
        base.OnClosed(e);
    }

    private void ConfigureNativeWindow()
    {
        if (Handle == nint.Zero)
        {
            return;
        }

        SetNoActivateStyle();
        NativeMethods.SetWindowPos(Handle, HwndTopmost, 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpNoActivate);
    }

    private void SetNoActivateStyle()
    {
        var current = NativeMethods.GetWindowLongPtrW(Handle, GwlExstyle).ToInt64();
        var desired = current | WsExNoActivate | WsExToolWindow | WsExTopmost;
        if (desired != current)
        {
            NativeMethods.SetWindowLongPtrW(Handle, GwlExstyle, new nint(desired));
        }
    }

    private static string IconFor(string iconId) => iconId switch
    {
        "compress" => "↘",
        "webp" => "W",
        "metadata-remove" => "✦",
        _ => "•"
    };

    private string LocalizeActionLabel(ActionId actionId) => actionId.Value switch
    {
        "image.compress" => _language == "ru" ? "Сжать" : "Compress",
        "image.convert.webp" => _language == "ru" ? "Конвертировать в WebP" : "Convert to WebP",
        "image.remove-metadata" => _language == "ru" ? "Удалить метаданные" : "Remove Metadata",
        _ => _language == "ru" ? "Действие SmartDrag" : "SmartDrag action"
    };

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
        presenter.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Stretch);
        presenter.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);
        presenter.SetBinding(ContentPresenter.ContentProperty, new Binding(nameof(ContentControl.Content))
        {
            RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent)
        });
        border.AppendChild(presenter);
        var template = new ControlTemplate(typeof(Button)) { VisualTree = border };
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
        return template;
    }

    private static class NativeMethods
    {
        [DllImport("user32.dll", SetLastError = true)]
        internal static extern nint GetWindowLongPtrW(nint hwnd, int index);

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern nint SetWindowLongPtrW(nint hwnd, int index, nint value);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool ShowWindow(nint hwnd, int command);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetWindowPos(nint hwnd, nint insertAfter, int x, int y, int width, int height, uint flags);
    }
}
