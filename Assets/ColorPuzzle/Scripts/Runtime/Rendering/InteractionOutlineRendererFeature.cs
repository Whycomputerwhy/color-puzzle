using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace ColorPuzzle.Rendering
{
    /// <summary>
    /// Screen-space soft outline adapted from Unity Technologies' Unity 6
    /// Per-Object Outline RenderGraph Renderer Feature example (Apache-2.0).
    /// </summary>
    public sealed class InteractionOutlineRendererFeature : ScriptableRendererFeature
    {
        [SerializeField] private RenderPassEvent renderEvent = RenderPassEvent.AfterRenderingTransparents;

        private Material maskMaterial;
        private Material greyscaleDilationMaterial;
        private Material colorDilationMaterial;
        private InteractionOutlinePass outlinePass;

        public override void Create()
        {
            name = "Interaction Soft Outline";
            maskMaterial = CreateMaterial("Shaders/InteractionOutlineMask", "Interaction Outline Mask");
            greyscaleDilationMaterial = CreateMaterial(
                "Shaders/InteractionOutlineDilation",
                "Interaction Outline Greyscale Dilation");
            colorDilationMaterial = CreateMaterial(
                "Shaders/InteractionOutlineDilation",
                "Interaction Outline Color Dilation");
            outlinePass = new InteractionOutlinePass
            {
                renderPassEvent = renderEvent,
                requiresIntermediateTexture = true
            };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (outlinePass == null || maskMaterial == null ||
                greyscaleDilationMaterial == null || colorDilationMaterial == null ||
                !InteractionOutlineRuntime.HasVisibleTarget)
                return;

            outlinePass.renderPassEvent = renderEvent;
            outlinePass.MaskMaterial = maskMaterial;
            outlinePass.GreyscaleDilationMaterial = greyscaleDilationMaterial;
            outlinePass.ColorDilationMaterial = colorDilationMaterial;
            renderer.EnqueuePass(outlinePass);
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(maskMaterial);
            CoreUtils.Destroy(greyscaleDilationMaterial);
            CoreUtils.Destroy(colorDilationMaterial);
            maskMaterial = null;
            greyscaleDilationMaterial = null;
            colorDilationMaterial = null;
        }

        private static Material CreateMaterial(string resourcePath, string materialName)
        {
            Shader shader = Resources.Load<Shader>(resourcePath);
            if (shader == null)
            {
                Debug.LogWarning($"Missing interaction outline shader at Resources/{resourcePath}.");
                return null;
            }

            Material material = CoreUtils.CreateEngineMaterial(shader);
            material.name = materialName;
            return material;
        }

        private sealed class InteractionOutlinePass : ScriptableRenderPass
        {
            private const string MaskTextureName = "_InteractionOutlineMaskTexture";
            private const string DilationTextureName = "_InteractionOutlineDilationTexture";
            private static readonly int SpreadId = Shader.PropertyToID("_Spread");
            private static readonly int SoftnessId = Shader.PropertyToID("_Softness");
            private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
            private static readonly int OpacityId = Shader.PropertyToID("_Opacity");

            public Material MaskMaterial { private get; set; }
            public Material GreyscaleDilationMaterial { private get; set; }
            public Material ColorDilationMaterial { private get; set; }

            private RenderTextureDescriptor outlineDescriptor;

            public InteractionOutlinePass()
            {
                outlineDescriptor = new RenderTextureDescriptor(
                    Screen.width,
                    Screen.height,
                    RenderTextureFormat.ARGB32,
                    0)
                {
                    msaaSamples = 1
                };
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                Renderer targetRenderer = InteractionOutlineRuntime.TargetRenderer;
                if (resourceData.isActiveTargetBackBuffer || targetRenderer == null)
                    return;

                TextureHandle screenColor = resourceData.activeColorTexture;
                TextureHandle screenDepthStencil = resourceData.activeDepthTexture;
                if (!screenColor.IsValid() || !screenDepthStencil.IsValid())
                    return;

                outlineDescriptor.width = cameraData.cameraTargetDescriptor.width;
                outlineDescriptor.height = cameraData.cameraTargetDescriptor.height;
                outlineDescriptor.graphicsFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R8G8B8A8_UNorm;
                outlineDescriptor.depthStencilFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.None;
                outlineDescriptor.msaaSamples = 1;

                TextureHandle maskTexture = UniversalRenderer.CreateRenderGraphTexture(
                    renderGraph,
                    outlineDescriptor,
                    MaskTextureName,
                    true);
                TextureHandle dilationTexture = UniversalRenderer.CreateRenderGraphTexture(
                    renderGraph,
                    outlineDescriptor,
                    DilationTextureName,
                    true);
                if (!maskTexture.IsValid() || !dilationTexture.IsValid())
                    return;

                bool isColorCamera = InteractionOutlineRuntime.IsColorCamera(cameraData.camera);
                Material dilationMaterial = isColorCamera
                    ? ColorDilationMaterial
                    : GreyscaleDilationMaterial;
                Color outlineColor = isColorCamera
                    ? InteractionOutlineRuntime.ColorViewColor
                    : InteractionOutlineRuntime.GreyscaleColor;
                float viewScale = isColorCamera ? InteractionOutlineRuntime.ColorViewScale : 1f;
                dilationMaterial.SetInteger(
                    SpreadId,
                    Mathf.Max(1, Mathf.RoundToInt(InteractionOutlineRuntime.WidthPixels * viewScale)));
                dilationMaterial.SetFloat(SoftnessId, InteractionOutlineRuntime.Softness);
                dilationMaterial.SetColor(OutlineColorId, outlineColor);
                dilationMaterial.SetFloat(
                    OpacityId,
                    InteractionOutlineRuntime.Opacity * Mathf.Clamp01(outlineColor.a) * viewScale);

                using (var builder = renderGraph.AddRasterRenderPass<DrawMaskPassData>(
                           "Interaction Outline: Draw Object Mask",
                           out DrawMaskPassData passData))
                {
                    passData.Renderer = targetRenderer;
                    passData.Material = MaskMaterial;
                    builder.SetRenderAttachment(maskTexture, 0);
                    // The stencil records the real object silhouette. The mask shader uses
                    // ZTest Always so nearby scenery cannot chop the interaction cue apart.
                    builder.SetRenderAttachmentDepth(screenDepthStencil, AccessFlags.ReadWrite);
                    builder.SetRenderFunc((DrawMaskPassData data, RasterGraphContext context) =>
                        DrawObjectMask(data, context));
                }

                using (var builder = renderGraph.AddRasterRenderPass<BlitPassData>(
                           "Interaction Outline: Horizontal Soft Expansion",
                           out BlitPassData passData))
                {
                    passData.Source = maskTexture;
                    passData.Material = dilationMaterial;
                    passData.PassIndex = 0;
                    builder.UseTexture(maskTexture);
                    builder.SetRenderAttachment(dilationTexture, 0);
                    builder.SetRenderFunc((BlitPassData data, RasterGraphContext context) =>
                        Blit(data, context));
                }

                using (var builder = renderGraph.AddRasterRenderPass<BlitPassData>(
                           "Interaction Outline: Vertical Soften + Composite",
                           out BlitPassData passData))
                {
                    passData.Source = dilationTexture;
                    passData.Material = dilationMaterial;
                    passData.PassIndex = 1;
                    builder.UseTexture(dilationTexture);
                    builder.SetRenderAttachment(screenColor, 0);
                    builder.SetRenderAttachmentDepth(screenDepthStencil, AccessFlags.ReadWrite);
                    builder.SetRenderFunc((BlitPassData data, RasterGraphContext context) =>
                        Blit(data, context));
                }
            }

            private static void DrawObjectMask(DrawMaskPassData data, RasterGraphContext context)
            {
                if (data.Renderer == null) return;
                int materialCount = data.Renderer.sharedMaterials.Length;
                for (int materialIndex = 0; materialIndex < materialCount; materialIndex++)
                    context.cmd.DrawRenderer(data.Renderer, data.Material, materialIndex, 0);
            }

            private static void Blit(BlitPassData data, RasterGraphContext context)
            {
                Blitter.BlitTexture(
                    context.cmd,
                    data.Source,
                    new Vector4(1f, 1f, 0f, 0f),
                    data.Material,
                    data.PassIndex);
            }

            private sealed class DrawMaskPassData
            {
                internal Renderer Renderer;
                internal Material Material;
            }

            private sealed class BlitPassData
            {
                internal TextureHandle Source;
                internal Material Material;
                internal int PassIndex;
            }
        }
    }
}
