# Phase 6: In Progress (resume here)

Stopped on 30 October 2026 when the usage limit was reached.

## Built, and compiling with 0 errors

- **Asset adoption** (`Editor/Adoption`):
  - Analysis, owned wrapper prefabs, neutralising conflicting components, the material converter, the Humanoid locomotion controller, player looks, and the Adopt for Workshop window with its Assets menu entries.
  - Transactional cleanup: a failed adoption removes the folder it created.
- **Builder:**
  - Adopted props and looks appear in the catalog.
  - Drag-and-drop goes through `WorkshopPlacement` (`WorkshopCatalogDrop`).
  - Team-owned custom movement (`WorkshopTeamAssets`), with a preflight warning when a custom preset isn't team-owned.
  - Facilitator section in Help: Restore System Rig, Change Perspective with a preview, Recovery copy, and Adopt.
- **`WorkshopRigTools`:** plan/apply for restoring the rig and changing the template, with Undo, and an adopted look carried across a template change.
- **`WorkshopSceneOverlay`:** uses the Unity 6000.5 `[Overlay]` IMGUI API.
- **Generator:** 16 recovery milestones under `Recovery/<Perspective>/`, and a `PlayerVisualAdapter` on every player prefab. The batch preflight now checks the recovery folder too. All 24 generated scenes have 0 errors and 0 warnings.

## First test run: 70/78 EditMode passed, 8 failed (`first-run-failures.txt`)

1. **`VendorSpinner` lives in an Editor test folder,** so it cannot be added to a prefab ("editor script"). This breaks the robot fixture: `AnalysisDetectsConflictsAndMaterials` and `ConflictingPartsAreTurnedOffOnlyInTheOwnedCopy`.
   Fix: move `VendorSpinner.cs` to a non-editor folder, for example the PlayMode test assembly or a new folder under `WorkshopKitDeveloper`.
2. **`MissingReferenceException` in `ApplyPlayerLook`** (line ~571, `look.gameObject`). The `AdoptedAsset` loaded before `EditorSceneManager.OpenScene` is unloaded by the scene switch. This is the known NewScene/OpenScene unload gotcha.
   Fix: in tests and Builder callers, re-load the look by path after opening the scene. Or have `ApplyPlayerLook` take the prefab path or GUID and load it itself. Affects 4 tests.
3. **`AdoptedPropIsAnOwnedWrapper…`: converted `_BaseColor` does not match.** Likely sRGB/linear colour handling in the Standard→URP copy, or the `_Color` value is not what was set.
   Fix: inspect the value; compare with a tolerance and check the conversion.
4. **`ChangingTemplate…` and `ChangingToSideScroller…`** may be follow-on failures. Re-check after fixes 1–2 and read their messages.
5. **`DragAndDropLandsOnSurfacesOrThePlayPlane`: "Lands on the floor" failed.** The ray from (-8, 20, -8) probably hits a boundary or wall top, or the test scene's floor top is at y = 0 but another collider is higher.
   Fix: aim the ray at an open tile centre, for example (-6, 20, -6), or assert against `SurfaceHeight`.

## Not started yet

- PlayMode suite rerun.
- GUID-stability check.
- Builds.
- Phase 6 report and checklist update.
- Humanoid end-to-end adoption: blocked until a real Humanoid FBX is supplied. Only the controller mapping is unit-tested.
