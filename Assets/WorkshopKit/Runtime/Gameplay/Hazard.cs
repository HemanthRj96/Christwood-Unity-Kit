using UnityEngine;

namespace UnityWorkshop
{
    /// <summary>
    /// Sends the player back to the start (or last checkpoint) when touched. Collected items and the
    /// clock are kept. After a respawn the player is briefly protected so it cannot be hit again at once.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Unity Workshop/Hazard")]
    public sealed class Hazard : WorkshopSceneBehaviour
    {
        protected override void OnConnect(WorkshopGameContext context) { }

        void OnTriggerEnter(Collider other) => Touch(other);

        void OnTriggerStay(Collider other) => Touch(other);

        void Touch(Collider other)
        {
            if (IsPlayer(other))
                Context.Session.RequestRespawn();
        }
    }
}
