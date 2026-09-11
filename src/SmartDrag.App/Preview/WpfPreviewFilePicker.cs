using System.Windows;
using Microsoft.Win32;
using SmartDrag.Presentation;

namespace SmartDrag.App.Preview;

/// <summary>
/// Native Windows picker for the safe Preview surface. The owner is captured at construction so the dialog cannot
/// open behind the main window.
/// </summary>
public sealed class WpfPreviewFilePicker(Window owner) : IPreviewFilePicker
{
    private readonly Window _owner = owner ?? throw new ArgumentNullException(nameof(owner));

    public string? PickImage(PreviewFilePickerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var dialog = new OpenFileDialog
        {
            Title = options.Title,
            Filter = options.Filter,
            FilterIndex = 1,
            CheckFileExists = true,
            Multiselect = false,
            AddExtension = false,
            InitialDirectory = options.InitialDirectory
        };
        return dialog.ShowDialog(_owner) == true ? dialog.FileName : null;
    }
}
