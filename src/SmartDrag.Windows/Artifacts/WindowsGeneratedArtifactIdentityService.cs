using System.ComponentModel;
using System.Globalization;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using SmartDrag.Core.Artifacts;
using SmartDrag.Core.Errors;

namespace SmartDrag.Windows.Artifacts;

/// <summary>
/// Captures and verifies Windows file identity using FILE_ID_INFO. Destructive deletion is handle-bound:
/// the service opens the current path without following reparse points, reads identity from that same handle,
/// compares it with the identity captured at commit, then marks that handle for deletion.
/// </summary>
public sealed partial class WindowsGeneratedArtifactIdentityService :
    IGeneratedArtifactIdentityProvider,
    IGeneratedArtifactDeletionService
{
    public const string IdentityScheme = "windows-file-id-v1";

    private const uint FileReadAttributes = 0x00000080;
    private const uint DeleteAccess = 0x00010000;
    private const uint FileShareRead = 0x00000001;
    private const uint FileShareWrite = 0x00000002;
    private const uint FileShareDelete = 0x00000004;
    private const uint OpenExisting = 3;
    private const uint FileFlagOpenReparsePoint = 0x00200000;
    private const int FileIdInfoClass = 18;
    private const int FileBasicInfoClass = 0;
    private const int FileDispositionInfoClass = 4;
    private const int ErrorFileNotFound = 2;
    private const int ErrorPathNotFound = 3;
    private const uint FileAttributeDirectory = 0x00000010;
    private const uint FileAttributeReparsePoint = 0x00000400;

    public bool IsSupported => OperatingSystem.IsWindows();

    public Task<ArtifactIdentityCaptureResult> CaptureAsync(string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!OperatingSystem.IsWindows())
        {
            return Task.FromResult(ArtifactIdentityCaptureResult.Failed(new AppError(
                ErrorCode.ArtifactIdentityUnavailable,
                "Windows file identity was requested on a non-Windows platform.",
                "SmartDrag could not record a strong identity for the generated file.")));
        }

        try
        {
            using var handle = OpenForIdentity(path, includeDeleteAccess: false);
            if (handle.IsInvalid)
            {
                return Task.FromResult(ArtifactIdentityCaptureResult.Failed(Win32IdentityError(
                    "CreateFileW(identity capture)", Marshal.GetLastWin32Error())));
            }

            if (!TryReadFileAttributes(handle, out var attributes))
            {
                return Task.FromResult(ArtifactIdentityCaptureResult.Failed(Win32IdentityError(
                    "GetFileInformationByHandleEx(FileBasicInfo)", Marshal.GetLastWin32Error())));
            }

            if ((attributes & (FileAttributeDirectory | FileAttributeReparsePoint)) != 0)
            {
                return Task.FromResult(ArtifactIdentityCaptureResult.Failed(new AppError(
                    ErrorCode.ArtifactIdentityUnavailable,
                    "Identity capture rejected a directory or reparse-point target.",
                    "SmartDrag could not verify this generated file safely.")));
            }

            return Task.FromResult(ReadIdentity(handle));
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return Task.FromResult(ArtifactIdentityCaptureResult.Failed(new AppError(
                ErrorCode.ArtifactIdentityUnavailable,
                $"Identity capture path validation failed: {ex.GetType().FullName} (0x{ex.HResult:X8}).",
                "SmartDrag could not record a strong identity for the generated file.")));
        }
    }

    public Task<GeneratedArtifactDeletionResult> DeleteIfIdentityMatchesAsync(
        GeneratedArtifact artifact,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        cancellationToken.ThrowIfCancellationRequested();

        if (!OperatingSystem.IsWindows() || !IsSupported)
        {
            return Task.FromResult(Result(
                GeneratedArtifactDeletionStatus.Failed,
                ErrorCode.GeneratedArtifactDeleteFailed,
                "Handle-bound generated-artifact deletion is not available on this platform.",
                "Deleting the generated file is not available."));
        }

        if (artifact.Identity is null)
        {
            return Task.FromResult(Result(
                GeneratedArtifactDeletionStatus.IdentityUnavailable,
                ErrorCode.ArtifactIdentityUnavailable,
                "Generated artifact has no strong identity.",
                "SmartDrag cannot safely verify this generated file."));
        }

        if (!string.Equals(artifact.Identity.Scheme, IdentityScheme, StringComparison.Ordinal))
        {
            return Task.FromResult(Result(
                GeneratedArtifactDeletionStatus.UnsupportedIdentityScheme,
                ErrorCode.ArtifactIdentityUnavailable,
                $"Unsupported artifact identity scheme '{artifact.Identity.Scheme}'.",
                "SmartDrag cannot safely verify this generated file."));
        }

        SafeFileHandle? handle = null;
        try
        {
            handle = OpenForIdentity(artifact.Path, includeDeleteAccess: true);
            if (handle.IsInvalid)
            {
                var error = Marshal.GetLastWin32Error();
                return Task.FromResult(error is ErrorFileNotFound or ErrorPathNotFound
                    ? Result(
                        GeneratedArtifactDeletionStatus.FileMissing,
                        ErrorCode.SourceNotFound,
                        "Generated artifact path no longer exists when delete was requested.",
                        "The generated file could not be found.")
                    : Result(
                        GeneratedArtifactDeletionStatus.Failed,
                        ErrorCode.GeneratedArtifactDeleteFailed,
                        $"CreateFileW(delete verification) failed with Win32 error {error}.",
                    "SmartDrag could not safely open the generated file for deletion."));
            }

            if (!TryReadFileAttributes(handle, out var attributes))
            {
                return Task.FromResult(Result(
                    GeneratedArtifactDeletionStatus.Failed,
                    Win32IdentityError("GetFileInformationByHandleEx(FileBasicInfo)", Marshal.GetLastWin32Error())));
            }

            if ((attributes & (FileAttributeDirectory | FileAttributeReparsePoint)) != 0)
            {
                return Task.FromResult(Result(
                    GeneratedArtifactDeletionStatus.IdentityMismatch,
                    ErrorCode.ArtifactIdentityMismatch,
                    "The generated artifact path now resolves to a directory or reparse point.",
                    "The file at this location has changed, so SmartDrag will not delete it."));
            }

            var current = ReadIdentity(handle);
            if (!current.Success || current.Identity is null)
            {
                return Task.FromResult(Result(
                    GeneratedArtifactDeletionStatus.Failed,
                    current.Error ?? new AppError(
                        ErrorCode.ArtifactIdentityUnavailable,
                        "Current artifact identity could not be read.",
                        "SmartDrag could not safely verify the generated file.")));
            }

            if (current.Identity != artifact.Identity)
            {
                return Task.FromResult(Result(
                    GeneratedArtifactDeletionStatus.IdentityMismatch,
                    ErrorCode.ArtifactIdentityMismatch,
                    "Current file identity does not match the identity captured when SmartDrag committed the output.",
                    "The file at this location has changed, so SmartDrag will not delete it."));
            }

            cancellationToken.ThrowIfCancellationRequested();
            var disposition = new FILE_DISPOSITION_INFO { DeleteFile = 1 };
            if (!NativeMethods.SetFileInformationByHandle(
                    handle,
                    FileDispositionInfoClass,
                    ref disposition,
                    (uint)Marshal.SizeOf<FILE_DISPOSITION_INFO>()))
            {
                var error = Marshal.GetLastWin32Error();
                return Task.FromResult(Result(
                    GeneratedArtifactDeletionStatus.Failed,
                    ErrorCode.GeneratedArtifactDeleteFailed,
                    $"SetFileInformationByHandle(FileDispositionInfo) failed with Win32 error {error}.",
                    "SmartDrag could not delete the generated file."));
            }

            // FILE_DISPOSITION_INFO marks the opened object for deletion; closing this verified handle completes
            // deletion when sharing rules permit. No second path lookup occurs after identity verification.
            handle.Dispose();
            handle = null;
            return Task.FromResult(GeneratedArtifactDeletionResult.Deleted());
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return Task.FromResult(Result(
                GeneratedArtifactDeletionStatus.Failed,
                ErrorCode.GeneratedArtifactDeleteFailed,
                $"Generated artifact deletion failed before native disposition: {ex.GetType().FullName} (0x{ex.HResult:X8}).",
                "SmartDrag could not safely delete the generated file."));
        }
        finally
        {
            handle?.Dispose();
        }
    }

    private static SafeFileHandle OpenForIdentity(string path, bool includeDeleteAccess)
    {
        var fullPath = Path.GetFullPath(path);
        var desiredAccess = FileReadAttributes | (includeDeleteAccess ? DeleteAccess : 0u);
        return NativeMethods.CreateFileW(
            fullPath,
            desiredAccess,
            FileShareRead | FileShareWrite | FileShareDelete,
            IntPtr.Zero,
            OpenExisting,
            FileFlagOpenReparsePoint,
            IntPtr.Zero);
    }

    private static ArtifactIdentityCaptureResult ReadIdentity(SafeFileHandle handle)
    {
        if (!NativeMethods.GetFileInformationByHandleEx(
                handle,
                FileIdInfoClass,
                out FILE_ID_INFO info,
                (uint)Marshal.SizeOf<FILE_ID_INFO>()))
        {
            return ArtifactIdentityCaptureResult.Failed(Win32IdentityError(
                "GetFileInformationByHandleEx(FileIdInfo)", Marshal.GetLastWin32Error()));
        }

        return ArtifactIdentityCaptureResult.Captured(new ArtifactIdentity
        {
            Scheme = IdentityScheme,
            VolumeId = info.VolumeSerialNumber.ToString("X16", CultureInfo.InvariantCulture),
            ObjectId = string.Concat(
                info.FileId.Low.ToString("X16", CultureInfo.InvariantCulture),
                info.FileId.High.ToString("X16", CultureInfo.InvariantCulture))
        });
    }

    private static bool TryReadFileAttributes(SafeFileHandle handle, out uint attributes)
    {
        if (!NativeMethods.GetFileInformationByHandleEx(
                handle,
                FileBasicInfoClass,
                out FILE_BASIC_INFO info,
                (uint)Marshal.SizeOf<FILE_BASIC_INFO>()))
        {
            attributes = 0;
            return false;
        }

        attributes = info.FileAttributes;
        return true;
    }

    private static AppError Win32IdentityError(string operation, int error) => new(
        ErrorCode.ArtifactIdentityUnavailable,
        $"{operation} failed with Win32 error {error}.",
        "SmartDrag could not record a strong identity for the generated file.");

    private static GeneratedArtifactDeletionResult Result(
        GeneratedArtifactDeletionStatus status,
        ErrorCode code,
        string technical,
        string user) => new()
    {
        Status = status,
        Error = new AppError(code, technical, user)
    };

    private static GeneratedArtifactDeletionResult Result(
        GeneratedArtifactDeletionStatus status,
        AppError error) => new()
    {
        Status = status,
        Error = error
    };

    [StructLayout(LayoutKind.Sequential)]
    private struct FILE_ID_128
    {
        public ulong Low;
        public ulong High;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FILE_ID_INFO
    {
        public ulong VolumeSerialNumber;
        public FILE_ID_128 FileId;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FILE_BASIC_INFO
    {
        public long CreationTime;
        public long LastAccessTime;
        public long LastWriteTime;
        public long ChangeTime;
        public uint FileAttributes;
        public uint Reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FILE_DISPOSITION_INFO
    {
        public byte DeleteFile;
    }

    private static partial class NativeMethods
    {
        [LibraryImport("kernel32.dll", EntryPoint = "CreateFileW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
        internal static partial SafeFileHandle CreateFileW(
            string fileName,
            uint desiredAccess,
            uint shareMode,
            IntPtr securityAttributes,
            uint creationDisposition,
            uint flagsAndAttributes,
            IntPtr templateFile);

        [LibraryImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool GetFileInformationByHandleEx(
            SafeFileHandle file,
            int fileInformationClass,
            out FILE_ID_INFO fileInformation,
            uint bufferSize);

        [LibraryImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool GetFileInformationByHandleEx(
            SafeFileHandle file,
            int fileInformationClass,
            out FILE_BASIC_INFO fileInformation,
            uint bufferSize);

        [LibraryImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool SetFileInformationByHandle(
            SafeFileHandle file,
            int fileInformationClass,
            ref FILE_DISPOSITION_INFO fileInformation,
            uint bufferSize);
    }
}
