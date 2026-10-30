using UnityEditor;
using UnityEngine;

namespace UnityWorkshop
{
    /// <summary>First-person starter and example scenes.</summary>
    public static partial class WorkshopContentGenerator
    {
        static void BuildFirstPersonStarter(Kit kit)
        {
            var builder = new SceneBuilder(kit, FirstPersonRig(kit));
            builder.Floor(3);
            builder.Systems("My First-Person Game", "Collect every gem, then reach the exit. Jump onto the platform for the last one!",
                GoalRule.CollectAllThenExit, new Vector3(-4f, 0f, -4f), 0f);
            builder.Add(kit.Gem, new Vector3(-4f, 0f, 3f), 0f, "Gameplay");
            builder.Add(kit.Gem, new Vector3(0f, 0f, 0f), 0f, "Gameplay");
            builder.Add(kit.Platform, new Vector3(3f, 0f, -3f), 0f, "Environment");
            builder.Add(kit.Gem, new Vector3(3f, 1f, -3f), 0f, "Gameplay");
            builder.Add(kit.Exit, new Vector3(4f, 0f, 4f), 0f, "Gameplay");
            builder.Save(FirstPersonStarterPath);
            SetStarterScene(FirstPersonTemplatePath, FirstPersonStarterPath);
        }

        /// <summary>
        /// "Key Maze": three tall-walled lanes, each closed by a coloured door. The red key sits on a raised
        /// platform (jump), the blue key past moving spikes, the green key in the last lane before the exit.
        /// </summary>
        static void BuildFirstPersonExample(Kit kit)
        {
            var builder = new SceneBuilder(kit, FirstPersonRig(kit));
            builder.Floor(5);
            builder.Systems("Key Maze",
                "Find the red, blue and green keys to open the doors, then reach the exit. Jump onto the platform for the first key!",
                GoalRule.ReachExit, new Vector3(-8f, 0f, -8f), 0f, TimerPreset.Relaxed);

            foreach (var z in new[] { -8f, -4f, 0f, 4f })
                builder.Add(kit.TallWall4, new Vector3(-4f, 0f, z), 90f, "Environment");
            foreach (var z in new[] { 0f, 4f, 8f })
                builder.Add(kit.TallWall4, new Vector3(4f, 0f, z), 90f, "Environment");

            // Left lane: red door at z = 4.
            builder.Add(kit.TallWall4, new Vector3(-8f, 0f, 4f), 0f, "Environment");
            SetChannel(builder.Add(kit.Door, new Vector3(-5f, 0f, 4f), 0f, "Gameplay"), ChannelColor.Red);
            // Middle lane: blue door at z = -2.
            builder.Add(kit.TallWall4, new Vector3(-2f, 0f, -2f), 0f, "Environment");
            builder.Add(kit.TallWall2, new Vector3(1f, 0f, -2f), 0f, "Environment");
            SetChannel(builder.Add(kit.Door, new Vector3(3f, 0f, -2f), 0f, "Gameplay"), ChannelColor.Blue);
            // Right lane: green door at z = 4.
            builder.Add(kit.TallWall4, new Vector3(6f, 0f, 4f), 0f, "Environment");
            SetChannel(builder.Add(kit.Door, new Vector3(9f, 0f, 4f), 0f, "Gameplay"), ChannelColor.Green);

            builder.Add(kit.Platform, new Vector3(-8f, 0f, -2f), 0f, "Environment");
            SetChannel(builder.Add(kit.Key, new Vector3(-8f, 1f, -2f), 0f, "Gameplay"), ChannelColor.Red);
            SetChannel(builder.Add(kit.Key, new Vector3(-1f, 0f, 4f), 0f, "Gameplay"), ChannelColor.Blue);
            SetChannel(builder.Add(kit.Key, new Vector3(9f, 0f, -5f), 0f, "Gameplay"), ChannelColor.Green);

            builder.Add(kit.Spikes, new Vector3(-6f, 0f, -5f), 0f, "Gameplay");
            builder.Add(kit.Spikes, new Vector3(-2f, 0f, 2f), 0f, "Gameplay");
            builder.Add(kit.Spikes, new Vector3(2f, 0f, 5f), 0f, "Gameplay");
            builder.Add(kit.Spikes, new Vector3(6f, 0f, -5f), 0f, "Gameplay");
            var mover = builder.Add(kit.MovingSpikes, new Vector3(-3f, 0f, 8f), 0f, "Gameplay").GetComponent<HazardMover>();
            var moverSettings = new SerializedObject(mover);
            moverSettings.FindProperty("m_Travel").vector3Value = new Vector3(6f, 0f, 0f);
            moverSettings.FindProperty("m_Speed").enumValueIndex = (int)MoveSpeed.Slow;
            moverSettings.ApplyModifiedPropertiesWithoutUndo();

            builder.Add(kit.Checkpoint, new Vector3(-7f, 0f, 8f), 90f, "Gameplay");
            builder.Add(kit.Checkpoint, new Vector3(6f, 0f, -8f), 0f, "Gameplay");
            builder.Add(kit.Exit, new Vector3(7f, 0f, 8f), 0f, "Gameplay");

            builder.Add(kit.Crate, new Vector3(-9.3f, 0f, -9.3f), 20f, "Decoration");
            builder.Add(kit.Rock, new Vector3(0f, 0f, -8f), 40f, "Decoration");
            builder.Add(kit.Tree, new Vector3(-2f, 0f, -7f), 0f, "Decoration");
            builder.Add(kit.Tree, new Vector3(2.8f, 0f, 1f), 70f, "Decoration");
            builder.Save(FirstPersonExamplePath);
        }

        static void SetStarterScene(string templatePath, string starterPath)
        {
            var serialized = new SerializedObject(AssetDatabase.LoadAssetAtPath<GameTemplateDefinition>(templatePath));
            serialized.FindProperty("m_StarterSceneGuid").stringValue = AssetDatabase.AssetPathToGUID(starterPath);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
        }
    }
}
