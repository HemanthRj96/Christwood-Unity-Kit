using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnityWorkshop
{
    /// <summary>
    /// Scene-view guides drawn from the same colliders and settings the game uses at runtime.
    /// Guides are informational; editable handles live in each piece's inspector (OnSceneGUI).
    /// </summary>
    static class WorkshopGizmos
    {
        static readonly Color StartColor = new Color(0.3f, 0.6f, 1f);
        static readonly Color CollectColor = new Color(1f, 0.85f, 0.2f);
        static readonly Color HazardColor = new Color(1f, 0.25f, 0.2f);
        static readonly Color GoalOpenColor = new Color(0.3f, 1f, 0.45f);
        static readonly Color GoalLockedColor = new Color(0.65f, 0.8f, 0.65f);

        // Capsule size per scene, refreshed when the hierarchy changes rather than on every repaint.
        static readonly Dictionary<Scene, Vector2> s_CapsuleCache = new Dictionary<Scene, Vector2>();
        static readonly Dictionary<Scene, WorkshopGameContext> s_ContextCache = new Dictionary<Scene, WorkshopGameContext>();
        static GUIStyle s_LabelStyle;

        static WorkshopGizmos() => EditorApplication.hierarchyChanged += () =>
        {
            s_CapsuleCache.Clear();
            s_ContextCache.Clear();
            s_ChannelCache.Clear();
            s_SideRigCache.Clear();
        };

        static WorkshopGameContext ContextOf(Component component)
        {
            var scene = component.gameObject.scene;
            if (!s_ContextCache.TryGetValue(scene, out var context) || context == null)
                s_ContextCache[scene] = context = WorkshopSceneQuery.FindContext(scene);
            return context;
        }

        static GUIStyle LabelStyle => s_LabelStyle ??= new GUIStyle(EditorStyles.miniBoldLabel)
        {
            normal = { textColor = Color.white },
            alignment = TextAnchor.MiddleCenter,
        };

        static bool Enabled => WorkshopEditorGUI.ShowPreview;

        static Vector2 Capsule(Component component)
        {
            var scene = component.gameObject.scene;
            if (s_CapsuleCache.TryGetValue(scene, out var size))
                return size;
            WorkshopSceneQuery.GetPlayerCapsule(ContextOf(component), out var radius, out var height);
            size = new Vector2(radius, height);
            s_CapsuleCache[scene] = size;
            return size;
        }

        [DrawGizmo(GizmoType.NonSelected | GizmoType.Selected | GizmoType.Pickable)]
        static void DrawStart(PlayerSpawn spawn, GizmoType type)
        {
            if (!Enabled)
                return;
            var capsule = Capsule(spawn);
            var pose = spawn.SpawnPose;
            var selected = (type & GizmoType.Selected) != 0;
            Gizmos.color = selected ? StartColor : StartColor * new Color(1f, 1f, 1f, 0.6f);
            DrawWireCapsule(pose.position, capsule.x, capsule.y);

            var forward = pose.rotation * Vector3.forward;
            var arrowBase = pose.position + Vector3.up * 0.1f;
            var tip = arrowBase + forward * 1.5f;
            Gizmos.DrawLine(arrowBase, tip);
            var side = Vector3.Cross(Vector3.up, forward) * 0.3f;
            Gizmos.DrawLine(tip, tip - forward * 0.4f + side);
            Gizmos.DrawLine(tip, tip - forward * 0.4f - side);

            if (selected)
                Label(pose.position + Vector3.up * (capsule.y + 0.3f), "Start");
        }

        [DrawGizmo(GizmoType.NonSelected | GizmoType.Selected)]
        static void DrawCollectible(Collectible collectible, GizmoType type)
        {
            if (!Enabled)
                return;
            var sphere = collectible.Trigger;
            if (sphere == null)
                return;
            var selected = (type & GizmoType.Selected) != 0;
            Gizmos.color = selected ? CollectColor : CollectColor * new Color(1f, 1f, 1f, 0.35f);
            var scale = collectible.transform.lossyScale;
            Gizmos.DrawWireSphere(collectible.transform.TransformPoint(sphere.center),
                sphere.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
            if (selected)
                Label(collectible.transform.position + Vector3.up * 1.2f, "Collectible");
        }

        [DrawGizmo(GizmoType.NonSelected | GizmoType.Selected)]
        static void DrawHazard(Hazard hazard, GizmoType type)
        {
            if (!Enabled)
                return;
            var selected = (type & GizmoType.Selected) != 0;
            Gizmos.color = selected ? HazardColor : HazardColor * new Color(1f, 1f, 1f, 0.5f);
            foreach (var collider in hazard.GetComponents<Collider>())
                DrawCollider(collider);
            if (selected)
                Label(hazard.transform.position + Vector3.up * 1.2f, "Hazard: back to start");
        }

        [DrawGizmo(GizmoType.NonSelected | GizmoType.Selected)]
        static void DrawGoal(GoalZone goal, GizmoType type)
        {
            if (!Enabled)
                return;
            var context = ContextOf(goal);
            var collectAll = context != null && context.Rules.goalRule == GoalRule.CollectAllThenExit;
            Gizmos.color = collectAll ? GoalLockedColor : GoalOpenColor;
            DrawCollider(goal.Trigger);
            Label(goal.transform.TransformPoint(goal.Trigger.center) + Vector3.up * (goal.Trigger.size.y * 0.5f + 0.4f),
                collectAll ? "Exit (collect all first)" : "Exit");
        }

        [DrawGizmo(GizmoType.Selected)]
        static void DrawTopDownCamera(TopDownCameraRig rig, GizmoType type)
        {
            if (!Enabled || rig.Camera == null)
                return;
            Gizmos.color = Color.white;
            var aim = rig.transform.position + Vector3.up * TopDownCameraRig.AimHeight;
            Gizmos.DrawLine(rig.Camera.transform.position, aim);
            Gizmos.DrawWireSphere(aim, 0.25f);
            Label(aim + Vector3.up * 0.5f, "Camera follows the player here");
        }

        static readonly Color CheckpointColor = new Color(0.35f, 0.9f, 0.5f);
        static readonly Color FallColor = new Color(0.7f, 0.4f, 1f);
        static readonly Color PathColor = new Color(1f, 0.6f, 0.15f);

        static Color ChannelGizmoColor(ChannelColor channel)
        {
            switch (channel)
            {
                case ChannelColor.Red: return new Color(1f, 0.3f, 0.3f);
                case ChannelColor.Blue: return new Color(0.35f, 0.55f, 1f);
                case ChannelColor.Green: return new Color(0.3f, 0.9f, 0.4f);
                default: return new Color(1f, 0.85f, 0.2f);
            }
        }

        [DrawGizmo(GizmoType.NonSelected | GizmoType.Selected | GizmoType.Pickable)]
        static void DrawCheckpoint(Checkpoint checkpoint, GizmoType type)
        {
            if (!Enabled)
                return;
            var capsule = Capsule(checkpoint);
            var pose = checkpoint.RespawnPose;
            var selected = (type & GizmoType.Selected) != 0;
            Gizmos.color = selected ? CheckpointColor : CheckpointColor * new Color(1f, 1f, 1f, 0.45f);
            DrawWireCapsule(pose.position, capsule.x, capsule.y);
            var forward = pose.rotation * Vector3.forward;
            var arrowBase = pose.position + Vector3.up * 0.1f;
            Gizmos.DrawLine(arrowBase, arrowBase + forward * 1.2f);
            if (selected)
            {
                DrawCollider(checkpoint.Trigger);
                Label(pose.position + Vector3.up * (capsule.y + 0.3f), "Checkpoint: respawn here");
            }
        }

        [DrawGizmo(GizmoType.NonSelected | GizmoType.Selected | GizmoType.Pickable)]
        static void DrawFallZone(FallZone zone, GizmoType type)
        {
            if (!Enabled)
                return;
            Gizmos.color = (type & GizmoType.Selected) != 0 ? FallColor : FallColor * new Color(1f, 1f, 1f, 0.5f);
            DrawCollider(zone.Trigger);
            if ((type & GizmoType.Selected) != 0)
                Label(zone.transform.position + Vector3.up * 0.5f, "Fall Zone (invisible in game)");
        }

        [DrawGizmo(GizmoType.Selected)]
        static void DrawMoverPath(HazardMover mover, GizmoType type)
        {
            if (!Enabled)
                return;
            var start = mover.transform.position;
            var end = start + mover.Travel;
            Gizmos.color = PathColor;
            Gizmos.DrawLine(start, end);
            var direction = mover.Travel.normalized;
            var side = Vector3.Cross(Vector3.up, direction).normalized * 0.25f;
            for (var t = 0.25f; t < 1f; t += 0.25f)
            {
                var p = Vector3.Lerp(start, end, t);
                Gizmos.DrawLine(p, p - direction * 0.35f + side);
                Gizmos.DrawLine(p, p - direction * 0.35f - side);
            }
            foreach (var collider in mover.GetComponents<Collider>())
            {
                var bounds = collider.bounds;
                Gizmos.DrawWireCube(bounds.center + mover.Travel, bounds.size);
            }
            Label(Vector3.Lerp(start, end, 0.5f) + Vector3.up * 1f,
                $"Moves {mover.Travel.magnitude:0.#} m, {mover.Speed.ToString().ToLowerInvariant()}");
        }

        [DrawGizmo(GizmoType.NonSelected | GizmoType.Selected)]
        static void DrawChannelPiece(ChannelPiece piece, GizmoType type)
        {
            if (!Enabled)
                return;
            var selected = (type & GizmoType.Selected) != 0;
            if (!selected && !WorkshopChannelLinks.ShowAll)
                return;
            var color = ChannelGizmoColor(piece.Channel);
            Gizmos.color = color;
            var from = piece.transform.position + Vector3.up * 1f;
            foreach (var other in ChannelPiecesIn(piece))
            {
                if (other == piece || other == null || other.Channel != piece.Channel)
                    continue;
                // Draw each pair once when every link is shown.
                if (!selected && other.GetEntityId().GetHashCode() < piece.GetEntityId().GetHashCode())
                    continue;
                Gizmos.DrawLine(from, other.transform.position + Vector3.up * 1f);
            }
            Label(from + Vector3.up * 0.6f, Channels.Label(piece.Channel));
        }

        [DrawGizmo(GizmoType.Selected)]
        static void DrawFallHeight(WorkshopGameContext context, GizmoType type)
        {
            if (!Enabled || !context.UsesFallBoundary)
                return;
            Gizmos.color = FallColor * new Color(1f, 1f, 1f, 0.6f);
            var center = new Vector3(context.transform.position.x, context.FallHeight, context.transform.position.z);
            const float half = 30f;
            for (var i = -6; i <= 6; i++)
            {
                var offset = i * half / 6f;
                Gizmos.DrawLine(center + new Vector3(-half, 0f, offset), center + new Vector3(half, 0f, offset));
                Gizmos.DrawLine(center + new Vector3(offset, 0f, -half), center + new Vector3(offset, 0f, half));
            }
            Label(center + Vector3.up * 0.3f, $"Fall Height ({context.FallHeight:0.#} m): players below this go back");
        }

        // Channel pieces per scene, refreshed when the hierarchy changes rather than on every repaint.
        static readonly Dictionary<Scene, List<ChannelPiece>> s_ChannelCache = new Dictionary<Scene, List<ChannelPiece>>();

        static List<ChannelPiece> ChannelPiecesIn(Component component)
        {
            var scene = component.gameObject.scene;
            if (!s_ChannelCache.TryGetValue(scene, out var pieces))
                s_ChannelCache[scene] = pieces = WorkshopSceneQuery.FindActive<ChannelPiece>(scene);
            return pieces;
        }

        [DrawGizmo(GizmoType.Selected)]
        static void DrawFirstPersonCamera(FirstPersonCameraRig rig, GizmoType type)
        {
            if (!Enabled)
                return;
            Gizmos.color = Color.white;
            var eye = rig.transform.position;
            Gizmos.DrawWireSphere(eye, 0.15f);
            Gizmos.DrawLine(eye, eye + rig.transform.forward * 1.5f);
            Label(eye + Vector3.up * 0.4f, "Player's eyes (moves to the player in Play mode)");
        }

        [DrawGizmo(GizmoType.Selected)]
        static void DrawThirdPersonCamera(ThirdPersonCameraRig rig, GizmoType type)
        {
            if (!Enabled || rig.Camera == null)
                return;
            var pivot = rig.transform.position;
            var cameraPosition = rig.Camera.transform.position;
            var fullPosition = pivot + (cameraPosition - pivot).normalized * rig.FullDistance;
            Gizmos.color = Color.white;
            Gizmos.DrawWireSphere(pivot, 0.15f);
            Gizmos.DrawLine(pivot, cameraPosition);
            Gizmos.DrawWireSphere(cameraPosition, ThirdPersonCameraRig.CollisionRadius);
            Gizmos.color = new Color(1f, 1f, 1f, 0.35f);
            Gizmos.DrawLine(cameraPosition, fullPosition);
            Gizmos.DrawWireSphere(fullPosition, ThirdPersonCameraRig.CollisionRadius);
            Label(pivot + Vector3.up * 0.4f, "Camera orbits here");
            if ((fullPosition - cameraPosition).sqrMagnitude > 0.01f)
                Label(fullPosition + Vector3.up * 0.4f, "Pulled in by a wall");
        }

        static readonly Color PlaneColor = new Color(0.4f, 0.8f, 1f);
        static readonly Color OffPlaneColor = new Color(1f, 0.3f, 0.2f);

        [DrawGizmo(GizmoType.NonSelected | GizmoType.Selected)]
        static void DrawSideScrollerCamera(SideScrollerCameraRig rig, GizmoType type)
        {
            if (!Enabled)
                return;
            var z = rig.PlaneDepth;
            var selected = (type & GizmoType.Selected) != 0;
            // The play plane: a faint grid line along the level, stronger when the camera is selected.
            var min = rig.UsesBounds ? Vector2.Min(rig.BoundsMin, rig.BoundsMax) - new Vector2(12f, 6f) : new Vector2(-30f, -5f);
            var max = rig.UsesBounds ? Vector2.Max(rig.BoundsMin, rig.BoundsMax) + new Vector2(12f, 6f) : new Vector2(60f, 15f);
            Gizmos.color = PlaneColor * new Color(1f, 1f, 1f, selected ? 0.6f : 0.25f);
            Gizmos.DrawLine(new Vector3(min.x, 0f, z), new Vector3(max.x, 0f, z));
            if (!selected)
                return;
            Gizmos.DrawWireCube(new Vector3((min.x + max.x) * 0.5f, (min.y + max.y) * 0.5f, z), new Vector3(max.x - min.x, max.y - min.y, 0f));
            if (rig.UsesBounds)
            {
                var bMin = Vector2.Min(rig.BoundsMin, rig.BoundsMax);
                var bMax = Vector2.Max(rig.BoundsMin, rig.BoundsMax);
                Gizmos.color = new Color(1f, 0.75f, 0.2f);
                Gizmos.DrawWireCube(new Vector3((bMin.x + bMax.x) * 0.5f, (bMin.y + bMax.y) * 0.5f, z), new Vector3(bMax.x - bMin.x, bMax.y - bMin.y, 0f));
            }
            Label(new Vector3(min.x + 2f, max.y, z), $"Play plane (Z = {z:0.##})");
        }

        /// <summary>Side-scroller scenes: a red line from any gameplay piece that is off the play plane back to it.</summary>
        [DrawGizmo(GizmoType.NonSelected | GizmoType.Selected)]
        static void DrawOffPlane(WorkshopSceneBehaviour piece, GizmoType type)
        {
            if (!Enabled || piece is WorkshopCameraRig || piece is WorkshopHUD || piece is WorkshopPlayer)
                return;
            var rig = SideRigOf(piece);
            if (rig == null)
                return;
            var p = piece.transform.position;
            if (Mathf.Abs(p.z - rig.PlaneDepth) <= WorkshopPreflight.PlaneTolerance)
                return;
            var onPlane = new Vector3(p.x, p.y, rig.PlaneDepth);
            Gizmos.color = OffPlaneColor;
            Gizmos.DrawLine(p, onPlane);
            Gizmos.DrawWireSphere(onPlane, 0.2f);
            Label(p + Vector3.up * 0.8f, "Off the play plane");
        }

        static readonly Dictionary<Scene, SideScrollerCameraRig> s_SideRigCache = new Dictionary<Scene, SideScrollerCameraRig>();

        static SideScrollerCameraRig SideRigOf(Component component)
        {
            var scene = component.gameObject.scene;
            if (!s_SideRigCache.TryGetValue(scene, out var rig))
            {
                var rigs = WorkshopSceneQuery.FindActive<SideScrollerCameraRig>(scene);
                s_SideRigCache[scene] = rig = rigs.Count == 1 ? rigs[0] : null;
            }
            return rig;
        }

        static void DrawCollider(Collider collider)
        {
            if (collider == null)
                return;
            var previous = Gizmos.matrix;
            Gizmos.matrix = collider.transform.localToWorldMatrix;
            switch (collider)
            {
                case BoxCollider box:
                    Gizmos.DrawWireCube(box.center, box.size);
                    break;
                case SphereCollider sphere:
                    Gizmos.DrawWireSphere(sphere.center, sphere.radius);
                    break;
                default:
                    Gizmos.matrix = previous;
                    var bounds = collider.bounds;
                    Gizmos.DrawWireCube(bounds.center, bounds.size);
                    break;
            }
            Gizmos.matrix = previous;
        }

        static void DrawWireCapsule(Vector3 feet, float radius, float height)
        {
            var bottom = feet + Vector3.up * radius;
            var top = feet + Vector3.up * (height - radius);
            Gizmos.DrawWireSphere(bottom, radius);
            Gizmos.DrawWireSphere(top, radius);
            Gizmos.DrawLine(bottom + Vector3.forward * radius, top + Vector3.forward * radius);
            Gizmos.DrawLine(bottom - Vector3.forward * radius, top - Vector3.forward * radius);
            Gizmos.DrawLine(bottom + Vector3.right * radius, top + Vector3.right * radius);
            Gizmos.DrawLine(bottom - Vector3.right * radius, top - Vector3.right * radius);
        }

        static void Label(Vector3 position, string text) => Handles.Label(position, text, LabelStyle);
    }
}
