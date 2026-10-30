using UnityEngine;

namespace UnityWorkshop
{
    public enum CameraDistance
    {
        Close,
        Medium,
        Far,
    }

    /// <summary>
    /// Follows behind the player. The mouse orbits the camera around them; W moves away from the camera.
    /// When a wall gets between the camera and the player, the camera moves in at once so the player is
    /// never hidden, then eases back out when the way is clear. Respawning resets the orbit behind the player.
    /// </summary>
    [AddComponentMenu("Unity Workshop/Third-Person Camera")]
    public sealed class ThirdPersonCameraRig : WorkshopCameraRig
    {
        /// <summary>Height above the player's feet that the camera orbits around.</summary>
        public const float PivotHeight = 1.5f;
        public const float MinPitch = -20f;
        public const float MaxPitch = 60f;
        public const float DefaultPitch = 15f;

        /// <summary>Radius of the camera's collision probe, in metres.</summary>
        public const float CollisionRadius = 0.25f;

        /// <summary>Closest the camera comes to the pivot when pushed in by walls.</summary>
        public const float MinDistance = 0.3f;

        /// <summary>How fast the camera moves back out once the way is clear, in metres per second.</summary>
        public const float RestoreSpeed = 6f;

        const float MaxLookDeltaPerFrame = 250f;
        const int LookSettleFrames = 2;
        static readonly RaycastHit[] s_Hits = new RaycastHit[16];

        [SerializeField, Tooltip("How far the camera turns when the mouse moves.")]
        LookSensitivity m_Sensitivity = LookSensitivity.Medium;
        [SerializeField, Tooltip("How far behind the player the camera sits when nothing is in the way.")]
        CameraDistance m_Distance = CameraDistance.Medium;
        [SerializeField, Range(50f, 80f), Tooltip("How wide the view is, in degrees.")]
        float m_FieldOfView = 60f;

        float m_Yaw;
        float m_Pitch = DefaultPitch;
        float m_CurrentDistance = -1f;
        int m_IgnoreLookFrames;

        public LookSensitivity Sensitivity => m_Sensitivity;
        public float Yaw => m_Yaw;
        public float Pitch => m_Pitch;
        public float FieldOfView => m_FieldOfView;
        public override bool LocksCursor => true;

        /// <summary>Distance used when nothing blocks the camera.</summary>
        public float FullDistance => MetresFor(m_Distance);

        /// <summary>Distance currently used, after wall collision and smoothing.</summary>
        public float CurrentDistance => m_CurrentDistance < 0f ? FullDistance : m_CurrentDistance;

        public static float MetresFor(CameraDistance distance) =>
            distance == CameraDistance.Close ? 3.5f : distance == CameraDistance.Far ? 7f : 5f;

        protected override void Awake()
        {
            m_Yaw = transform.eulerAngles.y;
            m_CurrentDistance = FullDistance;
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

        /// <summary>Positions the child camera: tilted by the orbit pitch, pulled back by the current distance.</summary>
        public override void ApplyCameraPose()
        {
            var cam = Camera;
            if (cam == null)
                return;
            var tilt = Quaternion.Euler(m_Pitch, 0f, 0f);
            cam.transform.localRotation = tilt;
            cam.transform.localPosition = tilt * (Vector3.back * CurrentDistance);
            cam.fieldOfView = m_FieldOfView;
        }

        public override void SnapToTarget()
        {
            if (Target != null)
                PlaceBehind(new Pose(Target.position, Target.rotation));
        }

        /// <summary>
        /// Resets the orbit behind a player standing at <paramref name="feet"/>: their facing, the default tilt
        /// and the collision-checked distance, with no smoothing. Also used for the editor preview.
        /// </summary>
        public void PlaceBehind(Pose feet)
        {
            m_Yaw = feet.rotation.eulerAngles.y;
            m_Pitch = DefaultPitch;
            transform.SetPositionAndRotation(feet.position + Vector3.up * PivotHeight, Quaternion.Euler(0f, m_Yaw, 0f));
            Physics.SyncTransforms();
            m_CurrentDistance = AllowedDistance();
            m_IgnoreLookFrames = LookSettleFrames;
            ApplyCameraPose();
        }

        /// <summary>Orbits by a mouse movement in pixels. Tilt is clamped so the camera never flips or goes underground.</summary>
        public void ApplyLook(Vector2 pixels)
        {
            pixels = Vector2.ClampMagnitude(pixels, MaxLookDeltaPerFrame);
            var scale = FirstPersonCameraRig.DegreesPerPixel(m_Sensitivity);
            m_Yaw = Mathf.Repeat(m_Yaw + pixels.x * scale, 360f);
            m_Pitch = Mathf.Clamp(m_Pitch - pixels.y * scale, MinPitch, MaxPitch);
            transform.rotation = Quaternion.Euler(0f, m_Yaw, 0f);
            ApplyCameraPose();
        }

        /// <summary>
        /// How far back the camera can sit before touching solid geometry, ignoring the player and triggers.
        /// Uses the same sphere probe in the Editor preview and in play.
        /// </summary>
        public float AllowedDistance()
        {
            var full = FullDistance;
            var direction = Quaternion.Euler(m_Pitch, m_Yaw, 0f) * Vector3.back;
            var count = Physics.SphereCastNonAlloc(transform.position, CollisionRadius, direction, s_Hits, full, ~0, QueryTriggerInteraction.Ignore);
            var nearest = float.PositiveInfinity;
            for (var i = 0; i < count; i++)
            {
                var hit = s_Hits[i];
                if (!IsOwnPlayer(hit.collider))
                    nearest = Mathf.Min(nearest, hit.distance);
            }
            // Stop a little short of whatever was hit; with nothing in the way use the full distance.
            return float.IsPositiveInfinity(nearest) ? full : Mathf.Clamp(nearest - 0.05f, MinDistance, full);
        }

        bool IsOwnPlayer(Collider collider)
        {
            if (Target != null)
                return collider.transform.IsChildOf(Target);
            return collider.GetComponentInParent<WorkshopPlayer>() != null;
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

            transform.SetPositionAndRotation(Target.position + Vector3.up * PivotHeight, Quaternion.Euler(0f, m_Yaw, 0f));
            var allowed = AllowedDistance();
            // In at once (never show the inside of a wall), out smoothly (no popping).
            m_CurrentDistance = allowed < CurrentDistance
                ? allowed
                : Mathf.MoveTowards(CurrentDistance, allowed, RestoreSpeed * Time.deltaTime);
            ApplyCameraPose();
        }
    }
}
