using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace UnityWorkshop
{
    public enum MaterialSupport
    {
        /// <summary>Already renders in URP; reused as it is.</summary>
        Compatible,
        /// <summary>A known built-in shader with an explicit mapping to a URP shader; an owned copy is made.</summary>
        Convertible,
        /// <summary>No reliable mapping; the original is kept and a facilitator decides.</summary>
        Unsupported,
    }

    /// <summary>
    /// Explicit, limited conversion of built-in pipeline materials to URP. Only recognised shaders are
    /// converted, into owned copies; originals are never modified. Anything else is reported, not guessed.
    /// </summary>
    public static class WorkshopMaterialConverter
    {
        const string UrpLit = "Universal Render Pipeline/Lit";
        const string UrpUnlit = "Universal Render Pipeline/Unlit";

        static readonly HashSet<string> s_LitSources = new HashSet<string>
        {
            "Standard", "Standard (Specular setup)", "Legacy Shaders/Diffuse", "Legacy Shaders/Bumped Diffuse",
            "Legacy Shaders/Specular", "Legacy Shaders/Bumped Specular", "Mobile/Diffuse", "Mobile/Bumped Diffuse",
        };

        static readonly HashSet<string> s_UnlitSources = new HashSet<string> { "Unlit/Texture", "Unlit/Color", "Mobile/Unlit (Supports Lightmap)" };

        public static MaterialSupport Classify(Material material)
        {
            if (material == null || material.shader == null)
                return MaterialSupport.Unsupported;
            var name = material.shader.name;
            if (name.StartsWith("Universal Render Pipeline/") || name.StartsWith("Shader Graphs/"))
                return MaterialSupport.Compatible;
            if (s_LitSources.Contains(name) || s_UnlitSources.Contains(name))
                return MaterialSupport.Convertible;
            return MaterialSupport.Unsupported;
        }

        /// <summary>
        /// Creates an owned URP copy of <paramref name="source"/> at <paramref name="path"/>. Maps base colour and
        /// texture, normal map, metallic/smoothness, emission and cut-out. Transparency becomes opaque and is
        /// reported in <paramref name="notes"/>.
        /// </summary>
        public static Material CreateOwnedCopy(Material source, string path, List<string> notes)
        {
            var unlit = s_UnlitSources.Contains(source.shader.name);
            var copy = new Material(Shader.Find(unlit ? UrpUnlit : UrpLit)) { name = source.name };

            if (source.HasProperty("_Color"))
                copy.SetColor("_BaseColor", source.GetColor("_Color"));
            if (source.HasProperty("_MainTex") && source.GetTexture("_MainTex") != null)
            {
                copy.SetTexture("_BaseMap", source.GetTexture("_MainTex"));
                copy.SetTextureScale("_BaseMap", source.GetTextureScale("_MainTex"));
                copy.SetTextureOffset("_BaseMap", source.GetTextureOffset("_MainTex"));
            }

            if (!unlit)
            {
                if (source.HasProperty("_BumpMap") && source.GetTexture("_BumpMap") != null)
                {
                    copy.SetTexture("_BumpMap", source.GetTexture("_BumpMap"));
                    copy.EnableKeyword("_NORMALMAP");
                }
                if (source.HasProperty("_Metallic"))
                    copy.SetFloat("_Metallic", source.GetFloat("_Metallic"));
                if (source.HasProperty("_Glossiness"))
                    copy.SetFloat("_Smoothness", source.GetFloat("_Glossiness"));
                if (source.IsKeywordEnabled("_EMISSION") && source.HasProperty("_EmissionColor"))
                {
                    copy.EnableKeyword("_EMISSION");
                    copy.SetColor("_EmissionColor", source.GetColor("_EmissionColor"));
                    if (source.HasProperty("_EmissionMap") && source.GetTexture("_EmissionMap") != null)
                        copy.SetTexture("_EmissionMap", source.GetTexture("_EmissionMap"));
                    copy.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                }
            }

            if (source.HasProperty("_Mode"))
            {
                var mode = Mathf.RoundToInt(source.GetFloat("_Mode"));
                if (mode == 1)
                {
                    copy.SetFloat("_AlphaClip", 1f);
                    copy.EnableKeyword("_ALPHATEST_ON");
                    if (source.HasProperty("_Cutoff"))
                        copy.SetFloat("_Cutoff", source.GetFloat("_Cutoff"));
                }
                else if (mode >= 2)
                {
                    notes.Add($"Material '{source.name}' was transparent; its workshop copy is opaque. A facilitator can change Surface Type on the copy.");
                }
            }

            AssetDatabase.CreateAsset(copy, path);
            return copy;
        }
    }
}
