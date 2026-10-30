# Phase 3 Report: First-Person Template

Date: 30 October 2026. Editor 6000.5.4f1, URP 17.5.0, Input System 1.19.0, Windows 11 development machine.

## Implemented behaviour

- **First-Person Camera (`FirstPersonCameraRig`):** the rig sits at eye height (1.6 m) on the player.
  - The mouse turns the view.
  - Up and down look is clamped to ±80°, so the view can never flip.
  - Sensitivity presets: Low, Medium and High (0.06, 0.12 and 0.20° per pixel).
  - View width is adjustable from 60° to 90°.
  - No head bob or camera shake.
  - Large mouse jumps are capped per frame, and the first 2 frames after control returns are ignored, so recapturing the mouse cannot spin the view.
- **Cursor lifecycle (`WorkshopGameContext.CursorShouldLock`):** captured only while playing or respawning with a first-person camera. It is released on pause, win/loss and setup errors.
  - Losing window focus pauses the game and frees the mouse.
  - Regaining focus keeps the game paused until the player resumes, which captures the mouse again.
- **Player:**
  - The first-person player faces wherever the camera looks.
  - Movement uses the camera's yaw only, so looking up or down never makes W climb or dig.
  - The body is hidden from its own camera by rendering shadows only. No extra layers or project changes are needed.
  - Respawn and restart restore the pose and a level view facing the start or checkpoint direction.
- **Jumping (shared motor, all jumping templates):** Space jumps to the movement preset's height (1.2 m). There is a 0.1 s grace period after walking off an edge and a 0.12 s window for pressing jump just before landing.
- **Template-aware catalog:** a new `PieceTraits` component marks pieces that need jumping or would block an overhead view.
  - The Builder hides unsuitable pieces per template. Top-Down hides Raised Platform and the Tall Walls; First-Person shows everything.
  - Preflight warns when such a piece is placed by hand, and never deletes it.
- **Content:**
  - First-Person Player and Camera prefabs, Tall Wall 4 m and 2 m (3 m high), and a Raised Platform (1 m, jump to reach).
  - The First-Person template with a rendered opening-view preview, and a starter scene with a gem on a platform.
  - The **Key Maze** example: three tall-walled lanes behind red, blue and green doors. The red key is on a platform (jump), and the blue key is past slow moving spikes. It also has 4 spike traps, 2 checkpoints, the Relaxed timer, and a Reach the Exit goal.
- **Developer build** now produces one Windows player per example: `Builds/Windows/TopDown_Example/` and `Builds/Windows/FirstPerson_Example/`.

## Acceptance results

| Check | Type | Result |
|---|---|---|
| Compile | Auto | 0 errors, 0 warnings in owned code |
| Generate + preflight (4 scenes) | Auto | SUCCESS; every scene has 0 errors and 0 warnings |
| Generator repeatability | Auto | 89/89 GUIDs identical across runs |
| EditMode tests | Auto | **45/45 passed** (7 new) |
| PlayMode tests | Auto | **26/26 passed** (8 new first-person tests) |
| Player builds | Auto | Both succeeded, 0 errors, 0 warnings |
| First-person player run | Auto (real mouse and key events) | Mouse turned the view; looking up stopped at the clamp with no flip; W plus Space landed on the 1 m platform; OS cursor hidden while playing, shown when paused, hidden after resume; no errors in the player log |

### Phase 3 gate

| Gate item | Evidence |
|---|---|
| All shared mechanics work | `FirstPersonTests.KeysCheckpointsAndDoorsWorkInFirstPerson` (keys, doors, checkpoint, hazard respawn, win), `RestartRestoresTheFirstPersonGame` |
| Pause, UI and focus release and reacquire the cursor | `PauseFocusAndResultsReleaseTheCursor`; `Reports/Phase3/cursor-lifecycle.txt` from the real build |
| Respawn resets pose and view | `RespawnRestoresPoseAndView`, plus checkpoint facing in the keys test |
| Look never flips vertically | `LookIsClampedAndNeverFlips`; `screenshots/03_looked_up_clamped.png` |
| Jump works | `JumpReachesThePresetHeightAndLands`, `JumpingOntoThePlatformWorksAndWalkingDoesNot`; `screenshots/04_jumping.png` |

Evidence: `DeveloperVerification/Reports/Phase3/`.

## Defects found and fixed during the phase

- **Generated scenes lost their template reference.** The template asset was fetched before a new scene was opened, and opening the scene unloaded it. Preflight flagged it in all four scenes. Rigs now hold the template's path and load it when the scene is built.

## Exact remaining work for sign-off (manual)

1. Judge mouse sensitivity on lab mice. The presets are untested on real lab hardware.
2. Check the Editor Game view: click in to capture the mouse, press Esc to pause, then Resume, Alt+Tab, Resume.
3. Play the Key Maze end to end in the build.
4. Builder: create a First-Person team game and confirm Tall Walls and the Raised Platform appear in its catalog but not in a Top-Down game.

## Risks and notes

- **Mouse sensitivity is in degrees per pixel of raw mouse movement.** On very high-DPI mice, "Low" may still feel fast. That is a lab-hardware question.
- **E (interact) is bound but unused.** Aimed interaction is post-V1, so the controls card does not mention it.
- **Third-person and side-scroller** reuse the same motor, mechanics and cursor rules; their own gates run in Phases 4 and 5.
- **The A13 performance target is still blocked.** Lab hardware is unknown.

## Next phase

**Phase 4: third-person template.** Camera-relative movement, a visible character that turns coherently, orbit with a clamp and predictable reset, and a camera that shortens near walls and smoothly restores. The optional animation adapter, the starter scene, and the "short adventure route" example are also part of it.
