using UnityEngine;

namespace UnityWorkshop
{
    /// <summary>
    /// Base for scene pieces that belong to a Game Setup. Resolves the owning context once, registers
    /// on enable and unregisters on disable. If no unambiguous Game Setup exists the piece reports
    /// the problem and switches itself off instead of guessing.
    /// </summary>
    public abstract class WorkshopSceneBehaviour : MonoBehaviour
    {
        /// <summary>Serialized property name, used by editor tools that pre-assign the context.</summary>
        public const string ContextPropertyName = "m_Context";

        [SerializeField, HideInInspector] WorkshopGameContext m_Context;

        bool m_Started;
        bool m_Connected;

        protected WorkshopGameContext Context { get; private set; }

        protected virtual void Start()
        {
            m_Started = true;
            Connect();
        }

        protected virtual void OnEnable()
        {
            if (m_Started)
                Connect();
        }

        protected virtual void OnDisable()
        {
            if (!m_Connected)
                return;
            m_Connected = false;
            if (Context != null)
                OnDisconnect(Context);
        }

        void Connect()
        {
            if (m_Connected)
                return;
            if (Context == null)
            {
                Context = WorkshopGameContext.Resolve(this, m_Context);
                if (Context == null)
                {
                    enabled = false;
                    return;
                }
            }
            m_Connected = true;
            OnConnect(Context);
        }

        /// <summary>Called when the piece becomes active with a resolved context.</summary>
        protected abstract void OnConnect(WorkshopGameContext context);

        /// <summary>Called when the piece is disabled or destroyed while connected.</summary>
        protected virtual void OnDisconnect(WorkshopGameContext context) { }

        /// <summary>True when <paramref name="other"/> is this scene's registered player.</summary>
        protected bool IsPlayer(Collider other)
        {
            if (Context == null || Context.Player == null)
                return false;
            return other.TryGetComponent(out WorkshopPlayer player) && player == Context.Player;
        }
    }
}
