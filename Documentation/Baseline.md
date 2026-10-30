# Project Baseline (Phase 0)

Recorded 30 October 2026 from the actual project, not assumed.

## Environment

| Item | Value | Source |
|---|---|---|
| Unity Editor | 6000.5.4f1 (revision d550df8bd089) | `ProjectSettings/ProjectVersion.txt` |
| Project origin | Unity "3D (URP) cross-platform" template 17.0.14 | Editor command line at creation |
| Render pipeline | URP 17.5.0 (`PC_RPAsset` / `Mobile_RPAsset` from the template) | `Packages/manifest.json`, `Assets/Settings` |
| Colour space | Linear | `ProjectSettings.asset` (`m_ActiveColorSpace: 1`) |
| Input | Input System 1.19.0 only (`activeInputHandler: 1`) | `ProjectSettings.asset` |
| UI | uGUI 2.5.0 (legacy `Text` with the built-in `LegacyRuntime.ttf`) | lockfile |
| Tests | Unity Test Framework 1.7.0 | lockfile |
| Enter Play Mode | Options enabled, reload domain and scene (`m_EnterPlayModeOptions: 0`) | `EditorSettings.asset` |
| Build target | Windows Standalone x64 (confirmed by the user for the lab) | user, 30 Oct 2026 |
| Build modules installed | Windows Standalone, WebGL | `Editor/Data/PlaybackEngines` |
| Development machine | Windows 11, NVIDIA RTX 4070 Laptop GPU, D3D12 | player log |

Lab machine hardware is **not yet confirmed**. Performance targets (A13) remain provisional until the
weakest lab machine is profiled.

## Package decisions

- No packages were added, removed or upgraded. The kit works with what the template ships.
- **Camera:** Cinemachine is not installed, so the kit uses compact custom camera rigs (one strategy only).
- **Text:** uGUI legacy `Text` was chosen over TextMeshPro, because TMP needs its Essential Resources
  imported into `Assets/`, which would be a project change on import.
- **Input:** actions are created in code (`WorkshopInput`), so the kit does not depend on, or modify, the
  project-wide actions asset. UI uses `InputSystemUIInputModule`.
- **Physics:** 3D PhysX for every template, with `CharacterController` as the owned capsule motor.

## How Claude runs Unity

Unity runs headless from the installed Editor. The Editor must be closed while this happens, because a
project can only be open in one Editor instance.

```bash
"/c/Program Files/Unity/Hub/Editor/6000.5.4f1/Editor/Unity.exe" -batchmode -projectPath "<project>" -logFile <log> -executeMethod UnityWorkshop.WorkshopDeveloperBatch.Generate
"/c/Program Files/Unity/Hub/Editor/6000.5.4f1/Editor/Unity.exe" -batchmode -projectPath "<project>" -logFile <log> -runTests -testPlatform EditMode -assemblyNames UnityWorkshop.Tests.Editor -testResults <xml>
"/c/Program Files/Unity/Hub/Editor/6000.5.4f1/Editor/Unity.exe" -batchmode -projectPath "<project>" -logFile <log> -runTests -testPlatform PlayMode -assemblyNames UnityWorkshop.Tests.PlayMode -testResults <xml>
"/c/Program Files/Unity/Hub/Editor/6000.5.4f1/Editor/Unity.exe" -batchmode -projectPath "<project>" -logFile <log> -executeMethod UnityWorkshop.WorkshopDeveloperBatch.BuildWindowsExample
```

`Generate` must run **without** `-nographics`, because it renders the template preview image.
Batch entry points exit with code 0 on success and 1 on failure. Logs are read for `[Workshop]` lines and
`error CS` / `warning CS` from owned code.

## Ownership boundaries

| Path | Owner | Rule |
|---|---|---|
| `Assets/WorkshopKit/` | Kit | Generated and authored kit content. Ships to classrooms. |
| `Assets/WorkshopKit/TeamGames/` | Teams | Created by the Builder. Never overwritten or regenerated. |
| `Assets/WorkshopKit/AdoptedAssets/` | Kit | Wrappers and copies made by asset adoption (Phase 6). |
| `Assets/WorkshopKitDeveloper/` | Developers | Content generator, build script and tests. Excluded from the classroom package. |
| `DeveloperVerification/` | Developers | Reports, test results, screenshots and builds. Outside `Assets/`. |
| `Assets/Scenes`, `Assets/Settings`, `Assets/TutorialInfo`, `Readme.asset`, `InputSystem_Actions.inputactions` | Unity template | Left untouched. |
| `Packages/`, `ProjectSettings/` | Project | Not modified by the kit. Exception: the Builder's explicit **Create Team Game** action adds that scene to the build list, and says so before creating it. |
