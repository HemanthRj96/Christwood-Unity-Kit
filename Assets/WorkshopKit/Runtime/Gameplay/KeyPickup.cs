using UnityEngine;

namespace UnityWorkshop
{
    /// <summary>
    /// Touch it to pick it up. Every door with the same colour and symbol in this game opens and stays open.
    /// Keys do not count toward "Collect All". Restart brings the key back.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SphereCollider))]
    [AddComponentMenu("Unity Workshop/Key")]
    public sealed class KeyPickup : ChannelPiece
    {
        [SerializeField] GameObject m_Visual;
        [SerializeField] ParticleSystem m_Burst;
        [SerializeField] AudioClip m_Sound;

        SphereCollider m_Trigger;
        Vector3 m_RestPosition;

        public bool IsCollected { get; private set; }

        public SphereCollider Trigger => m_Trigger != null ? m_Trigger : (m_Trigger = GetComponent<SphereCollider>());

        protected override void Awake()
        {
            base.Awake();
            m_Trigger = GetComponent<SphereCollider>();
            m_Trigger.isTrigger = true;
            if (m_Visual != null)
                m_RestPosition = m_Visual.transform.localPosition;
        }

        protected override void OnConnect(WorkshopGameContext context) { }

        void OnTriggerEnter(Collider other) => TryCollect(other);

        void OnTriggerStay(Collider other) => TryCollect(other);

        void TryCollect(Collider other)
        {
            if (IsCollected || !IsPlayer(other) || !Context.Session.TryCollectKey(this, Channel))
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
            visual.localRotation = Quaternion.Euler(0f, Time.time * 120f, 0f);
            visual.localPosition = m_RestPosition + Vector3.up * (Mathf.Sin(Time.time * 2.5f) * 0.12f);
        }
    }
}
