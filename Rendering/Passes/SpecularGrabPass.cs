using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Settings = DeferredLightsFeature.Settings;

class SpecularGrabPass : ScriptableRenderPass
{
    const string Specular_ID = "_SpecularTexture";

    static readonly ShaderTagId ShaderTagMeta = new ShaderTagId("SpecularPass");

    RenderTargetHandle specularHandle;

    FilteringSettings filteringSettings;
    RenderStateBlock renderStateBlock;

    Settings _settings;
    ComputeShader _lightsCompute;

    public SpecularGrabPass(Settings settings)
    {
        _settings = settings;
        _lightsCompute = ComputeShaderUtils.LightsCompute;

        renderPassEvent = RenderPassEvent.AfterRenderingPrePasses;
        filteringSettings = new FilteringSettings(RenderQueueRange.opaque);

        renderStateBlock = new RenderStateBlock(RenderStateMask.Nothing);

        specularHandle.Init(Specular_ID);
    }

    public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
    {
        int width = (int)((float)cameraTextureDescriptor.width * _settings.ResolutionMultiplier);
        int height = (int)((float)cameraTextureDescriptor.height * _settings.ResolutionMultiplier);

        var rtd = cameraTextureDescriptor;
        rtd.graphicsFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R8G8B8A8_UNorm;
        rtd.width = width;
        rtd.height = height;
        rtd.depthBufferBits = 24;
        rtd.msaaSamples = 1;
        rtd.enableRandomWrite = true;
        cmd.GetTemporaryRT(specularHandle.id, rtd, FilterMode.Point);

        ConfigureTarget(specularHandle.Identifier());
        ConfigureClear(ClearFlag.All, Color.black);
    }

    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        var cmd = CommandBufferPool.Get();
        context.ExecuteCommandBuffer(cmd);
        cmd.Clear();

        using (new ProfilingScope(cmd, new ProfilingSampler("DeferredLightsPass: Grab Specular")))
        {
            var sortFlags = renderingData.cameraData.defaultOpaqueSortFlags;
            var drawSettings = CreateDrawingSettings(ShaderTagMeta, ref renderingData, sortFlags);
            drawSettings.enableDynamicBatching = true;
            drawSettings.enableInstancing = true;

            // CoreUtils.SetRenderTarget(cmd, specularHandle.Identifier(), ClearFlag.None, Color.clear);
            context.DrawRenderers(renderingData.cullResults, ref drawSettings, ref filteringSettings, ref renderStateBlock);

            cmd.SetComputeTextureParam(_lightsCompute, ComputeShaderUtils.LightsComputeKernels.ComputeLightsKernelID, Specular_ID, specularHandle.Identifier());
            cmd.SetGlobalTexture("_DeferredPass_Specular_Texture", specularHandle.Identifier());
        }

        context.ExecuteCommandBuffer(cmd);
        CommandBufferPool.Release(cmd);
    }

    public override void FrameCleanup(CommandBuffer cmd)
    {
        cmd.ReleaseTemporaryRT(specularHandle.id);
    }
}