# Implementation Checklist

Resume point for any new session: read this file, `Documentation/Baseline.md` and `Documentation/Architecture.md`,
then the latest phase report in `DeveloperVerification/Reports/`. Status is based on evidence, not on files existing.

Legend: **Auto** = executed automated check · **Manual** = needs a person · **Blocked** = cannot run yet · **Pending** = not implemented yet.

## Phases

| Phase | Status | Report |
|---|---|---|
| 0 Baseline | Done | `Documentation/Baseline.md` |
| 1 Complete top-down game | Done (automated gate passed; manual Editor checks outstanding) | `Reports/Phase1-Report.md` |
| 2 Shared mechanics and safe authoring | Done (automated gate passed; manual Editor checks outstanding) | `Reports/Phase2-Report.md` |
| 3 First-person | Done (automated gate passed; manual feel and Editor checks outstanding) | `Reports/Phase3-Report.md` |
| 4 Third-person | Done (automated gate passed; feel, slope and Editor checks outstanding) | `Reports/Phase4-Report.md` |
| 5 Side-scroller | Done (automated gate passed; course feel and Editor checks outstanding) | `Reports/Phase5-Report.md` |
| 6 Builder completion and asset adoption | **In progress**: code complete, 8 of 78 EditMode tests failing; see `Reports/Phase6-InProgress/RESUME.md` | – |
| 7 Hardening and classroom delivery | Pending | – |

## Acceptance matrix

Mechanics are verified in all four templates: Top-Down, First-Person, Third-Person and Side-Scroller.

| ID | Requirement | Status | Evidence |
|---|---|---|---|
| A01 | Clean open, zero compile or missing-script errors | **Auto** pass for the current project; a clean-copy import is not yet tested | 0 `error CS`/`warning CS` in owned code; `AuthoringTests.EveryKitAssetLoadsWithItsScript` |
| A02 | Each template creates a unique team scene with no hand wiring | **Auto** pass for all four templates | `AuthoringTests.CreatingTeamScenesNeverOverwrites`, `FirstPersonAuthoringTests.TeamSceneFromFirstPersonTemplateIsReady`, `ThirdPersonAuthoringTests.TeamSceneFromThirdPersonTemplateIsReady` , `SideScrollerAuthoringTests.TeamSceneFromSideScrollerTemplateIsReady` |
| A03 | Mechanics pass their template matrix | **Auto** pass for spawn, collect, hazard, goal, checkpoint, fall boundary, fall zone, moving hazard, key/door, switch and timer, in Top-Down; keys, doors, checkpoint, hazard, goal and restart in First-Person; gems, checkpoint, fall zone, hazard, goal and restart in Third-Person; gems, checkpoint, pit fall, moving hazard, goal and restart in Side-Scroller | `GameplayTests.*`, `MechanicsTests.*`, `SessionTests.*`, `FirstPersonTests.*`, `ThirdPersonTests.*`, `SideScrollerTests.*` |
| A04 | Collect once; no double win, score or respawn | **Auto** pass, keys included | `SessionTests`, `GameplayTests.CollectAllThenExitWinsOnce`, `MechanicsTests.CheckpointBecomesTheRespawnPoint` |
| A05 | Timer, victory, pause and respawn priority | **Auto** pass | `SessionTests.TimerExpiryLosesOnce`, `ExpiredTimerBeatsSimultaneousGoal`, `TimerPausesAndKeepsRunningDuringRespawn`, `PauseRestoresThePausedState` |
| A06 | Ten restart cycles restore everything | **Auto** pass, 10 cycles each in all four templates (moving hazard reset tested separately) | `GameplayTests.RestartRestoresEverythingTenTimes`, `MechanicsTests.MovingHazardMovesFreezesAndResetsOnRestart` |
| A07 | Repeated Play with domain reload disabled | **Auto** pass | `PlaySessionTests.RepeatedPlaySessionsStartClean` |
| A08 | Save/reopen, Undo/Redo, multi-select, handles | **Auto** partial: scene persistence, and channel change plus Undo via API. **Manual** outstanding: handles, inspectors and multi-select in the real Editor UI | `AuthoringTests.ChannelChangeUpdatesLookAndIsUndoable`, `SharedPresetsAreNotTeamSettings` |
| A09 | Repeatable setup, safe fixes and cleanup | **Auto** pass for generator idempotency (79 GUIDs identical) and fix idempotency (fall height, missing systems) | `Reports/Phase2/guids-*.txt`, `AuthoringTests.StartBelowFallHeightIsAnErrorWithFix` |
| A10 | Two contexts in separate scenes do not cross-connect | **Auto** pass | `MechanicsTests.TwoGamesInSeparateScenesDoNotCrossConnect`; preflight warns on multi-scene editing and only fixes the checked scene |
| A11 | Asset adoption | **Pending** (Phase 6). Groundwork: `PlayerVisualAdapter` and competing-motion preflight are tested | `ThirdPersonTests.AnimationAdapterDrivesOnlyTheParametersThatExist`, `ThirdPersonAuthoringTests.CompetingMotionInAModelIsReported` |
| A12 | Build and launch on the workshop OS | **Auto** partial: Windows x64 build launched; synthesized keys collected a gem, the countdown ran and paused, and doors, switch and checkpoints rendered. Win/loss screens and mouse clicks are **Manual**. First-person, third-person and side-scroller builds: real mouse look/orbit, keyboard movement (including arrow keys), jump and cursor behaviour recorded | `Reports/Phase2/screenshots`, `Reports/Phase3/`, `Reports/Phase4/`, `Reports/Phase5/` |
| A13 | 60 FPS at 1080p on the weakest lab machine | **Blocked**: lab hardware unknown | – |
| A14 | Offline after install | **Auto** pass by construction (no network APIs or downloads); not tested disconnected | – |
| A15 | Novice rehearsal | **Manual** (Phase 7) | – |
| A16 | Classroom package contents | **Pending** (Phase 7); developer content is isolated in `Assets/WorkshopKitDeveloper` | – |

## Known follow-ups carried into Phase 6

- Builder: template replacement with preview, restore system rig, recovery milestone scenes, scene overlay, richer catalog previews. Builder drag-and-drop does not snap to the side-scroller plane (Check reports it).
- Asset adoption (A11): wrappers under AdoptedAssets, material copies, Humanoid mapping through `PlayerVisualAdapter`, and checks that vendor sources are unchanged.
- A ramp/slope piece, then a slope camera test (third-person gate note).
- Manual Editor pass over all inspectors and handles (Phases 1–5), and feel checks on lab hardware.
