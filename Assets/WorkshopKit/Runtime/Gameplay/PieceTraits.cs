using UnityEngine;

namespace UnityWorkshop
{
    /// <summary>
    /// Placement facts about a kit piece, used by the Builder catalog and preflight to tell which
    /// templates it suits. It has no gameplay behaviour.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public sealed class PieceTraits : MonoBehaviour
    {
        [SerializeField, Tooltip("Only reachable or useful when the player can jump.")]
        bool m_RequiresJump;
        [SerializeField, Tooltip("Tall enough to hide the player from an overhead camera.")]
        bool m_BlocksOverheadView;

        public bool RequiresJump => m_RequiresJump;
        public bool BlocksOverheadView => m_BlocksOverheadView;

        /// <summary>Why <paramref name="template"/> should not use this piece, or null when it suits.</summary>
        public static string Unsuitability(GameObject piece, GameTemplateDefinition template)
        {
            if (piece == null || template == null || !piece.TryGetComponent(out PieceTraits traits))
                return null;
            if (traits.m_RequiresJump && !template.CanJump)
                return $"needs jumping, but {template.DisplayName} players cannot jump";
            if (traits.m_BlocksOverheadView && template.Perspective == TemplatePerspective.TopDown)
                return "is tall enough to hide the player from the Top-Down camera";
            return null;
        }
    }
}
