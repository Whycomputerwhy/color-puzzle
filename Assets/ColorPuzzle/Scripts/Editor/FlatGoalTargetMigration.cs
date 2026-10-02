using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ColorPuzzle.Editor
{
    public static class FlatGoalTargetMigration
    {
        // CENTRALIZED FLAT TARGET ART PARAMETERS
        private const string GoalPrefabPath =
            "Assets/ColorPuzzle/Prefabs/Underwater/UnderwaterCylinderGoal.prefab";
        private const string TexturePath =
            "Assets/ColorPuzzle/Generated/UnderwaterGoalTargetFlat.tga";
        private const string LegacyTexturePath =
            "Assets/ColorPuzzle/Generated/UnderwaterGoalTargetFlat.png";
        private const string MaterialPath =
            "Assets/ColorPuzzle/Materials/Underwater/Underwater_GoalTargetFlat.mat";
        private const string FlatTargetName = "PlacementTargetFlatImage";
        private const string OldRedTargetName = "RedHintMark";
        private const string OldWhiteOutlineName = "PlacementTargetWhiteOutline";
        private const int TextureSize = 256;
        private const float OuterRadius = 0.46f;
        private const float InnerRadius = 0.31f;
        private const float EdgeFeatherPixels = 1.25f;
        private const float TargetLocalDiameter = 0.36f;
        private const float TargetLocalHeight = 1.105f;
        private const byte TargetRedR = 217;
        private const byte TargetRedG = 20;
        private const byte TargetRedB = 20;
        private const byte TargetWhite = 255;
        private static readonly Quaternion TargetLocalRotation = Quaternion.Euler(90f, 0f, 0f);

        [MenuItem("Color Puzzle/Migrations/Convert Underwater Goal Target To Flat Image")]
        public static void ConvertUnderwaterGoalTargetToFlatImage()
        {
            Texture2D texture = CreateOrUpdateTexture();
            Material material = CreateOrUpdateMaterial(texture);
            UpdateGoalPrefab(material);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Converted the underwater goal target from stacked cylinders to one flat image.");
        }

        private static Texture2D CreateOrUpdateTexture()
        {
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(LegacyTexturePath) != null)
                AssetDatabase.DeleteAsset(LegacyTexturePath);

            const int headerSize = 18;
            const int bytesPerPixel = 4;
            byte[] tgaBytes = new byte[headerSize + TextureSize * TextureSize * bytesPerPixel];
            tgaBytes[2] = 2; // Uncompressed true-color image.
            tgaBytes[12] = (byte)(TextureSize & 0xff);
            tgaBytes[13] = (byte)(TextureSize >> 8);
            tgaBytes[14] = (byte)(TextureSize & 0xff);
            tgaBytes[15] = (byte)(TextureSize >> 8);
            tgaBytes[16] = 32;
            tgaBytes[17] = 0x28; // Eight alpha bits, top-left origin.

            double feather = EdgeFeatherPixels / TextureSize;
            for (int y = 0; y < TextureSize; y++)
            {
                for (int x = 0; x < TextureSize; x++)
                {
                    double normalizedX = (x + 0.5) / TextureSize - 0.5;
                    double normalizedY = (y + 0.5) / TextureSize - 0.5;
                    double radius = System.Math.Sqrt(
                        normalizedX * normalizedX + normalizedY * normalizedY);
                    double outerAlpha = 1.0 - SmoothStep(
                        OuterRadius - feather,
                        OuterRadius + feather,
                        radius);
                    double whiteRingBlend = SmoothStep(
                        InnerRadius - feather,
                        InnerRadius + feather,
                        radius);
                    int pixelOffset = headerSize + (y * TextureSize + x) * bytesPerPixel;
                    tgaBytes[pixelOffset] = LerpByte(TargetRedB, TargetWhite, whiteRingBlend);
                    tgaBytes[pixelOffset + 1] = LerpByte(TargetRedG, TargetWhite, whiteRingBlend);
                    tgaBytes[pixelOffset + 2] = LerpByte(TargetRedR, TargetWhite, whiteRingBlend);
                    tgaBytes[pixelOffset + 3] = ToByte(outerAlpha);
                }
            }

            Directory.CreateDirectory(Path.GetDirectoryName(TexturePath));
            File.WriteAllBytes(TexturePath, tgaBytes);

            AssetDatabase.ImportAsset(TexturePath, ImportAssetOptions.ForceSynchronousImport);
            TextureImporter importer = AssetImporter.GetAtPath(TexturePath) as TextureImporter;
            if (importer != null)
            {
                importer.alphaIsTransparency = true;
                importer.sRGBTexture = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.mipmapEnabled = false;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
        }

        private static double SmoothStep(double edge0, double edge1, double value)
        {
            double normalized = System.Math.Clamp((value - edge0) / (edge1 - edge0), 0.0, 1.0);
            return normalized * normalized * (3.0 - 2.0 * normalized);
        }

        private static byte LerpByte(byte from, byte to, double amount)
        {
            return (byte)System.Math.Round(from + (to - from) * amount);
        }

        private static byte ToByte(double value)
        {
            return (byte)System.Math.Round(System.Math.Clamp(value, 0.0, 1.0) * 255.0);
        }

        private static Material CreateOrUpdateMaterial(Texture2D texture)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
                throw new System.InvalidOperationException("Unity's Sprites/Default shader is unavailable.");

            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(shader) { name = "Underwater_GoalTargetFlat" };
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            else
            {
                material.shader = shader;
            }

            material.mainTexture = texture;
            material.color = Color.white;
            material.renderQueue = (int)RenderQueue.Transparent;
            material.doubleSidedGI = false;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void UpdateGoalPrefab(Material material)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(GoalPrefabPath);
            try
            {
                RemoveChildIfPresent(root.transform, OldRedTargetName);
                RemoveChildIfPresent(root.transform, OldWhiteOutlineName);

                Transform existingTarget = root.transform.Find(FlatTargetName);
                GameObject target = existingTarget != null
                    ? existingTarget.gameObject
                    : GameObject.CreatePrimitive(PrimitiveType.Quad);
                target.name = FlatTargetName;
                target.layer = root.layer;
                target.transform.SetParent(root.transform, false);
                target.transform.localPosition = new Vector3(0f, TargetLocalHeight, 0f);
                target.transform.localRotation = TargetLocalRotation;
                target.transform.localScale = new Vector3(
                    TargetLocalDiameter,
                    TargetLocalDiameter,
                    1f);

                Collider targetCollider = target.GetComponent<Collider>();
                if (targetCollider != null) Object.DestroyImmediate(targetCollider);

                MeshRenderer renderer = target.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;

                if (PrefabUtility.SaveAsPrefabAsset(root, GoalPrefabPath) == null)
                    throw new System.InvalidOperationException("Unity could not save the underwater goal prefab.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void RemoveChildIfPresent(Transform root, string childName)
        {
            Transform child = root.Find(childName);
            if (child != null) Object.DestroyImmediate(child.gameObject);
        }
    }
}
