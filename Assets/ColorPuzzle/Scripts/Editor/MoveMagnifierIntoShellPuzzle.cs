using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ColorPuzzle.Editor
{
    /// <summary>Creates a standalone magnifier reward at Puzzle 1's old reflective-shell position.</summary>
    internal static class MoveMagnifierIntoShellPuzzle
    {
        [MenuItem("Color Puzzle/Move Magnifier Into Shell Puzzle")]
        public static void Move()
        {
            const string IntroPrefabPath = "Assets/ColorPuzzle/Prefabs/Level/CampaignStages/00 Magnifier Intro.prefab";
            Transform reward = FindTransform("Reflective Shell");
            Transform intro = FindTransform("00 Magnifier Intro");
            Transform clamCasing = FindTransform("Clam Casing");
            OceanCampaignManager campaign = Object.FindAnyObjectByType<OceanCampaignManager>();

            if (clamCasing == null || campaign == null)
            {
                Debug.LogError("Expected a Clam Casing and OceanCampaignManager in the open scene.");
                return;
            }

            // Do not reparent the child of the old nested prefab: Unity destroys that
            // child with its old prefab root. Clone it from its authored source instead.
            GameObject introContents = PrefabUtility.LoadPrefabContents(IntroPrefabPath);
            MagnifierPickup sourcePickup = introContents.GetComponentInChildren<MagnifierPickup>(true);
            if (sourcePickup == null)
            {
                PrefabUtility.UnloadPrefabContents(introContents);
                Debug.LogError("The Magnifier Intro prefab has no Magnifier Pickup.");
                return;
            }

            GameObject rewardObject = Object.Instantiate(sourcePickup.gameObject);
            PrefabUtility.UnloadPrefabContents(introContents);
            Undo.RegisterCreatedObjectUndo(rewardObject, "Create magnifier reward");
            rewardObject.name = "Magnifier Reward";
            rewardObject.transform.SetParent(clamCasing.parent, false);
            rewardObject.transform.localPosition = new Vector3(0.44f, 1.03f, 7.28f);
            rewardObject.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            MagnifierPickup magnifier = rewardObject.GetComponent<MagnifierPickup>();

            if (reward != null)
                Undo.DestroyObjectImmediate(reward.gameObject);

            SerializedObject campaignData = new SerializedObject(campaign);
            campaignData.FindProperty("magnifierPickup").objectReferenceValue = magnifier;
            campaignData.ApplyModifiedProperties();

            if (intro != null)
                Undo.DestroyObjectImmediate(intro.gameObject);

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            Selection.activeGameObject = magnifier.gameObject;
            Debug.Log("Created the magnifier reward in Puzzle 1 and removed the old tutorial pillar.");
        }

        private static Transform FindTransform(string name)
        {
            foreach (Transform candidate in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (candidate.name == name) return candidate;
            return null;
        }
    }
}
