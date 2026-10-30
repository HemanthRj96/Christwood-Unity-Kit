using UnityEngine;

namespace UnityWorkshop
{
    public enum MoveSpeed
    {
        Slow,
        Medium,
        Fast,
    }

    /// <summary>
    /// Moves a hazard back and forth between where it is placed and an end point. Moves in physics
    /// steps as a kinematic body, so pausing freezes it and restarting puts it back at the start.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    [AddComponentMenu("Unity Workshop/Hazard Mover")]
    public sealed class HazardMover : MonoBehaviour
    {
        public const float MinTravel = 0.5f;

        [SerializeField, Tooltip("Where the hazard travels to, measured from where it is placed (metres).")]
        Vector3 m_Travel = new Vector3(4f, 0f, 0f);
        [SerializeField, Tooltip("How fast it moves.")]
        MoveSpeed m_Speed = MoveSpeed.Medium;
        [SerializeField, Range(0f, 2f), Tooltip("Seconds it waits at each end.")]
        float m_WaitAtEnds = 0.3f;

        Rigidbody m_Body;
        Vector3 m_Start;
        float m_Time;

        public Vector3 Travel => m_Travel;
        public MoveSpeed Speed => m_Speed;
        public float WaitAtEnds => m_WaitAtEnds;

        public static float MetresPerSecond(MoveSpeed speed) =>
            speed == MoveSpeed.Slow ? 1.5f : speed == MoveSpeed.Fast ? 5f : 3f;

        void Awake()
        {
            m_Body = GetComponent<Rigidbody>();
            m_Body.isKinematic = true;
            m_Body.useGravity = false;
            m_Body.interpolation = RigidbodyInterpolation.Interpolate;
            m_Start = m_Body.position;
        }

        void FixedUpdate()
        {
            m_Time += Time.fixedDeltaTime;
            m_Body.MovePosition(PositionAt(m_Start, m_Time));
        }

        /// <summary>
        /// Position after <paramref name="time"/> seconds when starting at <paramref name="start"/>. The Scene
        /// view path preview uses the same function.
        /// </summary>
        public Vector3 PositionAt(Vector3 start, float time) =>
            start + m_Travel * Progress(time, m_Travel.magnitude, MetresPerSecond(m_Speed), m_WaitAtEnds);

        /// <summary>0 at the start, 1 at the end: move out, wait, move back, wait, repeat.</summary>
        public static float Progress(float time, float distance, float speed, float wait)
        {
            if (distance < 0.0001f || speed <= 0f)
                return 0f;
            var move = distance / speed;
            var period = 2f * (move + wait);
            var t = Mathf.Repeat(time, period);
            if (t < move)
                return Smooth(t / move);
            t -= move;
            if (t < wait)
                return 1f;
            t -= wait;
            if (t < move)
                return 1f - Smooth(t / move);
            return 0f;
        }

        static float Smooth(float x) => x * x * (3f - 2f * x);
    }
}
