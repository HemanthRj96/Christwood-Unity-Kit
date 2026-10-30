using UnityEngine;

namespace UnityWorkshop
{
    /// <summary>
    /// Touch it to make it your respawn point. Hazards and falls then return you here instead of the
    /// Start Point, keeping collected items and time. Restarting the level clears it.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    [AddComponentMenu("Unity Workshop/Checkpoint")]
    public sealed class Checkpoint : WorkshopSceneBehaviour
    {
        [SerializeField, Tooltip("Renderers that change colour when the checkpoint is reached.")]
        Renderer[] m_Flag = new Renderer[0];
        [SerializeField] Material m_InactiveMaterial;
        [SerializeField] Material m_ActiveMaterial;
        [SerializeField] ParticleSystem m_Burst;
        [SerializeField] AudioClip m_Sound;

        BoxCollider m_Trigger;

        public bool IsActive { get; private set; }

        public BoxCollider Trigger => m_Trigger != null ? m_Trigger : (m_Trigger = GetComponent<BoxCollider>());

        /// <summary>Respawn pose for the player root: the marker position, facing its Y rotation only.</summary>
        public Pose RespawnPose => new Pose(
            transform.position + Vector3.up * PlayerSpawn.SpawnLift,
            Quaternion.Euler(0f, transform.eulerAngles.y, 0f));

        void Awake()
        {
            m_Trigger = GetComponent<BoxCollider>();
            m_Trigger.isTrigger = true;
            SetFlagMaterial(m_InactiveMaterial);
        }

        protected override void OnConnect(WorkshopGameContext context) { }

        void OnTriggerEnter(Collider other) => Touch(other);

        void OnTriggerStay(Collider other) => Touch(other);

        void Touch(Collider other)
        {
            if (IsActive || !IsPlayer(other))
                return;
            if (Context.ActivateCheckpoint(this))
            {
                if (m_Burst != null)
                    m_Burst.Play(true);
                Context.PlaySound(m_Sound);
            }
        }

        /// <summary>Called by the Game Setup when this checkpoint becomes or stops being the respawn point.</summary>
        public void ShowActive(bool active)
        {
            IsActive = active;
            SetFlagMaterial(active ? m_ActiveMaterial : m_InactiveMaterial);
        }

        void SetFlagMaterial(Material material)
        {
            if (material == null || m_Flag == null)
                return;
            foreach (var renderer in m_Flag)
                if (renderer != null)
                    renderer.sharedMaterial = material;
        }
    }
}
