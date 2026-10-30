using UnityEngine;

namespace UnityWorkshop
{
    /// <summary>
    /// A door that opens for keys and switches with the same colour and symbol. Opening removes its
    /// collision immediately; closing waits until nobody is standing in the doorway, so the player can
    /// never be trapped inside it. The panel slides as a visual only.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    [AddComponentMenu("Unity Workshop/Door")]
    public sealed class Door : ChannelPiece
    {
        const float SlideSpeed = 2.5f;
        static readonly Collider[] s_Overlaps = new Collider[16];

        [SerializeField, Tooltip("The moving part of the door. It slides into the floor when open.")]
        Transform m_Panel;
        [SerializeField, Tooltip("How far the panel slides down when open, in metres.")]
        float m_SlideDistance = 1.9f;
        [SerializeField] AudioClip m_Sound;

        BoxCollider m_Blocker;
        Vector3 m_PanelRest;
        bool m_WantOpen;
        float m_OpenAmount;

        public bool IsOpen { get; private set; }

        /// <summary>True while the door wants to close but the doorway is occupied.</summary>
        public bool IsWaitingToClose => !m_WantOpen && IsOpen;

        public BoxCollider Blocker => m_Blocker != null ? m_Blocker : (m_Blocker = GetComponent<BoxCollider>());

        protected override void Awake()
        {
            base.Awake();
            m_Blocker = GetComponent<BoxCollider>();
            m_Blocker.isTrigger = false;
            if (m_Panel != null)
                m_PanelRest = m_Panel.localPosition;
        }

        protected override void OnConnect(WorkshopGameContext context)
        {
            context.Session.ChannelChanged += OnChannelChanged;
            m_WantOpen = context.Session.IsChannelOpen(Channel);
        }

        protected override void OnDisconnect(WorkshopGameContext context)
        {
            if (context.Session != null)
                context.Session.ChannelChanged -= OnChannelChanged;
        }

        void OnChannelChanged(ChannelColor channel)
        {
            if (channel == Channel)
                m_WantOpen = Context.Session.IsChannelOpen(channel);
        }

        void Update()
        {
            if (m_WantOpen && !IsOpen)
            {
                IsOpen = true;
                m_Blocker.enabled = false;
                Context.PlaySound(m_Sound);
            }
            else if (!m_WantOpen && IsOpen && !DoorwayOccupied())
            {
                IsOpen = false;
                m_Blocker.enabled = true;
            }

            var target = IsOpen ? 1f : 0f;
            if (!Mathf.Approximately(m_OpenAmount, target))
            {
                m_OpenAmount = Mathf.MoveTowards(m_OpenAmount, target, SlideSpeed * Time.deltaTime);
                if (m_Panel != null)
                    m_Panel.localPosition = m_PanelRest + Vector3.down * (m_SlideDistance * m_OpenAmount);
            }
        }

        /// <summary>Whether the player overlaps the closed door's volume (checked even while its collider is off).</summary>
        bool DoorwayOccupied()
        {
            if (Context == null || Context.Player == null)
                return false;
            var box = Blocker;
            var center = transform.TransformPoint(box.center);
            var halfExtents = Vector3.Scale(box.size, transform.lossyScale) * 0.5f + Vector3.one * 0.05f;
            var count = Physics.OverlapBoxNonAlloc(center, halfExtents, s_Overlaps, transform.rotation, ~0, QueryTriggerInteraction.Ignore);
            var playerCollider = Context.Player.Motor.Controller;
            for (var i = 0; i < count; i++)
                if (s_Overlaps[i] == playerCollider)
                    return true;
            return false;
        }
    }
}
