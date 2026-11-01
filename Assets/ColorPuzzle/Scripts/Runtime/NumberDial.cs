using UnityEngine;

namespace ColorPuzzle
{
    /// <summary>One clickable wheel in Puzzle 3's four-number lock.</summary>
    public sealed class NumberDial : MonoBehaviour
    {
        [SerializeField, Range(0, 10)] private int targetValue;
        [SerializeField, Range(0, 10)] private int currentValue;
        [SerializeField] private Transform pointer;
        [SerializeField] private TextMesh valueText;

        public int TargetValue => targetValue;
        public int CurrentValue => currentValue;

        private void Awake() => RefreshVisual();

        public void Advance()
        {
            currentValue = (currentValue + 1) % 11;
            RefreshVisual();
        }

        private void RefreshVisual()
        {
            if (pointer != null)
                pointer.localRotation = Quaternion.Euler(0f, 0f, -currentValue * (360f / 11f));
            if (valueText != null)
                valueText.text = currentValue.ToString();
        }
    }
}
