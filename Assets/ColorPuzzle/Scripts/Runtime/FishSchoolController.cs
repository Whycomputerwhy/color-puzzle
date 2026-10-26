using System;
using UnityEngine;

namespace ColorPuzzle
{
    /// <summary>Spawns a deterministic, lightweight school from one reusable fish prefab.</summary>
    [ExecuteAlways]
    public sealed class FishSchoolController : MonoBehaviour
    {
        [Serializable]
        private struct FishGroup
        {
            public string label;
            public FishSwimmer prefab;
            [Min(0)] public int count;
        }

        [Header("Fish groups")]
        [SerializeField] private FishGroup[] fishGroups;
        [Tooltip("Use the same seed to reproduce a school layout; change it for a new variation.")]
        [SerializeField] private int randomSeed = 20261025;
        [Tooltip("Shows the exact seeded school in the Scene view without saving preview fish into the level.")]
        [SerializeField] private bool previewInEditor = true;

        [Header("School volume (local to this object)")]
        [SerializeField] private Vector2 horizontalRange = new Vector2(11f, 12f);
        [SerializeField] private Vector2 swimHeightRange = new Vector2(1.2f, 5.5f);
        [SerializeField] private Vector2 fishSizeRange = new Vector2(0.6f, 1.15f);

        [Header("Individual fish variation")]
        [SerializeField] private Vector2 swimSpeedRange = new Vector2(0.2f, 0.42f);
        [SerializeField] private Vector2 roamRadiusRange = new Vector2(1.8f, 3.2f);
        [SerializeField] private Vector2 roamHeightRange = new Vector2(0.45f, 1.1f);
        [SerializeField] private Vector2 destinationIntervalRange = new Vector2(3.5f, 6f);

        private readonly System.Collections.Generic.List<GameObject> spawnedFish = new();
        private const string EditorPreviewContainerName = "Fish Preview (Editor Only)";
        private const string RuntimeContainerName = "Spawned Fish";
#if UNITY_EDITOR
        private bool refreshQueued;
#endif

        private void OnEnable()
        {
            RefreshSchool();
        }

        private void OnDisable()
        {
            ClearSchool();
        }

        private void OnValidate()
        {
            if (Application.isPlaying) return;
#if UNITY_EDITOR
            // Changing an Inspector value invokes OnValidate while Unity is checking
            // the hierarchy. Queue the rebuild so creating/removing preview children
            // does not send hierarchy messages during that restricted moment.
            if (refreshQueued) return;
            refreshQueued = true;
            UnityEditor.EditorApplication.delayCall += RefreshAfterValidation;
#else
            RefreshSchool();
#endif
        }

#if UNITY_EDITOR
        private void RefreshAfterValidation()
        {
            refreshQueued = false;
            if (this == null || Application.isPlaying) return;
            RefreshSchool();
        }
#endif

        private void RefreshSchool()
        {
            ClearSchool();
#if UNITY_EDITOR
            // Prefab loading also runs ExecuteAlways. A prefab asset is not a scene
            // owner, so a preview made here becomes a loose duplicate in the editor.
            if (!Application.isPlaying && UnityEditor.EditorUtility.IsPersistent(gameObject)) return;
#endif
            if (!HasFishToSpawn() || (!Application.isPlaying && !previewInEditor)) return;

            Transform fishParent = GetOrCreateFishContainer();
            System.Random random = new System.Random(randomSeed);
            int fishNumber = 1;
            foreach (FishGroup group in fishGroups)
            {
                if (group.prefab == null) continue;
                for (int index = 0; index < group.count; index++)
                {
                    FishSwimmer fish = Instantiate(group.prefab, fishParent);
                    fish.name = $"{group.label} Fish {fishNumber++:00}";
                    fish.transform.localPosition = new Vector3(
                        Range(random, -horizontalRange.x, horizontalRange.x),
                        Range(random, swimHeightRange.x, swimHeightRange.y),
                        Range(random, -horizontalRange.y, horizontalRange.y));
                    fish.transform.localRotation = Quaternion.Euler(0f, Range(random, 0f, 360f), 0f);
                    fish.transform.localScale = Vector3.one * Range(random, fishSizeRange.x, fishSizeRange.y);
                    fish.Configure(
                        Range(random, swimSpeedRange.x, swimSpeedRange.y),
                        Range(random, roamRadiusRange.x, roamRadiusRange.y),
                        Range(random, roamHeightRange.x, roamHeightRange.y),
                        Range(random, destinationIntervalRange.x, destinationIntervalRange.y));
                    if (!Application.isPlaying) SetEditorOnly(fish.gameObject);
                    spawnedFish.Add(fish.gameObject);
                }
            }
        }

        private bool HasFishToSpawn()
        {
            if (fishGroups == null) return false;
            foreach (FishGroup group in fishGroups)
                if (group.prefab != null && group.count > 0) return true;
            return false;
        }

        private void ClearSchool()
        {
            foreach (GameObject fish in spawnedFish)
            {
                if (fish == null) continue;
                if (Application.isPlaying) Destroy(fish);
                else DestroyImmediate(fish);
            }
            spawnedFish.Clear();

            // Rebuilds and script reloads reset the in-memory list. Any direct fish
            // children still belong to this one school and must be cleared first.
            for (int index = transform.childCount - 1; index >= 0; index--)
            {
                Transform child = transform.GetChild(index);
                if (child.GetComponent<FishSwimmer>() == null) continue;
                if (Application.isPlaying) Destroy(child.gameObject);
                else DestroyImmediate(child.gameObject);
            }

            // Clean up previews created by an earlier version of this component.
            for (int index = transform.childCount - 1; index >= 0; index--)
            {
                Transform child = transform.GetChild(index);
                if (child.name != EditorPreviewContainerName && child.name != RuntimeContainerName) continue;
                if (Application.isPlaying) Destroy(child.gameObject);
                else DestroyImmediate(child.gameObject);
            }

            if (!Application.isPlaying) ClearLegacyOrphanedPreviews();
        }

        private Transform GetOrCreateFishContainer()
        {
            string containerName = Application.isPlaying ? RuntimeContainerName : EditorPreviewContainerName;
            Transform existing = transform.Find(containerName);
            if (existing != null) return existing;

            GameObject container = new GameObject(containerName);
            container.transform.SetParent(transform, false);
            if (!Application.isPlaying) SetEditorOnly(container);
            return container.transform;
        }

        // A previous editor-preview implementation briefly instantiated fish with a
        // persistent prefab parent. Unity then made them loose scene objects. Remove
        // only those unsaved preview leftovers; authored fish are never touched.
        private void ClearLegacyOrphanedPreviews()
        {
            foreach (FishSwimmer fish in FindObjectsByType<FishSwimmer>(FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                if (fish == null || fish.transform.parent == transform) continue;
                if ((fish.gameObject.hideFlags & HideFlags.DontSaveInEditor) == 0) continue;
                if (!fish.name.Contains(" Fish ")) continue;
                DestroyImmediate(fish.gameObject);
            }
        }

        private static void SetEditorOnly(GameObject target)
        {
            target.hideFlags = HideFlags.DontSaveInEditor;
            foreach (Transform child in target.transform)
                SetEditorOnly(child.gameObject);
        }

        private static float Range(System.Random random, float a, float b) =>
            Mathf.Lerp(Mathf.Min(a, b), Mathf.Max(a, b), (float)random.NextDouble());
    }
}
