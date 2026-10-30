using System;
using UnityEngine;

namespace UnityWorkshop
{
    /// <summary>How a team's level is won.</summary>
    public enum GoalRule
    {
        [InspectorName("Reach the Exit")] ReachExit,
        [InspectorName("Collect All, Then Exit")] CollectAllThenExit,
    }

    /// <summary>Optional countdown. Running out of time loses the game.</summary>
    public enum TimerPreset
    {
        Off,
        Relaxed,
        Challenge,
    }

    /// <summary>
    /// Team-specific game settings. Lives on the scene's Game Setup object, never in a shared preset,
    /// so one team's changes cannot alter another team's game.
    /// </summary>
    [Serializable]
    public sealed class WorkshopGameRules
    {
        public const int MaxTitleLength = 40;
        public const int MaxObjectiveLength = 120;

        [Tooltip("The name of your game. Shown at the top of the screen.")]
        public string title = "My Workshop Game";

        [Tooltip("One sentence telling the player what to do.")]
        public string objective = "Collect every gem, then reach the exit.";

        [Tooltip("What the player must do to win.")]
        public GoalRule goalRule = GoalRule.CollectAllThenExit;

        [Tooltip("What the collectible items are called on screen, for example Gem, Coin or Key Card.")]
        public string collectibleName = "Gem";

        [Tooltip("Optional time limit. When the time runs out the player loses.")]
        public TimerPreset timer = TimerPreset.Off;

        public const float RelaxedSeconds = 180f;
        public const float ChallengeSeconds = 90f;

        /// <summary>Time limit in seconds for a preset; 0 means no limit.</summary>
        public static float SecondsFor(TimerPreset preset) =>
            preset == TimerPreset.Relaxed ? RelaxedSeconds : preset == TimerPreset.Challenge ? ChallengeSeconds : 0f;

        public float TimeLimit => SecondsFor(timer);

        public string CollectibleLabel => string.IsNullOrWhiteSpace(collectibleName) ? "Item" : collectibleName.Trim();

        public string Title => string.IsNullOrWhiteSpace(title) ? "Untitled Game" : title.Trim();

        public string Objective => string.IsNullOrWhiteSpace(objective) ? DefaultObjectiveFor(goalRule) : objective.Trim();

        public static string DefaultObjectiveFor(GoalRule rule) =>
            rule == GoalRule.ReachExit ? "Reach the exit." : "Collect everything, then reach the exit.";
    }
}
