#if UNITY_EDITOR
using System;
using ColorPuzzle.Rendering;
using UnityEditor;
using UnityEngine;

namespace ColorPuzzle.Editor
{
    /// <summary>Creates reusable shell-puzzle assets without changing authored scenes or stages.</summary>
    public static class ShellPuzzleBlockoutBuilder
    {
        // All provisional geometry, spacing, and pairing choices live here.
        private const string PrefabFolder = "Assets/ColorPuzzle/Prefabs/Gameplay/ShellPuzzle";
        private const string MaterialFolder = "Assets/ColorPuzzle/Materials/ShellPuzzle";
        private const float PillarHeight = 1.5f;
        private const float PillarDiameter = 1.3f;
        private const float SlotClearance = 0.08f;
        private const float BaseKeyWidth = 0.34f;
        private static readonly float[] FitDiameters = { 0.42f, 0.56f, 0.7f, 0.84f };
        private static readonly Color[] PearlColors =
        {
            new Color(0.94f, 0.28f, 0.28f),
            new Color(0.25f, 0.77f, 0.96f),
            new Color(0.86f, 0.54f, 0.96f),
            new Color(0.97f, 0.79f, 0.28f)
        };

        [MenuItem("Color Puzzle/Create Shell Puzzle Prefabs (No Placement)")]
        public static void Build()
        {
            EnsureFolder(PrefabFolder);
            EnsureFolder(MaterialFolder);
            Material stoneGrey = RequiredMaterial("Assets/ColorPuzzle/Materials/Underwater/M_Cel_Grey_Stone.mat");
            Material shellGrey = RequiredMaterial("Assets/ColorPuzzle/Materials/Underwater/M_Cel_Grey_Shell.mat");
            Material stoneColor = CelMaterial("Keyhole Stone", new Color(0.66f, 0.68f, 0.61f));
            Material beige = CelMaterial("Key Beige", new Color(0.83f, 0.73f, 0.56f));
            Material darkSlot = CelMaterial("Keyhole Recess", new Color(0.10f, 0.14f, 0.17f));
            Material slotGrey = CelMaterial("Keyhole Recess Grey", new Color(0.16f, 0.16f, 0.16f));
            Material[] pearls = new Material[4];
            for (int i = 0; i < 4; i++) pearls[i] = CelMaterial($"Pearl {i + 1:00}", PearlColors[i]);
            Material casing = LitMaterial("Clam Casing Transparent", new Color(0.64f, 0.9f, 1f, 0.24f), 0.1f, 0.85f, true);
            Material casingGrey = LitMaterial("Clam Casing Grey", new Color(0.78f, 0.82f, 0.84f, 0.24f), 0.1f, 0.85f, true);
            Material reflective = LitMaterial("Reflective Shell Metal", new Color(0.8f, 0.9f, 0.96f), 0.93f, 0.94f, false);
            Material reflectiveGrey = LitMaterial("Reflective Shell Grey", new Color(0.75f, 0.75f, 0.75f), 0.93f, 0.94f, false);

            GameObject keyholeBase = GetOrCreatePrefab("Shell Keyhole Base", () => MakeKeyhole(stoneGrey, stoneColor, slotGrey, darkSlot, pearls[0]));
            GameObject keyBase = GetOrCreatePrefab("Shell Key Base", () => MakeKey(shellGrey, beige, pearls[0]));
            for (int i = 0; i < 4; i++)
            {
                int pair = i + 1;
                GetOrCreateVariant($"Shell Keyhole {pair:00}", keyholeBase, root =>
                {
                    SetIdentity(root, ShellPuzzlePiece.PieceKind.Keyhole, pair, FitDiameters[i], PearlColors[i]);
                    SetCylinderDiameter(root.transform.Find("Slot Recess"), FitDiameters[i]);
                    SetCylinderDiameter(root.transform.Find("Slot Rim"), FitDiameters[i] + 0.16f);
                    SetAppearance(root.transform.Find("Slot Rim").GetComponent<Renderer>(), slotGrey, pearls[i]);
                });
                GetOrCreateVariant($"Shell Key {pair:00}", keyBase, root =>
                {
                    SetIdentity(root, ShellPuzzlePiece.PieceKind.Key, pair, FitDiameters[i], PearlColors[i]);
                    float width = FitDiameters[i] - SlotClearance;
                    root.transform.localScale = Vector3.one * (width / BaseKeyWidth);
                    Transform pearl = root.transform.Find("Color Pearl");
                    SetAppearance(pearl.GetComponent<Renderer>(), shellGrey, pearls[i]);
                });
            }

            GetOrCreatePrefab("Clam Casing", () => MakeSphere("Clam Casing", 2.6f, casingGrey, casing));
            GetOrCreatePrefab("Reflective Shell", () => MakeSphere("Reflective Shell", 0.48f, reflectiveGrey, reflective));

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Created 4 matched shell keyhole/key prefab pairs, a transparent clam casing, and a reflective shell. No stage or scene positions were changed; matching gameplay is not connected.");
        }

        private static GameObject MakeKeyhole(Material grey, Material color, Material slotGrey, Material slotColor, Material rimColor)
        {
            GameObject root = new GameObject("Shell Keyhole Base");
            root.AddComponent<ShellPuzzlePiece>();
            Primitive("Stone Pillar", PrimitiveType.Cylinder, root.transform,
                new Vector3(0f, PillarHeight * 0.5f, 0f),
                new Vector3(PillarDiameter, PillarHeight * 0.5f, PillarDiameter), grey, color);
            Primitive("Slot Recess", PrimitiveType.Cylinder, root.transform,
                new Vector3(0f, PillarHeight + 0.009f, 0f), new Vector3(0.42f, 0.009f, 0.42f), slotGrey, slotColor);
            Primitive("Slot Rim", PrimitiveType.Cylinder, root.transform,
                new Vector3(0f, PillarHeight + 0.004f, 0f), new Vector3(0.58f, 0.004f, 0.58f), slotGrey, rimColor);
            // This is a visible inset disc, not yet a cut mesh or functional socket.
            return root;
        }

        private static GameObject MakeKey(Material grey, Material beige, Material pearl)
        {
            GameObject root = new GameObject("Shell Key Base");
            root.AddComponent<ShellPuzzlePiece>();
            Primitive("Beige Cube", PrimitiveType.Cube, root.transform,
                new Vector3(0f, 0.025f, 0f), new Vector3(BaseKeyWidth, 0.05f, BaseKeyWidth), grey, beige);
            Primitive("Color Pearl", PrimitiveType.Sphere, root.transform,
                new Vector3(0f, 0.155f, 0f), Vector3.one * 0.2f, grey, pearl);
            return root;
        }

        private static GameObject MakeSphere(string name, float diameter, Material grey, Material color)
        {
            GameObject root = new GameObject(name);
            Primitive(name + " Visual", PrimitiveType.Sphere, root.transform, Vector3.up * (diameter * 0.5f),
                Vector3.one * diameter, grey, color);
            return root;
        }

        private static GameObject Primitive(string name, PrimitiveType type, Transform parent, Vector3 position,
            Vector3 scale, Material grey, Material color)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            SetAppearance(go.GetComponent<Renderer>(), grey, color);
            return go;
        }

        private static void SetAppearance(Renderer renderer, Material grey, Material color)
        {
            renderer.sharedMaterial = grey;
            DualViewMaterial appearance = renderer.GetComponent<DualViewMaterial>();
            if (appearance == null) appearance = renderer.gameObject.AddComponent<DualViewMaterial>();
            SerializedObject serialized = new SerializedObject(appearance);
            SetMaterial(serialized.FindProperty("greyscaleMaterials"), grey);
            SetMaterial(serialized.FindProperty("colorMaterials"), color);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetMaterial(SerializedProperty array, Material material)
        {
            array.arraySize = 1;
            array.GetArrayElementAtIndex(0).objectReferenceValue = material;
        }

        private static void SetCylinderDiameter(Transform cylinder, float diameter)
        {
            Vector3 scale = cylinder.localScale;
            cylinder.localScale = new Vector3(diameter, scale.y, diameter);
        }

        private static void SetIdentity(GameObject root, ShellPuzzlePiece.PieceKind kind, int pair,
            float fitDiameter, Color color)
        {
            SerializedObject serialized = new SerializedObject(root.GetComponent<ShellPuzzlePiece>());
            serialized.FindProperty("kind").enumValueIndex = (int)kind;
            serialized.FindProperty("pairNumber").intValue = pair;
            serialized.FindProperty("fitDiameter").floatValue = fitDiameter;
            serialized.FindProperty("pearlColor").colorValue = color;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject GetOrCreatePrefab(string name, Func<GameObject> create)
        {
            string path = $"{PrefabFolder}/{name}.prefab";
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;
            GameObject temporary = create();
            try { return PrefabUtility.SaveAsPrefabAsset(temporary, path); }
            finally { UnityEngine.Object.DestroyImmediate(temporary); }
        }

        private static GameObject GetOrCreateVariant(string name, GameObject basePrefab, Action<GameObject> configure)
        {
            string path = $"{PrefabFolder}/{name}.prefab";
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab);
            try
            {
                instance.name = name;
                configure(instance);
                return PrefabUtility.SaveAsPrefabAsset(instance, path);
            }
            finally { UnityEngine.Object.DestroyImmediate(instance); }
        }

        private static Material CelMaterial(string name, Color color)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            Material source = RequiredMaterial("Assets/ColorPuzzle/Materials/Underwater/M_Cel_Environment_Base.mat");
            Material material = new Material(source) { name = name };
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static Material LitMaterial(string name, Color color, float metallic, float smoothness, bool transparent)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("URP/Lit shader is missing.");
            Material material = new Material(shader) { name = name };
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", smoothness);
            if (transparent)
            {
                material.SetFloat("_Surface", 1f);
                material.SetFloat("_Blend", 0f);
                material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_ZWrite", 0f);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.SetOverrideTag("RenderType", "Transparent");
                material.renderQueue = 3000;
            }
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static Material RequiredMaterial(string path) =>
            AssetDatabase.LoadAssetAtPath<Material>(path) ?? throw new InvalidOperationException($"Missing material: {path}");

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = path.Substring(0, path.LastIndexOf('/'));
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(path.LastIndexOf('/') + 1));
        }
    }
}
#endif
