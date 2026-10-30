using UnityEngine;

namespace UnityWorkshop
{
    /// <summary>
    /// Connects the player's look to its movement without the look ever moving the player. It places the
    /// visual model (offset, turn, size), and, when an Animator is supplied, feeds it optional parameters:
    /// <c>Speed</c> (0–1), <c>Grounded</c> and <c>VerticalSpeed</c>. Parameters the controller does not have
    /// are skipped. Root motion is always switched off, because the workshop motor moves the player.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerMotor))]
    [AddComponentMenu("Unity Workshop/Player Visual Adapter")]
    public sealed class PlayerVisualAdapter : MonoBehaviour
    {
        public static readonly int SpeedParameter = Animator.StringToHash("Speed");
        public static readonly int GroundedParameter = Animator.StringToHash("Grounded");
        public static readonly int VerticalSpeedParameter = Animator.StringToHash("VerticalSpeed");

        [SerializeField, Tooltip("The model shown for the player. Only this child is moved, turned or scaled.")]
        Transform m_Visual;
        [SerializeField, Tooltip("Moves the model so its feet touch the ground, in metres.")]
        Vector3 m_VisualOffset;
        [SerializeField, Range(-180f, 180f), Tooltip("Turns the model so it faces the way the player walks.")]
        float m_VisualTurn;
        [SerializeField, Range(0.1f, 5f), Tooltip("Makes the model bigger or smaller. The player's collision size does not change.")]
        float m_VisualScale = 1f;
        [SerializeField, Tooltip("Optional. An Animator on the model that receives Speed, Grounded and VerticalSpeed.")]
        Animator m_Animator;

        PlayerMotor m_Motor;
        bool m_ParametersScanned;
        bool m_HasSpeed;
        bool m_HasGrounded;
        bool m_HasVerticalSpeed;

        public Transform Visual => m_Visual;
        public Animator Animator => m_Animator;

        void Awake()
        {
            m_Motor = GetComponent<PlayerMotor>();
            ApplyPlacement();
            UseAnimator(m_Animator);
        }

        /// <summary>Applies offset, turn and size to the visual child. The editor preview uses the same method.</summary>
        public void ApplyPlacement()
        {
            if (m_Visual == null || m_Visual == transform)
                return;
            m_Visual.localPosition = m_VisualOffset;
            m_Visual.localRotation = Quaternion.Euler(0f, m_VisualTurn, 0f);
            m_Visual.localScale = Vector3.one * m_VisualScale;
        }

        /// <summary>Switches to a different Animator (for example after adopting a new model).</summary>
        public void UseAnimator(Animator animator)
        {
            m_Animator = animator;
            m_ParametersScanned = false;
            if (m_Animator != null)
                m_Animator.applyRootMotion = false;
        }

        void ScanParameters()
        {
            m_ParametersScanned = true;
            m_HasSpeed = m_HasGrounded = m_HasVerticalSpeed = false;
            foreach (var parameter in m_Animator.parameters)
            {
                if (parameter.nameHash == SpeedParameter && parameter.type == AnimatorControllerParameterType.Float)
                    m_HasSpeed = true;
                else if (parameter.nameHash == GroundedParameter && parameter.type == AnimatorControllerParameterType.Bool)
                    m_HasGrounded = true;
                else if (parameter.nameHash == VerticalSpeedParameter && parameter.type == AnimatorControllerParameterType.Float)
                    m_HasVerticalSpeed = true;
            }
        }

        void LateUpdate()
        {
            if (m_Animator == null || !m_Animator.isActiveAndEnabled || m_Animator.runtimeAnimatorController == null || m_Motor.Settings == null)
                return;
            if (!m_ParametersScanned)
                ScanParameters();

            if (m_HasSpeed)
                m_Animator.SetFloat(SpeedParameter, Mathf.Clamp01(m_Motor.PlanarVelocity.magnitude / m_Motor.Settings.MoveSpeed));
            if (m_HasGrounded)
                m_Animator.SetBool(GroundedParameter, m_Motor.IsGrounded);
            if (m_HasVerticalSpeed)
                m_Animator.SetFloat(VerticalSpeedParameter, m_Motor.Velocity.y);
        }
    }
}
