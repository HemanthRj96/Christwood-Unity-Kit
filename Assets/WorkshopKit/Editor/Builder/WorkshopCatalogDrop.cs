using UnityEditor;
using UnityEngine;

namespace UnityWorkshop
{
    /// <summary>
    /// Drops Builder catalog pictures into the Scene view through the same placement as the Add button:
    /// grouping, grid and surface snapping, the side-scroller plane and automatic Game Setup wiring.
    /// </summary>
    [InitializeOnLoad]
    static class WorkshopCatalogDrop
    {
        static WorkshopCatalogDrop() => SceneView.duringSceneGui += OnSceneGUI;

        static void OnSceneGUI(SceneView view)
        {
            var e = Event.current;
            if (e.type != EventType.DragUpdated && e.type != EventType.DragPerform)
                return;
            if (!(DragAndDrop.GetGenericData(WorkshopBuilderWindow.DragDataKey) is CatalogEntry entry))
                return;

            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            if (e.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                DragAndDrop.SetGenericData(WorkshopBuilderWindow.DragDataKey, null);
                WorkshopBuilderWindow.AddEntry(entry, DropPoint(HandleUtility.GUIPointToWorldRay(e.mousePosition)));
            }
            e.Use();
        }

        /// <summary>Where a drop along <paramref name="ray"/> lands: the first solid surface, else the ground plane (or play plane).</summary>
        public static Vector3 DropPoint(Ray ray)
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            Physics.SyncTransforms();
            if (WorkshopSceneQuery.TryGetMovementPlane(scene, out var depth) && new Plane(Vector3.back, new Vector3(0f, 0f, depth)).Raycast(ray, out var planeDistance))
                return ray.GetPoint(planeDistance);
            if (Physics.Raycast(ray, out var hit, 1000f, ~0, QueryTriggerInteraction.Ignore))
                return hit.point;
            if (new Plane(Vector3.up, Vector3.zero).Raycast(ray, out var groundDistance))
                return ray.GetPoint(groundDistance);
            return ray.GetPoint(10f);
        }
    }
}
