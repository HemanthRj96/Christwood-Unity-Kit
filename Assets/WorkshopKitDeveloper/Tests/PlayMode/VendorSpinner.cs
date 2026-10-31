using UnityEngine;

namespace UnityWorkshop.Tests
{
    /// <summary>
    /// Stands in for a third-party gameplay script inside a vendor prefab during adoption tests. It lives in a
    /// non-editor test assembly because Unity refuses to attach editor-only scripts to prefabs.
    /// </summary>
    public sealed class VendorSpinner : MonoBehaviour
    {
        public float degreesPerSecond = 90f;

        void Update() => transform.Rotate(0f, degreesPerSecond * Time.deltaTime, 0f);
    }
}
