# Phase 5 Report: Side-Scroller Template

Date: 30 October 2026. Editor 6000.5.4f1, URP 17.5.0, Input System 1.19.0, Windows 11 development machine.

## Implemented behaviour

- **Play plane.** The side-scroller camera defines a world-Z plane (`TryGetMovementPlane`), and the shared `PlayerMotor` locks to it:
  - Desired movement and velocity lose their depth component.
  - Any drift after a collision is pulled back through the CharacterController, so walls are still respected.
  - Teleports, respawns and restarts land exactly on the plane.
  - Other templates are unaffected (`UnlockDepth`).
- **Controls:** A/D and the arrow keys move left and right, and Space jumps. W and S do nothing, because the camera's forward basis is zero. The character turns to face its direction of travel. The mouse is never captured.
- **Side-Scroller Camera (`SideScrollerCameraRig`):**
  - Distance presets: Close, Medium or Far (10, 14 or 18 m). An 8° tilt shows the top of the ground.
  - Smooth sideways follow.
  - **Restrained vertical follow:** the camera moves up or down only when the player's aim point leaves a calm zone (default 1.5 m), so normal jumps don't move the view.
  - **Authored bounds:** the aim point is clamped inside a Bottom Left / Top Right rectangle, edited with corner handles in the plane.
  - No auto-scroll. Start, respawn and restart snap the framing.
- **Placement snapping:** Builder **Add** puts every piece on the play plane, and doors are turned 90° so they block left/right travel. The Build tab says this.
- **Off-plane preflight:**
  - A gameplay piece more than 0.25 m off the plane gets a warning with an idempotent **Snap to Plane** fix.
  - A moving hazard whose path leaves the plane gets a warning with a **Keep on Plane** fix.
  - Scenery (trees, crates) may sit in front of or behind the plane.
- **Visualization:** a play-plane line and level rectangle, the camera bounds rectangle with draggable corners, and a red "Off the play plane" line from any misplaced gameplay piece to its spot on the plane.
- **Content:**
  - Side-Scroller Player and Camera prefabs, and a **Floating Platform** (3 × 3 m; needs jumping, so it is hidden in Top-Down).
  - The template with a rendered preview, and a starter scene.
  - **Cliff Run**, the platform-course sample: four ground sections and pits crossed on floating platforms, a raised step with a gem, spike traps to hop over, slow moving spikes, a checkpoint halfway, 5 gems, and the exit at the far right. Falling into a pit returns the player to the start or the checkpoint.
- **Test tooling:** the virtual keyboard (temporary Input System settings that accept unfocused input, restored afterwards) is now shared by all play tests.

## Acceptance results

| Check | Type | Result |
|---|---|---|
| Compile | Auto | 0 errors, 0 warnings in owned code |
| Generate + preflight (8 scenes) | Auto | SUCCESS; every scene has 0 errors and 0 warnings |
| Generator repeatability | Auto | 104/104 GUIDs identical across runs |
| EditMode tests | Auto | **58/58 passed** (7 new) |
| PlayMode tests | Auto | **42/42 passed** (8 new side-scroller tests) |
| Player builds (4) | Auto | All succeeded, 0 errors, 0 warnings |
| Side-scroller player run | Auto (real key events) | D ran right with the camera following; a running jump lifted the player; W did nothing; the Left arrow ran left and the character turned left; the cursor stayed free; pause works; no errors in the player log |

### Phase 5 gate

| Gate item | Evidence |
|---|---|
| No depth drift | `RunningAndJumpingNeverLeaveThePlane`: worst |Δz| < 0.001 m over 1 s of real-input running and jumping; an off-plane teleport is pulled back within 2 frames |
| Objects align with the playable plane | `EveryGameplayPieceInTheGeneratedScenesIsOnThePlane`, `BuilderPlacementSnapsToThePlaneAndTurnsDoors`, `OffPlanePiecesAndPathsAreReportedAndFixed`, `MovingSpikesStayOnThePlane` |
| Jumps and landings are repeatable | `JumpsAreRepeatable`: 5 jumps, peak spread < 0.05 m, lands in place, stays on the plane |
| Fall recovery and shared mechanics pass | `CliffRunCourseWorks` (gems, checkpoint, pit fall to the checkpoint with the camera snapping, win), `RestartRestoresTheSideScrollerTenTimes` |
| Camera bounds and restrained vertical follow | `CameraStaysInBoundsAndKeepsSmallJumpsCalm` |

Evidence: `DeveloperVerification/Reports/Phase5/`.

## Defects found and fixed during the phase

- **The side-on view hid the ground surface.** The camera now has an 8° tilt.
- **First build run looked like the Left arrow failed.** The driver script sent arrow keys without the Windows extended-key flag, so they arrived as numpad keys. With the flag set, the build confirmed arrow-key movement. This was a harness defect, not a game defect.

## Exact remaining work for sign-off (manual)

1. Play Cliff Run end to end in the build and judge jump spacing and difficulty for 16-year-olds.
2. Editor: drag the camera-bounds corners, drag a gem off the plane, and check that the red line and Check's **Snap to Plane** behave as expected. Also confirm that dragging a prefab from the Builder into the Scene view gets flagged when it lands off the plane (drag-drop does not snap; Add does).
3. Judge the calm-zone size (1.5 m) and follow delay for feel.

## Risks and notes

- **Placing a Floating Platform** through the Builder puts it on the surface below the Scene-view centre; students then raise it with the move tool. Its top is at its pivot, which the description says.
- **The side-scroller uses the same 3D CharacterController.** Very thin platforms (< 0.2 m) may be stepped through at high fall speeds. The kit's floating platform is 0.4 m thick.
- **The A13 performance target is still blocked.** Lab hardware is unknown.

## Next phase

**Phase 6: Workshop Builder completion and third-party asset adoption.**
- Template replacement with an incompatibility preview.
- Restore a system rig without replacing the level.
- Recovery milestone scenes (movement ready, first winnable, objective ready, challenge ready).
- A scene overlay.
- Richer catalog previews.
- Adopt for Workshop: model/prefab wrappers, material handling and Humanoid animation mapping through `PlayerVisualAdapter`, with vendor sources left untouched (A11).
