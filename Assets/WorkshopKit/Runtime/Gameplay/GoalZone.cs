using UnityEngine;

namespace UnityWorkshop
{
    /// <summary>
    /// The exit. Entering it wins the game once the rule chosen in Game Setup is met; otherwise the
    /// player is told what is missing. Its look changes from locked to open as soon as it can be used.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    [AddComponentMenu("Unity Workshop/Exit Goal")]
    public sealed class GoalZone : WorkshopSceneBehaviour
    {
        [SerializeField, Tooltip("Shown while the exit cannot be used yet.")]
        GameObject m_LockedVisual;
        [SerializeField, Tooltip("Shown once the exit can be used.")]
        GameObject m_OpenVisual;
        [SerializeField, Tooltip("Particles played when the player wins here.")]
        ParticleSystem m_WinBurst;

        BoxCollider m_Trigger;

        public BoxCollider Trigger => m_Trigger != null ? m_Trigger : (m_Trigger = GetComponent<BoxCollider>());

        void Awake()
        {
            m_Trigger = GetComponent<BoxCollider>();
            m_Trigger.isTrigger = true;
        }

        protected override void OnConnect(WorkshopGameContext context)
        {
            context.Session.ProgressChanged += RefreshVisual;
            context.Session.StateChanged += OnStateChanged;
            RefreshVisual();
        }

        protected override void OnDisconnect(WorkshopGameContext context)
        {
            if (context.Session == null)
                return;
            context.Session.ProgressChanged -= RefreshVisual;
            context.Session.StateChanged -= OnStateChanged;
        }

        void OnTriggerEnter(Collider other)
        {
            if (IsPlayer(other))
                Context.Session.TryReachGoal(reportBlocked: true);
        }

        void OnTriggerStay(Collider other)
        {
            if (IsPlayer(other))
                Context.Session.TryReachGoal(reportBlocked: false);
        }

        void OnStateChanged(GameState previous, GameState current)
        {
            if (current == GameState.Won && m_WinBurst != null && Context.Player != null &&
                Trigger.bounds.Contains(Context.Player.transform.position + Vector3.up * 0.5f))
                m_WinBurst.Play(true);
            RefreshVisual();
        }

        void RefreshVisual()
        {
            if (Context == null || Context.Session == null)
                return;
            var open = Context.Session.GoalRequirementsMet;
            if (m_LockedVisual != null)
                m_LockedVisual.SetActive(!open);
            if (m_OpenVisual != null)
                m_OpenVisual.SetActive(open);
        }
    }
}
