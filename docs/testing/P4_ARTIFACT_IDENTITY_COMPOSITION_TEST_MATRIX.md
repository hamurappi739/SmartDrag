# P4 — Generated Artifact Identity + Composition Test Matrix

Status: **SOURCE TESTS PREPARED / WINDOWS EXECUTION PENDING G0**

## A. Commit-time identity

1. identity provider succeeds for partial and final path -> same identity retained in `GeneratedArtifact`;
2. identity provider unavailable -> output remains successful, identity null;
3. pre/post identity mismatch -> output remains successful, identity null;
4. output collision retry -> retained identity must still belong to the finally committed object;
5. source file remains untouched in every case.

## B. Completion projection

1. deletion service absent -> Delete command not offered;
2. deletion service present but artifact identity null -> not offered;
3. identity-backed single generated artifact -> Delete may be offered;
4. output path differs from artifact path -> not offered;
5. multiple outputs/artifacts -> current MVP Delete not offered.

## C. Windows handle-bound delete

On NTFS and, where available, ReFS:

1. capture identity -> delete same generated file -> success;
2. capture identity -> replace path with another file -> `IdentityMismatch`, replacement remains;
3. capture identity -> remove path -> `FileMissing`;
4. replace path with symbolic link/reparse point -> must not follow/delete target;
5. sharing violation / missing DELETE access -> safe failure, file remains;
6. read-only/unsupported filesystem cases -> safe failure, never path fallback.

Retain diagnostic result statuses but do not log raw full file paths by default.

## D. Production composition

1. `StartupSafetyBootstrapResult.AlreadyRunning` -> graph creation rejected;
2. `RecoveryBlocked` -> graph creation rejected;
3. clean bootstrap -> graph uses the same recovery journal in `PhysicalOutputManager`;
4. handlers receive `GuardedImageProcessor`, never raw codec;
5. completion executor receives the same Windows identity service used by output commit;
6. `ProductionActivationPolicy.NativeActivationEnabled` remains false until G0/G1/G2/G3 acceptance.

## E. Image guard wiring

1. unsupported content never reaches inner codec;
2. animated negative never reaches inner codec;
3. source/dimension/pixel limit rejection never reaches inner codec;
4. valid inspected JPEG/PNG reaches inner codec exactly once;
5. cancellation during inspection propagates cancellation, not a decode failure.
