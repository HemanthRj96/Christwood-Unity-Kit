# Phase 6 Report: Workshop Builder Completion and Asset Adoption

Dates: 30–31 October 2026. Editor 6000.5.4f1, URP 17.5.0, Input System 1.19.0, Windows 11 development machine.

## Implemented behaviour

### Asset adoption (A11): Assets > Adopt for Workshop, or Tools > Unity Workshop > Adopt for Workshop

- **Detection before anything is written.** The window shows:
  - the asset type (model or prefab), its renderers and size;
  - whether it is a Humanoid, Generic or unrigged model;
  - conflicting parts (cameras, audio listeners, character controllers, physics bodies, root-motion Animators, vendor scripts, missing scripts);
  - which materials are compatible, convertible or unsupported;
  - the files it will create.

  Materials alone, animations alone and the kit's own assets are refused with an explanation.
- **Owned wrapper.** `AdoptedAssets/<Name>/<Name>.prefab`:
  - The root stays at rotation zero and scale one, and holds an `AdoptedAsset` marker recording the role, the vendor GUID and notes.
  - The model is a **nested reference to the vendor prefab or model**, never a copy.
  - Facing turn, size presets (0.5, 1.8 or 3 m tall), floor alignment and centring go on the model child only.
- **Conflicts are switched off only in the owned copy:**
  - cameras, audio listeners and character controllers are disabled;
  - physics bodies are made kinematic;
  - root motion is turned off;
  - vendor scripts are disabled.

  Each change is listed in the result and stored in the marker's notes.
- **Collision policy:** Automatic keeps simple existing colliders and replaces mesh colliders with an owned box. Keep, Add Box and None are also available. Player looks never collide; the player capsule handles collision.
- **Materials:**
  - Recognised built-in shaders (Standard, Legacy Diffuse/Specular, Mobile, Unlit) get owned URP copies. Copies are shared between renderers that used the same original.
  - Mapped properties: base colour and texture, normal map, metallic/smoothness, emission and cut-out.
  - Transparency becomes opaque, and this is reported.
  - Unrecognised shaders are kept as they are and reported as limitations; nothing is guessed.
  - There is no project-wide conversion.
- **Player looks:**
  - Humanoid models with valid Idle and Run clips get an owned controller: a Speed blend tree, plus Grounded and VerticalSpeed parameters for `PlayerVisualAdapter`.
  - Humanoid models without clips, and Generic rigs, become still models, and this is reported.
- **Transactional:** a failed adoption deletes only the folder it created, including any material copies. Repeated adoption makes a new, uniquely named folder.

### Builder

- **Catalog:** adopted props appear under Decoration. Adopted looks appear under Player Appearance with **Use**, and **Default Look** restores the template's model.
- **Applying a look:**
  - The default model is hidden (not deleted) and the look is added under the player.
  - `PlayerVisualAdapter` is pointed at the look, and its Animator if it has one.
  - On a first-person player, the look renders shadows only.
  - Applying is one Undo step, and applying twice never stacks models.
- **Drag-and-drop** from catalog pictures now goes through the same placement as Add: grouping, grid and surface snapping, the side-scroller play plane, door turning and Game Setup wiring. Drops ignore the player's capsule.
- **Rules:**
  - **Customise for This Team…** copies the chosen movement preset into the team's folder; it can then be edited inline or turned off.
  - Shared presets never change, and the option only works in TeamGames scenes.
  - Preflight warns if a custom preset is not team-owned, with an idempotent fix.
- **Help → Advanced (facilitator):**
  - **Restore System Rig:** re-adds or replaces a missing or broken Game Setup, Player, Camera, HUD or Event System. It keeps the level and the Game Setup configuration, and refuses when there are several Game Setups.
  - **Change Perspective:** switches to another template with a preview. Pieces unsuitable for the new template and pieces off a new side-scroller plane are listed. Rules, the level and any adopted look are kept, it is one Undo step, and preflight runs afterwards.
  - **Recovery Milestones:** copies a milestone into a team folder as a new, uniquely named scene. The current scene is never overwritten.
  - **Adopt for Workshop…**
- **Scene overlay "Unity Workshop"** (Unity 6 `[Overlay]` IMGUI API): shows the template, the snapping rule, what the selected piece does, the last Check result, Run Check, and a link to open the Builder.

### Content

- **16 recovery milestones**, under `Recovery/<Perspective>/`: 1 Movement Ready, 2 First Winnable, 3 Objective Ready, 4 Challenge Ready, for each of the four templates. Each is a valid, playable game.
- **Every player prefab** now has a `PlayerVisualAdapter`, so any template can take an adopted look.

## Acceptance results

| Check | Type | Result |
|---|---|---|
| Compile | Auto | 0 errors, 0 warnings in owned code |
| Generate + preflight (24 scenes: 4 starters, 4 examples, 16 milestones) | Auto | SUCCESS; every scene has 0 errors and 0 warnings |
| Generator repeatability | Auto | 124/124 GUIDs identical across runs (see the churn note below) |
| EditMode tests | Auto | **78/78 passed** (20 new: 11 adoption, 9 facilitator) |
| PlayMode tests | Auto | **42/42 passed** |
| Player builds (4) | Auto | All succeeded, 0 errors, 0 warnings |
| Project settings | Auto | `EditorSettings` play-mode options back at the committed value (0) |

### A11: Adopt a static prop, a compatible animated character and an incompatible asset

| Case | Status | Evidence |
|---|---|---|
| Static prop | **Auto** pass: owned wrapper; vendor bytes and GUIDs unchanged (SHA-256 of every vendor file and `.meta`); floor-aligned, centred and sized; mesh collider replaced by a box; Standard material converted to URP Lit (colour, smoothness, cut-out); in the catalog and places cleanly | `AdoptedPropIsAnOwnedWrapperAndVendorFilesAreUntouched`, `AdoptedPropIsInTheCatalogAndPlacesCleanly`, `RepeatedAdoptionNeverCollides` |
| Incompatible asset (camera, listener, controller, physics body, root motion, vendor script, unsupported shader) | **Auto** pass: all neutralised in the owned copy only; the vendor original keeps everything; the unsupported shader is reported | `AnalysisDetectsConflictsAndMaterials`, `ConflictingPartsAreTurnedOffOnlyInTheOwnedCopy` |
| Character as player look (Generic rig) | **Auto** pass: applied, undoable, shadows-only in first-person, survives a template change, and plays with only the workshop capsule colliding while standing still on the floor | `PlayerLook…`, `AdoptedLookSurvivesAChangeOfTemplate`, `AdoptedLookPlaysWithTheWorkshopMotor` |
| **Compatible animated Humanoid** | **Blocked**: the project has no Humanoid FBX. The controller mapping is unit-tested (`LocomotionControllerMappingIsComplete`), and non-Humanoid clips are refused, but avatar validation and animation playback have not been run end to end | – |
| Failure cleanup | **Auto** pass: a simulated failure removes only the new folder; earlier adoptions and vendor files are unchanged | `FailedAdoptionRemovesOnlyItsOwnFiles` |

The test "vendor" assets are generated by the tests themselves (`VendorFixture`, `Assets/ZZ_TestVendor`, removed afterwards). Real third-party packages may have quirks these do not cover.

### Phase 6 gate

| Gate item | Evidence |
|---|---|
| A new user can create a game, adopt a supported visual, place mechanics, validate and play without code or wiring | Automated steps: `TeamSceneFactory` create → adopt → apply look → place → preflight 0 errors → play (`AdoptedLookPlaysWithTheWorkshopMotor`). **Not yet done by a person in the Editor UI.** |
| Repeated setup creates no duplicates and preserves content | `RestoreRigReAddsMissingSystemsAndKeepsTheLevel` (second run: "Nothing to do"), `MissingSystemFixesAreIdempotent`, `RepeatedAdoptionNeverCollides`, `CopyingAMilestoneMakesANewTeamScene` |
| Template replacement is explicit, previewed and keeps the environment | `ChangingTemplateKeepsTheLevelAndRulesAndIsUndoable`, `ChangingToSideScrollerPreviewsOffPlanePieces` |

Evidence: `DeveloperVerification/Reports/Phase6/`. The diagnosis of the first test run is in `first-run-failures.txt`.

## Defects found and fixed during the phase

1. **The stand-in vendor script was in an Editor test assembly,** and Unity refuses to attach editor scripts to prefabs. It was moved to the PlayMode test assembly.
2. **Tests held an adopted asset across `OpenScene`,** which unloads assets held only in code. Tests now reload by path after opening a scene. The Builder already builds its entries fresh each repaint.
3. **A colour comparison failed on float precision.** It now uses a tolerance.
4. **Drag-and-drop landed on the player's capsule** when dropped on the Start Point. Drops now ignore the player.
5. **The play-mode adoption test changed `EditorSettings`.** The test yielded `EnterPlayMode` from a nested routine, which the test framework ignores, so it never entered Play mode. When it then failed, its `finally` never ran and the project's play-mode options were left changed. The fix: `EnterPlayMode` is yielded directly, the options are restored in `[TearDown]`, and `ProjectSettings/EditorSettings.asset` was restored from git and confirmed at its committed value.

## Exact remaining work for sign-off (manual)

1. With a real third-party pack:
   - adopt a prop and a Humanoid character with Idle and Run clips;
   - check the materials look right;
   - use the look in each template.
2. In the Editor, go through the Builder end to end:
   - drag pieces into the Scene view;
   - Customise for This Team;
   - Restore System Rig and Change Perspective dialogs, then Ctrl+Z;
   - copy a milestone;
   - check the overlay is readable.
3. Novice walkthrough of create → adopt → place → check → play (A15 rehearsal).

## Risks and notes

- **Regeneration churns local object IDs.** `SaveAsPrefabAsset` and rebuilt scenes give child objects new internal IDs on every run. GUIDs and behaviour stay identical, but each regeneration produces large git diffs in generated scenes and prefabs.
  - Follow-up: update prefabs in place, for example with `PrefabUtility.LoadPrefabContents`, so output is byte-stable.
  - The working tree currently holds such a regeneration. It is equivalent to the committed, tested content: same GUIDs, all 24 scenes pass preflight.
- **`DeveloperVerification/Builds` is committed to git** (four players, about 100 MB each). A `.gitignore` entry is recommended. It was not changed without asking.
- **Humanoid adoption is unverified** until a real asset is supplied.
- **The A13 performance target is still blocked.** Lab hardware is unknown.

## Next phase

**Phase 7: hardening and classroom delivery.**
- Full acceptance pass.
- Clean-copy import (A01).
- Offline check (A14).
- Lab profiling once hardware is known (A13).
- Novice rehearsal (A15).
- Remove temporary artifacts.
- Package the developer source and a classroom version without `WorkshopKitDeveloper` (A16).
- Write the facilitator guide, student quick start and controls cards.
