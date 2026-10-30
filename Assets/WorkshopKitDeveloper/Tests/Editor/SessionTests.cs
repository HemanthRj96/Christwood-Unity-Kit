using NUnit.Framework;

namespace UnityWorkshop.Tests
{
    /// <summary>Rules of <see cref="WorkshopGameSession"/> without a scene (A04, A05).</summary>
    public sealed class SessionTests
    {
        static WorkshopGameSession Playing(GoalRule rule, params object[] items)
        {
            var session = new WorkshopGameSession(rule, "Gem");
            foreach (var item in items)
                session.RegisterRequired(item);
            Assert.That(session.Begin(), Is.True);
            return session;
        }

        [Test]
        public void CollectCountsEachItemOnce()
        {
            var a = new object();
            var b = new object();
            var session = Playing(GoalRule.CollectAllThenExit, a, b);
            var events = 0;
            session.ProgressChanged += () => events++;

            Assert.That(session.TryCollect(a), Is.True);
            Assert.That(session.TryCollect(a), Is.False);
            Assert.That(session.TryCollect(new object()), Is.False, "Unregistered items never count.");
            Assert.That(session.CollectedCount, Is.EqualTo(1));
            Assert.That(events, Is.EqualTo(1));
        }

        [Test]
        public void CollectAllBlocksGoalUntilEverythingIsCollected()
        {
            var a = new object();
            var b = new object();
            var session = Playing(GoalRule.CollectAllThenExit, a, b);
            string message = null;
            session.GoalBlocked += m => message = m;

            Assert.That(session.TryReachGoal(true), Is.False);
            Assert.That(message, Is.EqualTo("Collect 2 more Gems to unlock the exit."));
            session.TryCollect(a);
            message = null;
            Assert.That(session.TryReachGoal(false), Is.False);
            Assert.That(message, Is.Null, "Silent checks do not repeat the message.");
            session.TryCollect(b);
            Assert.That(session.TryReachGoal(true), Is.True);
            Assert.That(session.State, Is.EqualTo(GameState.Won));
        }

        [Test]
        public void ReachExitWinsImmediately()
        {
            var session = Playing(GoalRule.ReachExit, new object());
            Assert.That(session.TryReachGoal(true), Is.True);
        }

        [Test]
        public void TerminalStateRejectsFurtherActions()
        {
            var item = new object();
            var session = Playing(GoalRule.ReachExit, item);
            var wins = 0;
            session.StateChanged += (_, next) => { if (next == GameState.Won) wins++; };

            Assert.That(session.TryReachGoal(true), Is.True);
            Assert.That(session.TryReachGoal(true), Is.False);
            Assert.That(session.TryCollect(item), Is.False);
            Assert.That(session.RequestRespawn(), Is.False);
            Assert.That(session.TogglePause(), Is.False);
            Assert.That(session.Lose(), Is.False);
            Assert.That(wins, Is.EqualTo(1));
        }

        [Test]
        public void RespawnRunsThenProtectsThenEnds()
        {
            var session = Playing(GoalRule.ReachExit);
            var respawns = 0;
            session.RespawnStarted += () => respawns++;

            Assert.That(session.RequestRespawn(), Is.True);
            Assert.That(session.RequestRespawn(), Is.False, "Overlapping hazards cannot start a second respawn.");
            Assert.That(session.State, Is.EqualTo(GameState.Respawning));
            Assert.That(session.AcceptsPlayerInput, Is.False);
            Assert.That(session.TryReachGoal(true), Is.False);

            session.Tick(WorkshopGameSession.RespawnDuration + 0.01f);
            Assert.That(session.State, Is.EqualTo(GameState.Playing));
            Assert.That(session.IsHazardProtected, Is.True);
            Assert.That(session.RequestRespawn(), Is.False, "Protected right after respawning.");

            session.Tick(WorkshopGameSession.HazardProtectionDuration + 0.01f);
            Assert.That(session.IsHazardProtected, Is.False);
            Assert.That(session.RequestRespawn(), Is.True);
            Assert.That(respawns, Is.EqualTo(2));
        }

        [Test]
        public void RespawnKeepsCollectionAndClock()
        {
            var item = new object();
            var session = Playing(GoalRule.CollectAllThenExit, item, new object());
            session.TryCollect(item);
            session.Tick(3f);
            session.RequestRespawn();
            session.Tick(1f);
            Assert.That(session.CollectedCount, Is.EqualTo(1));
            Assert.That(session.ElapsedTime, Is.EqualTo(4f).Within(0.0001f));
        }

        [Test]
        public void PauseRestoresThePausedState()
        {
            var session = Playing(GoalRule.ReachExit);
            session.RequestRespawn();
            Assert.That(session.TogglePause(), Is.True);
            Assert.That(session.State, Is.EqualTo(GameState.Paused));

            session.Tick(10f);
            Assert.That(session.ElapsedTime, Is.EqualTo(0f), "The clock stops while paused.");
            Assert.That(session.TryReachGoal(true), Is.False);

            Assert.That(session.TogglePause(), Is.True);
            Assert.That(session.State, Is.EqualTo(GameState.Respawning), "Resume returns to the state that was paused.");
        }

        [Test]
        public void ShutdownStopsAllEvents()
        {
            var item = new object();
            var session = Playing(GoalRule.CollectAllThenExit, item);
            var events = 0;
            session.ProgressChanged += () => events++;
            session.StateChanged += (_, __) => events++;
            session.Shutdown();

            Assert.That(session.TryCollect(item), Is.False);
            Assert.That(session.TryReachGoal(true), Is.False);
            Assert.That(session.RequestRespawn(), Is.False);
            Assert.That(events, Is.EqualTo(0));
        }

        [Test]
        public void ReadyRejectsGameplayUntilBegin()
        {
            var item = new object();
            var session = new WorkshopGameSession(GoalRule.CollectAllThenExit);
            session.RegisterRequired(item);
            Assert.That(session.TryCollect(item), Is.False);
            Assert.That(session.RequestRespawn(), Is.False);
            Assert.That(session.TogglePause(), Is.False);
            Assert.That(session.Lose(), Is.False);
        }

        // ---- Timer ---------------------------------------------------------------------------------

        static WorkshopGameSession Timed(float limit)
        {
            var session = new WorkshopGameSession(GoalRule.ReachExit, "Gem", limit);
            session.Begin();
            return session;
        }

        [Test]
        public void TimerExpiryLosesOnce()
        {
            var session = Timed(10f);
            var losses = 0;
            session.StateChanged += (_, next) => { if (next == GameState.Lost) losses++; };
            session.Tick(9.5f);
            Assert.That(session.State, Is.EqualTo(GameState.Playing));
            Assert.That(session.TimeRemaining, Is.EqualTo(0.5f).Within(0.0001f));
            session.Tick(1f);
            Assert.That(session.State, Is.EqualTo(GameState.Lost));
            Assert.That(session.LossReason, Is.EqualTo(LossReason.TimeUp));
            Assert.That(session.ElapsedTime, Is.EqualTo(10f), "The clock stops exactly at the limit.");
            session.Tick(1f);
            Assert.That(session.TryReachGoal(true), Is.False);
            Assert.That(losses, Is.EqualTo(1));
        }

        [Test]
        public void ExpiredTimerBeatsSimultaneousGoal()
        {
            var session = Timed(10f);
            session.Tick(9.99f);
            Assert.That(session.TryReachGoal(true), Is.True, "Inside the limit the goal wins.");

            var late = Timed(10f);
            // Same authoritative clock: the limit is reached in the frame the goal is entered.
            late.Tick(9.99f);
            late.Tick(0.02f);
            Assert.That(late.TryReachGoal(true), Is.False);
            Assert.That(late.State, Is.EqualTo(GameState.Lost));
            Assert.That(late.LossReason, Is.EqualTo(LossReason.TimeUp));
        }

        [Test]
        public void TimerPausesAndKeepsRunningDuringRespawn()
        {
            var session = Timed(10f);
            session.TogglePause();
            session.Tick(20f);
            Assert.That(session.TimeRemaining, Is.EqualTo(10f));
            session.TogglePause();
            session.RequestRespawn();
            session.Tick(0.2f);
            Assert.That(session.TimeRemaining, Is.EqualTo(9.8f).Within(0.0001f));
        }

        [Test]
        public void NoTimerNeverExpires()
        {
            var session = Timed(0f);
            session.Tick(100000f);
            Assert.That(session.State, Is.EqualTo(GameState.Playing));
            Assert.That(session.HasTimeLimit, Is.False);
        }

        [Test]
        public void TimerPresetsShowRealSeconds()
        {
            Assert.That(WorkshopGameRules.SecondsFor(TimerPreset.Off), Is.EqualTo(0f));
            Assert.That(WorkshopGameRules.SecondsFor(TimerPreset.Relaxed), Is.EqualTo(180f));
            Assert.That(WorkshopGameRules.SecondsFor(TimerPreset.Challenge), Is.EqualTo(90f));
        }

        // ---- Channels ------------------------------------------------------------------------------

        [Test]
        public void KeysOpenOnlyTheirOwnChannel()
        {
            var session = Playing(GoalRule.ReachExit);
            var changes = 0;
            session.ChannelChanged += _ => changes++;
            var red = new object();
            Assert.That(session.TryCollectKey(red, ChannelColor.Red), Is.True);
            Assert.That(session.TryCollectKey(red, ChannelColor.Red), Is.False, "A key is collected once.");
            Assert.That(session.TryCollectKey(new object(), ChannelColor.Red), Is.True, "A second red key is still picked up.");
            Assert.That(changes, Is.EqualTo(1), "The channel only changes once.");
            Assert.That(session.IsChannelOpen(ChannelColor.Red), Is.True);
            Assert.That(session.IsChannelOpen(ChannelColor.Blue), Is.False);
            Assert.That(session.IsChannelOpen(ChannelColor.Green), Is.False);
            Assert.That(session.IsChannelOpen(ChannelColor.Yellow), Is.False);
        }

        [Test]
        public void SwitchesCombineWithOr()
        {
            var session = Playing(GoalRule.ReachExit);
            var a = new object();
            var b = new object();
            var changes = 0;
            session.ChannelChanged += _ => changes++;

            session.SetSwitch(a, ChannelColor.Blue, true, false);
            session.SetSwitch(b, ChannelColor.Blue, true, false);
            Assert.That(session.IsChannelOpen(ChannelColor.Blue), Is.True);
            session.SetSwitch(a, ChannelColor.Blue, false, false);
            Assert.That(session.IsChannelOpen(ChannelColor.Blue), Is.True, "Still open while another switch is pressed.");
            session.SetSwitch(b, ChannelColor.Blue, false, false);
            Assert.That(session.IsChannelOpen(ChannelColor.Blue), Is.False);
            Assert.That(changes, Is.EqualTo(2), "Only open/closed transitions are reported.");
        }

        [Test]
        public void LatchAndKeyKeepTheChannelOpen()
        {
            var session = Playing(GoalRule.ReachExit);
            var latch = new object();
            session.SetSwitch(latch, ChannelColor.Green, true, true);
            session.SetSwitch(latch, ChannelColor.Green, false, true);
            Assert.That(session.IsChannelOpen(ChannelColor.Green), Is.True);

            var plate = new object();
            session.TryCollectKey(new object(), ChannelColor.Yellow);
            session.SetSwitch(plate, ChannelColor.Yellow, true, false);
            session.SetSwitch(plate, ChannelColor.Yellow, false, false);
            Assert.That(session.IsChannelOpen(ChannelColor.Yellow), Is.True, "A held key keeps the door open after the switch is released.");
        }

        [Test]
        public void ChannelsIgnoreActionsOutsidePlay()
        {
            var session = Playing(GoalRule.ReachExit);
            session.TogglePause();
            session.SetSwitch(new object(), ChannelColor.Red, true, true);
            Assert.That(session.TryCollectKey(new object(), ChannelColor.Red), Is.False);
            session.TogglePause();
            session.TryReachGoal(true);
            session.SetSwitch(new object(), ChannelColor.Red, true, true);
            Assert.That(session.IsChannelOpen(ChannelColor.Red), Is.False);
        }

        [Test]
        public void FallRespawnIgnoresHazardProtection()
        {
            var session = Playing(GoalRule.ReachExit);
            session.RequestRespawn();
            session.Tick(WorkshopGameSession.RespawnDuration + 0.01f);
            Assert.That(session.IsHazardProtected, Is.True);
            Assert.That(session.RequestRespawn(), Is.False);
            Assert.That(session.RequestRespawn(ignoreProtection: true), Is.True);
        }
    }
}
