using ColorPuzzle.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace ColorPuzzle
{
    /// <summary>
    /// Adds small flat circular markers at the authored vertices/endpoints of a
    /// patterned stone. The markers share the stone's dual-view material setup,
    /// without changing its mesh, transform, or existing pattern segments.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-200)]
    public sealed class PatternEndpointMarkers : MonoBehaviour
    {
        [Header("Endpoint Layout")]
        [SerializeField] private Vector3[] endpointPositions;
        [SerializeField, Min(0.005f)] private float markerRadius = 0.032f;
        [SerializeField, Min(0.002f)] private float markerHeight = 0.016f;
        [Tooltip("Tiny clearance above the authored pattern segments. Marker height automatically follows their current vertical position.")]
        [SerializeField, Min(0f)] private float heightAbovePattern = 0.001f;

        [Header("Dual-view Appearance")]
        [SerializeField] private Material greyscaleMaterial;
        [SerializeField] private Material colorMaterial;

        private GameObject markerRoot;

        private void Awake() => CreateMarkers();

        private void CreateMarkers()
        {
            if (markerRoot != null || endpointPositions == null || endpointPositions.Length == 0 ||
                greyscaleMaterial == null || colorMaterial == null)
                return;

            markerRoot = new GameObject("Pattern Endpoints");
            markerRoot.transform.SetParent(transform, false);
            markerRoot.layer = gameObject.layer;
            float markerCenterY = FindPatternTopLocalY() + markerHeight * 0.5f + heightAbovePattern;

            for (int index = 0; index < endpointPositions.Length; index++)
            {
                GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                marker.name = $"Pattern Endpoint {index + 1}";
                marker.transform.SetParent(markerRoot.transform, false);
                Vector3 position = endpointPositions[index];
                position.y = markerCenterY;
                marker.transform.localPosition = position;
                marker.transform.localScale = new Vector3(markerRadius * 2f, markerHeight * 0.5f,
                    markerRadius * 2f);
                marker.layer = gameObject.layer;

                Collider markerCollider = marker.GetComponent<Collider>();
                if (markerCollider != null) Destroy(markerCollider);

                MeshRenderer renderer = marker.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = greyscaleMaterial;
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
                DualViewMaterial appearance = marker.AddComponent<DualViewMaterial>();
                appearance.ConfigureMaterialSets(
                    new[] { greyscaleMaterial }, new[] { colorMaterial });
            }
        }

        private float FindPatternTopLocalY()
        {
            bool foundPattern = false;
            float highestWorldY = float.NegativeInfinity;
            foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null || !renderer.name.StartsWith("Pattern Segment")) continue;
                foundPattern = true;
                highestWorldY = Mathf.Max(highestWorldY, renderer.bounds.max.y);
            }

            if (!foundPattern) return 0f;
            return transform.InverseTransformPoint(
                new Vector3(transform.position.x, highestWorldY, transform.position.z)).y;
        }

        private void OnDestroy()
        {
            if (markerRoot != null) Destroy(markerRoot);
        }
    }
}
