using UnityEditor;
using UnityEngine;

namespace UnityWorkshop
{
    /// <summary>Third-person starter and example scenes.</summary>
    public static partial class WorkshopContentGenerator
    {
        static void BuildThirdPersonStarter(Kit kit)
        {
            var builder = new SceneBuilder(kit, ThirdPersonRig(kit));
            builder.Floor(3);
            builder.Systems("My Third-Person Game", "Collect every gem, then reach the exit. Jump onto the platform for the last one!",
                GoalRule.CollectAllThenExit, new Vector3(-4f, 0f, -4f), 0f);
            builder.Add(kit.Gem, new Vector3(-4f, 0f, 3f), 0f, "Gameplay");
            builder.Add(kit.Gem, new Vector3(0f, 0f, 0f), 0f, "Gameplay");
            builder.Add(kit.Platform, new Vector3(3f, 0f, -3f), 0f, "Environment");
            builder.Add(kit.Gem, new Vector3(3f, 1f, -3f), 0f, "Gameplay");
            builder.Add(kit.Exit, new Vector3(4f, 0f, 4f), 0f, "Gameplay");
            builder.Save(ThirdPersonStarterPath);
            SetStarterScene(ThirdPersonTemplatePath, ThirdPersonStarterPath);
        }

        /// <summary>
        /// "Sky Steps": a short adventure route northward from a start with room for the camera behind it. Gems on the ground and on a platform, a canyon of
        /// tall walls (camera collision), moving spikes, a checkpoint, then a jump across a pit via a floating
        /// stone, and the exit. Falling into the pit returns the player to the checkpoint.
        /// </summary>
        static void BuildThirdPersonExample(Kit kit)
        {
            var builder = new SceneBuilder(kit, ThirdPersonRig(kit));
            var tileRows = new[] { -12f, -8f, -4f, 0f, 4f, 12f, 16f };
            for (var row = 0; row < tileRows.Length; row++)
            {
                var z = tileRows[row];
                builder.Add(row % 2 == 0 ? kit.FloorLight : kit.FloorDark, new Vector3(-2f, 0f, z), 0f, "Environment");
                builder.Add(row % 2 == 0 ? kit.FloorDark : kit.FloorLight, new Vector3(2f, 0f, z), 0f, "Environment");
                // Tall walls at z = 0 form a narrow canyon that pushes the camera in when orbiting.
                var side = Mathf.Approximately(z, 0f) ? kit.TallWall4 : kit.Boundary4;
                builder.Add(side, new Vector3(-4.2f, 0f, z), 90f, "Environment");
                builder.Add(side, new Vector3(4.2f, 0f, z), 90f, "Environment");
            }
            foreach (var x in new[] { -2f, 2f })
            {
                builder.Add(kit.Boundary4, new Vector3(x, 0f, -14.2f), 0f, "Environment");
                builder.Add(kit.Boundary4, new Vector3(x, 0f, 18.2f), 0f, "Environment");
            }

            builder.Systems("Sky Steps",
                "Collect all 5 gems along the route. Jump to the floating stone to cross the pit. Don't fall!",
                GoalRule.CollectAllThenExit, new Vector3(0f, 0f, -8f), 0f);

            builder.Add(kit.Platform, new Vector3(-2f, 0f, 0f), 0f, "Environment");
            builder.Add(kit.Platform, new Vector3(0f, 0f, 8f), 0f, "Environment");
            var pit = builder.Add(kit.FallZone, new Vector3(0f, -2.5f, 8f), 0f, "Gameplay").GetComponent<BoxCollider>();
            var pitBox = new SerializedObject(pit);
            pitBox.FindProperty("m_Size").vector3Value = new Vector3(12f, 1f, 8f);
            pitBox.ApplyModifiedPropertiesWithoutUndo();

            foreach (var gem in new[]
                     {
                         new Vector3(-2f, 0f, -4f), new Vector3(2f, 0f, -2f), new Vector3(-2f, 1f, 0f),
                         new Vector3(0f, 1f, 8f), new Vector3(2f, 0f, 12f),
                     })
                builder.Add(kit.Gem, gem, 0f, "Gameplay");

            builder.Add(kit.Spikes, new Vector3(2f, 0f, -5.5f), 0f, "Gameplay");
            builder.Add(kit.Spikes, new Vector3(-2f, 0f, 15f), 0f, "Gameplay");
            var mover = builder.Add(kit.MovingSpikes, new Vector3(-3f, 0f, 2.5f), 0f, "Gameplay").GetComponent<HazardMover>();
            var moverSettings = new SerializedObject(mover);
            moverSettings.FindProperty("m_Travel").vector3Value = new Vector3(6f, 0f, 0f);
            moverSettings.FindProperty("m_Speed").enumValueIndex = (int)MoveSpeed.Slow;
            moverSettings.ApplyModifiedPropertiesWithoutUndo();

            builder.Add(kit.Checkpoint, new Vector3(0f, 0f, 4.5f), 0f, "Gameplay");
            builder.Add(kit.Exit, new Vector3(1.5f, 0f, 16f), 0f, "Gameplay");

            builder.Add(kit.Tree, new Vector3(3f, 0f, -13f), 0f, "Decoration");
            builder.Add(kit.Rock, new Vector3(-3f, 0f, 13f), 30f, "Decoration");
            builder.Add(kit.Crate, new Vector3(3.3f, 0f, 15f), 15f, "Decoration");
            builder.Save(ThirdPersonExamplePath);
        }
    }
}
