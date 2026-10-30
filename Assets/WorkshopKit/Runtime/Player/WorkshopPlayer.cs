using UnityEngine;

namespace UnityWorkshop
{
    public enum PlayerFacing
    {
        /// <summary>Turns toward the way it walks (top-down, third-person).</summary>
        MovementDirection,
        /// <summary>Always faces where the camera looks (first-person).</summary>
        CameraDirection,
    }

    /// <summary>
    /// The player character. Reads shared input only while the game is playing, converts it into a
    /// ground direction using the active camera, and drives the shared <see cref="PlayerMotor"/>.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerMotor))]
    [AddComponentMenu("Unity Workshop/Player")]
    public sealed class WorkshopPlayer : WorkshopSceneBehaviour
    {
        [SerializeField, Tooltip("Whether this player can jump. Set by the template.")]
        bool m_CanJump;
        [SerializeField, Tooltip("Which way the character faces. Set by the template.")]
        PlayerFacing m_Facing = PlayerFacing.MovementDirection;

        PlayerMotor m_Motor;

        public PlayerMotor Motor => m_Motor != null ? m_Motor : (m_Motor = GetComponent<PlayerMotor>());
        public bool CanJump => m_CanJump;
        public PlayerFacing Facing => m_Facing;

        void Awake() => m_Motor = GetComponent<PlayerMotor>();

        protected override void OnConnect(WorkshopGameContext context)
        {
            var movement = context.Movement;
            if (movement != null)
                Motor.Settings = movement;
            context.RegisterPlayer(this);
        }

        protected override void OnDisconnect(WorkshopGameContext context) => context.UnregisterPlayer(this);

        void Update()
        {
            if (Context == null || Context.Session == null || Context.Player != this || Context.SetupError != null)
                return;

            var session = Context.Session;
            var deltaTime = Time.deltaTime;
            var acceptsInput = session.AcceptsPlayerInput;
            var move = acceptsInput ? Context.Input.Move : Vector2.zero;
            var jump = acceptsInput && Context.Input.JumpPressed;

            var direction = Vector3.zero;
            var forward = transform.forward;
            if (Context.CameraRig != null && Context.CameraRig.TryGetMovementPlane(out var depth))
                Motor.LockDepth(depth);
            else
                Motor.UnlockDepth();
            if (Context.CameraRig != null)
            {
                Context.CameraRig.GetMoveBasis(out forward, out var right);
                if (move.sqrMagnitude > 0f)
                    direction = Vector3.ClampMagnitude(forward * move.y + right * move.x, 1f);
            }

            Motor.Step(direction, jump, m_CanJump, deltaTime);
            if (!acceptsInput)
                return;
            if (m_Facing == PlayerFacing.CameraDirection)
            {
                if (forward.sqrMagnitude > 0.0001f)
                    Motor.SetFacing(Quaternion.LookRotation(forward, Vector3.up).eulerAngles.y);
            }
            else
                Motor.FaceTowards(direction, deltaTime);
        }

        /// <summary>Places the player at a pose with no leftover motion.</summary>
        public void PlaceAt(Pose pose) => Motor.Teleport(pose);
    }
}
