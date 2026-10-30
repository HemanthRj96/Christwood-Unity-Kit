using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace UnityWorkshop
{
    public enum CatalogCategory
    {
        Environment,
        [InspectorName("Player Appearance")] PlayerAppearance,
        Objectives,
        Challenges,
        Progress,
        Decoration,
    }

    [Serializable]
    public sealed class CatalogEntry
    {
        public string displayName = "";
        [TextArea(1, 3)] public string description = "";
        [Tooltip("Limits or controls the student should know about.")]
        public string constraints = "";
        public GameObject prefab;
        public CatalogCategory category;
        [Tooltip("Placement grid in metres. 0 places freely.")]
        public float gridSize = 1f;
        [Tooltip("Hierarchy group under Level the piece is added to.")]
        public string groupName = "Environment";

        /// <summary>Set for adopted player looks; using the entry swaps the player's model instead of placing it.</summary>
        [NonSerialized] public AdoptedAsset playerLook;

        /// <summary>Whether this piece suits the template, from the prefab's <see cref="PieceTraits"/>.</summary>
        public bool IsCompatible(GameTemplateDefinition template) => PieceTraits.Unsuitability(prefab, template) == null;
    }

    /// <summary>The pieces students can add from the Workshop Builder.</summary>
    public sealed class WorkshopCatalog : ScriptableObject
    {
        public List<CatalogEntry> entries = new List<CatalogEntry>();

        public static WorkshopCatalog Load() => AssetDatabase.LoadAssetAtPath<WorkshopCatalog>(WorkshopPaths.Catalog);

        /// <summary>The kit's entries plus adopted props (Decoration) and adopted player looks (Player Appearance).</summary>
        public List<CatalogEntry> AllEntries()
        {
            var all = new List<CatalogEntry>(entries);
            foreach (var prop in WorkshopAssetAdoption.Find(AdoptedRole.Prop))
                all.Add(new CatalogEntry
                {
                    displayName = prop.DisplayName, prefab = prop.gameObject, category = CatalogCategory.Decoration,
                    description = "An adopted model.", constraints = "Adopted asset. See its notes in the Inspector.",
                    gridSize = 0.5f, groupName = "Decoration",
                });
            foreach (var look in WorkshopAssetAdoption.Find(AdoptedRole.PlayerLook))
                all.Add(new CatalogEntry
                {
                    displayName = look.DisplayName, prefab = look.gameObject, category = CatalogCategory.PlayerAppearance,
                    description = "Changes how the player looks. Movement and collision stay the same.", playerLook = look,
                });
            return all;
        }

        public static string CategoryLabel(CatalogCategory category) =>
            category == CatalogCategory.PlayerAppearance ? "Player Appearance" : category.ToString();
    }
}
