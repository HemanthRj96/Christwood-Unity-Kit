using UnityEngine;

namespace UnityWorkshop
{
    /// <summary>
    /// Watches the level from the side. The player moves left and right on a single plane (the play
    /// plane). The camera follows sideways smoothly, only moves up or down when the player leaves a calm
    /// band around the middle (so small jumps do not shake the view), and never shows outside its bounds.
    /// </summary>
    [AddComponentMenu("Unity Workshop/Side-Scroller Camera")]
    public sealed class SideScrollerCameraRig : WorkshopCameraRig
    {
        /// <summary>Height above the player's feet that the camera aims at.</summary>
        public const float AimHeight = 1.5f;

        [SerializeField, Tooltip("World depth (Z) of the play plane. The player and gameplay pieces stay on it.")]
        float m_PlaneDepth;
        [SerializeField, Tooltip("How far the camera is from the play plane.")]
        CameraDistance m_Distance = CameraDistance.Medium;
        [SerializeField, Range(0f, 0.6f), Tooltip("Seconds the camera takes to catch up. 0 follows instantly.")]
        float m_FollowDelay = 0.15f;
        [SerializeField, Range(0f, 4f), Tooltip("How far (metres) the player can move up or down before the camera follows.")]
        float m_VerticalCalmZone = 1.5f;
        [SerializeField, Tooltip("Keep the camera inside the Bounds rectangle.")]
        bool m_UseBounds = true;
        [SerializeField, Tooltip("Lowest left point the camera may aim at, on the play plane.")]
        Vector2 m_BoundsMin = new Vector2(-10f, 0f);
        [SerializeField, Tooltip("Highest right point the camera may aim at, on the play plane.")]
        Vector2 m_BoundsMax = new Vector2(30f, 10f);

        Vector2 m_Focus;
        Vector2 m_FollowVelocity;

        public float PlaneDepth => m_PlaneDepth;
        public bool UsesBounds => m_UseBounds;
        public Vector2 BoundsMin => m_BoundsMin;
        public Vector2 BoundsMax => m_BoundsMax;
        public float VerticalCalmZone => m_VerticalCalmZone;
        public Vector2 Focus => m_Focus;

        /// <summary>Distance from the plane in metres for a preset.</summary>
        public static float MetresFor(CameraDistance distance) =>
            distance == CameraDistance.Close ? 10f : distance == CameraDistance.Far ? 18f : 14f;

        public float DistanceMetres => MetresFor(m_Distance);

        public override bool TryGetMovementPlane(out float depth)
        {
            depth = m_PlaneDepth;
            return true;
        }

        /// <summary>D and the right arrow move toward +X; W and S do nothing in a side view.</summary>
        public override void GetMoveBasis(out Vector3 forward, out Vector3 right)
        {
            forward = Vector3.zero;
            right = Vector3.right;
        }

        /// <summary>Slight downward tilt so the top of the ground is visible, in degrees.</summary>
        public const float Tilt = 8f;

        /// <summary>Places the child camera at the preset distance in front of the plane, aimed at the rig point.</summary>
        public override void ApplyCameraPose()
        {
            var cam = Camera;
            if (cam == null)
                return;
            var tilt = Quaternion.Euler(Tilt, 0f, 0f);
            cam.transform.localRotation = tilt;
            cam.transform.localPosition = tilt * (Vector3.back * DistanceMetres);
        }

        public override void SnapToTarget()
        {
            if (Target != null)
                FrameFeet(Target.position);
        }

        /// <summary>Aims straight at a player standing at <paramref name="feet"/>, inside the bounds. Also used by the editor preview.</summary>
        public void FrameFeet(Vector3 feet)
        {
            m_Focus = Clamp(new Vector2(feet.x, feet.y + AimHeight));
            m_FollowVelocity = Vector2.zero;
            transform.SetPositionAndRotation(new Vector3(m_Focus.x, m_Focus.y, m_PlaneDepth), Quaternion.identity);
            ApplyCameraPose();
        }

        /// <summary>Where the camera wants to aim for a player at <paramref name="feet"/>, given the current aim.</summary>
        public Vector2 DesiredFocus(Vector3 feet, Vector2 current)
        {
            var aimY = feet.y + AimHeight;
            var y = current.y;
            if (aimY > y + m_VerticalCalmZone)
                y = aimY - m_VerticalCalmZone;
            else if (aimY < y - m_VerticalCalmZone)
                y = aimY + m_VerticalCalmZone;
            return Clamp(new Vector2(feet.x, y));
        }

        public Vector2 Clamp(Vector2 focus)
        {
            if (!m_UseBounds)
                return focus;
            var min = Vector2.Min(m_BoundsMin, m_BoundsMax);
            var max = Vector2.Max(m_BoundsMin, m_BoundsMax);
            return new Vector2(Mathf.Clamp(focus.x, min.x, max.x), Mathf.Clamp(focus.y, min.y, max.y));
        }

        void LateUpdate()
        {
            if (Target == null)
                return;
            var desired = DesiredFocus(Target.position, m_Focus);
            m_Focus = m_FollowDelay <= 0f
                ? desired
                : Vector2.SmoothDamp(m_Focus, desired, ref m_FollowVelocity, m_FollowDelay);
            transform.SetPositionAndRotation(new Vector3(m_Focus.x, m_Focus.y, m_PlaneDepth), Quaternion.identity);
        }
    }
}
