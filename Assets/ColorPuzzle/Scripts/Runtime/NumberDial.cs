using UnityEngine;
using ColorPuzzle.Rendering;

namespace ColorPuzzle
{
    /// <summary>One clickable wheel in Puzzle 3's four-number lock.</summary>
    public sealed class NumberDial : MonoBehaviour
    {
        [SerializeField, Range(1, 10)] private int targetValue = 1;
        [SerializeField, Range(1, 10)] private int currentValue = 1;
        [SerializeField] private Transform pointer;
        [SerializeField] private TextMesh valueText;

        public int TargetValue => targetValue;
        public int CurrentValue => currentValue;

        private void Awake()
        {
            UseBakedFontRingLabels();
            RefreshVisual();
        }

        public void Advance()
        {
            currentValue = currentValue >= 10 ? 1 : currentValue + 1;
            RefreshVisual();
        }

        private void RefreshVisual()
        {
            if (pointer != null)
                pointer.localRotation = Quaternion.Euler(0f, 0f, -(currentValue - 1) * 36f);
            if (valueText != null)
                valueText.text = currentValue.ToString();
        }

        /// <summary>
        /// Uses Unity's baked font-atlas texture, but renders it in an alpha-cutout
        /// world pass. This retains the authored Chilanka appearance while each
        /// ink pixel participates in normal depth testing and depth writing.
        /// </summary>
        private void UseBakedFontRingLabels()
        {
            foreach (TextMesh legacyLabel in GetComponentsInChildren<TextMesh>(true))
            {
                if (legacyLabel == null || legacyLabel.font == null) continue;

                // Force the static dial characters into the font atlas before
                // assigning the cutout material. This is the baked source used by
                // every number label on this dial.
                legacyLabel.font.RequestCharactersInTexture(legacyLabel.text, legacyLabel.fontSize);
                MeshRenderer legacyRenderer = legacyLabel.GetComponent<MeshRenderer>();
                if (legacyRenderer == null) continue;

                Shader cutoutShader = Resources.Load<Shader>("Shaders/WorldFontAlphaCutout");
                if (cutoutShader == null) continue;

                var material = new Material(cutoutShader);
                material.mainTexture = legacyLabel.font.material.mainTexture;

                // Covers either Awake order: if DualViewRoot scanned this label
                // before NumberDial did, turn its material-swapping component off
                // before assigning the baked cutout material.
                DualViewMaterial dualView = legacyLabel.GetComponent<DualViewMaterial>();
                if (dualView != null) dualView.enabled = false;
                legacyRenderer.sharedMaterial = material;
                legacyRenderer.enabled = true;
            }
        }

    }
}
