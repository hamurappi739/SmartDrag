# P2 — Production Orchestration Test Matrix

`P2` is primarily deterministic/unit-testable and may be completed before G1/G2 empirical acceptance, but it must not be wired as production startup until prior gates pass.

| Case | Expected |
|---|---|
| eligible one-file image + imaging capability | overlay prepared |
| Unknown qualification | suppressed |
| Rejected qualification | suppressed |
| imaging capability unavailable | suppressed/no actions |
| empty drag session id | suppressed |
| authoritative Drop matches preflight | commit accepted |
| authoritative Drop differs from preflight | `PreflightMismatch` |
| Drop evidence is only Eligible/non-authoritative | reject |
| ActionId was not in shown overlay | `ActionWasNotOffered` |
| capability disappears before Drop | `ActionNoLongerAvailable` |
| caller attempts `PreserveSource=false` | `UnsafeOutputPolicy` |
| rejected decision passed to dispatcher | exception; zero enqueue |
| accepted decision dispatched | exactly one enqueue |
| same accepted decision dispatched twice | same `JobId`, one enqueue |
| same RequestId reused with different action | integration fault; no second enqueue |
| external adapter attempts to construct Accepted capability directly | impossible through normal typed API (internal constructors) |

## Manual integration assertions later

When P2 is wired to the real overlay/DropTarget:

- one physical Drop produces at most one ActionRequest;
- dismiss/native drag completion produces zero ActionRequests;
- stale overlay from a previous DragSession cannot authorize the next drag;
- exception in native boundary does not reach orchestration as a partially valid commit;
- a visible action that becomes unavailable before Drop fails closed rather than executing stale behavior.
