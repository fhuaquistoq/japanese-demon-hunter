# Reins and three-lane carriage driving

## Objective
Make two visible, hand-grabbable reins connect the rider to the two horse placeholders. Only while a rein is held by a valid tracked hand, a quick lift-and-drop accelerates, pulling backward brakes, and pulling laterally requests one of three lane changes. At an outer lane, pulling farther outward causes a speed penalty instead of leaving the road.

## Problem and rationale
On Quest 2 the user's hand tracking works but the existing small RopeProxy is behind the player and does not visually connect to the horses or drive the carriage. This feature turns the prototype into a testable hand-controlled ride.

## Scope and constraints
- Keep existing Meta XR SDK 205 rig and primitive carriage/horses; two distinct reachable handles and two visible tethers to the horse heads/harness.
- World forward is -Z. Define three lateral lanes and a configurable lane width; no yaw turns. Shift cart, horses, tethers and XR rig as a unit while preserving room-scale tracking relative to the deck. No artificial head clamp.
- Use tracked hand ownership of the corresponding grabbed Meta HandGrabInteractable as input. Neither unheld hand motions nor tracking loss issue gestures. Released handles return to visible, reachable rest positions, never fly away.
- Acceleration uses a rising-then-fast-downward stroke; backward pull brakes; left/right pulls change exactly one lane per gesture, with hysteresis/cooldown. Define comfortable configurable thresholds/speeds.
- No enemies, candles, destination, road art, audio or shipping build. User alone compiles APK and validates on headset; never invoke build/APK compilation. The Editor may auto-import/compile newly authored scripts, but the assistant does not invoke a build.
- Edit scene and prefab assets through the connected live Unity Editor, not raw YAML.

## Acceptance checks
- Left and right reins are visible from the rider's position and visually tethered to corresponding horses even as the carriage moves.
- Either rein can be grabbed/released by its corresponding tracked hand, returning to reach when released. Input is inert when released/untracked.
- Lift and quick downward motion increases speed up to a cap; backward pull brakes toward zero; a lateral pull changes one lane without turning; outer-boundary pull decreases speed with no extra lane.
- Carriage, horses and headset origin travel together; rider remains room-scale on deck; line endpoints remain attached during travel and grabs.
- Focused non-build EditMode behavior tests and static scene/reference checks pass where available. Device proof is left to the user.

## Route and verification
- Route: delegated direct writer for multi-file script/prefab/scene changes and read-to-write preparation, followed by non-build focused verification. No separate APK build or bundle export.
- TDD: no explicit TDD mode configured in this project; use ordinary behavior tests. Test runner: Unity Test Framework EditMode (if accessible without initiating any player build). Do not claim test execution if unavailable.
- Forecast: approximately 300–450 authored source/test lines plus Editor-generated scene/prefab YAML; generated YAML excluded from the authored-line planning heuristic. Delivery strategy: ask-on-risk, no PR or commit requested.

## Tasks
- [x] R1 — Create the two visible reins/anchors and reachable left/right grab points, replacing the ineffective RopeProxy scene instance.
- [ ] R2 — Implement tracked-held-hand gesture interpretation, three-lane speed/motion model and motor wiring with focused tests; correction authored but not verified by the current Editor assembly/test registry.
- [ ] R3 — Inspect saved scene and wiring, run focused non-build tests if available, and hand over headset gesture validation to the user.

## Evidence and progress
- Starting point: clean `main` at `1f5cc5c` (`first models and hand tracking`), Quest 2 hand tracking confirmed working by the user. `RopeProxy` was only 4 cm in diameter at (0, 0.895, +0.6), behind the rider; horse heads are at (±0.55, 1.5, -3.45), while previous anchor transforms are behind the rider. No gameplay scripts/tests exist.
- Implemented in working tree: `VehicleRoot` parents carriage, Meta rig, two kinematic non-throwable handles, and two colored world-space line tethers. `ReinHandle`, `ReinGestureStateMachine`, and `CarriageMotor` implement held/tracked gestures, speed, three lanes and boundary penalty; 14/14 focused EditMode tests passed without APK build. `git diff --check` and Editor scene validation passed; no Play Mode/Quest headset validation was attempted by the assistant.
- Independent verifier found two genuine correction needs: simultaneous hands could issue two commands per frame, and an invalid-tracking handle could return to rest while Meta still selected it. Correction authored: `ReinCommandArbitration` picks at most one command per frame with Brake > LanePull > Accelerate precedence; a selected handle remains in place until every interactor releases. Four additional regression cases were authored. Verifier's claim that moving `VehicleRoot` in world X loses the rider's relative room-scale offset does not follow from Unity parenting: the rig remains a child of `VehicleRoot`. Tether visibility on headset remains pending user test.
- The Editor reports `external_changes_dirty: true`, with no current compilation, and its test registry still discovers only the older 14/18 cases. The 14 previously known cases passed, but the four new tests and corrected source have NOT been verified in the current Editor assembly. The assistant did not force refresh or compilation because the user reserved project compilation for themselves. `git diff --check` passed; Editor reported ready with Play Mode stopped.
- Native risk assessment was unavailable because review scope contains untracked new files, so independent read-only verification was run; no ordinary native review receipt claimed.
- Next: user refreshes/compiles the project and runs the 18 EditMode cases, then tests actual handles, gestures, tethers and comfort on Quest 2; report errors or gesture tuning feedback. No APK/player build by assistant.
