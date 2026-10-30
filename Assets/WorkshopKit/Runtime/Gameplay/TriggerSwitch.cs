using UnityEngine;

namespace UnityWorkshop
{
    /// <summary>
    /// A pressure plate. Standing on it opens every door with the same colour and symbol; stepping off
    /// closes them again unless Latch is on. If several switches share a channel, the doors are open
    /// while any one of them is pressed.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    [AddComponentMenu("Unity Workshop/Pressure Switch")]
    public sealed class TriggerSwitch : ChannelPiece
    {
        public const string LatchPropertyName = "m_Latch";

        /// <summary>How far the plate sinks when pressed, in metres.</summary>
        const float PressDepth = 0.06f;

        [SerializeField, Tooltip("Stay pressed after the first step, so doors stay open.")]
        bool m_Latch;
        [SerializeField] Transform m_Plate;
        [SerializeField] AudioClip m_Sound;

        BoxCollider m_Trigger;
        Vector3 m_PlateRest;
        float m_LastTouched = float.NegativeInfinity;

        public bool Latch => m_Latch;
        public bool IsPressed { get; private set; }

        public BoxCollider Trigger => m_Trigger != null ? m_Trigger : (m_Trigger = GetComponent<BoxCollider>());

        protected override void Awake()
        {
            base.Awake();
            m_Trigger = GetComponent<BoxCollider>();
            m_Trigger.isTrigger = true;
            if (m_Plate != null)
                m_PlateRest = m_Plate.localPosition;
        }

        /// <summary>Test hook for building switches at runtime.</summary>
        internal void Configure(bool latch) => m_Latch = latch;

        protected override void OnConnect(WorkshopGameContext context) { }

        protected override void OnDisable()
        {
            if (IsPressed && Context != null && Context.Session != null)
                Context.Session.SetSwitch(this, Channel, false, m_Latch);
            IsPressed = false;
            base.OnDisable();
        }

        void OnTriggerEnter(Collider other) => Touch(other);

        void OnTriggerStay(Collider other) => Touch(other);

        void Touch(Collider other)
        {
            if (IsPlayer(other))
                m_LastTouched = Time.fixedTime;
        }

        // Pressed while the player was overlapping during the previous physics step. Using the last
        // contact time (rather than enter/exit pairs) stays correct when the player is teleported away.
        void FixedUpdate()
        {
            if (Context == null || Context.Session == null)
                return;
            var pressed = Time.fixedTime - m_LastTouched <= Time.fixedDeltaTime * 1.5f;
            if (pressed == IsPressed)
                return;
            IsPressed = pressed;
            Context.Session.SetSwitch(this, Channel, pressed, m_Latch);
            if (m_Plate != null)
                m_Plate.localPosition = m_PlateRest + (pressed || m_Latch && Context.Session.IsChannelOpen(Channel) ? Vector3.down * PressDepth : Vector3.zero);
            if (pressed)
                Context.PlaySound(m_Sound);
        }
    }
}
