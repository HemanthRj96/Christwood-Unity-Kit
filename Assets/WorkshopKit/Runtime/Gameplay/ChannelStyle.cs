using UnityEngine;

namespace UnityWorkshop
{
    /// <summary>
    /// Shared look of the channels: material, symbol mesh and UI colour per channel. Read-only at
    /// runtime; pieces only assign these to their own renderers.
    /// </summary>
    [CreateAssetMenu(menuName = "Unity Workshop/Facilitator/Channel Style", fileName = "Channel Style")]
    public sealed class ChannelStyle : ScriptableObject
    {
        [SerializeField] Material[] m_Materials = new Material[Channels.Count];
        [SerializeField] Mesh[] m_Symbols = new Mesh[Channels.Count];
        [SerializeField] Color[] m_UiColors = new Color[Channels.Count];

        public Material MaterialFor(ChannelColor channel) => Get(m_Materials, channel);
        public Mesh SymbolFor(ChannelColor channel) => Get(m_Symbols, channel);
        public Color UiColorFor(ChannelColor channel) => m_UiColors != null && (int)channel < m_UiColors.Length ? m_UiColors[(int)channel] : Color.white;

        static T Get<T>(T[] items, ChannelColor channel) where T : Object =>
            items != null && (int)channel < items.Length ? items[(int)channel] : null;

        /// <summary>Tints renderers and swaps symbol meshes for a channel. Edits only the given scene components.</summary>
        public void Apply(ChannelColor channel, Renderer[] tinted, MeshFilter[] symbols)
        {
            var material = MaterialFor(channel);
            if (material != null && tinted != null)
                foreach (var renderer in tinted)
                    if (renderer != null)
                        renderer.sharedMaterial = material;
            var mesh = SymbolFor(channel);
            if (mesh != null && symbols != null)
                foreach (var filter in symbols)
                    if (filter != null)
                        filter.sharedMesh = mesh;
        }
    }
}
