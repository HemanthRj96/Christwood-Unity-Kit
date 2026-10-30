using UnityEngine;

namespace UnityWorkshop
{
    /// <summary>
    /// Shared capsule motor used by every template: acceleration, gravity, grounding, jumping, facing
    /// and teleporting. Templates differ only in how they turn input into a desired direction.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerMotor : MonoBehaviour
    {
        const float GroundStickSpeed = 2f;

        /// <summary>A jump still works this long after walking off an edge.</summary>
        public const float CoyoteTime = 0.1f;

        /// <summary>A jump pressed this long before landing still happens on landing.</summary>
        public const float JumpBufferTime = 0.12f;

        [SerializeField] MovementSettings m_Settings;

        CharacterController m_Controller;
        Vector3 m_PlanarVelocity;
        float m_VerticalSpeed;
        bool m_Grounded;
        float m_SinceGrounded = float.PositiveInfinity;
        float m_JumpBuffer;
        bool m_LockDepth;
        float m_LockedDepth;

        public MovementSettings Settings
        {
            get => m_Settings;
            set => m_Settings = value;
        }

        public CharacterController Controller => m_Controller != null ? m_Controller : (m_Controller = GetComponent<CharacterController>());
        public bool IsGrounded => m_Grounded;
        public Vector3 Velocity => m_PlanarVelocity + Vector3.up * m_VerticalSpeed;
        public Vector3 PlanarVelocity => m_PlanarVelocity;

        void Awake() => m_Controller = GetComponent<CharacterController>();

        /// <summary>
        /// Moves for one frame. <paramref name="desiredDirection"/> is a world-space ground direction
        /// with magnitude up to 1.
        /// </summary>
        public void Step(Vector3 desiredDirection, bool jumpRequested, bool canJump, float deltaTime)
        {
            if (m_Settings == null || deltaTime <= 0f || !Controller.enabled)
                return;

            desiredDirection.y = 0f;
            if (m_LockDepth)
            {
                desiredDirection.z = 0f;
                m_PlanarVelocity.z = 0f;
            }
            desiredDirection = Vector3.ClampMagnitude(desiredDirection, 1f);
            var target = desiredDirection * m_Settings.MoveSpeed;
            var rate = desiredDirection.sqrMagnitude > 0.0001f ? m_Settings.Acceleration : m_Settings.Deceleration;
            if (!m_Grounded)
                rate *= m_Settings.AirControl;
            m_PlanarVelocity = Vector3.MoveTowards(m_PlanarVelocity, target, rate * deltaTime);

            if (m_Grounded && m_VerticalSpeed < 0f)
                m_VerticalSpeed = -GroundStickSpeed;

            m_SinceGrounded = m_Grounded ? 0f : m_SinceGrounded + deltaTime;
            m_JumpBuffer = jumpRequested ? JumpBufferTime : Mathf.Max(0f, m_JumpBuffer - deltaTime);
            if (canJump && m_JumpBuffer > 0f && m_SinceGrounded <= CoyoteTime && m_VerticalSpeed <= 0f)
            {
                m_VerticalSpeed = m_Settings.JumpSpeed;
                m_JumpBuffer = 0f;
                m_SinceGrounded = float.PositiveInfinity;
            }
            m_VerticalSpeed -= m_Settings.Gravity * deltaTime;

            var flags = Controller.Move((m_PlanarVelocity + Vector3.up * m_VerticalSpeed) * deltaTime);
            m_Grounded = (flags & CollisionFlags.Below) != 0;
            if ((flags & CollisionFlags.Above) != 0 && m_VerticalSpeed > 0f)
                m_VerticalSpeed = 0f;

            // Keep velocity consistent with walls so the player does not "stick" when sliding along them.
            if ((flags & CollisionFlags.Sides) != 0)
            {
                var actual = Controller.velocity;
                actual.y = 0f;
                if (m_LockDepth)
                    actual.z = 0f;
                if (actual.sqrMagnitude < m_PlanarVelocity.sqrMagnitude)
                    m_PlanarVelocity = actual;
            }

            // Collisions can nudge the capsule sideways; pull it back onto the play plane through the
            // controller so the correction still respects walls.
            if (m_LockDepth)
            {
                var drift = m_LockedDepth - transform.position.z;
                if (Mathf.Abs(drift) > 0.0001f)
                    Controller.Move(new Vector3(0f, 0f, drift));
            }
        }

        /// <summary>Keeps the player on the plane at world Z <paramref name="depth"/> (side-scroller).</summary>
        public void LockDepth(float depth)
        {
            m_LockDepth = true;
            m_LockedDepth = depth;
        }

        public void UnlockDepth() => m_LockDepth = false;

        public bool IsDepthLocked => m_LockDepth;

        /// <summary>Turns the character toward a ground direction at the preset's turn speed.</summary>
        public void FaceTowards(Vector3 direction, float deltaTime)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f || m_Settings == null)
                return;
            var target = Quaternion.LookRotation(direction, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, m_Settings.TurnSpeed * deltaTime);
        }

        /// <summary>Moves instantly to a pose and clears all motion.</summary>
        public void Teleport(Pose pose)
        {
            if (m_LockDepth)
                pose.position.z = m_LockedDepth;
            var controller = Controller;
            var wasEnabled = controller.enabled;
            controller.enabled = false;
            transform.SetPositionAndRotation(pose.position, pose.rotation);
            controller.enabled = wasEnabled;
            Physics.SyncTransforms();
            ClearVelocity();
        }

        public void ClearVelocity()
        {
            m_PlanarVelocity = Vector3.zero;
            m_VerticalSpeed = 0f;
            m_Grounded = false;
            m_SinceGrounded = float.PositiveInfinity;
            m_JumpBuffer = 0f;
        }

        /// <summary>Turns the character instantly to a compass direction (used when the camera decides facing).</summary>
        public void SetFacing(float yaw) => transform.rotation = Quaternion.Euler(0f, yaw, 0f);
    }
}
