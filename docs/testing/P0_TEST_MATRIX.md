# SmartDrag P0 Test Matrix

Status values: `NOT RUN`, `PASS`, `FAIL`, `BLOCKED`.

| ID | Scenario | Expected | Status | Evidence |
|---|---|---|---|---|
| P0-01 | Explorer file-list drag emits start signal | Drag-start event recorded | NOT RUN | |
| P0-02 | Desktop icon drag emits start signal | Drag-start event recorded | NOT RUN | |
| P0-03 | Esc during drag | Cancel signal/hide; native Explorer remains usable | NOT RUN | |
| P0-04 | Native drop while overlay ignored | Native drop succeeds normally | NOT RUN | |
| P0-05 | Overlay passive presentation | Foreground HWND unchanged | NOT RUN | |
| P0-06 | Enter overlay with one PNG | `DragEnter` receives readable `CF_HDROP` | NOT RUN | |
| P0-07 | Enter overlay with unsupported payload | Returned effect is NONE | NOT RUN | |
| P0-08 | Source permits COPY + MOVE | SmartDrag returns COPY, never MOVE | NOT RUN | |
| P0-09 | Source permits MOVE only | SmartDrag returns NONE | NOT RUN | |
| P0-10 | Drop on valid action | Action ID + authoritative path recorded | NOT RUN | |
| P0-11 | Drop on empty overlay area | Rejected; effect NONE | NOT RUN | |
| P0-12 | Re-enter/leave overlay repeatedly | No crash/stuck hover | NOT RUN | |
| P0-13 | 100% DPI | Placement usable | NOT RUN | |
| P0-14 | 125% DPI | Placement usable | NOT RUN | |
| P0-15 | 150% DPI | Placement usable | NOT RUN | |
| P0-16 | Mixed-DPI dual monitors | Overlay follows intended monitor; no focus theft | NOT RUN | |
| P0-17 | Monitor left of primary (negative X) | Placement clamps correctly | NOT RUN | |
| P0-18 | Explorer restart | Probe recovers on subsequent drags | NOT RUN | |
| P0-19 | Rapid repeated drags | No stale session/hook crash | NOT RUN | |
| P0-20 | Probe stopped | CPU/work returns to zero; no hook remains | NOT RUN | |

## P0 gate

Production overlay work is blocked until the following are all PASS:

- P0-01 or a documented replacement detector exists;
- P0-04;
- P0-05;
- P0-06;
- P0-08;
- P0-09;
- P0-10;
- P0-12;
- P0-16;
- P0-20.
