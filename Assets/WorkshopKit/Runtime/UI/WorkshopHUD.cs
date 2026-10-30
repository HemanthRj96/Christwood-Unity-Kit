using UnityEngine;
using UnityEngine.UI;

namespace UnityWorkshop
{
    /// <summary>
    /// On-screen game interface: title, objective, controls, progress, goal messages, pause and
    /// win/loss panels. Updates from session events; buttons keep working while gameplay input is off.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Unity Workshop/Workshop HUD")]
    public sealed class WorkshopHUD : WorkshopSceneBehaviour
    {
        const float MessageDuration = 2.5f;

        [Header("Always Visible")]
        [SerializeField] Text m_Title;
        [SerializeField] Text m_Objective;
        [SerializeField] Text m_Controls;
        [SerializeField] GameObject m_ProgressGroup;
        [SerializeField] Text m_Progress;
        [SerializeField] GameObject m_MessagePanel;
        [SerializeField] Text m_Message;
        [SerializeField] GameObject m_TimerGroup;
        [SerializeField] Text m_Timer;
        [SerializeField] GameObject m_KeysGroup;
        [SerializeField] Text m_Keys;

        [Header("Pause")]
        [SerializeField] GameObject m_PausePanel;
        [SerializeField] Button m_ResumeButton;
        [SerializeField] Button m_PauseRestartButton;

        [Header("Results")]
        [SerializeField] GameObject m_ResultPanel;
        [SerializeField] Text m_ResultTitle;
        [SerializeField] Text m_ResultDetail;
        [SerializeField] Button m_ResultRestartButton;

        [Header("Setup Problems")]
        [SerializeField] GameObject m_ErrorPanel;
        [SerializeField] Text m_ErrorText;

        /// <summary>Remaining seconds at which the countdown turns red.</summary>
        const int TimerWarningSeconds = 10;

        static readonly Color TimerNormal = new Color(0.97f, 0.97f, 0.98f);
        static readonly Color TimerWarning = new Color(1f, 0.35f, 0.3f);

        readonly System.Text.StringBuilder m_KeysText = new System.Text.StringBuilder();
        float m_MessageRemaining;
        int m_ShownSeconds = -1;

        void Awake()
        {
            SetActive(m_PausePanel, false);
            SetActive(m_ResultPanel, false);
            SetActive(m_ErrorPanel, false);
            SetActive(m_MessagePanel, false);
            SetActive(m_TimerGroup, false);
            SetActive(m_KeysGroup, false);
        }

        protected override void OnConnect(WorkshopGameContext context)
        {
            if (m_ResumeButton != null)
                m_ResumeButton.onClick.AddListener(context.TogglePause);
            if (m_PauseRestartButton != null)
                m_PauseRestartButton.onClick.AddListener(context.RestartGame);
            if (m_ResultRestartButton != null)
                m_ResultRestartButton.onClick.AddListener(context.RestartGame);

            context.Session.StateChanged += OnStateChanged;
            context.Session.ProgressChanged += RefreshProgress;
            context.Session.GoalBlocked += ShowMessage;
            context.Session.ChannelChanged += RefreshKeys;
            context.SetupFailed += ShowSetupError;
            context.CheckpointActivated += OnCheckpoint;

            var rules = context.Rules;
            SetText(m_Title, rules.Title);
            SetText(m_Objective, rules.Objective);
            var controls = context.Template != null ? context.Template.Controls : "";
            SetText(m_Controls, string.IsNullOrEmpty(controls) ? "Esc: Pause" : controls.TrimEnd() + "\nEsc: Pause");
            SetActive(m_TimerGroup, context.Session.HasTimeLimit);
            RefreshTimer();
            RefreshProgress();
            if (context.SetupError != null)
                ShowSetupError(context.SetupError);
        }

        protected override void OnDisconnect(WorkshopGameContext context)
        {
            if (m_ResumeButton != null)
                m_ResumeButton.onClick.RemoveListener(context.TogglePause);
            if (m_PauseRestartButton != null)
                m_PauseRestartButton.onClick.RemoveListener(context.RestartGame);
            if (m_ResultRestartButton != null)
                m_ResultRestartButton.onClick.RemoveListener(context.RestartGame);

            context.SetupFailed -= ShowSetupError;
            context.CheckpointActivated -= OnCheckpoint;
            if (context.Session == null)
                return;
            context.Session.StateChanged -= OnStateChanged;
            context.Session.ProgressChanged -= RefreshProgress;
            context.Session.GoalBlocked -= ShowMessage;
            context.Session.ChannelChanged -= RefreshKeys;
        }

        void Update()
        {
            RefreshTimer();
            if (m_MessageRemaining <= 0f)
                return;
            m_MessageRemaining -= Time.unscaledDeltaTime;
            if (m_MessageRemaining <= 0f)
                SetActive(m_MessagePanel, false);
        }

        /// <summary>Shows whole seconds left; only rebuilds the text when the number changes.</summary>
        void RefreshTimer()
        {
            if (m_Timer == null || Context == null || Context.Session == null || !Context.Session.HasTimeLimit)
                return;
            var seconds = Mathf.CeilToInt(Context.Session.TimeRemaining);
            if (seconds == m_ShownSeconds)
                return;
            m_ShownSeconds = seconds;
            m_Timer.text = "Time left: " + FormatTime(seconds);
            m_Timer.color = seconds <= TimerWarningSeconds ? TimerWarning : TimerNormal;
        }

        void RefreshKeys(ChannelColor changed)
        {
            var session = Context.Session;
            m_KeysText.Clear();
            for (var i = 0; i < Channels.Count; i++)
            {
                var channel = (ChannelColor)i;
                if (!session.HasKey(channel))
                    continue;
                if (m_KeysText.Length > 0)
                    m_KeysText.Append("\n");
                m_KeysText.Append("<color=#").Append(ColorUtility.ToHtmlStringRGB(KeyColor(channel))).Append('>')
                    .Append(Channels.Label(channel)).Append(" Key</color>");
            }
            SetActive(m_KeysGroup, m_KeysText.Length > 0);
            SetText(m_Keys, m_KeysText.ToString());
        }

        static Color KeyColor(ChannelColor channel)
        {
            switch (channel)
            {
                case ChannelColor.Red: return new Color(1f, 0.45f, 0.45f);
                case ChannelColor.Blue: return new Color(0.5f, 0.7f, 1f);
                case ChannelColor.Green: return new Color(0.45f, 0.95f, 0.55f);
                default: return new Color(1f, 0.88f, 0.35f);
            }
        }

        void OnCheckpoint(Checkpoint checkpoint) => ShowMessage("Checkpoint reached! You will return here.");

        void OnStateChanged(GameState previous, GameState current)
        {
            SetActive(m_PausePanel, current == GameState.Paused);

            var finished = current == GameState.Won || current == GameState.Lost;
            SetActive(m_ResultPanel, finished);
            if (!finished)
                return;

            m_MessageRemaining = 0f;
            SetActive(m_MessagePanel, false);
            var session = Context.Session;
            SetText(m_ResultTitle, current == GameState.Won ? "You Win!"
                : session.LossReason == LossReason.TimeUp ? "Time's Up!" : "Game Over");
            var detail = $"Time: {FormatTime(session.ElapsedTime)}";
            if (session.RequiredCount > 0)
                detail += $"\n{session.CollectibleLabel}s: {session.CollectedCount} / {session.RequiredCount}";
            SetText(m_ResultDetail, detail);
        }

        void RefreshProgress()
        {
            if (Context == null || Context.Session == null)
                return;
            var session = Context.Session;
            SetActive(m_ProgressGroup, session.RequiredCount > 0);
            SetText(m_Progress, $"{session.CollectibleLabel}s: {session.CollectedCount} / {session.RequiredCount}");
        }

        void ShowMessage(string message)
        {
            SetText(m_Message, message);
            SetActive(m_MessagePanel, true);
            m_MessageRemaining = MessageDuration;
        }

        void ShowSetupError(string message)
        {
            SetText(m_ErrorText, message);
            SetActive(m_ErrorPanel, true);
        }

        public static string FormatTime(float seconds)
        {
            var total = Mathf.Max(0, Mathf.FloorToInt(seconds));
            return $"{total / 60}:{total % 60:00}";
        }

        static void SetText(Text text, string value)
        {
            if (text != null)
                text.text = value;
        }

        static void SetActive(GameObject target, bool active)
        {
            if (target != null && target.activeSelf != active)
                target.SetActive(active);
        }
    }
}
