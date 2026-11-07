using ColorPuzzle.Rendering;
using UnityEngine;

namespace ColorPuzzle
{
    /// <summary>Keeps Puzzle 4's shape-key colours secret until the full board is solved.</summary>
    public sealed class FinalDialKeyColorLock : MonoBehaviour
    {
        private DualViewMaterial[] visuals;

        private void Awake()
        {
            visuals = GetComponentsInChildren<DualViewMaterial>(true);
            SetRevealed(false);
        }

        public void SetRevealed(bool revealed)
        {
            if (visuals == null) visuals = GetComponentsInChildren<DualViewMaterial>(true);
            foreach (DualViewMaterial visual in visuals)
            {
                visual.SetColorViewEnabled(revealed);
                visual.SetColorInGreyscaleCamera(revealed);
            }
        }
    }
}
