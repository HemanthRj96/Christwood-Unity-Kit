using UnityEngine;

namespace UnityWorkshop
{
    public enum LookSensitivity
    {
        Low,
        Medium,
        High,
    }

    /// <summary>
    /// Sees through the player's eyes. The mouse turns the view; looking up and down stops before
    /// straight up or down, so the view never flips. The cursor is captured while playing and released
    /// whenever the game pauses, ends or loses focus. Respawning faces the start or checkpoint direction.
    /// </summary>
    [AddComponentMenu("Unity Workshop/First-Person Camera")]
    public sealed class FirstPersonCameraRig : WorkshopCameraRig
    {
        public const float EyeHeight = 1.6f;
        public const float MaxPitch = 80f;

        /// <summary>Largest mouse movement used in one frame, so a hitch or refocus cannot spin the view.</summary>
        const float MaxLookDeltaPerFrame = 250f;

        /// <summary>Frames of mouse input ignored after control returns, to drop the jump the OS reports on recapture.</summary>
        const int LookSettleFrames = 2;

        [SerializeField, Tooltip("How far the view turns when the mouse moves.")]
        LookSensitivity m_Sensitivity = LookSensitivity.Medium;
        [SerializeField, Range(60f, 90f), Tooltip("How wide the view is, in degrees. Wider shows more but looks more stretched.")]
        float m_FieldOfView = 70f;

        float m_Yaw;
        float m_Pitch;
        int m_IgnoreLookFrames;

        public LookSensitivity Sensitivity => m_Sensitivity;
        public float FieldOfView => m_FieldOfView;
        public float Yaw => m_Yaw;
        public float Pitch => m_Pitch;
        public override bool LocksCursor => true;

        /// <summary>Degrees of turn per pixel of mouse movement.</summary>
        public static float DegreesPerPixel(LookSensitivity sensitivity) =>
            sensitivity == LookSensitivity.Low ? 0.06f : sensitivity == LookSensitivity.High ? 0.2f : 0.12f;

        protected override void Awake()
        {
            m_Yaw = transform.eulerAngles.y;
            m_Pitch = 0f;
            base.Awake();
        }

        protected override void OnConnect(WorkshopGameContext context)
        {
            base.OnConnect(context);
            context.Session.StateChanged += OnStateChanged;
        }

        protected override void OnDisconnect(WorkshopGameContext context)
        {
            if (context.Session != null)
                context.Session.StateChanged -= OnStateChanged;
            base.OnDisconnect(context);
        }

        void OnStateChanged(GameState previous, GameState current)
        {
            if (current == GameState.Playing)
                m_IgnoreLookFrames = LookSettleFrames;
        }

        public override void GetMoveBasis(out Vector3 forward, out Vector3 right)
        {
            var yaw = Quaternion.Euler(0f, m_Yaw, 0f);
            forward = yaw * Vector3.forward;
            right = yaw * Vector3.right;
        }

        /// <summary>Sets the child camera's tilt and field of view. The rig root's rotation holds the turn (yaw).</summary>
        public override void ApplyCameraPose()
        {
            var cam = Camera;
            if (cam == null)
                return;
            cam.transform.localPosition = Vector3.zero;
            cam.transform.localRotation = Quaternion.Euler(m_Pitch, 0f, 0f);
            cam.fieldOfView = m_FieldOfView;
        }

        /// <summary>Puts the eye on the player and faces the player's direction, looking level.</summary>
        public override void SnapToTarget()
        {
            if (Target != null)
            {
                m_Yaw = Target.eulerAngles.y;
                transform.position = Target.position + Vector3.up * EyeHeight;
            }
            m_Pitch = 0f;
            m_IgnoreLookFrames = LookSettleFrames;
            transform.rotation = Quaternion.Euler(0f, m_Yaw, 0f);
            ApplyCameraPose();
        }

        /// <summary>Turns the view by a mouse movement in pixels. Pitch is clamped so the view never flips.</summary>
        public void ApplyLook(Vector2 pixels)
        {
            pixels = Vector2.ClampMagnitude(pixels, MaxLookDeltaPerFrame);
            var scale = DegreesPerPixel(m_Sensitivity);
            m_Yaw = Mathf.Repeat(m_Yaw + pixels.x * scale, 360f);
            m_Pitch = Mathf.Clamp(m_Pitch - pixels.y * scale, -MaxPitch, MaxPitch);
            transform.rotation = Quaternion.Euler(0f, m_Yaw, 0f);
            ApplyCameraPose();
        }

        void LateUpdate()
        {
            if (Target == null || Context == null || Context.Session == null)
                return;
            if (Context.Session.AcceptsPlayerInput && Context.CursorShouldLock)
            {
                if (m_IgnoreLookFrames > 0)
                    m_IgnoreLookFrames--;
                else
                    ApplyLook(Context.Input.Look);
            }
            transform.position = Target.position + Vector3.up * EyeHeight;
        }
    }
}
