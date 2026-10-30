using UnityEngine;

namespace UnityWorkshop
{
    public enum TemplatePerspective
    {
        [InspectorName("Top-Down")] TopDown,
        [InspectorName("First-Person")] FirstPerson,
        [InspectorName("Third-Person")] ThirdPerson,
        [InspectorName("Side-Scroller")] SideScroller,
    }

    /// <summary>
    /// Shared, read-only description of one playable template: which player rig, camera rig and
    /// movement presets it uses and how it is explained to students. Team settings never live here.
    /// </summary>
    [CreateAssetMenu(menuName = "Unity Workshop/Facilitator/Game Template", fileName = "New Game Template")]
    public sealed class GameTemplateDefinition : ScriptableObject
    {
        [SerializeField] string m_DisplayName = "Template";
        [SerializeField] TemplatePerspective m_Perspective;
        [SerializeField, TextArea(2, 4)] string m_Description = "";
        [SerializeField, TextArea(2, 6), Tooltip("One control per line, for example \"WASD: Move\".")]
        string m_Controls = "";
        [SerializeField] Texture2D m_Preview;
        [SerializeField, Tooltip("Whether the player can jump. Jump-only catalog items are hidden when off.")]
        bool m_CanJump;
        [SerializeField] GameObject m_PlayerPrefab;
        [SerializeField] GameObject m_CameraRigPrefab;
        [SerializeField, Tooltip("Movement feels students may choose from. The first one is the default.")]
        MovementSettings[] m_MovementPresets = new MovementSettings[0];
        [SerializeField, Tooltip("GUID of the starter scene copied when a team creates a game.")]
        string m_StarterSceneGuid = "";

        public string DisplayName => m_DisplayName;
        public TemplatePerspective Perspective => m_Perspective;
        public string Description => m_Description;
        public string Controls => m_Controls;
        public Texture2D Preview => m_Preview;
        public bool CanJump => m_CanJump;
        public GameObject PlayerPrefab => m_PlayerPrefab;
        public GameObject CameraRigPrefab => m_CameraRigPrefab;
        public MovementSettings[] MovementPresets => m_MovementPresets;
        public MovementSettings DefaultMovement => m_MovementPresets != null && m_MovementPresets.Length > 0 ? m_MovementPresets[0] : null;
        public string StarterSceneGuid => m_StarterSceneGuid;

        public bool AllowsMovement(MovementSettings settings)
        {
            if (settings == null || m_MovementPresets == null)
                return false;
            foreach (var preset in m_MovementPresets)
                if (preset == settings)
                    return true;
            return false;
        }
    }
}
