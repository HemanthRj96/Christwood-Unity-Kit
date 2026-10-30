using UnityEditor;
using UnityEngine;

namespace UnityWorkshop
{
    /// <summary>
    /// Recovery milestones: four teaching scenes per template that show a game at each stage of the
    /// workshop. Facilitators copy them into a team folder when a team needs to catch up. Every milestone
    /// is a valid, playable game.
    /// </summary>
    public static partial class WorkshopContentGenerator
    {
        static readonly string[] MilestoneNames = { "1 Movement Ready", "2 First Winnable", "3 Objective Ready", "4 Challenge Ready" };

        static void BuildRecoveryMilestones(Kit kit)
        {
            BuildMilestones(kit, TemplatePerspective.TopDown, TopDownRig, kit.LowWall4);
            BuildMilestones(kit, TemplatePerspective.FirstPerson, FirstPersonRig, kit.TallWall4);
            BuildMilestones(kit, TemplatePerspective.ThirdPerson, ThirdPersonRig, kit.TallWall4);
            BuildSideScrollerMilestones(kit);
        }

        static string MilestonePath(TemplatePerspective perspective, int index)
        {
            var folder = $"{WorkshopPaths.Recovery}/{perspective}";
            EnsureFolder(folder);
            return $"{folder}/{MilestoneNames[index]}.unity";
        }

        static void BuildMilestones(Kit kit, TemplatePerspective perspective, System.Func<Kit, Rig> rig, GameObject wall)
        {
            for (var stage = 0; stage < MilestoneNames.Length; stage++)
            {
                var builder = new SceneBuilder(kit, rig(kit));
                builder.Floor(3);
                var collect = stage >= 2;
                builder.Systems($"Milestone {MilestoneNames[stage]}", MilestoneObjective(stage),
                    collect ? GoalRule.CollectAllThenExit : GoalRule.ReachExit, new Vector3(-4f, 0f, -4f), 0f);
                builder.Add(kit.Exit, new Vector3(4f, 0f, 4f), 0f, "Gameplay");
                if (stage >= 1)
                {
                    builder.Add(wall, new Vector3(0f, 0f, -2f), 0f, "Environment");
                    builder.Add(wall, new Vector3(2f, 0f, 2f), 90f, "Environment");
                }
                if (collect)
                    foreach (var gem in new[] { new Vector3(-4f, 0f, 3f), new Vector3(2f, 0f, -4f), new Vector3(4f, 0f, 0f) })
                        builder.Add(kit.Gem, gem, 0f, "Gameplay");
                if (stage >= 3)
                {
                    builder.Add(kit.Spikes, new Vector3(-1f, 0f, 1f), 0f, "Gameplay");
                    builder.Add(kit.Checkpoint, new Vector3(-4f, 0f, 0f), 0f, "Gameplay");
                }
                builder.Save(MilestonePath(perspective, stage));
            }
        }

        static void BuildSideScrollerMilestones(Kit kit)
        {
            for (var stage = 0; stage < MilestoneNames.Length; stage++)
            {
                var builder = new SceneBuilder(kit, SideScrollerRig(kit));
                foreach (var x in new[] { -6f, -2f, 2f, 6f })
                    builder.Add(kit.FloorLight, new Vector3(x, 0f, 0f), 0f, "Environment");
                builder.Add(kit.TallWall4, new Vector3(-8.2f, 0f, 0f), 90f, "Environment");
                builder.Add(kit.TallWall4, new Vector3(8.2f, 0f, 0f), 90f, "Environment");
                var collect = stage >= 2;
                builder.Systems($"Milestone {MilestoneNames[stage]}", MilestoneObjective(stage),
                    collect ? GoalRule.CollectAllThenExit : GoalRule.ReachExit, new Vector3(-6f, 0f, 0f), 90f);
                builder.Add(kit.Exit, new Vector3(7f, 0f, 0f), 0f, "Gameplay");
                if (stage >= 1)
                    builder.Add(kit.FloatingPlatform, new Vector3(1f, 1f, 0f), 0f, "Environment");
                if (collect)
                    foreach (var gem in new[] { new Vector3(-3f, 0f, 0f), new Vector3(1f, 1f, 0f), new Vector3(5f, 0f, 0f) })
                        builder.Add(kit.Gem, gem, 0f, "Gameplay");
                if (stage >= 3)
                {
                    builder.Add(kit.Spikes, new Vector3(3.5f, 0f, 0f), 0f, "Gameplay");
                    builder.Add(kit.Checkpoint, new Vector3(-2f, 0f, 0f), 90f, "Gameplay");
                }
                SetSideBounds(builder, new Vector2(-4f, 1.5f), new Vector2(4f, 5f), new Vector3(-6f, 0f, 0f));
                builder.Save(MilestonePath(TemplatePerspective.SideScroller, stage));
            }
        }

        static string MilestoneObjective(int stage)
        {
            switch (stage)
            {
                case 0: return "Walk around and reach the exit. Next: build a route.";
                case 1: return "Find your way around the walls to the exit. Next: add things to collect.";
                case 2: return "Collect all 3 gems, then reach the exit. Next: add a challenge.";
                default: return "Collect all 3 gems and reach the exit. Avoid the spikes; the checkpoint saves your place.";
            }
        }
    }
}
