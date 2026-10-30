using UnityEngine;

namespace UnityWorkshop
{
    /// <summary>
    /// An item the player picks up by touching it. Every collectible in the game counts toward
    /// "Collect All, Then Exit". It is collected once; respawning keeps it collected; restart brings it back.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SphereCollider))]
    [AddComponentMenu("Unity Workshop/Collectible")]
    public sealed class Collectible : WorkshopSceneBehaviour
    {
        [SerializeField, Tooltip("The model that spins and disappears when collected.")]
        GameObject m_Visual;
        [SerializeField, Tooltip("Particles played when collected.")]
        ParticleSystem m_Burst;
        [SerializeField] AudioClip m_Sound;
        [SerializeField, Range(0f, 360f), Tooltip("Spin speed in degrees per second.")]
        float m_SpinSpeed = 90f;
        [SerializeField, Range(0f, 0.5f), Tooltip("How far the item floats up and down.")]
        float m_BobHeight = 0.15f;

        SphereCollider m_Trigger;
        Vector3 m_VisualRestPosition;
        float m_BobPhase;

        public bool IsCollected { get; private set; }

        public SphereCollider Trigger => m_Trigger != null ? m_Trigger : (m_Trigger = GetComponent<SphereCollider>());

        void Awake()
        {
            m_Trigger = GetComponent<SphereCollider>();
            m_Trigger.isTrigger = true;
            if (m_Visual != null)
                m_VisualRestPosition = m_Visual.transform.localPosition;
            m_BobPhase = (transform.position.x + transform.position.z) * 0.7f;
        }

        protected override void OnConnect(WorkshopGameContext context)
        {
            if (!IsCollected)
                context.Session.RegisterRequired(this);
        }

        protected override void OnDisconnect(WorkshopGameContext context)
        {
            if (context.Session != null)
                context.Session.UnregisterRequired(this);
        }

        void OnTriggerEnter(Collider other) => TryCollect(other);

        void OnTriggerStay(Collider other) => TryCollect(other);

        void TryCollect(Collider other)
        {
            if (IsCollected || !IsPlayer(other) || !Context.Session.TryCollect(this))
                return;

            IsCollected = true;
            m_Trigger.enabled = false;
            if (m_Visual != null)
                m_Visual.SetActive(false);
            if (m_Burst != null)
                m_Burst.Play(true);
            Context.PlaySound(m_Sound);
        }

        void Update()
        {
            if (IsCollected || m_Visual == null)
                return;
            var visual = m_Visual.transform;
            visual.localRotation = Quaternion.Euler(0f, Time.time * m_SpinSpeed, 0f);
            visual.localPosition = m_VisualRestPosition + Vector3.up * (Mathf.Sin(Time.time * 2.5f + m_BobPhase) * m_BobHeight);
        }
    }
}
