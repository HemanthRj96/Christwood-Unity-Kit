using System;
using System.Collections.Generic;

namespace UnityWorkshop
{
    public enum GameState
    {
        Ready,
        Playing,
        Paused,
        Respawning,
        Won,
        Lost,
    }

    public enum LossReason
    {
        None,
        TimeUp,
    }

    /// <summary>
    /// Authoritative rules state for one play session: state machine, clock and time limit, collection
    /// progress, key/switch channels and respawn flow. Plain C# so the rules can be tested without a scene.
    /// Owned and ticked by <see cref="WorkshopGameContext"/>; it is discarded when the scene reloads.
    /// </summary>
    public sealed class WorkshopGameSession
    {
        /// <summary>Seconds the player is held at the respawn point before control returns.</summary>
        public const float RespawnDuration = 0.4f;

        /// <summary>Seconds after a respawn during which hazards are ignored.</summary>
        public const float HazardProtectionDuration = 1.0f;

        readonly GoalRule m_GoalRule;
        readonly HashSet<object> m_Required = new HashSet<object>();
        readonly HashSet<object> m_Collected = new HashSet<object>();
        readonly HashSet<object> m_CollectedKeys = new HashSet<object>();
        readonly bool[] m_HasKey = new bool[Channels.Count];
        readonly bool[] m_Latched = new bool[Channels.Count];
        readonly HashSet<object>[] m_Pressed = new HashSet<object>[Channels.Count];

        GameState m_StateBeforePause;
        float m_RespawnRemaining;
        float m_ProtectionRemaining;

        /// <summary>Raised with (previous, current) after every state change.</summary>
        public event Action<GameState, GameState> StateChanged;

        /// <summary>Raised when collected or required counts change.</summary>
        public event Action ProgressChanged;

        /// <summary>Raised with a player-facing message when the goal is touched before its requirements are met.</summary>
        public event Action<string> GoalBlocked;

        /// <summary>Raised when a respawn begins; the context moves the player.</summary>
        public event Action RespawnStarted;

        /// <summary>Raised when a channel's key, latch or switch state changes.</summary>
        public event Action<ChannelColor> ChannelChanged;

        public WorkshopGameSession(GoalRule goalRule, string collectibleLabel = "Item", float timeLimit = 0f)
        {
            m_GoalRule = goalRule;
            CollectibleLabel = string.IsNullOrWhiteSpace(collectibleLabel) ? "Item" : collectibleLabel;
            TimeLimit = Math.Max(0f, timeLimit);
            for (var i = 0; i < Channels.Count; i++)
                m_Pressed[i] = new HashSet<object>();
        }

        public GoalRule GoalRule => m_GoalRule;
        public string CollectibleLabel { get; }
        public GameState State { get; private set; } = GameState.Ready;
        public LossReason LossReason { get; private set; }
        public float ElapsedTime { get; private set; }
        public float TimeLimit { get; }
        public bool HasTimeLimit => TimeLimit > 0f;
        public float TimeRemaining => HasTimeLimit ? Math.Max(0f, TimeLimit - ElapsedTime) : 0f;
        public bool IsTimeUp => HasTimeLimit && ElapsedTime >= TimeLimit;
        public int CollectedCount => m_Collected.Count;
        public int RequiredCount => m_Required.Count;
        public int RemainingCount => m_Required.Count - m_Collected.Count;
        public bool IsTerminal => State == GameState.Won || State == GameState.Lost;
        public bool IsHazardProtected => m_ProtectionRemaining > 0f;
        public bool IsShutDown { get; private set; }

        /// <summary>True while the player should be able to move and act.</summary>
        public bool AcceptsPlayerInput => State == GameState.Playing;

        public bool GoalRequirementsMet => m_GoalRule == GoalRule.ReachExit || RemainingCount <= 0;

        bool InPlay => !IsShutDown && (State == GameState.Playing || State == GameState.Respawning);

        // ---- Collection ----------------------------------------------------------------------------

        public void RegisterRequired(object item)
        {
            if (item == null || IsShutDown || IsTerminal)
                return;
            if (m_Required.Add(item))
                ProgressChanged?.Invoke();
        }

        public void UnregisterRequired(object item)
        {
            if (item == null || IsShutDown || IsTerminal)
                return;
            var removed = m_Required.Remove(item);
            removed |= m_Collected.Remove(item);
            if (removed)
                ProgressChanged?.Invoke();
        }

        public bool IsCollected(object item) => item != null && m_Collected.Contains(item);

        /// <summary>Counts a required item once. Returns true only for the call that collected it.</summary>
        public bool TryCollect(object item)
        {
            if (item == null || !InPlay)
                return false;
            if (!m_Required.Contains(item) || !m_Collected.Add(item))
                return false;
            ProgressChanged?.Invoke();
            return true;
        }

        // ---- Channels ------------------------------------------------------------------------------

        public bool HasKey(ChannelColor channel) => m_HasKey[(int)channel];

        /// <summary>A channel's doors are open while its key is held, a switch latched it, or any switch is pressed.</summary>
        public bool IsChannelOpen(ChannelColor channel)
        {
            var i = (int)channel;
            return m_HasKey[i] || m_Latched[i] || m_Pressed[i].Count > 0;
        }

        /// <summary>Picks up a key once. Returns true only for the call that collected it.</summary>
        public bool TryCollectKey(object key, ChannelColor channel)
        {
            if (key == null || !InPlay || !m_CollectedKeys.Add(key))
                return false;
            if (m_HasKey[(int)channel])
                return true;
            m_HasKey[(int)channel] = true;
            ChannelChanged?.Invoke(channel);
            return true;
        }

        /// <summary>
        /// Reports whether a switch is pressed. Several switches on one channel combine with OR.
        /// A latching switch keeps its channel open after the first press.
        /// </summary>
        public void SetSwitch(object switchObject, ChannelColor channel, bool pressed, bool latch)
        {
            if (switchObject == null || !InPlay)
                return;
            var i = (int)channel;
            var wasOpen = IsChannelOpen(channel);
            var changed = pressed ? m_Pressed[i].Add(switchObject) : m_Pressed[i].Remove(switchObject);
            if (pressed && latch && !m_Latched[i])
            {
                m_Latched[i] = true;
                changed = true;
            }
            if (changed && wasOpen != IsChannelOpen(channel))
                ChannelChanged?.Invoke(channel);
        }

        // ---- Flow ----------------------------------------------------------------------------------

        /// <summary>Moves Ready to Playing. Called once by the context after every object has registered.</summary>
        public bool Begin()
        {
            if (State != GameState.Ready || IsShutDown)
                return false;
            SetState(GameState.Playing);
            return true;
        }

        /// <summary>Advances the session clock and timers. Pass scaled delta time.</summary>
        public void Tick(float deltaTime)
        {
            if (IsShutDown || deltaTime <= 0f || !InPlay)
                return;

            ElapsedTime += deltaTime;
            if (HasTimeLimit && ElapsedTime >= TimeLimit)
            {
                ElapsedTime = TimeLimit;
                Lose(LossReason.TimeUp);
                return;
            }

            if (State == GameState.Playing)
            {
                if (m_ProtectionRemaining > 0f)
                    m_ProtectionRemaining = Math.Max(0f, m_ProtectionRemaining - deltaTime);
            }
            else
            {
                m_RespawnRemaining -= deltaTime;
                if (m_RespawnRemaining <= 0f)
                {
                    m_ProtectionRemaining = HazardProtectionDuration;
                    SetState(GameState.Playing);
                }
            }
        }

        /// <summary>
        /// Evaluates the goal. Wins at most once. An expired timer takes precedence over reaching the goal.
        /// When requirements are unmet and <paramref name="reportBlocked"/> is true, raises <see cref="GoalBlocked"/>.
        /// </summary>
        public bool TryReachGoal(bool reportBlocked)
        {
            if (State != GameState.Playing || IsShutDown)
                return false;

            if (IsTimeUp)
            {
                Lose(LossReason.TimeUp);
                return false;
            }

            if (!GoalRequirementsMet)
            {
                if (reportBlocked)
                {
                    var remaining = RemainingCount;
                    GoalBlocked?.Invoke(remaining == 1
                        ? $"Collect 1 more {CollectibleLabel} to unlock the exit."
                        : $"Collect {remaining} more {CollectibleLabel}s to unlock the exit.");
                }
                return false;
            }

            SetState(GameState.Won);
            return true;
        }

        /// <summary>Ends the session as a loss. Ignored once terminal.</summary>
        public bool Lose(LossReason reason = LossReason.None)
        {
            if (IsShutDown || IsTerminal || State == GameState.Ready)
                return false;
            LossReason = reason;
            SetState(GameState.Lost);
            return true;
        }

        /// <summary>
        /// Starts a respawn. Rejected while respawning, paused or finished, and during hazard protection
        /// unless <paramref name="ignoreProtection"/> (used for falling out of the level).
        /// </summary>
        public bool RequestRespawn(bool ignoreProtection = false)
        {
            if (State != GameState.Playing || IsShutDown || (IsHazardProtected && !ignoreProtection))
                return false;
            m_RespawnRemaining = RespawnDuration;
            SetState(GameState.Respawning);
            RespawnStarted?.Invoke();
            return true;
        }

        /// <summary>Pauses from Playing/Respawning or resumes the state that was paused.</summary>
        public bool TogglePause()
        {
            if (IsShutDown)
                return false;
            if (State == GameState.Paused)
            {
                SetState(m_StateBeforePause);
                return true;
            }
            if (State == GameState.Playing || State == GameState.Respawning)
            {
                m_StateBeforePause = State;
                SetState(GameState.Paused);
                return true;
            }
            return false;
        }

        /// <summary>Stops all further events. Called when the owning scene object is destroyed.</summary>
        public void Shutdown()
        {
            IsShutDown = true;
            StateChanged = null;
            ProgressChanged = null;
            GoalBlocked = null;
            RespawnStarted = null;
            ChannelChanged = null;
        }

        void SetState(GameState next)
        {
            if (next == State)
                return;
            var previous = State;
            State = next;
            StateChanged?.Invoke(previous, next);
        }
    }
}
