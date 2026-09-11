# Generated Artifact Identity and Safe Deletion

## Goal

Allow the completion panel to delete a SmartDrag-generated result without ever treating a path string as sufficient destructive authority.

## Data model

A successful action carries:

```text
GeneratedArtifact
  Path
  Identity?
    Scheme
    VolumeId
    ObjectId
```

`Identity=null` is valid and means the output exists but destructive completion must not be offered.

## Windows v1 identity

`WindowsGeneratedArtifactIdentityService` uses `GetFileInformationByHandleEx(FileIdInfo)` and stores:

- `VolumeSerialNumber` as 16 hexadecimal characters;
- `FILE_ID_128` as 32 hexadecimal characters;
- scheme `windows-file-id-v1`.

The identity is not a content hash and does not mean the file contents are unchanged. It means the current filesystem object is the same object SmartDrag committed. Editing that same file in place therefore does not invalidate identity; replacing the path with a different file does.

## Commit-time authority

Strong identity is retained only if:

```text
identity(partial before move) == identity(final after move)
```

If either capture fails or the identities differ, the generated output remains successful but has no destructive capability.

## Delete-time authority

The deletion service:

```text
CreateFileW(current path,
            FILE_READ_ATTRIBUTES | DELETE,
            FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE,
            OPEN_EXISTING,
            FILE_FLAG_OPEN_REPARSE_POINT)
    -> GetFileInformationByHandleEx(FileIdInfo)
    -> compare recorded identity
    -> mismatch: refuse and leave file untouched
    -> match: SetFileInformationByHandle(FileDispositionInfo, DeleteFile=TRUE)
    -> close same verified handle
```

No path-based `DeleteFile` call follows verification.

## Safety cases

| Case | Result |
|---|---|
| original generated file still at path | delete may proceed |
| file replaced at same path | identity mismatch; refuse |
| path replaced by symlink/reparse point | reparse object is opened; mismatch; refuse |
| strong identity unavailable | Delete command not offered |
| file already missing | report missing; no other path touched |
| delete access/sharing denied | fail; file remains |

## Scope

This identity is currently used only to authorize generated-output completion deletion. It is not yet the source-file identity model for drag input authorization.
