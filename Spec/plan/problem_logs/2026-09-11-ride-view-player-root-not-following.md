# Ride View Does Not Move Player Root

- Date: 2026-09-11
- Status: fixed in working tree
- Area: Player / Zipline ride view
- Severity: Player collision and attached systems remain away from the visible rider

## Summary

`PlayerManager.BeginRideView` started zipline visual tracking, but the per-frame update moved only `_meshObj`. The rendered character followed the trolley while the player root and its collider or other attached systems could remain elsewhere.

## Evidence

- `Assets/Scripts/InGame/Player/PlayerManager.cs`: the ride update assigned world position and rotation only to `_meshObj.transform`.
- `BeginRideView` stored mesh-local pose values and a mesh world offset; it did not update the player root.
- User-visible symptom reported on 2026-09-11: only the mesh moves, so collision and related systems do not follow.

## Regression Context

The ride-view implementation introduced separate mesh-only tracking without a regression check that compares the player root and trolley pose during a ride.

## Cause

Ride tracking treated the mesh as the moving object. The player root, which owns or anchors gameplay components such as collision, was not the transform updated by `PlayerManager`.

## Fix Requirements

- Move the player root to the trolley position plus the configured rider offset while riding.
- Rotate the player root with the trolley so attached gameplay components share the rider pose.
- Stop movement updates owned by `PlayerMovement` while ride tracking is active.
- End tracking safely when the trolley is destroyed or the ride finishes.

## Verification

- A regression test should begin a ride, move and rotate the trolley, and assert that the player root position and rotation match the trolley pose and offset.
- The same test should assert that a collider attached to the player root has moved with it.
- Runtime tests were not run in this session because they were not explicitly requested.

## Follow-up

Verify the zipline on host and client in Unity Play Mode, including normal completion and interrupted interaction.
