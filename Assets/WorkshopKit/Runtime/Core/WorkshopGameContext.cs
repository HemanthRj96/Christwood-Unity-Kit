using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityWorkshop
{
    /// <summary>
    /// The scene's "Game Setup". Exactly one per team scene. Owns the team's rules, the play session,
    /// shared input, and the registrations of the player, camera and gameplay pieces in this scene.
    /// Pieces find it automatically; nothing needs to be dragged into reference slots.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)]
    [RequireComponent(typeof(AudioSource))]
    [AddComponentMenu("Unity Workshop/Game Setup")]
    public sealed class WorkshopGameContext : MonoBehaviour
    {
        static readonly List<WorkshopGameContext> s_Active = new List<WorkshopGameContext>();

        [SerializeField] WorkshopGameRules m_Rules = new WorkshopGameRules();
        [SerializeField, Tooltip("The template this game was created from. Set by the Workshop Builder.")]
        GameTemplateDefinition m_Template;
        [SerializeField, Tooltip("Movement feel. Must be one of the template's approved presets; empty uses the template default.")]
        MovementSettings m_Movement;
        [SerializeField, Tooltip("A movement preset copied for this team only. Overrides Movement Feel when set.")]
        MovementSettings m_CustomMovement;

        [Header("Falling")]
        [SerializeField, Tooltip("Send the player back when they fall below Fall Height.")]
        bool m_UseFallBoundary = true;
        [SerializeField, Tooltip("Height below which the player has fallen out of the level, in metres.")]
        float m_FallHeight = -10f;

        [Header("Feedback Sounds")]
        [SerializeField] AudioClip m_WinSound;
        [SerializeField] AudioClip m_LoseSound;
        [SerializeField] AudioClip m_BlockedSound;
        [SerializeField] AudioClip m_RespawnSound;

        /// <summary>A fall this soon after a respawn counts as "fell again straight away".</summary>
        const float QuickFallWindow = 1.5f;

        /// <summary>Repeated immediate falls before the respawn point is treated as broken.</summary>
        const int MaxQuickFalls = 3;

        readonly List<PlayerSpawn> m_Spawns = new List<PlayerSpawn>();
        AudioSource m_Audio;
        bool m_Begun;
        bool m_Restarting;
        float m_LastRespawnFinished = float.NegativeInfinity;
        int m_QuickFalls;

        public WorkshopGameRules Rules => m_Rules;
        public GameTemplateDefinition Template => m_Template;
        public WorkshopGameSession Session { get; private set; }
        public WorkshopInput Input { get; private set; }
        public WorkshopPlayer Player { get; private set; }
        public WorkshopCameraRig CameraRig { get; private set; }
        public IReadOnlyList<PlayerSpawn> Spawns => m_Spawns;
        public bool UsesFallBoundary => m_UseFallBoundary;
        public float FallHeight => m_FallHeight;

        /// <summary>The checkpoint the player returns to after a hazard, or null for the Start Point.</summary>
        public Checkpoint ActiveCheckpoint { get; private set; }

        /// <summary>Raised when a checkpoint becomes active.</summary>
        public event Action<Checkpoint> CheckpointActivated;

        /// <summary>Player-facing description of a setup problem that stopped the game, or null.</summary>
        public string SetupError { get; private set; }

        /// <summary>Raised when <see cref="SetupError"/> is set.</summary>
        public event Action<string> SetupFailed;

        /// <summary>
        /// The movement preset the player should use: the team's own custom copy, else the chosen approved
        /// preset, else the template default.
        /// </summary>
        public MovementSettings Movement
        {
            get
            {
                if (m_CustomMovement != null)
                    return m_CustomMovement;
                if (m_Movement != null && (m_Template == null || m_Template.AllowsMovement(m_Movement)))
                    return m_Movement;
                return m_Template != null ? m_Template.DefaultMovement : m_Movement;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => s_Active.Clear();

        /// <summary>
        /// Finds the Game Setup that owns <paramref name="requester"/>: a valid serialized reference in the
        /// same scene, an enclosing Game Setup, or the single Game Setup of the requester's scene. Logs a
        /// precise error and returns null when none or several exist.
        /// </summary>
        public static WorkshopGameContext Resolve(Component requester, WorkshopGameContext serialized)
        {
            var scene = requester.gameObject.scene;
            if (serialized != null && serialized.isActiveAndEnabled && serialized.gameObject.scene == scene)
                return serialized;

            var enclosing = requester.GetComponentInParent<WorkshopGameContext>();
            if (enclosing != null && enclosing.isActiveAndEnabled)
                return enclosing;

            WorkshopGameContext found = null;
            var count = 0;
            foreach (var context in s_Active)
            {
                if (context.gameObject.scene != scene)
                    continue;
                found = context;
                count++;
            }

            if (count == 1)
                return found;

            Debug.LogError(count == 0
                    ? $"'{requester.name}' has no Game Setup in scene '{scene.name}', so it is switched off. Open Tools > Unity Workshop > Workshop Builder and run Check."
                    : $"'{requester.name}' found {count} Game Setup objects in scene '{scene.name}' and cannot choose one, so it is switched off. Keep exactly one Game Setup per scene.",
                requester);
            return null;
        }

        void Awake()
        {
            s_Active.Add(this);
            Time.timeScale = 1f;
            m_Audio = GetComponent<AudioSource>();
            m_Audio.playOnAwake = false;
            m_Audio.spatialBlend = 0f;

            Session = new WorkshopGameSession(m_Rules.goalRule, m_Rules.CollectibleLabel, m_Rules.TimeLimit);
            Session.StateChanged += OnStateChanged;
            Session.RespawnStarted += OnRespawnStarted;
            Session.GoalBlocked += OnGoalBlocked;
            Input = new WorkshopInput();
        }

        void OnDestroy()
        {
            s_Active.Remove(this);
            Session?.Shutdown();
            Input?.Dispose();
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        void Update()
        {
            // Registration-ready step: every object loaded with the scene has run Awake/OnEnable/Start
            // before the first Update, so the game begins here regardless of script order.
            if (!m_Begun)
            {
                m_Begun = true;
                BeginPlay();
            }

            if (SetupError != null)
                return;

            if (Input.PausePressed && !Session.IsTerminal)
                Session.TogglePause();

            Session.Tick(Time.deltaTime);

            if (m_UseFallBoundary && Player != null && Session.State == GameState.Playing &&
                Player.transform.position.y < m_FallHeight)
                RequestFallRespawn();
        }

        void BeginPlay()
        {
            string problem = null;
            if (Player == null)
                problem = "There is no Player in this scene.";
            else if (m_Spawns.Count == 0)
                problem = "There is no Start Point in this scene.";
            else if (m_Spawns.Count > 1)
                problem = $"There are {m_Spawns.Count} Start Points. Keep exactly one.";
            else if (CameraRig == null)
                problem = "There is no gameplay camera in this scene.";
            else if (m_Rules.goalRule == GoalRule.CollectAllThenExit && Session.RequiredCount == 0)
                problem = "The goal is Collect All, Then Exit, but there is nothing to collect.";
            else if (m_UseFallBoundary && m_Spawns[0].SpawnPose.position.y < m_FallHeight)
                problem = "The Start Point is below the Fall Height, so the player would fall forever.";

            if (problem != null)
            {
                Fail(problem);
                return;
            }

            Player.PlaceAt(m_Spawns[0].SpawnPose);
            CameraRig.SnapToTarget();
            Session.Begin();
        }

        /// <summary>Stops the game with a player-facing setup problem instead of guessing.</summary>
        void Fail(string problem)
        {
            if (SetupError != null)
                return;
            SetupError = problem + " Open Tools > Unity Workshop > Workshop Builder and run Check.";
            Debug.LogError($"Game stopped: {SetupError}", this);
            SetupFailed?.Invoke(SetupError);
            ApplyCursor();
        }

        /// <summary>The start pose, used when no checkpoint is active.</summary>
        public Pose StartPose => m_Spawns.Count > 0 ? m_Spawns[0].SpawnPose : new Pose(transform.position, Quaternion.identity);

        /// <summary>Where the player returns after a hazard or fall: the active checkpoint, else the Start Point.</summary>
        public Pose RespawnPose => ActiveCheckpoint != null ? ActiveCheckpoint.RespawnPose : StartPose;

        /// <summary>Makes <paramref name="checkpoint"/> the respawn point. Only while playing; full restart clears it.</summary>
        public bool ActivateCheckpoint(Checkpoint checkpoint)
        {
            if (checkpoint == null || checkpoint == ActiveCheckpoint || Session.State != GameState.Playing)
                return false;
            if (m_UseFallBoundary && checkpoint.RespawnPose.position.y < m_FallHeight)
            {
                Debug.LogWarning($"Checkpoint '{checkpoint.name}' is below the Fall Height and is ignored.", checkpoint);
                return false;
            }
            var previous = ActiveCheckpoint;
            ActiveCheckpoint = checkpoint;
            if (previous != null)
                previous.ShowActive(false);
            checkpoint.ShowActive(true);
            CheckpointActivated?.Invoke(checkpoint);
            return true;
        }

        /// <summary>
        /// Returns a falling player to the respawn point, even during hazard protection. If the player keeps
        /// falling straight after respawning, a checkpoint is abandoned for the Start Point; if the Start Point
        /// itself keeps failing, the game stops with an explanation instead of looping forever.
        /// </summary>
        public void RequestFallRespawn()
        {
            if (SetupError != null || Session.State != GameState.Playing)
                return;

            m_QuickFalls = Time.time - m_LastRespawnFinished < QuickFallWindow ? m_QuickFalls + 1 : 1;
            if (m_QuickFalls >= MaxQuickFalls)
            {
                if (ActiveCheckpoint == null)
                {
                    Fail("The player keeps falling as soon as they appear at the Start Point. Move it onto solid floor.");
                    return;
                }
                Debug.LogWarning($"The player keeps falling after respawning at checkpoint '{ActiveCheckpoint.name}', so the Start Point is used instead.", ActiveCheckpoint);
                ActiveCheckpoint.ShowActive(false);
                ActiveCheckpoint = null;
                m_QuickFalls = 0;
            }
            Session.RequestRespawn(ignoreProtection: true);
        }

        public void TogglePause()
        {
            if (SetupError == null && !Session.IsTerminal)
                Session.TogglePause();
        }

        /// <summary>Full restart: reloads the saved scene so every object starts from its authored state.</summary>
        public void RestartGame()
        {
            if (m_Restarting)
                return;
            m_Restarting = true;
            Time.timeScale = 1f;
            Session.Shutdown();
            if (!WorkshopSceneLoader.Reload(gameObject.scene))
                m_Restarting = false;
        }

        public void PlaySound(AudioClip clip)
        {
            if (clip != null && m_Audio != null)
                m_Audio.PlayOneShot(clip);
        }

        // ---- Registration ------------------------------------------------------------------------

        public void RegisterPlayer(WorkshopPlayer player)
        {
            if (Player != null && Player != player)
            {
                Debug.LogError($"'{player.name}' is a second Player in this scene and is ignored. Keep exactly one Player.", player);
                return;
            }
            Player = player;
            if (CameraRig != null)
                CameraRig.SetTarget(player.transform);
        }

        public void UnregisterPlayer(WorkshopPlayer player)
        {
            if (Player == player)
                Player = null;
        }

        public void RegisterCameraRig(WorkshopCameraRig rig)
        {
            if (CameraRig != null && CameraRig != rig)
            {
                Debug.LogError($"'{rig.name}' is a second gameplay camera in this scene and is ignored. Keep exactly one.", rig);
                return;
            }
            CameraRig = rig;
            if (Player != null)
                rig.SetTarget(Player.transform);
        }

        public void UnregisterCameraRig(WorkshopCameraRig rig)
        {
            if (CameraRig == rig)
                CameraRig = null;
        }

        public void RegisterSpawn(PlayerSpawn spawn)
        {
            if (!m_Spawns.Contains(spawn))
                m_Spawns.Add(spawn);
        }

        public void UnregisterSpawn(PlayerSpawn spawn) => m_Spawns.Remove(spawn);

        // ---- Session reactions -------------------------------------------------------------------

        void OnStateChanged(GameState previous, GameState current)
        {
            Time.timeScale = current == GameState.Paused ? 0f : 1f;
            ApplyCursor();

            if (previous == GameState.Respawning && current == GameState.Playing)
                m_LastRespawnFinished = Time.time;

            if (current == GameState.Won)
                PlaySound(m_WinSound);
            else if (current == GameState.Lost)
                PlaySound(m_LoseSound);
        }

        /// <summary>
        /// True while the mouse should be captured: a cursor-locking camera (first-person) during play.
        /// Pause, results, setup problems and lost focus all release it so menus can be clicked.
        /// </summary>
        public bool CursorShouldLock =>
            CameraRig != null && CameraRig.LocksCursor && SetupError == null && Session != null &&
            (Session.State == GameState.Playing || Session.State == GameState.Respawning);

        void ApplyCursor()
        {
            var locked = CursorShouldLock;
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        void OnApplicationFocus(bool focused) => HandleFocusChanged(focused);

        /// <summary>
        /// Losing focus (Alt+Tab, clicking another window) pauses a cursor-locking game so the mouse is freed;
        /// the player resumes from the pause menu, which captures the mouse again.
        /// </summary>
        public void HandleFocusChanged(bool focused)
        {
            if (!focused && CursorShouldLock)
                Session.TogglePause();
            else
                ApplyCursor();
        }

        void OnRespawnStarted()
        {
            PlaySound(m_RespawnSound);
            if (Player != null)
                Player.PlaceAt(RespawnPose);
            if (CameraRig != null)
                CameraRig.SnapToTarget();
        }

        void OnGoalBlocked(string message) => PlaySound(m_BlockedSound);
    }
}
