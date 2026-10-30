using UnityEngine;

namespace UnityWorkshop
{
    /// <summary>
    /// Shared movement feel preset. Read-only at runtime: the motor copies nothing back into it.
    /// </summary>
    [CreateAssetMenu(menuName = "Unity Workshop/Facilitator/Movement Preset", fileName = "New Movement Preset")]
    public sealed class MovementSettings : ScriptableObject
    {
        [SerializeField] string m_DisplayName = "Standard";
        [SerializeField, TextArea(1, 3)] string m_Description = "";
        [SerializeField, Range(1f, 12f), Tooltip("Top walking speed in metres per second.")]
        float m_MoveSpeed = 5f;
        [SerializeField, Range(2f, 100f), Tooltip("How quickly the player reaches top speed.")]
        float m_Acceleration = 40f;
        [SerializeField, Range(2f, 100f), Tooltip("How quickly the player stops when keys are released.")]
        float m_Deceleration = 50f;
        [SerializeField, Range(90f, 1440f), Tooltip("How quickly the character turns to face its movement, in degrees per second.")]
        float m_TurnSpeed = 720f;
        [SerializeField, Range(0.3f, 3f), Tooltip("Jump height in metres. Ignored by templates without jumping.")]
        float m_JumpHeight = 1.2f;
        [SerializeField, Range(5f, 40f), Tooltip("Downward acceleration in metres per second squared.")]
        float m_Gravity = 20f;
        [SerializeField, Range(0f, 1f), Tooltip("How much the player can steer while in the air (0 = none, 1 = full).")]
        float m_AirControl = 0.6f;

        public string DisplayName => m_DisplayName;
        public string Description => m_Description;
        public float MoveSpeed => m_MoveSpeed;
        public float Acceleration => m_Acceleration;
        public float Deceleration => m_Deceleration;
        public float TurnSpeed => m_TurnSpeed;
        public float JumpHeight => m_JumpHeight;
        public float Gravity => m_Gravity;
        public float AirControl => m_AirControl;

        /// <summary>Upward speed needed to reach <see cref="JumpHeight"/> under <see cref="Gravity"/>.</summary>
        public float JumpSpeed => Mathf.Sqrt(2f * m_Gravity * m_JumpHeight);
    }
}
