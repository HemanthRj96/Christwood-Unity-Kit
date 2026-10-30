# Phase 4 Report: Third-Person Template

Date: 30 October 2026. Editor 6000.5.4f1, URP 17.5.0, Input System 1.19.0, Windows 11 development machine.

## Implemented behaviour

- **Third-Person Camera (`ThirdPersonCameraRig`):**
  - Orbits a pivot 1.5 m above the player's feet.
  - Mouse orbit is clamped to −20° … 60°, so the camera never flips or goes underground.
  - Distance presets: Close, Medium or Far (3.5, 5 or 7 m). Sensitivity presets are shared with first-person, and view width is 50–80°.
  - **Collision:** a 0.25 m sphere probe from the pivot ignores the player and triggers. The camera moves **in at once** when something is in the way, so it never shows the inside of a wall, and eases **back out** at 6 m/s.
  - **Predictable reset:** start, respawn and restart place the camera behind the player's facing at the default tilt.
  - The editor preview and the game use the same probe, so the Game view shows the real opening view before Play.
- **Camera-relative movement:** W walks away from the camera, A and D strafe relative to it, and the visible character turns smoothly toward where it walks (`PlayerFacing.MovementDirection`). Walking never turns the camera.
- **`PlayerVisualAdapter`:**
  - Places the model (offset, turn, size) on a visual child only.
  - Optionally drives an Animator's `Speed` (0–1), `Grounded` and `VerticalSpeed` parameters. Missing parameters are skipped, and the parameter list is read once and cached.
  - Always switches root motion off. The model never moves the player.
- **Competing-motion preflight:** inside the Player hierarchy, preflight reports:
  - an extra Character Controller (error, never removed automatically);
  - a non-kinematic Rigidbody (error, with a "Make Kinematic" fix);
  - Animator root motion (warning, with a "Turn Off Root Motion" fix).
  This prepares Phase 6 asset adoption.
- **Content:**
  - Third-Person Player (visible capsule with visor and backpack, so the facing reads clearly) and Third-Person Camera prefabs.
  - The template with a rendered opening-view preview, and a starter scene.
  - **Sky Steps**, the "short adventure route with collection, a jump, checkpoint and hazard": 5 gems (one on a raised platform and one on a floating stone), a tall-walled canyon that pushes the camera in, slow moving spikes, two spike traps, a checkpoint, a pit with a Fall Zone, and the exit.

## Acceptance results

| Check | Type | Result |
|---|---|---|
| Compile | Auto | 0 errors, 0 warnings in owned code |
| Generate + preflight (6 scenes) | Auto | SUCCESS; every scene has 0 errors and 0 warnings |
| Generator repeatability | Auto | 96/96 GUIDs identical across runs |
| EditMode tests | Auto | **51/51 passed** (6 new) |
| PlayMode tests | Auto | **34/34 passed** (8 new third-person; first-person restart raised from 3 to 10 cycles) |
| Player builds (3) | Auto | All succeeded, 0 errors, 0 warnings |
| Third-person player run | Auto (real mouse and key events) | Mouse orbited the camera; D moved the character right and it turned to face that way; Space jumped; OS cursor hidden while playing, visible when paused, hidden after resume; no errors in the player log |

### Phase 4 gate

| Gate item | Evidence |
|---|---|
| Camera follows reliably near walls, slopes and corners | `WallsPullTheCameraInAndItEasesBackOut`: end wall and canyon corner; the camera is never inside geometry, eases back to full distance, and the editor preview matches (`EditorPreviewPullsTheCameraInFrontOfWalls`). Slopes: the kit has no ramp piece yet, so slope behaviour is **not tested**. |
| Movement stays coherent during orbit | `KeyboardMovesRelativeToTheCameraAndTheCharacterTurns`: real Input System actions through a virtual keyboard after a 90° orbit; `OrbitIsClampedAndNeverFlips` |
| No competing adopted motor | `CompetingMotionInAModelIsReported`; `TemplateIsComplete` asserts the owned character has none; `AnimationAdapterDrivesOnlyTheParametersThatExist` asserts root motion is forced off |
| Shared mechanics pass | `SkyStepsRouteWorks` (gems, platform gem, checkpoint, pit fall zone to the checkpoint, win), `RestartRestoresTheThirdPersonGameTenTimes`, `RespawnResetsTheOrbitBehindThePlayer` |

Evidence: `DeveloperVerification/Reports/Phase4/`.

## Defects found and fixed during the phase

- **Camera stopped 5 cm short with nothing behind it.** The collision margin was subtracted even when nothing was hit. Fixed; tests now assert exactly full distance in the open.
- **Cramped opening view in the first Sky Steps layout.** The camera was correctly pushed in by the wall behind the start. The floor now extends one row south, so the opening view is at full distance.
- **Headless keyboard tests received no input.** The Input System ignores devices when the Game view has no focus. The test fixture now uses temporary input settings that accept unfocused input and restores the project's settings afterwards. Project settings are not changed.

## Exact remaining work for sign-off (manual)

1. In the built Sky Steps, play to the end: jump to the floating stone, deliberately fall once, and win. In the automated run, the screenshot after holding W matched the start view. That is consistent with walking into the spike trap and respawning with the camera reset, but it is not proof.
2. Judge orbit sensitivity, the Close/Medium/Far distances and the camera's ease-out speed for feel.
3. Editor: use the "Look From Behind the Start Point" preview and the adapter inspector's placement fields with Undo.
4. Try a real animated Humanoid model when one is chosen. The adapter has been tested with a generated controller only, and adoption itself is Phase 6.

## Risks and notes

- **There is no ramp or slope piece in V1 content yet.** The motor supports 45° slopes through the CharacterController, but neither slope walking nor camera behaviour on slopes is covered by tests.
- **When the player is pressed flat against a wall,** the camera can come as close as 0.3 m and sit inside the player's own capsule. That is acceptable, but noted.
- **Mechanics still verified only in Top-Down, First-Person and Third-Person.** Side-scroller is Phase 5.
- **The A13 performance target is still blocked.** Lab hardware is unknown.

## Next phase

**Phase 5: side-scroller template.** Movement locked to a 3D plane with A/D and arrow keys, jumping, a side camera with authored bounds and restrained vertical follow, snapping gameplay objects to the plane with off-plane warnings, plane-aware moving hazards, and a platform course sample.
