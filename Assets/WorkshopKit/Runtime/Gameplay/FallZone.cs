using UnityEngine;

namespace UnityWorkshop
{
    /// <summary>
    /// An invisible area that counts as "fell out of the level", for pits and gaps above the Fall Height.
    /// The player returns to the last checkpoint or the Start Point, even right after a respawn.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    [AddComponentMenu("Unity Workshop/Fall Zone")]
    public sealed class FallZone : WorkshopSceneBehaviour
    {
        public BoxCollider Trigger => GetComponent<BoxCollider>();

        void Awake() => GetComponent<BoxCollider>().isTrigger = true;

        protected override void OnConnect(WorkshopGameContext context) { }

        void OnTriggerEnter(Collider other) => Touch(other);

        void OnTriggerStay(Collider other) => Touch(other);

        void Touch(Collider other)
        {
            if (IsPlayer(other))
                Context.RequestFallRespawn();
        }
    }
}
