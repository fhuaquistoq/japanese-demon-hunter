# VR Carriage Foundation

## Goal
Create the first Meta Quest 2 prototype milestone: a room-scale player starts inside a primitive carriage, sees tracked virtual hands, can grab and release a rope handle, and remains spatially guided by visible carriage railings. Rope-driving gestures and carriage lane movement are intentionally deferred.

## Decisions
- Use Meta XR SDK 205 (`OVRCameraRig` and Interaction SDK) rather than Unity XR Hands.
- Use physical room-scale movement with no forced virtual recentering or head clamping, to avoid discomfort.
- Use visible carriage walls/railings as the boundary. They communicate the safe area but cannot physically prevent a person from crossing a virtual wall.
- Use a primitive grabbable rope handle for this milestone; gesture interpretation comes later.
- Build and modify scene assets through the connected Unity Editor, not by hand-editing Unity YAML.

## Acceptance criteria
- The active prototype scene contains a primitive carriage base, four wheels, walls/railings, and two primitive horses.
- A Meta XR rig starts on the carriage and is configured for floor-level room-scale tracking and hand tracking.
- Both virtual hands are available through the Meta XR Interaction SDK.
- A rope proxy can be grabbed and released only through the configured interaction system.
- The rope has no driving behavior yet.
- The project compiles without new errors and the scene hierarchy is saved.
- Available automated tests and an Editor/Play Mode smoke check are recorded.

## Tasks
- [x] T1 — Build the primitive carriage prototype scene and reusable hierarchy.
- [x] T2 — Configure the Meta XR room-scale rig, virtual hands, and grabbable rope proxy.
- [ ] T3 — Verify compilation, hierarchy, interaction wiring, and Play Mode behavior.

## Evidence
- Exploration: Unity 6000.6.2f1; Meta XR SDK 205; Interaction SDK, OpenXR, Input System, and XR Hands packages are installed; `SampleScene` initially contained only camera, light, and volume.
- User decision: physical room-scale movement.
- T1: `CarriagePrototype` was created and saved in `Assets/Scenes/SampleScene.unity`, with reusable prefab `Assets/Prefabs/Carriage/CarriagePrototype.prefab`, four wheels, waist-low walls, two horse placeholders, shafts, and rope anchors. The hierarchy is stationary and contains no Rigidbody. Unity Console showed no new scene/script errors; two pre-existing MCP port-conflict messages remain.
- T2: Added Meta SDK 205 `OVRCameraRig` at (0, 0.695, 0), FloorLevel tracking, `OVRComprehensiveInteractionRig` with both tracked hands and hand-grab interactors, and `Assets/Prefabs/Interaction/RopeProxy.prefab` with `Grabbable` + `HandGrabInteractable`; removed template Main Camera. Scene saved; exactly one active AudioListener and no Console errors. Actual Quest hand tracking and grab/release remain unverified pending T3. Meta's rig contains its default Locomotor; verify it does not permit artificial movement in this prototype.
- T3 blocked: Unity reported 890 package EditMode tests; none project-specific were run. Play Mode was entered but Unity MCP disconnected before inspection. The parent then confirmed `playing` via `unity command editor_status`, ran `unity command editor_stop` successfully, and confirmed `playMode: stopped`; the Editor was recompiling and its Pipeline server briefly became unreachable. The CLI subsequently reconnected on port 7802: `editor_status` reported ready, compilation clean, `playMode: stopped`. A later read-only audit found `Locomotor`, `RopeProxy` with its interaction components, and rig position (0, 0.695, 0), but did not establish locomotion activation or validate grab/release. Quest-device behavior remains unverified. Four `NullReferenceException` entries were traced to Meta's `QuickActionsWizard.OnGUI` after domain reload, alongside two Meta bridge port conflicts; these are Editor/package errors, not established gameplay errors.
- Work-unit commits: pending explicit user authorization.
