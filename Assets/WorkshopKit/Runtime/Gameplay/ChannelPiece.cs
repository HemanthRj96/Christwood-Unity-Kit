using UnityEngine;

namespace UnityWorkshop
{
    /// <summary>
    /// Base for keys, switches and doors. Pieces with the same channel in the same game work together
    /// automatically; there are no per-door references to wire.
    /// </summary>
    public abstract class ChannelPiece : WorkshopSceneBehaviour
    {
        public const string ChannelPropertyName = "m_Channel";

        [SerializeField, Tooltip("Pieces with the same colour and symbol work together.")]
        ChannelColor m_Channel = ChannelColor.Red;
        [SerializeField, HideInInspector] ChannelStyle m_Style;
        [SerializeField, HideInInspector] Renderer[] m_Tinted = new Renderer[0];
        [SerializeField, HideInInspector] MeshFilter[] m_Symbols = new MeshFilter[0];

        public ChannelColor Channel => m_Channel;
        public ChannelStyle Style => m_Style;

        protected virtual void Awake() => ApplyChannelLook();

        /// <summary>Colours and symbols this piece's own renderers for its channel. Also used by the editor.</summary>
        public void ApplyChannelLook()
        {
            if (m_Style != null)
                m_Style.Apply(m_Channel, m_Tinted, m_Symbols);
        }

        /// <summary>Renderers and filters changed by <see cref="ApplyChannelLook"/>, so editor tools can record Undo.</summary>
        public Object[] LookTargets()
        {
            var targets = new Object[(m_Tinted?.Length ?? 0) + (m_Symbols?.Length ?? 0)];
            var i = 0;
            if (m_Tinted != null)
                foreach (var renderer in m_Tinted)
                    targets[i++] = renderer;
            if (m_Symbols != null)
                foreach (var filter in m_Symbols)
                    targets[i++] = filter;
            return targets;
        }
    }
}
