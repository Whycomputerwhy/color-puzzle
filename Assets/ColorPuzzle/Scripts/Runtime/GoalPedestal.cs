using System.Collections.Generic;
using UnityEngine;

namespace ColorPuzzle
{
    public sealed class GoalPedestal : MonoBehaviour
    {
        private static readonly HashSet<GoalPedestal> ActivePedestals = new HashSet<GoalPedestal>();

        [Header("Matching")]
        [SerializeField] private string acceptedObjectId = "red-ball";
        [SerializeField] private Transform placementSocket;

        [Header("Completion Appearance")]
        [SerializeField] private Renderer pedestalRenderer;
        [SerializeField] private Material completedMaterial;
        [SerializeField] private GameObject colorProxy;

        private bool complete;
        public bool IsComplete => complete;
        public Bounds PlacementBounds => pedestalRenderer != null ? pedestalRenderer.bounds : GetComponent<Collider>().bounds;
        public static IEnumerable<GoalPedestal> ActiveGoals => ActivePedestals;

        private void OnEnable() => ActivePedestals.Add(this);
        private void OnDisable() => ActivePedestals.Remove(this);

        public bool CanAccept(DiscoverableObject item)
        {
            return !complete && item != null && item.ObjectId == acceptedObjectId;
        }

        public bool TryPlace(DiscoverableObject item)
        {
            if (!CanAccept(item)) return false;
            item.Place(placementSocket != null ? placementSocket : transform);
            complete = true;
            if (pedestalRenderer != null && completedMaterial != null)
                pedestalRenderer.sharedMaterial = completedMaterial;
            if (colorProxy != null) colorProxy.SetActive(false);
            return true;
        }
    }
}
