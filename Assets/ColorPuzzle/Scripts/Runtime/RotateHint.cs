using UnityEngine;

namespace ColorPuzzle
{
    public sealed class RotateHint : MonoBehaviour
    {
        [SerializeField] private Vector3 degreesPerSecond = new Vector3(0f, 45f, 0f);
        private void Update() => transform.Rotate(degreesPerSecond * Time.deltaTime, Space.Self);
    }
}
