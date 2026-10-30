using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnityWorkshop
{
    /// <summary>
    /// Undo-aware placement of workshop prefabs into a team scene: grouping, surface snapping and
    /// immediate assignment of the owning Game Setup so nothing needs manual wiring.
    /// </summary>
    public static class WorkshopPlacement
    {
        public const string LevelRootName = "Level";

        /// <summary>Instantiates <paramref name="prefab"/> into <paramref name="scene"/> at a snapped position.</summary>
        public static GameObject Place(GameObject prefab, Scene scene, Vector3 position, Quaternion rotation, string groupName, float gridSize)
        {
            if (prefab == null || !scene.IsValid())
                return null;

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            Undo.RegisterCreatedObjectUndo(instance, $"Add {prefab.name}");

            if (!string.IsNullOrEmpty(groupName))
            {
                var group = FindOrCreateGroup(scene, groupName);
                Undo.SetTransformParent(instance.transform, group, $"Add {prefab.name}");
            }

            var snapped = SnapToGrid(position, gridSize);
            if (WorkshopSceneQuery.TryGetMovementPlane(scene, out var depth))
            {
                // Side-scroller: everything sits on the play plane, and doors turn to block left/right travel.
                snapped.z = depth;
                if (instance.GetComponent<Door>() != null && rotation == Quaternion.identity)
                    rotation = Quaternion.Euler(0f, 90f, 0f);
            }
            snapped.y = SurfaceHeight(snapped, instance, position.y);
            instance.transform.SetPositionAndRotation(snapped, rotation);
            AssignContext(instance, WorkshopSceneQuery.FindContext(scene));
            EditorSceneManager.MarkSceneDirty(scene);
            return instance;
        }

        /// <summary>Writes the Game Setup reference into every workshop piece under <paramref name="root"/>.</summary>
        public static void AssignContext(GameObject root, WorkshopGameContext context)
        {
            if (root == null || context == null)
                return;
            foreach (var piece in root.GetComponentsInChildren<WorkshopSceneBehaviour>(true))
            {
                var serialized = new SerializedObject(piece);
                var property = serialized.FindProperty(WorkshopSceneBehaviour.ContextPropertyName);
                if (property == null || property.objectReferenceValue == context)
                    continue;
                property.objectReferenceValue = context;
                serialized.ApplyModifiedProperties();
                PrefabUtility.RecordPrefabInstancePropertyModifications(piece);
            }
        }

        /// <summary>A root object used purely to keep the Hierarchy tidy, e.g. Level/Collectibles.</summary>
        public static Transform FindOrCreateGroup(Scene scene, string groupName)
        {
            var level = FindRoot(scene, LevelRootName);
            if (level == null)
            {
                var levelObject = new GameObject(LevelRootName);
                SceneManager.MoveGameObjectToScene(levelObject, scene);
                Undo.RegisterCreatedObjectUndo(levelObject, "Create Level group");
                level = levelObject.transform;
            }
            var group = level.Find(groupName);
            if (group != null)
                return group;
            var groupObject = new GameObject(groupName);
            Undo.RegisterCreatedObjectUndo(groupObject, $"Create {groupName} group");
            Undo.SetTransformParent(groupObject.transform, level, $"Create {groupName} group");
            groupObject.transform.localPosition = Vector3.zero;
            return groupObject.transform;
        }

        static Transform FindRoot(Scene scene, string name)
        {
            foreach (var root in scene.GetRootGameObjects())
                if (root.name == name && root.GetComponent<WorkshopGameContext>() == null)
                    return root.transform;
            return null;
        }

        public static Vector3 SnapToGrid(Vector3 position, float gridSize)
        {
            if (gridSize <= 0f)
                return position;
            position.x = Mathf.Round(position.x / gridSize) * gridSize;
            position.z = Mathf.Round(position.z / gridSize) * gridSize;
            return position;
        }

        /// <summary>Height of the first solid surface below a point, ignoring the object being placed.</summary>
        public static float SurfaceHeight(Vector3 position, GameObject ignore, float fallback)
        {
            Physics.SyncTransforms();
            var origin = position + Vector3.up * 5f;
            var hits = Physics.RaycastAll(origin, Vector3.down, 100f, ~0, QueryTriggerInteraction.Ignore);
            var best = float.NegativeInfinity;
            foreach (var hit in hits)
            {
                if (ignore != null && hit.collider.transform.IsChildOf(ignore.transform))
                    continue;
                if (hit.point.y > best)
                    best = hit.point.y;
            }
            return float.IsNegativeInfinity(best) ? fallback : best;
        }

        /// <summary>A sensible place to add new pieces: the Scene view pivot, or the origin.</summary>
        public static Vector3 DefaultPlacementPoint()
        {
            var view = SceneView.lastActiveSceneView;
            return view != null ? view.pivot : Vector3.zero;
        }
    }
}
