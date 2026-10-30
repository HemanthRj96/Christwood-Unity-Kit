using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace UnityWorkshop
{
    public enum IssueSeverity
    {
        Error,
        Warning,
        Info,
    }

    /// <summary>One preflight finding: what is wrong, why it matters, and the smallest safe fix.</summary>
    public sealed class PreflightIssue
    {
        public IssueSeverity Severity;
        public string Message;
        public Object Target;
        public string FixLabel;
        public Action Fix;

        public PreflightIssue(IssueSeverity severity, string message, Object target = null, string fixLabel = null, Action fix = null)
        {
            Severity = severity;
            Message = message;
            Target = target;
            FixLabel = fixLabel;
            Fix = fix;
        }
    }

    /// <summary>
    /// Read-only scene checks. Fixes are explicit, idempotent and limited to the scene being checked.
    /// Passing preflight means the setup is complete; it does not prove the level is reachable or fun.
    /// </summary>
    public static class WorkshopPreflight
    {
        /// <summary>Approximate renderer budget until the weakest lab machine has been profiled.</summary>
        public const int RendererBudget = 1500;

        public static bool HasErrors(List<PreflightIssue> issues) => issues.Any(i => i.Severity == IssueSeverity.Error);

        public static List<PreflightIssue> Run(Scene scene)
        {
            var issues = new List<PreflightIssue>();

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                issues.Add(new PreflightIssue(IssueSeverity.Info, "Stop Play mode to run Check. Changes made while playing are not saved."));
                return issues;
            }
            if (PrefabStageUtility.GetCurrentPrefabStage() != null)
            {
                issues.Add(new PreflightIssue(IssueSeverity.Info, "You are editing a prefab. Return to the scene (the < arrow in the Hierarchy) to run Check."));
                return issues;
            }
            if (!scene.IsValid() || !scene.isLoaded)
            {
                issues.Add(new PreflightIssue(IssueSeverity.Error, "No scene is open."));
                return issues;
            }
            if (SceneManager.sceneCount > 1)
                issues.Add(new PreflightIssue(IssueSeverity.Warning,
                    $"{SceneManager.sceneCount} scenes are open. Only '{scene.name}' is checked and fixes only change that scene. Workshop games use one scene at a time."));

            if (string.IsNullOrEmpty(scene.path))
                issues.Add(new PreflightIssue(IssueSeverity.Error, "This scene has never been saved. Create your game from the Workshop Builder or save it inside TeamGames."));
            else if (scene.isDirty)
                issues.Add(new PreflightIssue(IssueSeverity.Warning, "There are unsaved changes. Restart reloads the saved scene, so save (Ctrl+S) before testing.",
                    null, "Save Scene", () => EditorSceneManager.SaveScene(scene)));

            CheckMissingScripts(scene, issues);

            var context = CheckContext(scene, issues);
            if (context == null)
                return issues;

            CheckPlayer(scene, context, issues);
            CheckSpawn(scene, context, issues);
            CheckCamera(scene, context, issues);
            CheckGoal(scene, context, issues);
            CheckHazards(scene, issues);
            CheckFalling(scene, context, issues);
            foreach (var checkpoint in WorkshopSceneQuery.FindActive<Checkpoint>(scene))
                issues.AddRange(CheckCheckpoint(checkpoint));
            foreach (var mover in WorkshopSceneQuery.FindActive<HazardMover>(scene))
                issues.AddRange(CheckMover(mover));
            foreach (var piece in WorkshopSceneQuery.FindActive<ChannelPiece>(scene))
                issues.AddRange(CheckChannelPiece(piece).Where(i => i.Severity != IssueSeverity.Info));
            CheckPieceSuitability(scene, context, issues);
            CheckPlayPlane(scene, issues);
            CheckUi(scene, context, issues);
            CheckBudget(scene, issues);
            CheckBuildList(scene, issues);
            return issues;
        }

        // ---- Individual checks ---------------------------------------------------------------------

        static void CheckMissingScripts(Scene scene, List<PreflightIssue> issues)
        {
            foreach (var root in scene.GetRootGameObjects())
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) > 0)
                    issues.Add(new PreflightIssue(IssueSeverity.Error,
                        $"'{t.name}' has a missing script. Its behaviour will not run. Ask a facilitator, or delete the object if it is not needed.", t.gameObject));
            }
        }

        static WorkshopGameContext CheckContext(Scene scene, List<PreflightIssue> issues)
        {
            var contexts = WorkshopSceneQuery.FindActive<WorkshopGameContext>(scene);
            if (contexts.Count == 0)
            {
                issues.Add(new PreflightIssue(IssueSeverity.Error,
                    "There is no Game Setup. Nothing in the game can start without it.",
                    null, "Add Game Setup", () => AddSystemPrefab(scene, WorkshopPaths.GameSetupPrefab, null)));
                return null;
            }
            if (contexts.Count > 1)
            {
                foreach (var extra in contexts)
                    issues.Add(new PreflightIssue(IssueSeverity.Error,
                        $"There are {contexts.Count} Game Setup objects. Pieces cannot tell which game they belong to. Delete all but one.", extra.gameObject));
                return null;
            }

            var context = contexts[0];
            if (context.Template == null)
                issues.Add(new PreflightIssue(IssueSeverity.Error,
                    "Game Setup has no template, so controls help and movement presets are unknown. Recreate the game from the Workshop Builder.", context));

            var movement = new SerializedObject(context).FindProperty("m_Movement");
            if (movement.objectReferenceValue is MovementSettings chosen && context.Template != null && !context.Template.AllowsMovement(chosen))
                issues.Add(new PreflightIssue(IssueSeverity.Warning,
                    $"Movement feel '{chosen.name}' is not approved for the {context.Template.DisplayName} template, so the default is used.",
                    context, "Use Default", () => SetObjectProperty(context, "m_Movement", null, "Use default movement")));

            var custom = new SerializedObject(context).FindProperty("m_CustomMovement");
            if (custom.objectReferenceValue != null && !WorkshopTeamAssets.IsOwnedBy(custom.objectReferenceValue, scene))
                issues.Add(new PreflightIssue(IssueSeverity.Warning,
                    $"Custom movement '{custom.objectReferenceValue.name}' is not in this team's folder, so changing it could change other games too.",
                    context, "Stop Using It", () => SetObjectProperty(context, "m_CustomMovement", null, "Stop using shared custom movement")));

            if (string.IsNullOrWhiteSpace(context.Rules.title))
                issues.Add(new PreflightIssue(IssueSeverity.Warning, "Your game has no title.", context));
            if (string.IsNullOrWhiteSpace(context.Rules.objective))
                issues.Add(new PreflightIssue(IssueSeverity.Warning, "Your game has no objective sentence. Players will see a default one.", context));

            var foreign = FindForeignPieces(scene, context);
            foreach (var piece in foreign)
                issues.Add(new PreflightIssue(IssueSeverity.Error,
                    $"'{piece.name}' points at a Game Setup that is not in this scene.", piece,
                    "Reconnect", () => SetObjectProperty(piece, WorkshopSceneBehaviour.ContextPropertyName, context, "Reconnect to Game Setup")));
            return context;
        }

        static IEnumerable<WorkshopSceneBehaviour> FindForeignPieces(Scene scene, WorkshopGameContext context)
        {
            foreach (var piece in WorkshopSceneQuery.FindActive<WorkshopSceneBehaviour>(scene))
            {
                var property = new SerializedObject(piece).FindProperty(WorkshopSceneBehaviour.ContextPropertyName);
                if (property?.objectReferenceValue is WorkshopGameContext assigned && assigned.gameObject.scene != scene)
                    yield return piece;
            }
        }

        static void CheckPlayer(Scene scene, WorkshopGameContext context, List<PreflightIssue> issues)
        {
            var players = WorkshopSceneQuery.FindActive<WorkshopPlayer>(scene);
            if (players.Count == 0)
            {
                var prefab = context.Template != null ? context.Template.PlayerPrefab : null;
                issues.Add(new PreflightIssue(IssueSeverity.Error, "There is no Player.", null,
                    prefab != null ? "Add Player" : null,
                    prefab != null ? () => AddPrefab(scene, prefab, null, StartPosition(scene)) : null));
                return;
            }
            if (players.Count > 1)
            {
                foreach (var player in players)
                    issues.Add(new PreflightIssue(IssueSeverity.Error, $"There are {players.Count} Players. Keep exactly one.", player.gameObject));
                return;
            }

            var motor = players[0].GetComponent<PlayerMotor>();
            if (context.Movement == null && (motor == null || motor.Settings == null))
                issues.Add(new PreflightIssue(IssueSeverity.Error, "The Player has no movement preset, so it cannot move.", players[0]));
            if (context.Template != null && players[0].CanJump != context.Template.CanJump)
                issues.Add(new PreflightIssue(IssueSeverity.Warning,
                    $"The Player's jump setting does not match the {context.Template.DisplayName} template.", players[0]));
            issues.AddRange(CheckCompetingMotion(players[0]));
        }

        /// <summary>
        /// Parts of a player model that would fight the workshop motor: extra character controllers, physics
        /// bodies or root-motion animation. Errors cannot be fixed safely and are left for a facilitator.
        /// </summary>
        public static IEnumerable<PreflightIssue> CheckCompetingMotion(WorkshopPlayer player)
        {
            var root = player.GetComponent<CharacterController>();
            foreach (var controller in player.GetComponentsInChildren<CharacterController>(true))
                if (controller != root && controller.enabled)
                    yield return new PreflightIssue(IssueSeverity.Error,
                        $"'{controller.name}' inside the Player has its own Character Controller, which fights the workshop motor. Ask a facilitator to remove it from the model.", controller.gameObject);
            foreach (var body in player.GetComponentsInChildren<Rigidbody>(true))
                if (!body.isKinematic)
                    yield return new PreflightIssue(IssueSeverity.Error,
                        $"'{body.name}' inside the Player has a physics body that will push the player around. Make it kinematic or remove it from the model.", body.gameObject,
                        "Make Kinematic", () =>
                        {
                            if (body.isKinematic)
                                return;
                            Undo.RecordObject(body, "Make model body kinematic");
                            body.isKinematic = true;
                            PrefabUtility.RecordPrefabInstancePropertyModifications(body);
                        });
            foreach (var animator in player.GetComponentsInChildren<Animator>(true))
                if (animator.applyRootMotion)
                    yield return new PreflightIssue(IssueSeverity.Warning,
                        $"'{animator.name}' uses root motion. The workshop motor moves the player, so it is switched off when the game starts.", animator.gameObject,
                        "Turn Off Root Motion", () =>
                        {
                            if (!animator.applyRootMotion)
                                return;
                            Undo.RecordObject(animator, "Turn off root motion");
                            animator.applyRootMotion = false;
                            PrefabUtility.RecordPrefabInstancePropertyModifications(animator);
                        });
        }

        static void CheckSpawn(Scene scene, WorkshopGameContext context, List<PreflightIssue> issues)
        {
            var spawns = WorkshopSceneQuery.FindActive<PlayerSpawn>(scene);
            if (spawns.Count == 0)
            {
                issues.Add(new PreflightIssue(IssueSeverity.Error, "There is no Start Point, so the game does not know where the player begins.",
                    null, "Add Start Point", () => AddSystemPrefab(scene, WorkshopPaths.StartPointPrefab, "Gameplay")));
                return;
            }
            if (spawns.Count > 1)
            {
                foreach (var spawn in spawns)
                    issues.Add(new PreflightIssue(IssueSeverity.Error,
                        $"There are {spawns.Count} Start Points. The game will not pick one at random. Delete all but one.", spawn.gameObject));
                return;
            }

            var start = spawns[0];
            WorkshopSceneQuery.GetPlayerCapsule(context, out var radius, out var height);
            var spawnCheck = CheckStandingSpace(start.SpawnPose.position, radius, height, start.gameObject.scene);
            if (spawnCheck != null)
                issues.Add(new PreflightIssue(IssueSeverity.Error, $"Start Point: {spawnCheck}", start));

            foreach (var hazard in WorkshopSceneQuery.FindActive<Hazard>(scene))
                foreach (var collider in hazard.GetComponentsInChildren<Collider>())
                    if (collider.enabled && Vector3.Distance(collider.ClosestPointOnBounds(start.transform.position), start.transform.position) < 1.5f)
                    {
                        issues.Add(new PreflightIssue(IssueSeverity.Warning,
                            $"The Start Point is very close to hazard '{hazard.name}'. Players may be hit as soon as they start or respawn.", hazard));
                        break;
                    }
        }

        /// <summary>
        /// Conservative standing-space test for a player capsule with its feet at <paramref name="feet"/>.
        /// Returns null when there is floor below and nothing solid inside the capsule.
        /// </summary>
        public static string CheckStandingSpace(Vector3 feet, float radius, float height, Scene scene)
        {
            Physics.SyncTransforms();
            var physicsScene = scene.GetPhysicsScene();
            var hits = new RaycastHit[8];
            var count = physicsScene.Raycast(feet + Vector3.up * 0.3f, Vector3.down, hits, 1.0f, ~0, QueryTriggerInteraction.Ignore);
            var hasFloor = false;
            for (var i = 0; i < count; i++)
                if (hits[i].collider.GetComponentInParent<WorkshopPlayer>() == null)
                    hasFloor = true;
            if (!hasFloor)
                return "there is no floor underneath. Move it onto a floor piece.";

            var bottom = feet + Vector3.up * (radius + 0.1f);
            var top = feet + Vector3.up * Mathf.Max(radius + 0.1f, height - radius);
            var overlaps = new Collider[16];
            count = physicsScene.OverlapCapsule(bottom, top, radius * 0.95f, overlaps, ~0, QueryTriggerInteraction.Ignore);
            for (var i = 0; i < count; i++)
                if (overlaps[i].GetComponentInParent<WorkshopPlayer>() == null)
                    return $"there is not enough room to stand ('{overlaps[i].name}' is in the way).";
            return null;
        }

        static void CheckCamera(Scene scene, WorkshopGameContext context, List<PreflightIssue> issues)
        {
            var rigs = WorkshopSceneQuery.FindActive<WorkshopCameraRig>(scene);
            if (rigs.Count == 0)
            {
                var prefab = context.Template != null ? context.Template.CameraRigPrefab : null;
                issues.Add(new PreflightIssue(IssueSeverity.Error, "There is no gameplay camera.", null,
                    prefab != null ? "Add Camera" : null,
                    prefab != null ? () => AddPrefab(scene, prefab, null, StartPosition(scene)) : null));
            }
            else if (rigs.Count > 1)
            {
                foreach (var rig in rigs)
                    issues.Add(new PreflightIssue(IssueSeverity.Error, $"There are {rigs.Count} gameplay cameras. Keep exactly one.", rig.gameObject));
            }

            var rigCamera = rigs.Count == 1 ? rigs[0].Camera : null;
            foreach (var camera in WorkshopSceneQuery.FindActive<Camera>(scene))
            {
                if (camera == rigCamera || camera.targetTexture != null)
                    continue;
                issues.Add(new PreflightIssue(IssueSeverity.Error,
                    $"Extra camera '{camera.name}' will fight with the gameplay camera.", camera.gameObject,
                    "Turn It Off", () => DisableBehaviour(camera, "Turn off extra camera")));
            }

            var listeners = WorkshopSceneQuery.FindActive<AudioListener>(scene);
            var rigListener = rigCamera != null ? rigCamera.GetComponent<AudioListener>() : null;
            if (rigCamera != null && (rigListener == null || !rigListener.enabled))
                issues.Add(new PreflightIssue(IssueSeverity.Error, "The gameplay camera has no Audio Listener, so no sound will play.", rigCamera.gameObject,
                    "Add Listener", () => { if (rigCamera.GetComponent<AudioListener>() == null) Undo.AddComponent<AudioListener>(rigCamera.gameObject); }));
            foreach (var listener in listeners)
                if (listener != rigListener)
                    issues.Add(new PreflightIssue(IssueSeverity.Error, $"Extra Audio Listener on '{listener.name}'. A scene needs exactly one.",
                        listener.gameObject, "Turn It Off", () => DisableBehaviour(listener, "Turn off extra Audio Listener")));
        }

        static void CheckGoal(Scene scene, WorkshopGameContext context, List<PreflightIssue> issues)
        {
            var goals = WorkshopSceneQuery.FindActive<GoalZone>(scene);
            if (goals.Count == 0)
                issues.Add(new PreflightIssue(IssueSeverity.Error, "There is no Exit Goal, so the game cannot be won.",
                    null, "Add Exit Goal", () => AddSystemPrefab(scene, WorkshopPaths.ExitGoalPrefab, "Gameplay")));

            foreach (var goal in goals)
            {
                var box = goal.GetComponent<BoxCollider>();
                var size = Vector3.Scale(box.size, goal.transform.lossyScale);
                if (Mathf.Abs(size.x) < 0.5f || Mathf.Abs(size.y) < 0.5f || Mathf.Abs(size.z) < 0.5f)
                    issues.Add(new PreflightIssue(IssueSeverity.Error, $"Exit Goal '{goal.name}' is too small for the player to enter.", goal));
            }

            var collectibles = WorkshopSceneQuery.FindActive<Collectible>(scene);
            if (context.Rules.goalRule == GoalRule.CollectAllThenExit && collectibles.Count == 0)
                issues.Add(new PreflightIssue(IssueSeverity.Error,
                    "The goal is Collect All, Then Exit, but there is nothing to collect. Add collectibles or change the goal to Reach the Exit.",
                    context, "Use Reach the Exit", () => SetGoalRule(context, GoalRule.ReachExit)));
            if (context.Rules.goalRule == GoalRule.ReachExit && collectibles.Count > 0)
                issues.Add(new PreflightIssue(IssueSeverity.Info,
                    $"There are {collectibles.Count} collectibles, but the goal is Reach the Exit, so they are optional.", context));
        }

        static void CheckHazards(Scene scene, List<PreflightIssue> issues)
        {
            foreach (var hazard in WorkshopSceneQuery.FindActive<Hazard>(scene))
            {
                var colliders = hazard.GetComponents<Collider>();
                if (colliders.Length == 0)
                    issues.Add(new PreflightIssue(IssueSeverity.Error, $"Hazard '{hazard.name}' has no collider, so the player can never touch it.", hazard));
                else if (!colliders.Any(c => c.isTrigger))
                    issues.Add(new PreflightIssue(IssueSeverity.Error, $"Hazard '{hazard.name}' is solid, so touching it does nothing. Hazards must be triggers.", hazard,
                        "Make Trigger", () =>
                        {
                            foreach (var c in hazard.GetComponents<Collider>())
                                if (!c.isTrigger)
                                {
                                    Undo.RecordObject(c, "Make hazard a trigger");
                                    c.isTrigger = true;
                                    PrefabUtility.RecordPrefabInstancePropertyModifications(c);
                                }
                        }));
            }
        }

        // ---- Phase 2 mechanics --------------------------------------------------------------------

        /// <summary>Lowest point of any solid, non-player collider in the scene, or null when there are none.</summary>
        public static float? LowestSolidPoint(Scene scene)
        {
            float? lowest = null;
            foreach (var collider in WorkshopSceneQuery.FindActive<Collider>(scene))
            {
                if (collider.isTrigger || collider.GetComponentInParent<WorkshopPlayer>() != null)
                    continue;
                var y = collider.bounds.min.y;
                if (!lowest.HasValue || y < lowest.Value)
                    lowest = y;
            }
            return lowest;
        }

        static void CheckFalling(Scene scene, WorkshopGameContext context, List<PreflightIssue> issues)
        {
            if (!context.UsesFallBoundary)
                return;
            var fallHeight = context.FallHeight;
            var lowest = LowestSolidPoint(scene);
            var fix = lowest.HasValue
                ? (Action)(() => SetFloatProperty(context, "m_FallHeight", Mathf.Floor(lowest.Value - 5f)))
                : null;

            foreach (var spawn in WorkshopSceneQuery.FindActive<PlayerSpawn>(scene))
                if (spawn.SpawnPose.position.y < fallHeight)
                    issues.Add(new PreflightIssue(IssueSeverity.Error,
                        $"The Start Point is below the Fall Height ({fallHeight:0.#} m), so the player would fall forever.",
                        context, fix != null ? "Lower Fall Height" : null, fix));

            if (lowest.HasValue && lowest.Value < fallHeight)
                issues.Add(new PreflightIssue(IssueSeverity.Warning,
                    $"Part of the level goes below the Fall Height ({fallHeight:0.#} m). Players standing there will be sent back.",
                    context, "Lower Fall Height", fix));

            foreach (var zone in WorkshopSceneQuery.FindActive<FallZone>(scene))
            foreach (var spawn in WorkshopSceneQuery.FindActive<PlayerSpawn>(scene))
                if (zone.Trigger.bounds.Contains(spawn.SpawnPose.position + Vector3.up * 0.5f))
                    issues.Add(new PreflightIssue(IssueSeverity.Error,
                        $"Fall Zone '{zone.name}' covers the Start Point, so the player would be caught as soon as they appear.", zone));
        }

        public static IEnumerable<PreflightIssue> CheckCheckpoint(Checkpoint checkpoint)
        {
            var scene = checkpoint.gameObject.scene;
            var context = WorkshopSceneQuery.OwnerOf(checkpoint);
            WorkshopSceneQuery.GetPlayerCapsule(context, out var radius, out var height);
            var feet = checkpoint.RespawnPose.position;

            var problem = CheckStandingSpace(feet, radius, height, scene);
            if (problem != null)
                yield return new PreflightIssue(IssueSeverity.Warning, $"Checkpoint '{checkpoint.name}': {problem} (approximate check)", checkpoint);

            if (context != null && context.UsesFallBoundary && feet.y < context.FallHeight)
                yield return new PreflightIssue(IssueSeverity.Error,
                    $"Checkpoint '{checkpoint.name}' is below the Fall Height, so it would be ignored.", checkpoint);

            var capsuleCenter = feet + Vector3.up * (height * 0.5f);
            foreach (var hazard in WorkshopSceneQuery.FindActive<Hazard>(scene))
            foreach (var collider in hazard.GetComponents<Collider>())
                if (collider.enabled && collider.bounds.Intersects(new Bounds(capsuleCenter, new Vector3(radius * 2f, height, radius * 2f))))
                {
                    yield return new PreflightIssue(IssueSeverity.Warning,
                        $"Checkpoint '{checkpoint.name}' is inside hazard '{hazard.name}'. Respawning there would hit the player again.", checkpoint);
                    break;
                }
        }

        public static IEnumerable<PreflightIssue> CheckMover(HazardMover mover)
        {
            if (mover.Travel.magnitude < HazardMover.MinTravel)
                yield return new PreflightIssue(IssueSeverity.Error,
                    $"Moving hazard '{mover.name}' has no path (end point is less than {HazardMover.MinTravel} m away). Drag its end handle.", mover);
            if (mover.GetComponent<Hazard>() == null)
                yield return new PreflightIssue(IssueSeverity.Warning,
                    $"'{mover.name}' moves but is not a Hazard, so touching it does nothing.", mover);

            var scene = mover.gameObject.scene;
            var start = mover.transform.position;
            var end = start + mover.Travel;
            foreach (var spawn in WorkshopSceneQuery.FindActive<PlayerSpawn>(scene))
            {
                var p = spawn.transform.position;
                var closest = start + Vector3.Project(p - start, mover.Travel.normalized);
                if (Vector3.Dot(closest - start, end - closest) >= 0f && Vector3.Distance(closest, p) < 2f)
                    yield return new PreflightIssue(IssueSeverity.Warning,
                        $"Moving hazard '{mover.name}' passes close to the Start Point. Players may be hit as soon as they appear.", mover);
            }
        }

        public static IEnumerable<PreflightIssue> CheckChannelPiece(ChannelPiece piece)
        {
            var counts = ChannelCounts.For(piece.gameObject.scene, piece.Channel);
            var label = Channels.Label(piece.Channel);
            if (piece is Door)
            {
                if (!counts.HasOpener)
                    yield return new PreflightIssue(IssueSeverity.Warning,
                        $"Door '{piece.name}' ({label}) can never open: there is no {label} key or switch.", piece);
                else
                    yield return new PreflightIssue(IssueSeverity.Info,
                        $"Opened by {counts.Keys} key(s) and {counts.Switches} switch(es) on {label}.", piece);
            }
            else if (counts.Doors == 0)
            {
                yield return new PreflightIssue(IssueSeverity.Warning,
                    $"'{piece.name}' ({label}) has no {label} door to open.", piece);
            }
            else
            {
                yield return new PreflightIssue(IssueSeverity.Info, $"Opens {counts.Doors} {label} door(s).", piece);
            }
            if (piece.Style == null)
                yield return new PreflightIssue(IssueSeverity.Warning, $"'{piece.name}' has no channel style, so its colour and symbol will not show.", piece);
        }

        /// <summary>
        /// Pieces placed by hand (not through the filtered catalog) that do not suit the template.
        /// Reported with the reason; never deleted automatically.
        /// </summary>
        static void CheckPieceSuitability(Scene scene, WorkshopGameContext context, List<PreflightIssue> issues)
        {
            if (context.Template == null)
                return;
            foreach (var traits in WorkshopSceneQuery.FindActive<PieceTraits>(scene))
            {
                var reason = PieceTraits.Unsuitability(traits.gameObject, context.Template);
                if (reason != null)
                    issues.Add(new PreflightIssue(IssueSeverity.Warning,
                        $"'{traits.name}' {reason}. Remove it or replace it with a piece from the Build tab.", traits.gameObject));
            }
        }

        /// <summary>How far (metres) a gameplay piece may sit from the play plane before it is reported.</summary>
        public const float PlaneTolerance = 0.25f;

        /// <summary>
        /// Side-scroller only: gameplay pieces must sit on the play plane, or the player (locked to it) may
        /// never touch them. Decoration may sit in front of or behind the plane.
        /// </summary>
        static void CheckPlayPlane(Scene scene, List<PreflightIssue> issues)
        {
            if (!WorkshopSceneQuery.TryGetMovementPlane(scene, out var depth))
                return;
            foreach (var piece in WorkshopSceneQuery.FindActive<WorkshopSceneBehaviour>(scene))
            {
                if (piece is WorkshopCameraRig || piece is WorkshopHUD || piece is WorkshopPlayer)
                    continue;
                var offset = piece.transform.position.z - depth;
                if (Mathf.Abs(offset) <= PlaneTolerance)
                    continue;
                var transform = piece.transform;
                issues.Add(new PreflightIssue(IssueSeverity.Warning,
                    $"'{piece.name}' is {Mathf.Abs(offset):0.##} m off the play plane, so the player may never reach it.", piece.gameObject,
                    "Snap to Plane", () =>
                    {
                        if (Mathf.Approximately(transform.position.z, depth))
                            return;
                        Undo.RecordObject(transform, "Snap to play plane");
                        var p = transform.position;
                        transform.position = new Vector3(p.x, p.y, depth);
                        PrefabUtility.RecordPrefabInstancePropertyModifications(transform);
                    }));
            }
            foreach (var mover in WorkshopSceneQuery.FindActive<HazardMover>(scene))
            {
                if (Mathf.Abs(mover.Travel.z) <= 0.01f)
                    continue;
                issues.Add(new PreflightIssue(IssueSeverity.Warning,
                    $"Moving hazard '{mover.name}' travels {Mathf.Abs(mover.Travel.z):0.##} m in depth and leaves the play plane.", mover,
                    "Keep on Plane", () =>
                    {
                        var serialized = new SerializedObject(mover);
                        var travel = serialized.FindProperty("m_Travel");
                        if (Mathf.Approximately(travel.vector3Value.z, 0f))
                            return;
                        travel.vector3Value = new Vector3(travel.vector3Value.x, travel.vector3Value.y, 0f);
                        serialized.ApplyModifiedProperties();
                    }));
            }
        }

        static void SetFloatProperty(Object target, string propertyName, float value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(propertyName);
            if (property == null || Mathf.Approximately(property.floatValue, value))
                return;
            property.floatValue = value;
            serialized.ApplyModifiedProperties();
        }

        static void CheckUi(Scene scene, WorkshopGameContext context, List<PreflightIssue> issues)
        {
            var huds = WorkshopSceneQuery.FindActive<WorkshopHUD>(scene);
            if (huds.Count == 0)
                issues.Add(new PreflightIssue(IssueSeverity.Error, "There is no Workshop HUD, so players cannot see the objective, pause or restart.",
                    null, "Add HUD", () => AddSystemPrefab(scene, WorkshopPaths.HudPrefab, null)));
            else if (huds.Count > 1)
                foreach (var hud in huds)
                    issues.Add(new PreflightIssue(IssueSeverity.Error, $"There are {huds.Count} HUDs. Keep exactly one.", hud.gameObject));

            var systems = WorkshopSceneQuery.FindActive<EventSystem>(scene);
            if (systems.Count > 1)
                foreach (var system in systems)
                    issues.Add(new PreflightIssue(IssueSeverity.Error, $"There are {systems.Count} Event Systems. Buttons only work with exactly one.", system.gameObject));
            else if (systems.Count == 0 && huds.Count > 0)
                issues.Add(new PreflightIssue(IssueSeverity.Error, "There is no Event System, so HUD buttons will not respond.", null,
                    "Add Event System", () => AddEventSystem(scene)));
            foreach (var system in systems)
                if (system.GetComponent<InputSystemUIInputModule>() == null)
                    issues.Add(new PreflightIssue(IssueSeverity.Error,
                        $"Event System '{system.name}' does not use the Input System UI module, so buttons will not respond.", system.gameObject,
                        "Fix Module", () =>
                        {
                            foreach (var module in system.GetComponents<BaseInputModule>())
                                Undo.DestroyObjectImmediate(module);
                            if (system.GetComponent<InputSystemUIInputModule>() == null)
                                Undo.AddComponent<InputSystemUIInputModule>(system.gameObject);
                        }));
        }

        static void CheckBudget(Scene scene, List<PreflightIssue> issues)
        {
            var renderers = WorkshopSceneQuery.FindActive<Renderer>(scene).Count;
            if (renderers > RendererBudget)
                issues.Add(new PreflightIssue(IssueSeverity.Warning,
                    $"The scene has {renderers} visible objects, more than the approximate lab budget of {RendererBudget}. It may run slowly on lab computers."));
        }

        static void CheckBuildList(Scene scene, List<PreflightIssue> issues)
        {
            if (string.IsNullOrEmpty(scene.path))
                return;
            if (EditorBuildSettings.scenes.Any(s => s.path == scene.path && s.enabled))
                return;
            issues.Add(new PreflightIssue(IssueSeverity.Warning,
                "This scene is not in the build list, so it will be missing from a built game. Play mode still works.",
                null, "Add to Build", () => AddToBuildList(scene.path)));
        }

        // ---- Fix helpers ---------------------------------------------------------------------------

        public static void AddToBuildList(string scenePath)
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            var existing = scenes.FindIndex(s => s.path == scenePath);
            if (existing >= 0)
            {
                if (scenes[existing].enabled)
                    return;
                scenes[existing] = new EditorBuildSettingsScene(scenePath, true);
            }
            else
            {
                scenes.Add(new EditorBuildSettingsScene(scenePath, true));
            }
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        static Vector3 StartPosition(Scene scene)
        {
            var spawns = WorkshopSceneQuery.FindActive<PlayerSpawn>(scene);
            return spawns.Count > 0 ? spawns[0].SpawnPose.position : Vector3.zero;
        }

        static void AddSystemPrefab(Scene scene, string path, string group)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogError($"Workshop prefab missing at '{path}'. The kit content is incomplete; ask a facilitator to reimport the kit.");
                return;
            }
            AddPrefab(scene, prefab, group, WorkshopPlacement.DefaultPlacementPoint());
        }

        static void AddPrefab(Scene scene, GameObject prefab, string group, Vector3 position)
        {
            // Idempotent: never add a second copy of a single-instance system.
            if (prefab.GetComponent<WorkshopGameContext>() != null && WorkshopSceneQuery.FindActive<WorkshopGameContext>(scene).Count > 0) return;
            if (prefab.GetComponent<WorkshopPlayer>() != null && WorkshopSceneQuery.FindActive<WorkshopPlayer>(scene).Count > 0) return;
            if (prefab.GetComponent<PlayerSpawn>() != null && WorkshopSceneQuery.FindActive<PlayerSpawn>(scene).Count > 0) return;
            if (prefab.GetComponent<WorkshopCameraRig>() != null && WorkshopSceneQuery.FindActive<WorkshopCameraRig>(scene).Count > 0) return;
            if (prefab.GetComponent<WorkshopHUD>() != null && WorkshopSceneQuery.FindActive<WorkshopHUD>(scene).Count > 0) return;

            var instance = WorkshopPlacement.Place(prefab, scene, position, Quaternion.identity, group, 0f);
            if (instance != null)
                Selection.activeGameObject = instance;
        }

        static void AddEventSystem(Scene scene)
        {
            if (WorkshopSceneQuery.FindActive<EventSystem>(scene).Count > 0)
                return;
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            SceneManager.MoveGameObjectToScene(go, scene);
            Undo.RegisterCreatedObjectUndo(go, "Add Event System");
        }

        static void DisableBehaviour(Behaviour behaviour, string undoName)
        {
            if (behaviour == null || !behaviour.enabled)
                return;
            Undo.RecordObject(behaviour, undoName);
            behaviour.enabled = false;
            PrefabUtility.RecordPrefabInstancePropertyModifications(behaviour);
        }

        static void SetObjectProperty(Object target, string propertyName, Object value, string undoName)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(propertyName);
            if (property == null || property.objectReferenceValue == value)
                return;
            property.objectReferenceValue = value;
            serialized.ApplyModifiedProperties();
            Undo.SetCurrentGroupName(undoName);
        }

        static void SetGoalRule(WorkshopGameContext context, GoalRule rule)
        {
            var serialized = new SerializedObject(context);
            var property = serialized.FindProperty("m_Rules.goalRule");
            if (property.enumValueIndex == (int)rule)
                return;
            property.enumValueIndex = (int)rule;
            serialized.ApplyModifiedProperties();
        }
    }
}
