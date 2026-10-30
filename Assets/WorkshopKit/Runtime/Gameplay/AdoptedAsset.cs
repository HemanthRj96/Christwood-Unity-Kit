using UnityEngine;

namespace UnityWorkshop
{
    public enum AdoptedRole
    {
        /// <summary>Placed in levels from the Builder catalog.</summary>
        Prop,
        /// <summary>Replaces how the player looks.</summary>
        PlayerLook,
    }

    /// <summary>
    /// Marks a workshop-owned wrapper around a third-party model. The vendor asset is referenced, never
    /// copied or edited. Holds what the Builder needs to list it and what adoption found out about it.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public sealed class AdoptedAsset : MonoBehaviour
    {
        [SerializeField] string m_DisplayName = "";
        [SerializeField] AdoptedRole m_Role;
        [SerializeField, Tooltip("Asset GUID of the original vendor asset.")]
        string m_SourceGuid = "";
        [SerializeField, TextArea(2, 6), Tooltip("What adoption changed in this owned copy and what it could not support.")]
        string m_Notes = "";
        [SerializeField, Tooltip("The model child (a reference to the vendor prefab or model).")]
        Transform m_Visual;
        [SerializeField, Tooltip("Animator on the model that the workshop drives, or empty for a still model.")]
        Animator m_Animator;

        public string DisplayName => m_DisplayName;
        public AdoptedRole Role => m_Role;
        public string SourceGuid => m_SourceGuid;
        public string Notes => m_Notes;
        public Transform Visual => m_Visual;
        public Animator Animator => m_Animator;
    }
}
