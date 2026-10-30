using UnityEngine;

namespace UnityWorkshop
{
    public enum TopDownView
    {
        [InspectorName("Angled (see walls and height)")] Angled,
        [InspectorName("Overhead (map view)")] Overhead,
    }

    /// <summary>
    /// Fixed-angle camera that follows the player from above. W always moves "up the screen", because
    /// movement uses the camera's ground-plane directions.
    /// </summary>
    [AddComponentMenu("Unity Workshop/Top-Down Camera")]
    public sealed class TopDownCameraRig : WorkshopCameraRig
    {
        public const float MinDistance = 8f;
        public const float MaxDistance = 30f;

        [SerializeField, Tooltip("Camera angle preset.")]
        TopDownView m_View = TopDownView.Angled;
        [SerializeField, Range(MinDistance, MaxDistance), Tooltip("How far the camera is from the player. Larger shows more of the level.")]
        float m_Distance = 16f;
        [SerializeField, Range(0f, 359f), Tooltip("Which way the camera looks across the level, in degrees.")]
        float m_Direction;
        [SerializeField, Range(0f, 0.6f), Tooltip("Seconds the camera takes to catch up. 0 follows instantly.")]
        float m_FollowDelay = 0.15f;

        Vector3 m_FollowVelocity;

        public TopDownView View => m_View;
        public float Distance => m_Distance;
        public float Direction => m_Direction;

        /// <summary>Downward tilt for the current preset, in degrees.</summary>
        public float Pitch => m_View == TopDownView.Overhead ? 85f : 55f;

        /// <summary>The height above the player's feet the camera aims at.</summary>
        public const float AimHeight = 1f;

        public Quaternion ViewRotation => Quaternion.Euler(Pitch, m_Direction, 0f);

        public override void GetMoveBasis(out Vector3 forward, out Vector3 right)
        {
            var yaw = Quaternion.Euler(0f, m_Direction, 0f);
            forward = yaw * Vector3.forward;
            right = yaw * Vector3.right;
        }

        public override void ApplyCameraPose()
        {
            var cam = Camera;
            if (cam == null)
                return;
            var rotation = ViewRotation;
            cam.transform.localPosition = Vector3.up * AimHeight + rotation * (Vector3.back * m_Distance);
            cam.transform.localRotation = rotation;
        }

        public override void SnapToTarget()
        {
            ApplyCameraPose();
            m_FollowVelocity = Vector3.zero;
            if (Target != null)
                transform.SetPositionAndRotation(Target.position, Quaternion.identity);
        }

        void LateUpdate()
        {
            if (Target == null)
                return;
            transform.position = m_FollowDelay <= 0f
                ? Target.position
                : Vector3.SmoothDamp(transform.position, Target.position, ref m_FollowVelocity, m_FollowDelay);
        }
    }
}
