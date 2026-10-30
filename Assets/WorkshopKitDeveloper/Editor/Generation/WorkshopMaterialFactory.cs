using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace UnityWorkshop
{
    /// <summary>Creates or updates the kit's URP Lit materials in place, so their GUIDs never change.</summary>
    static class WorkshopMaterialFactory
    {
        const string LitShader = "Universal Render Pipeline/Lit";

        public static Material Get(string name, Color color, float smoothness = 0.2f, Color? emission = null)
        {
            var path = $"{WorkshopPaths.Materials}/{name}.mat";
            var shader = Shader.Find(LitShader);
            if (shader == null)
                throw new System.InvalidOperationException($"Shader '{LitShader}' not found. The kit requires the Universal Render Pipeline package.");

            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            var created = material == null;
            if (created)
                material = new Material(shader);
            else if (material.shader != shader)
                material.shader = shader;

            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", 0f);
            if (emission.HasValue)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", emission.Value);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            else
            {
                material.DisableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", Color.black);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            }
            material.enableInstancing = true;

            if (created)
                AssetDatabase.CreateAsset(material, path);
            else
                EditorUtility.SetDirty(material);
            return material;
        }

        public static Color Hex(string hex) => ColorUtility.TryParseHtmlString(hex, out var color) ? color : Color.magenta;
    }

    /// <summary>The "Workshop Meadow" theme: one finished, consistent palette.</summary>
    sealed class WorkshopPalette
    {
        public Material FloorLight, FloorDark, Wall, Boundary, Wood, Trunk, Leaves, Stone;
        public Material PlayerBody, PlayerVisor, Gem, HazardSpike, HazardBase, GoalLocked, GoalOpen, StartPad;
        public Material CheckpointIdle, CheckpointActive, Symbol, Frame;
        public Material[] Channel;

        public static WorkshopPalette Create()
        {
            var hex = (System.Func<string, Color>)WorkshopMaterialFactory.Hex;
            return new WorkshopPalette
            {
                FloorLight = WorkshopMaterialFactory.Get("Floor Light", hex("#D8CDA8"), 0.1f),
                FloorDark = WorkshopMaterialFactory.Get("Floor Dark", hex("#C7BA90"), 0.1f),
                Wall = WorkshopMaterialFactory.Get("Wall", hex("#5D7290"), 0.25f),
                Boundary = WorkshopMaterialFactory.Get("Boundary", hex("#3E4A5E"), 0.2f),
                Wood = WorkshopMaterialFactory.Get("Wood", hex("#A8743F"), 0.15f),
                Trunk = WorkshopMaterialFactory.Get("Trunk", hex("#6B4A2E"), 0.1f),
                Leaves = WorkshopMaterialFactory.Get("Leaves", hex("#4E9C55"), 0.1f),
                Stone = WorkshopMaterialFactory.Get("Stone", hex("#8C9095"), 0.3f),
                PlayerBody = WorkshopMaterialFactory.Get("Player Body", hex("#2F80ED"), 0.45f),
                PlayerVisor = WorkshopMaterialFactory.Get("Player Visor", hex("#F4F6F8"), 0.8f),
                Gem = WorkshopMaterialFactory.Get("Gem", hex("#FFC83D"), 0.85f, hex("#B87A00")),
                HazardSpike = WorkshopMaterialFactory.Get("Hazard Spike", hex("#E5484D"), 0.5f, hex("#7A0E12")),
                HazardBase = WorkshopMaterialFactory.Get("Hazard Base", hex("#2E2E33"), 0.2f),
                GoalLocked = WorkshopMaterialFactory.Get("Goal Locked", hex("#9AA5A0"), 0.3f),
                GoalOpen = WorkshopMaterialFactory.Get("Goal Open", hex("#3DDC84"), 0.6f, hex("#11A050")),
                StartPad = WorkshopMaterialFactory.Get("Start Pad", hex("#4C8DFF"), 0.4f, hex("#0B3A99")),
                CheckpointIdle = WorkshopMaterialFactory.Get("Checkpoint Idle", hex("#B9C0BC"), 0.3f),
                CheckpointActive = WorkshopMaterialFactory.Get("Checkpoint Active", hex("#3DDC84"), 0.6f, hex("#0E8A45")),
                Symbol = WorkshopMaterialFactory.Get("Channel Symbol", hex("#F7F7F2"), 0.5f, hex("#3A3A36")),
                Frame = WorkshopMaterialFactory.Get("Door Frame", hex("#34383F"), 0.3f),
                Channel = new[]
                {
                    WorkshopMaterialFactory.Get("Channel Red", hex("#E5484D"), 0.45f, hex("#3D0A0C")),
                    WorkshopMaterialFactory.Get("Channel Blue", hex("#3E7BFA"), 0.45f, hex("#0A1E4D")),
                    WorkshopMaterialFactory.Get("Channel Green", hex("#2FBF71"), 0.45f, hex("#08361E")),
                    WorkshopMaterialFactory.Get("Channel Yellow", hex("#F5C518"), 0.45f, hex("#4A3A00")),
                },
            };
        }
    }
}
