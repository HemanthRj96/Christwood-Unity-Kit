using UnityEditor;
using UnityEngine;

namespace UnityWorkshop
{
    /// <summary>Side-scroller starter and example scenes. Everything gameplay sits on the play plane at Z = 0.</summary>
    public static partial class WorkshopContentGenerator
    {
        static void BuildSideScrollerStarter(Kit kit)
        {
            var builder = new SceneBuilder(kit, SideScrollerRig(kit));
            foreach (var x in new[] { -6f, -2f, 2f, 6f })
                builder.Add(x % 8f == 6f || x % 8f == -2f ? kit.FloorDark : kit.FloorLight, new Vector3(x, 0f, 0f), 0f, "Environment");
            builder.Add(kit.TallWall4, new Vector3(-8.2f, 0f, 0f), 90f, "Environment");
            builder.Add(kit.TallWall4, new Vector3(8.2f, 0f, 0f), 90f, "Environment");

            builder.Systems("My Side-Scroller", "Collect every gem, then run to the exit. Jump up to the floating platform!",
                GoalRule.CollectAllThenExit, new Vector3(-6f, 0f, 0f), 90f);
            builder.Add(kit.Gem, new Vector3(-2f, 0f, 0f), 0f, "Gameplay");
            builder.Add(kit.FloatingPlatform, new Vector3(2f, 1f, 0f), 0f, "Environment");
            builder.Add(kit.Gem, new Vector3(2f, 1f, 0f), 0f, "Gameplay");
            builder.Add(kit.Gem, new Vector3(5f, 0f, 0f), 0f, "Gameplay");
            builder.Add(kit.Exit, new Vector3(7f, 0f, 0f), 0f, "Gameplay");
            SetSideBounds(builder, new Vector2(-4f, 1.5f), new Vector2(4f, 5f), new Vector3(-6f, 0f, 0f));
            builder.Save(SideScrollerStarterPath);
            SetStarterScene(SideScrollerTemplatePath, SideScrollerStarterPath);
        }

        /// <summary>
        /// "Cliff Run": four ground sections separated by pits, crossed on floating platforms. Spikes to hop
        /// over, slow moving spikes, a raised step with a gem on top, a checkpoint halfway, and the exit at the
        /// far right. Falling into a pit returns the player to the start or checkpoint.
        /// </summary>
        static void BuildSideScrollerExample(Kit kit)
        {
            var builder = new SceneBuilder(kit, SideScrollerRig(kit));
            var tiles = new[] { -8f, -4f, 0f, 9f, 13f, 22f, 26f, 35f, 39f };
            for (var i = 0; i < tiles.Length; i++)
                builder.Add(i % 2 == 0 ? kit.FloorLight : kit.FloorDark, new Vector3(tiles[i], 0f, 0f), 0f, "Environment");
            builder.Add(kit.TallWall4, new Vector3(-10.2f, 0f, 0f), 90f, "Environment");
            builder.Add(kit.TallWall4, new Vector3(41.2f, 0f, 0f), 90f, "Environment");

            builder.Systems("Cliff Run",
                "Collect all 5 gems and reach the exit on the far right. Jump across the floating platforms and don't fall!",
                GoalRule.CollectAllThenExit, new Vector3(-8f, 0f, 0f), 90f);

            builder.Add(kit.FloatingPlatform, new Vector3(4.5f, 1f, 0f), 0f, "Environment");
            builder.Add(kit.FloatingPlatform, new Vector3(17.5f, 0.8f, 0f), 0f, "Environment");
            builder.Add(kit.Platform, new Vector3(27f, 0f, 0f), 0f, "Environment");
            builder.Add(kit.FloatingPlatform, new Vector3(30.5f, 1.8f, 0f), 0f, "Environment");

            foreach (var gem in new[]
                     {
                         new Vector3(4.5f, 1f, 0f), new Vector3(9f, 0f, 0f), new Vector3(17.5f, 0.8f, 0f),
                         new Vector3(27f, 1f, 0f), new Vector3(35f, 0f, 0f),
                     })
                builder.Add(kit.Gem, gem, 0f, "Gameplay");

            builder.Add(kit.Spikes, new Vector3(11.5f, 0f, 0f), 0f, "Gameplay");
            builder.Add(kit.Spikes, new Vector3(37f, 0f, 0f), 0f, "Gameplay");
            var mover = builder.Add(kit.MovingSpikes, new Vector3(21f, 0f, 0f), 0f, "Gameplay").GetComponent<HazardMover>();
            var moverSettings = new SerializedObject(mover);
            moverSettings.FindProperty("m_Travel").vector3Value = new Vector3(3.5f, 0f, 0f);
            moverSettings.FindProperty("m_Speed").enumValueIndex = (int)MoveSpeed.Slow;
            moverSettings.ApplyModifiedPropertiesWithoutUndo();

            builder.Add(kit.Checkpoint, new Vector3(14f, 0f, 0f), 90f, "Gameplay");
            builder.Add(kit.Exit, new Vector3(40f, 0f, 0f), 0f, "Gameplay");

            // Scenery behind the play plane.
            builder.Add(kit.Tree, new Vector3(-3f, 0f, 1.6f), 0f, "Decoration");
            builder.Add(kit.Tree, new Vector3(13f, 0f, 1.6f), 50f, "Decoration");
            builder.Add(kit.Crate, new Vector3(24f, 0f, 1.5f), 20f, "Decoration");
            builder.Add(kit.Tree, new Vector3(38.5f, 0f, 1.6f), 10f, "Decoration");
            builder.Add(kit.Rock, new Vector3(0.5f, 0f, 1.4f), 30f, "Decoration");

            SetSideBounds(builder, new Vector2(-6f, 1.5f), new Vector2(37f, 6f), new Vector3(-8f, 0f, 0f));
            builder.Save(SideScrollerExamplePath);
        }

        static void SetSideBounds(SceneBuilder builder, Vector2 min, Vector2 max, Vector3 start)
        {
            var rig = WorkshopSceneQuery.FindActive<SideScrollerCameraRig>(builder.Scene)[0];
            var serialized = new SerializedObject(rig);
            serialized.FindProperty("m_UseBounds").boolValue = true;
            serialized.FindProperty("m_BoundsMin").vector2Value = min;
            serialized.FindProperty("m_BoundsMax").vector2Value = max;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            rig.FrameFeet(start);
        }
    }
}
