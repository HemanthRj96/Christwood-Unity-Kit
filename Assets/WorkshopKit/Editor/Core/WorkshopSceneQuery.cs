using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnityWorkshop
{
    /// <summary>Edit-time lookups scoped to one scene. Used by inspectors, gizmos, preflight and the builder.</summary>
    public static class WorkshopSceneQuery
    {
        static readonly List<GameObject> s_Roots = new List<GameObject>();

        /// <summary>Components of type T in <paramref name="scene"/> that are active and enabled (they take part in play).</summary>
        public static List<T> FindActive<T>(Scene scene) where T : Component
        {
            var result = new List<T>();
            if (!scene.IsValid() || !scene.isLoaded)
                return result;
            scene.GetRootGameObjects(s_Roots);
            var buffer = new List<T>();
            foreach (var root in s_Roots)
            {
                root.GetComponentsInChildren(false, buffer);
                foreach (var component in buffer)
                    if (!(component is Behaviour behaviour) || behaviour.enabled)
                        result.Add(component);
            }
            s_Roots.Clear();
            return result;
        }

        /// <summary>The scene's Game Setup when exactly one is active; otherwise null and the count found.</summary>
        public static WorkshopGameContext FindContext(Scene scene, out int count)
        {
            var contexts = FindActive<WorkshopGameContext>(scene);
            count = contexts.Count;
            return count == 1 ? contexts[0] : null;
        }

        public static WorkshopGameContext FindContext(Scene scene) => FindContext(scene, out _);

        /// <summary>
        /// The Game Setup a piece belongs to, using the same order as runtime resolution: a serialized
        /// reference in the same scene, an enclosing Game Setup, then the scene's only Game Setup.
        /// </summary>
        public static WorkshopGameContext OwnerOf(Component component)
        {
            if (component == null)
                return null;
            var scene = component.gameObject.scene;
            if (component is WorkshopSceneBehaviour)
            {
                var serialized = new SerializedObject(component).FindProperty(WorkshopSceneBehaviour.ContextPropertyName);
                if (serialized != null && serialized.objectReferenceValue is WorkshopGameContext assigned &&
                    assigned.gameObject.scene == scene)
                    return assigned;
            }
            var enclosing = component.GetComponentInParent<WorkshopGameContext>();
            return enclosing != null ? enclosing : FindContext(scene);
        }

        /// <summary>The play plane depth when the scene's single gameplay camera locks movement to a plane.</summary>
        public static bool TryGetMovementPlane(Scene scene, out float depth)
        {
            depth = 0f;
            var rigs = FindActive<WorkshopCameraRig>(scene);
            return rigs.Count == 1 && rigs[0].TryGetMovementPlane(out depth);
        }

        /// <summary>Capsule size of the template's player, used so gizmos and checks match the real player.</summary>
        public static void GetPlayerCapsule(WorkshopGameContext context, out float radius, out float height)
        {
            radius = 0.4f;
            height = 1.8f;
            CharacterController controller = null;
            if (context != null)
            {
                var player = FindActive<WorkshopPlayer>(context.gameObject.scene);
                if (player.Count > 0)
                    controller = player[0].GetComponent<CharacterController>();
                else if (context.Template != null && context.Template.PlayerPrefab != null)
                    controller = context.Template.PlayerPrefab.GetComponent<CharacterController>();
            }
            if (controller == null)
                return;
            radius = controller.radius;
            height = Mathf.Max(controller.height, radius * 2f);
        }
    }
}
