# Kit Architecture

Everything is in one namespace, `UnityWorkshop`. There is no dependency injection, service locator, event bus,
reflection-based wiring or ECS.

## Assemblies

| Assembly | Folder | Why it exists |
|---|---|---|
| `UnityWorkshop.Runtime` | `Assets/WorkshopKit/Runtime` | Tests cannot reference `Assembly-CSharp`, so runtime code needs a named assembly. It also keeps `UnityEditor` out of player code, which a test checks. |
| `UnityWorkshop.Editor` | `Assets/WorkshopKit/Editor` | Editor-only tools, referenced by tests and the developer tools. |
| `UnityWorkshop.Developer` | `Assets/WorkshopKitDeveloper/Editor` | Content generator and build script. Kept separate so the classroom package can exclude it. |
| `UnityWorkshop.Tests.Editor` / `.PlayMode` | `Assets/WorkshopKitDeveloper/Tests` | Test-runner discovery. Compiled only with `UNITY_INCLUDE_TESTS`. |

Features are organised by folder, not by assembly.

## Runtime responsibilities

| Class | Responsibility |
|---|---|
| `WorkshopGameContext` ("Game Setup") | One per team scene. Holds team rules, the template and the movement choice. Owns the session and input. Registers the player, camera and start points. Begins play, runs respawn, pause and full restart. |
| `WorkshopGameSession` | Plain C#. The authoritative state machine (Ready, Playing, Paused, Respawning, Won, Lost), clock, collection progress and respawn/protection timers. Raises ordinary C# events, which are cleared on `Shutdown`. |
| `WorkshopSceneBehaviour` | Base class for scene pieces. Resolves the context once, registers on enable and unregisters on disable. Switches itself off with a precise error when resolution is ambiguous. |
| `GameTemplateDefinition` | Read-only template asset: player rig, camera rig, approved movement presets, controls text, preview and starter scene GUID. |
| `MovementSettings` | Read-only movement preset. Nothing is written back at runtime. |
| `PlayerMotor` | Shared capsule motor: acceleration, gravity, grounding, jump, facing and teleport. |
| `WorkshopPlayer` | Turns input into a ground direction using the camera's basis, and drives the motor only while Playing. |
| `WorkshopCameraRig` / `TopDownCameraRig` | The single gameplay camera. The rig root follows the player; the child camera pose comes from settings, and the editor preview uses the same code. |
| `PlayerSpawn`, `Collectible`, `Hazard`, `GoalZone` | Focused pieces with local serialized settings. |
| `FirstPersonCameraRig` | Eye-height view with clamped pitch, sensitivity presets and a per-frame delta cap. The root holds yaw and the child camera holds pitch. `LocksCursor` is true. |
| `WorkshopGameContext.CursorShouldLock` | The single cursor rule: captured only while playing or respawning with a locking camera. Losing focus pauses. |
| `ThirdPersonCameraRig` | Orbit around a pivot above the player, with clamped tilt and distance presets. A sphere-probe collision check moves the camera in at once and back out smoothly. `PlaceBehind` resets the orbit and is shared with the editor preview. |
| `PlayerVisualAdapter` | Places the visual model and optionally feeds an Animator `Speed`, `Grounded` and `VerticalSpeed`. Root motion is always off. Preflight reports competing controllers, physics bodies and root motion inside the Player. |
| `SideScrollerCameraRig` | Defines the play plane (`TryGetMovementPlane`), follows sideways with a calm vertical zone, and clamps its aim to authored bounds. `PlayerMotor.LockDepth` keeps the player on the plane, Builder placement snaps pieces to it, and preflight reports off-plane pieces and paths. |
| `AdoptedAsset` + `WorkshopAssetAdoption` (editor) | Owned wrapper around a vendor model, held as a nested prefab reference so vendor files are never edited. Conflicts are disabled in the copy; built-in materials get owned URP copies; a failed adoption deletes only the folder it created. |
| `WorkshopRigTools` (editor) | Plan, then apply, a rig restore or template change: system objects only, the level and rules are kept, one Undo step. Also lists recovery milestones. |
| `PieceTraits` | Marks pieces that need jumping or block an overhead view. Drives catalog filtering and preflight suitability warnings. |
| `WorkshopHUD` | Event-driven UI: title, objective, controls, progress, countdown, held keys, goal and checkpoint messages, pause, results and setup errors. |
| `Checkpoint` | Becomes the context's respawn pose on contact while playing; one active at a time. |
| `FallZone` + Game Setup **Fall Height** | Fall respawns bypass hazard protection. The quick-fall loop guard drops the checkpoint, then stops the game with an explanation. |
| `HazardMover` | Kinematic ping-pong between the placed position and `m_Travel`, stepped in `FixedUpdate`. `PositionAt` is shared with the gizmo. |
| `ChannelPiece` → `KeyPickup`, `TriggerSwitch`, `Door` | Channel state (keys, latched channels, pressed switches) lives in the session. Doors open immediately and close only when the doorway is empty. `ChannelStyle` supplies per-channel material and symbol mesh. |
| `WorkshopSceneLoader` | Full restart reloads the saved scene. In the Editor it uses `EditorSceneManager.LoadSceneInPlayMode`, installed by the editor assembly. |

## Context resolution (runtime and editor use the same order)

1. A serialized `m_Context` reference in the same scene. The Builder and preflight fixes write this on placement.
2. An enclosing `WorkshopGameContext`.
3. The single active context registered for the requester's scene.
4. If there is no context, or more than one, the piece logs which object and scene are affected, then disables itself.

**Registration-ready step:** pieces register in `Start`/`OnEnable`. The context begins play in its first
`Update`, by which point every object loaded with the scene has registered. Script execution order does not
decide correctness. Static registries are cleared at `SubsystemRegistration`, so a run with domain reload
disabled starts clean (test A07).

## Rules that hold everywhere

- Terminal states (Won, Lost) reject collect, goal, respawn and pause.
- A hazard respawn keeps collection and the clock, clears motor velocity, suspends input for 0.4 s, then gives 1 s of protection.
- Overlapping hazard triggers cannot start a second respawn.
- Pause sets `timeScale` to 0, and resume returns to the exact state that was paused.
- The timer runs during Playing and Respawning. Expiry loses the game, and an expired timer beats a goal checked on the same clock.
- Channels: a door is open while its channel has a key, a latched switch, or any pressed switch (OR).
- ScriptableObject classes each live in a file of the same name, or Unity saves their assets without a script. A test checks this.
- Full restart reloads the saved scene, so students must save before testing. The HUD, Builder and preflight all say this.

## Editor

- **Inspectors:** a shared pattern of purpose, essential controls, status, preview toggle, then a collapsed Advanced (facilitator) section. Edits use `SerializedObject`, Undo and `RecordPrefabInstancePropertyModifications`.
- **Gizmos** (`[DrawGizmo]`) draw from the same colliders and settings the game uses. Editable handles live in each inspector's `OnSceneGUI`: start-point facing disc, pickup radius and exit bounds.
- **Preflight** (`WorkshopPreflight.Run`) is read-only. Each issue carries its target object, a message and an optional fix. Fixes are idempotent and scoped to the checked scene.
- **Workshop Builder** has Start, Build, Rules, Check and Help tabs. Team scenes are copies of a template's starter scene, given unique names under `TeamGames/`, with transactional cleanup of only the files that call created.
