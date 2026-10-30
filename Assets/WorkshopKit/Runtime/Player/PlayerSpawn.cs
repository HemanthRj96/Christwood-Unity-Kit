using UnityEngine;

namespace UnityWorkshop
{
    /// <summary>
    /// Where the player starts. The blue arrow shows which way the player will face.
    /// Keep exactly one Start Point in each game.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Unity Workshop/Start Point")]
    public sealed class PlayerSpawn : WorkshopSceneBehaviour
    {
        /// <summary>Height above the marker the player is placed, so the capsule never starts inside the floor.</summary>
        public const float SpawnLift = 0.05f;

        /// <summary>Pose for the player root (feet). Only the Y rotation is used so the player stands upright.</summary>
        public Pose SpawnPose => new Pose(
            transform.position + Vector3.up * SpawnLift,
            Quaternion.Euler(0f, transform.eulerAngles.y, 0f));

        protected override void OnConnect(WorkshopGameContext context) => context.RegisterSpawn(this);

        protected override void OnDisconnect(WorkshopGameContext context) => context.UnregisterSpawn(this);
    }
}
