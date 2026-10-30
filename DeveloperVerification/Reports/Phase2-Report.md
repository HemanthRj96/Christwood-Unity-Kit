# Phase 2 Report: Shared Mechanics and Safe Authoring

Date: 30 October 2026. Editor 6000.5.4f1, URP 17.5.0, Input System 1.19.0, Windows 11 development machine.

## Implemented behaviour

- **Timer:** Off, Relaxed (3:00, 180 s) or Challenge (1:30, 90 s), set in Game Setup. The real seconds are shown in the inspector and the HUD, and the countdown turns red under 10 s. The timer runs during play and respawn and stops while paused or finished. Running out loses the game with "Time's Up!". An expired timer beats a goal reached on the same clock.
- **Checkpoints:** a checkpoint activates on player contact while playing and becomes the respawn pose, using its marker position and facing. Only the latest checkpoint is active, with idle and active visuals, a burst, a sound and a HUD message. Collection and time are kept on respawn, and full restart clears the checkpoint. A checkpoint below the Fall Height is ignored.
- **Fall recovery:**
  - Default lower boundary: **Catch Falling Players** plus **Fall Height**, with a "Set 5 m Below the Lowest Floor" button.
  - Optional invisible **Fall Zone** volumes.
  - Falls bypass hazard protection.
  - **Loop guard:** after 3 falls each within 1.5 s of a respawn, an active checkpoint is abandoned for the Start Point. If the Start Point itself keeps failing, the game stops with an explanation instead of looping.
  - The game also refuses to start if the Start Point is below the Fall Height.
- **Moving hazards:** `HazardMover` moves a kinematic body between the placed position and an authored end point. Speed presets are Slow, Medium and Fast (1.5, 3 and 5 m/s average), with an optional wait at each end. It moves in physics steps, so pause freezes it and restart puts it back at the start. The Scene view path uses the same `PositionAt` function as the game.
- **Channels:** Red (Circle), Blue (Square), Green (Triangle) and Yellow (Diamond). Each channel has a colour and an owned symbol mesh, so colour is never the only cue.
  - **Key:** collected once; opens every door on its channel permanently. Keys do not count toward Collect All.
  - **Pressure Switch:** held, or **Latch**. Several switches on one channel combine with an OR rule. Presses are tracked by last physics contact, so teleports release them correctly.
  - **Door:** open means no collision straight away. Closing waits until the doorway is empty, so the player can never be trapped. The panel slide is visual only.
  - No per-door references; all channel state is in the session.
- **HUD:** countdown, held keys (colour plus label), a "Time's Up!" result and a checkpoint message.
- **Editor:**
  - Inspectors for Checkpoint (facing disc), Fall Zone (box handle), Moving Hazard (end-point position handle, speed in m/s) and Key/Door/Switch.
  - Key/Door/Switch inspectors have a channel popup that recolours the piece's own renderers with Undo, plus "Opens N doors" / "Opened by …" status.
  - Gizmos: checkpoint capsule and facing, fall zones, mover path and end ghost, selected-piece channel links with an optional "show all links" toggle, and the fall-height grid when Game Setup is selected.
  - Game Setup has a Time Limit popup and a Falling section.
- **Preflight additions:** Start Point below the Fall Height (error, with an idempotent fix), level below the Fall Height (warning), Fall Zone covering the start (error), checkpoint without standing room or inside a hazard (warning), mover with no path (error), mover that isn't a hazard or passes near the start (warning), door with no opener or opener with no door (warning), and a missing channel style (warning).
- **Content:**
  - 6 new prefabs: Checkpoint, Moving Spikes, Fall Zone, Key, Door and Pressure Switch.
  - Channel materials, 4 symbol meshes, the Channel Style asset and 4 new sounds.
  - 6 new catalog entries.
  - The Gem Maze example is redesigned as a three-lane route that uses every mechanic: 6 gems, 3 static spike traps, moving spikes, 2 checkpoints, a red key and door, a latched blue switch and door, and the Relaxed timer.

## Acceptance results

| Check | Type | Result |
|---|---|---|
| Compile | Auto | 0 errors, 0 warnings in owned code |
| Generate + preflight | Auto | SUCCESS; starter and example have 0 errors and 0 warnings |
| Generator repeatability | Auto | 79/79 GUIDs identical across runs |
| EditMode tests | Auto | **38/38 passed** |
| PlayMode tests | Auto | **18/18 passed** (includes A10 two-scene isolation and the 10-cycle restart) |
| Player build | Auto | Succeeded, 0 errors, 0 warnings |
| Player run | Auto (synthesized keys) | Gem collected (1/6), countdown 2:47 then held at 2:46 while paused, doors, switch and checkpoints rendered, no errors in the player log |

Evidence: `DeveloperVerification/Reports/Phase2/` (test XMLs, logs, GUID lists, screenshots, template preview).

## Defects found and fixed during the phase

- **Channel Style asset saved without a script.** The `ChannelStyle` class shared a file with other types, so Unity saved the asset with no script reference and every key, door and switch lost its colour. Preflight's "no channel style" warning caught it. The class now has its own file, and `EveryKitAssetLoadsWithItsScript` guards against this happening again.
- **Test timing.** Frame-count waits finish too early in batch mode. Tests now wait on game time.

## Exact remaining work for sign-off (manual)

1. In the Editor:
   - Drag the moving hazard's end handle, the Fall Zone box and the checkpoint facing disc.
   - Change a door's channel with several doors selected, then Undo/Redo.
   - Check that the channel link lines read clearly.
2. In the built player:
   - Play the Gem Maze route end to end: checkpoint, key, latched switch, both doors, then win.
   - Let the timer run out once to see "Time's Up!".
   - Click Restart.
3. Judge the moving-spike timing and the countdown length for 16-year-old players.

## Risks and notes

- **Mechanics are verified in Top-Down only.** First-person, third-person and side-scroller will re-run the same tests with their own rigs in Phases 3–5.
- **The door doorway check uses an overlap box against the player's capsule.** It is conservative, so a door can stay open slightly longer than strictly needed.
- **Quick-fall loop guard thresholds** (3 falls, 1.5 s) are chosen values, not measured ones.
- **The A13 performance target is still blocked.** Lab hardware is unknown.

## Next phase

**Phase 3: first-person template.** Mouse look with clamped pitch and sensitivity presets, cursor lock and release around pause and focus, jumping, hiding the local player mesh, and resetting the view on respawn. This also includes a first-person starter and example scene, and enabling jump-only catalog filtering.
