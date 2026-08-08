using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace FalsePositive.Rendering
{
    /// <summary>
    /// URP RenderGraph replacement for the discontinued "Drunk Color Pulse" PPv2
    /// asset (deleted from Assets/DrunkColorPulse/ -- see
    /// Shaders/DrunkColorPulse.shader's header for why the port was needed at
    /// all). Registered in Assets/Settings/PC_Renderer.asset's renderer feature
    /// list, same place ScreenSpaceAmbientOcclusion lives.
    ///
    /// This project runs URP with RenderGraph active (Unity 6000.5, no
    /// compatibility-mode override in UniversalRenderPipelineGlobalSettings) --
    /// a pass that only implements the legacy Execute(CommandBuffer) silently
    /// never runs under RenderGraph, so this only implements RecordRenderGraph.
    ///
    /// DrunkEffectController.CurrentSettings is read fresh every frame rather
    /// than pushed in -- see that class for why. When nothing is active the pass
    /// is skipped entirely in AddRenderPasses, before it costs a single texture
    /// allocation.
    /// </summary>
    public sealed class DrunkColorPulseFeature : ScriptableRendererFeature
    {
        [SerializeField] private Shader shader;

        private Material _material;
        private DrunkColorPulsePass _pass;

        public override void Create()
        {
            if (shader != null) _material = CoreUtils.CreateEngineMaterial(shader);
            _pass = new DrunkColorPulsePass(_material)
            {
                renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing
            };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (_material == null) return;
            if (!DrunkEffectController.CurrentSettings.IsActive) return;
            renderer.EnqueuePass(_pass);
        }

        protected override void Dispose(bool disposing)
        {
            _pass?.Dispose();
            CoreUtils.Destroy(_material);
        }

        private sealed class DrunkColorPulsePass : ScriptableRenderPass
        {
            private static readonly int OverlayColorId = Shader.PropertyToID("_OverlayColor");
            private static readonly int PulseEnabledId = Shader.PropertyToID("_PulseEnabled");
            private static readonly int PulseSpeedId = Shader.PropertyToID("_PulseSpeed");
            private static readonly int OverlayIntensityId = Shader.PropertyToID("_OverLayMaxIntensity");
            private static readonly int TrailBlurId = Shader.PropertyToID("_TrailBlurStrength");
            private static readonly int PrevFrameTexId = Shader.PropertyToID("_PrevFrameTex");

            private readonly Material _material;
            private RTHandle _prevFrame;

            public DrunkColorPulsePass(Material material)
            {
                _material = material;
                profilingSampler = new ProfilingSampler("Drunk Color Pulse");
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();

                // Fixed-size persistent handle (not render-graph-managed) so the
                // feedback buffer survives across frames -- a render-graph
                // CreateTexture() is only alive for the one frame that recorded it.
                // ReAllocateHandleIfNeeded asserts exactly one of
                // graphicsFormat/depthStencilFormat is set (a colour-only
                // descriptor) -- cameraTargetDescriptor carries both, so it has
                // to be stripped down to colour-only here or the assert fails
                // every frame and the whole render graph execution aborts.
                RenderTextureDescriptor prevFrameDesc = cameraData.cameraTargetDescriptor;
                prevFrameDesc.depthBufferBits = 0;
                prevFrameDesc.depthStencilFormat = GraphicsFormat.None;
                prevFrameDesc.msaaSamples = 1;
                RenderingUtils.ReAllocateHandleIfNeeded(ref _prevFrame, prevFrameDesc,
                    FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_DrunkColorPulsePrevFrame");
                TextureHandle prevFrameHandle = renderGraph.ImportTexture(_prevFrame);

                TextureHandle source = resourceData.activeColorTexture;
                TextureDesc compositeDesc = renderGraph.GetTextureDesc(source);
                compositeDesc.name = "_DrunkColorPulseComposite";
                compositeDesc.clearBuffer = false;
                TextureHandle composite = renderGraph.CreateTexture(compositeDesc);

                DrunkEffectController.Settings settings = DrunkEffectController.CurrentSettings;

                using (var builder = renderGraph.AddRasterRenderPass<PassData>("Drunk Color Pulse Composite",
                           out PassData passData, profilingSampler))
                {
                    passData.material = _material;
                    passData.source = source;
                    passData.prevFrame = prevFrameHandle;
                    passData.settings = settings;

                    builder.UseTexture(passData.source, AccessFlags.Read);
                    builder.UseTexture(passData.prevFrame, AccessFlags.Read);
                    builder.SetRenderAttachment(composite, 0, AccessFlags.Write);

                    builder.SetRenderFunc(static (PassData data, RasterGraphContext ctx) =>
                    {
                        data.material.SetColor(OverlayColorId, data.settings.overlayColor);
                        data.material.SetInteger(PulseEnabledId, data.settings.pulseEnabled ? 1 : 0);
                        data.material.SetFloat(PulseSpeedId, data.settings.pulseSpeed);
                        data.material.SetFloat(OverlayIntensityId, data.settings.overlayIntensity);
                        data.material.SetFloat(TrailBlurId, data.settings.trailBlurStrength);
                        data.material.SetTexture(PrevFrameTexId, (RTHandle)data.prevFrame);

                        Blitter.BlitTexture(ctx.cmd, (RTHandle)data.source, new Vector4(1f, 1f, 0f, 0f), data.material, 0);
                    });
                }

                // Present the composite to the camera target, then copy the same
                // result into the feedback buffer for next frame's trail blend --
                // mirrors the PPv2 original's own Render(): one BlitFullscreenTriangle
                // to destination, one Blit(null, rtid) to refresh the feedback copy.
                renderGraph.AddBlitPass(composite, source, Vector2.one, Vector2.zero,
                    passName: "Drunk Color Pulse Present");
                renderGraph.AddBlitPass(composite, prevFrameHandle, Vector2.one, Vector2.zero,
                    passName: "Drunk Color Pulse Feedback Copy");
            }

            public void Dispose()
            {
                _prevFrame?.Release();
                _prevFrame = null;
            }

            private class PassData
            {
                internal Material material;
                internal TextureHandle source;
                internal TextureHandle prevFrame;
                internal DrunkEffectController.Settings settings;
            }
        }
    }
}
