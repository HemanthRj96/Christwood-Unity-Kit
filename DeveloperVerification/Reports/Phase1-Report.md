# Phase 0 and Phase 1 Report: Complete Top-Down Game

Date: 30 October 2026. Editor 6000.5.4f1, URP 17.5.0, Input System 1.19.0, Windows 11 development machine.

## Implemented behaviour

- **Game Setup (`WorkshopGameContext`):** team title, objective, win rule, collectible name, template and approved movement feel. It begins play only after every piece has registered. When setup is invalid it shows a player-facing error and stops safely instead of guessing.
- **Session:** the Ready, Playing, Paused, Respawning, Won and Lost state machine, with an authoritative clock, collection counted once, goal gating with "Collect N more Gems" messages, and a single win. Respawn holds input for 0.4 s, then gives 1 s of hazard protection. Pause resumes the exact prior state.
- **Top-down template:** WASD and arrow-key movement on the camera's ground basis, a shared capsule motor (acceleration, gravity, grounding, wall sliding, facing), and a fixed-angle follow camera with Angled and Overhead presets, distance, direction and follow delay. Three approved movement presets: Standard, Calm and Speedy.
- **Pieces:** Start Point, Gem collectible (spin, bob, particle burst, sound), Spike Trap hazard, and Exit Goal (locked and open visuals, win burst).
- **HUD:** title, objective, controls, progress, unmet-goal message, pause panel (Resume / Restart Level), results panel (time, count, Play Again) and setup-error panel. It has its own Event System with the Input System UI module.
- **Full restart:** reloads the saved scene, in the Editor even when the scene is not in the build list.
- **Editor tools:**
  - Plain-language inspectors with status, a preview toggle and a collapsed facilitator section.
  - Gizmos drawn from the real colliders.
  - Handles for start facing, pickup radius and exit size.
  - Read-only preflight with Select, Frame and idempotent fixes.
  - Workshop Builder (Tools > Unity Workshop > Workshop Builder) with Start (create a team scene), Build (catalog with thumbnails, Add, drag), Rules, Check (preflight plus the workshop checklist, with team-confirmed items marked as such) and Help tabs.
- **Owned content**, all generated through Editor APIs:
  - One "Meadow" theme with 16 URP materials.
  - 5 synthesized sounds.
  - 16 prefabs.
  - The Top-Down template asset with a rendered preview.
  - A catalog.
  - The Top-Down starter scene.
  - The "Gem Maze" example scene, which uses 6 gems, 3 spike traps, a maze, decoration and an exit.

## Acceptance results

| Check | Type | Command or procedure | Result |
|---|---|---|---|
| Compile | Auto | headless open | 0 errors, 0 warnings in owned code |
| Generate + preflight | Auto | `-executeMethod UnityWorkshop.WorkshopDeveloperBatch.Generate` | SUCCESS; starter and example have 0 errors, 0 warnings |
| Generator repeatability | Auto | 3 consecutive `Generate` runs, `.meta` GUID diff | 56/56 GUIDs identical |
| EditMode tests | Auto | `-runTests -testPlatform EditMode` | **22/22 passed** |
| PlayMode tests | Auto | `-runTests -testPlatform PlayMode` | **7/7 passed** (includes 10 full restart cycles) |
| Player build | Auto | `-executeMethod UnityWorkshop.WorkshopDeveloperBatch.BuildWindowsExample` | Succeeded, 0 errors, 0 warnings, 100 MB |
| Player run | Auto (synthesized keys) | windowed launch, W/D/Esc key events, window screenshots | Renders correctly; movement, camera follow, wall stop and Esc pause/resume work; no errors in the player log |
| Project settings untouched | Auto | content inspection | Build list, play mode options, input handler and pipeline unchanged (Unity re-serializes these files during builds) |

Evidence is in `DeveloperVerification/Reports/Phase1/`: test XMLs, generator and build logs, GUID lists, player log and screenshots.
The player build is in `DeveloperVerification/Builds/Windows/TopDownExample/`.

## Exact remaining work for Phase 1 sign-off (manual)

1. Open the Editor and use the Builder end to end: create a team game, add pieces, run Check, apply a fix, then press Play.
2. Exercise Undo/Redo after Add, the handles (facing disc, pickup radius, exit box) and multi-selection editing of gems.
3. In the built player, collect all gems, see the results panel, and click **Play Again** and **Restart Level** with the mouse.
4. Judge camera framing and movement feel. These are human checks, not automated ones.

## Risks and notes

- **Lab hardware is unknown.** Performance (A13) is unmeasured. The preflight renderer budget of 1500 is a placeholder.
- **Restart reloads the saved file.** Unsaved edits disappear on Restart. This is mitigated by messages in the HUD, Builder Help and preflight ("unsaved changes" warning with a Save fix).
- **Creating a team game adds its scene to the build list.** This is the only project-setting change, and it is part of the explicit Create action, stated in the Builder before creation.
- **Template preview rendering needs a graphics device.** Run `Generate` without `-nographics`. Without a device, it logs and skips the preview.
- **Hazard respawn in the built player is unconfirmed.** The screenshot after walking into the spike trap is consistent with a respawn but does not prove it. Respawn is covered by PlayMode tests in the Editor.
- **Unity's own warning.** Unity logs that it re-serialized `RuntimeDebugWindow_PanelSettings.asset` inside the URP package during test runs. The kit does not touch that file.
- The project is not under version control.

## Re-estimate

Phase 1 established the shared core: session, context, motor, HUD, preflight, Builder shell, generator and test harness. Phases 3–5 mainly add a camera rig, player prefab, template asset and scenes per template, on top of that core. Phase 2 (mechanics with handles) and Phase 6 (asset adoption) remain the largest pieces. The original 15–23 working-day allowance still looks plausible. It should be revisited once lab hardware and any third-party assets are known.

## Next phase

**Phase 2:** checkpoints, fall/reset volume, timer presets, moving hazards, channel keys/doors/switches, multi-scene ownership checks (A10) and their handles and preflight rules.
