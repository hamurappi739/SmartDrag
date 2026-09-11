using System.Runtime.InteropServices;
using SmartDrag.Windows.Interop;

namespace SmartDrag.Windows.Shell;

/// <summary>
/// Diagnostic/documented-Shell-automation experiment for G2.
/// Reads the selected paths from the Explorer top-level window that owns a drag-start WinEvent.
/// This is not yet an accepted production mechanism: Windows 11 tabs, Desktop, virtual folders,
/// race conditions, and selection-vs-actual-drag identity must be proven in the G2 matrix.
/// </summary>
public sealed class ExplorerSelectionSnapshotReader
{
    public ExplorerSelectionReadResult Read(nint sourceHwnd)
    {
        if (sourceHwnd == nint.Zero)
        {
            return ExplorerSelectionReadResult.Failed("WinEvent did not provide an HWND.");
        }

        var sourceRoot = NativeMethods.GetAncestor(sourceHwnd, NativeConstants.GA_ROOT);
        if (sourceRoot == nint.Zero)
        {
            sourceRoot = sourceHwnd;
        }

        object? shell = null;
        object? windowsObject = null;
        try
        {
            var shellType = Type.GetTypeFromProgID("Shell.Application", throwOnError: false);
            if (shellType is null)
            {
                return ExplorerSelectionReadResult.Failed("Shell.Application COM class is unavailable.", sourceRoot);
            }

            shell = Activator.CreateInstance(shellType);
            if (shell is null)
            {
                return ExplorerSelectionReadResult.Failed("Shell.Application activation returned null.", sourceRoot);
            }

            dynamic shellDynamic = shell;
            windowsObject = shellDynamic.Windows();
            dynamic windows = windowsObject;
            var count = Convert.ToInt32(windows.Count);

            for (var index = 0; index < count; index++)
            {
                object? windowObject = null;
                object? documentObject = null;
                object? selectedObject = null;
                try
                {
                    windowObject = windows.Item(index);
                    if (windowObject is null)
                    {
                        continue;
                    }

                    dynamic window = windowObject;
                    var candidateHwnd = (nint)Convert.ToInt64(window.HWND);
                    if (candidateHwnd != sourceRoot)
                    {
                        continue;
                    }

                    documentObject = window.Document;
                    if (documentObject is null)
                    {
                        return ExplorerSelectionReadResult.Failed("Matched Explorer window has no Document object.", sourceRoot);
                    }

                    dynamic document = documentObject;
                    selectedObject = document.SelectedItems();
                    if (selectedObject is null)
                    {
                        return ExplorerSelectionReadResult.Failed("SelectedItems returned null.", sourceRoot);
                    }

                    dynamic selected = selectedObject;
                    var selectedCount = Convert.ToInt32(selected.Count);
                    var paths = new List<string>(selectedCount);
                    for (var itemIndex = 0; itemIndex < selectedCount; itemIndex++)
                    {
                        object? itemObject = null;
                        try
                        {
                            itemObject = selected.Item(itemIndex);
                            if (itemObject is null)
                            {
                                continue;
                            }

                            dynamic item = itemObject;
                            var path = item.Path as string;
                            if (!string.IsNullOrWhiteSpace(path))
                            {
                                paths.Add(path);
                            }
                        }
                        finally
                        {
                            ReleaseCom(itemObject);
                        }
                    }

                    return ExplorerSelectionReadResult.Succeeded(sourceRoot, paths);
                }
                catch (Exception ex)
                {
                    return ExplorerSelectionReadResult.Failed(
                        $"Explorer automation failed for matched window: {DescribeException(ex)}",
                        sourceRoot);
                }
                finally
                {
                    ReleaseCom(selectedObject);
                    ReleaseCom(documentObject);
                    ReleaseCom(windowObject);
                }
            }

            return ExplorerSelectionReadResult.Failed(
                "No ShellWindows entry matched the root HWND. Desktop and some tab/virtual-folder cases may require another strategy.",
                sourceRoot);
        }
        catch (Exception ex)
        {
            return ExplorerSelectionReadResult.Failed(
                $"Shell automation failed: {DescribeException(ex)}",
                sourceRoot);
        }
        finally
        {
            ReleaseCom(windowsObject);
            ReleaseCom(shell);
        }
    }

    private static string DescribeException(Exception exception) =>
        $"{exception.GetType().Name} (HRESULT 0x{exception.HResult:X8})";

    private static void ReleaseCom(object? value)
    {
        if (value is not null && Marshal.IsComObject(value))
        {
            try
            {
                Marshal.FinalReleaseComObject(value);
            }
            catch
            {
                // Diagnostic reader must fail open. COM cleanup problems are not allowed to affect Explorer.
            }
        }
    }
}

public sealed record ExplorerSelectionReadResult
{
    public required bool Success { get; init; }
    public nint RootHwnd { get; init; }
    public IReadOnlyList<string> Paths { get; init; } = Array.Empty<string>();
    public string? Error { get; init; }

    public static ExplorerSelectionReadResult Succeeded(nint rootHwnd, IReadOnlyList<string> paths) => new()
    {
        Success = true,
        RootHwnd = rootHwnd,
        Paths = paths
    };

    public static ExplorerSelectionReadResult Failed(string error, nint rootHwnd = default) => new()
    {
        Success = false,
        RootHwnd = rootHwnd,
        Error = error
    };
}
