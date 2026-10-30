using UnityEngine;

namespace UnityWorkshop
{
    /// <summary>
    /// Base for the single gameplay camera of a scene. The rig root follows the player; the camera is a
    /// child positioned from the rig's settings, so students edit settings rather than camera internals.
    /// </summary>
    [DisallowMultipleComponent]
    public abstract class WorkshopCameraRig : WorkshopSceneBehaviour
    {
        [SerializeField, HideInInspector] Camera m_Camera;

        protected Transform Target { get; private set; }

        public Camera Camera => m_Camera != null ? m_Camera : (m_Camera = GetComponentInChildren<Camera>(true));

        /// <summary>When true the cursor is captured while playing (first-person style look).</summary>
        public virtual bool LocksCursor => false;

        /// <summary>
        /// When the template plays on a single plane (side-scroller), the world Z the player and gameplay
        /// pieces are locked to. Other templates move freely in depth.
        /// </summary>
        public virtual bool TryGetMovementPlane(out float depth)
        {
            depth = 0f;
            return false;
        }

        protected virtual void Awake() => ApplyCameraPose();

        protected override void OnConnect(WorkshopGameContext context) => context.RegisterCameraRig(this);

        protected override void OnDisconnect(WorkshopGameContext context) => context.UnregisterCameraRig(this);

        public void SetTarget(Transform target)
        {
            Target = target;
            SnapToTarget();
        }

        /// <summary>World-space ground directions that W and D should move the player.</summary>
        public abstract void GetMoveBasis(out Vector3 forward, out Vector3 right);

        /// <summary>Jumps straight to the target without smoothing (start, respawn).</summary>
        public abstract void SnapToTarget();

        /// <summary>Positions the child camera from the rig settings. Also used by the editor preview.</summary>
        public abstract void ApplyCameraPose();
    }
}
