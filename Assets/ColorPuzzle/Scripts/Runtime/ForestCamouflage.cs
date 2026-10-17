using UnityEngine;

namespace ColorPuzzle
{
    /// <summary>
    /// Fades a forest prop from its authored material color near the player to a shared
    /// camouflage color at distance. Apply this only to the regular-world renderer;
    /// its ColorReveal proxy should retain its authored color.
    /// </summary>
    [RequireComponent(typeof(Renderer))]
    public sealed class ForestCamouflage : MonoBehaviour
    {
        // CENTRALIZED FOREST CAMOUFLAGE PARAMETERS
        [Header("Camouflage")]
        [SerializeField] private Color camouflageColor = new Color(0.24f, 0.32f, 0.12f, 1f);
        [SerializeField, Min(0f)] private float clearDistance = 3f;
        [SerializeField, Min(0f)] private float camouflageDistance = 10f;
        [SerializeField] private Transform playerOverride;

        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int MaterialColor = Shader.PropertyToID("_Color");

        private Renderer targetRenderer;
        private MaterialPropertyBlock properties;
        private Transform player;
        private Color trueColor = Color.white;
        private int colorProperty;

        private void Awake()
        {
            targetRenderer = GetComponent<Renderer>();
            properties = new MaterialPropertyBlock();
            ConfigureColorProperty();
        }

        private void OnEnable()
        {
            FindPlayer();
        }

        private void LateUpdate()
        {
            if (targetRenderer == null || colorProperty == 0) return;
            if (player == null) FindPlayer();
            if (player == null) return;

            float distance = Vector3.Distance(player.position, transform.position);
            float blend = clearDistance >= camouflageDistance
                ? (distance >= camouflageDistance ? 1f : 0f)
                : Mathf.InverseLerp(clearDistance, camouflageDistance, distance);

            targetRenderer.GetPropertyBlock(properties);
            properties.SetColor(colorProperty, Color.Lerp(trueColor, camouflageColor, blend));
            targetRenderer.SetPropertyBlock(properties);
        }

        private void OnDisable()
        {
            if (targetRenderer != null) targetRenderer.SetPropertyBlock(null);
        }

        private void ConfigureColorProperty()
        {
            Material material = targetRenderer != null ? targetRenderer.sharedMaterial : null;
            if (material == null) return;

            if (material.HasProperty(BaseColor)) colorProperty = BaseColor;
            else if (material.HasProperty(MaterialColor)) colorProperty = MaterialColor;
            else
            {
                Debug.LogWarning(
                    $"ForestCamouflage on '{name}' needs a material with _BaseColor or _Color.", this);
                enabled = false;
                return;
            }

            trueColor = material.GetColor(colorProperty);
        }

        private void FindPlayer()
        {
            if (playerOverride != null)
            {
                player = playerOverride;
                return;
            }

            Camera playerCamera = Camera.main;
            player = playerCamera != null ? playerCamera.transform : null;
        }
    }
}
