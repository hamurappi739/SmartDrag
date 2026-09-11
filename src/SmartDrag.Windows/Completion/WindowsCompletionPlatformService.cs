using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using SmartDrag.Core.Completion;
using SmartDrag.Core.Errors;

namespace SmartDrag.Windows.Completion;

/// <summary>
/// Windows implementation for non-destructive completion actions that do not require a production OLE
/// drag-source. Clipboard capability is advertised only when composition supplies a real owner HWND; this avoids
/// the OpenClipboard(NULL) / EmptyClipboard ownership trap and does not force a UI framework choice.
/// </summary>
public sealed partial class WindowsCompletionPlatformService : ICompletionPlatformService
{
    private const uint CfUnicodeText = 13;
    private const uint GmemMoveable = 0x0002;
    private readonly IntPtr _clipboardOwnerHwnd;

    public WindowsCompletionPlatformService(IntPtr clipboardOwnerHwnd = default)
    {
        _clipboardOwnerHwnd = clipboardOwnerHwnd;
        Capabilities = new CompletionCapabilities
        {
            CanOpenContainingFolder = true,
            CanCopyResultPath = clipboardOwnerHwnd != IntPtr.Zero,
            CanStartResultDrag = false,
            CanDeleteGeneratedOutput = false
        };
    }

    public CompletionCapabilities Capabilities { get; }

    public Task<AppError?> OpenContainingFolderAsync(string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var fullPath = Path.GetFullPath(path);
            if (!File.Exists(fullPath))
            {
                return Task.FromResult<AppError?>(new AppError(
                    ErrorCode.SourceNotFound,
                    "Completion output no longer exists when Explorer reveal was requested.",
                    "The generated file could not be found."));
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{fullPath}\"",
                UseShellExecute = true
            });
            return Task.FromResult<AppError?>(null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return Task.FromResult<AppError?>(Failure("Explorer reveal", ex));
        }
    }

    public async Task<AppError?> CopyTextAsync(string text, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(text);
        cancellationToken.ThrowIfCancellationRequested();

        if (_clipboardOwnerHwnd == IntPtr.Zero)
        {
            return new AppError(
                ErrorCode.CompletionCommandUnavailable,
                "Clipboard command was invoked without an explicit owner HWND.",
                "Copying the result path is not available in this UI context.");
        }

        // Prepare the complete HGLOBAL before touching the user's clipboard. EmptyClipboard is therefore never
        // called merely to discover that allocation/encoding failed.
        var bytes = checked((text.Length + 1) * sizeof(char));
        var memory = NativeMethods.GlobalAlloc(GmemMoveable, (UIntPtr)(uint)bytes);
        if (memory == IntPtr.Zero)
        {
            return Failure("GlobalAlloc", new Win32Exception(Marshal.GetLastWin32Error()));
        }

        var ownershipTransferred = false;
        try
        {
            var buffer = NativeMethods.GlobalLock(memory);
            if (buffer == IntPtr.Zero)
            {
                return Failure("GlobalLock", new Win32Exception(Marshal.GetLastWin32Error()));
            }

            try
            {
                var chars = (text + '\0').ToCharArray();
                Marshal.Copy(chars, 0, buffer, chars.Length);
            }
            finally
            {
                NativeMethods.GlobalUnlock(memory);
            }

            // Clipboard ownership can be transiently busy. Retry briefly; no native drag callback waits here.
            for (var attempt = 0; attempt < 5; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (NativeMethods.OpenClipboard(_clipboardOwnerHwnd))
                {
                    try
                    {
                        if (!NativeMethods.EmptyClipboard())
                        {
                            return Failure("EmptyClipboard", new Win32Exception(Marshal.GetLastWin32Error()));
                        }

                        if (NativeMethods.SetClipboardData(CfUnicodeText, memory) == IntPtr.Zero)
                        {
                            return Failure("SetClipboardData", new Win32Exception(Marshal.GetLastWin32Error()));
                        }

                        // On success SetClipboardData transfers ownership of HGLOBAL to the system.
                        ownershipTransferred = true;
                        return null;
                    }
                    finally
                    {
                        NativeMethods.CloseClipboard();
                    }
                }

                await Task.Delay(TimeSpan.FromMilliseconds(20 * (attempt + 1)), cancellationToken).ConfigureAwait(false);
            }

            return new AppError(
                ErrorCode.CompletionCommandFailed,
                "OpenClipboard remained unavailable after bounded retry.",
                "The result path could not be copied because the clipboard is busy.");
        }
        finally
        {
            if (!ownershipTransferred)
            {
                NativeMethods.GlobalFree(memory);
            }
        }
    }

    public Task<AppError?> StartResultDragAsync(string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<AppError?>(new AppError(
            ErrorCode.CompletionCommandUnavailable,
            "Production result-drag source is not implemented in foundation v0.7.",
            "Dragging the result from the completion panel is not available yet."));
    }

    private static AppError Failure(string operation, Exception ex) => new(
        ErrorCode.CompletionCommandFailed,
        $"{operation} failed: {ex.GetType().FullName} (0x{ex.HResult:X8}).",
        "The completion action failed.");

    private static partial class NativeMethods
    {
        [LibraryImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool OpenClipboard(IntPtr newOwner);

        [LibraryImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool CloseClipboard();

        [LibraryImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool EmptyClipboard();

        [LibraryImport("user32.dll", SetLastError = true)]
        internal static partial IntPtr SetClipboardData(uint format, IntPtr memory);

        [LibraryImport("kernel32.dll", SetLastError = true)]
        internal static partial IntPtr GlobalAlloc(uint flags, UIntPtr bytes);

        [LibraryImport("kernel32.dll", SetLastError = true)]
        internal static partial IntPtr GlobalLock(IntPtr memory);

        [LibraryImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool GlobalUnlock(IntPtr memory);

        [LibraryImport("kernel32.dll", SetLastError = true)]
        internal static partial IntPtr GlobalFree(IntPtr memory);
    }
}
