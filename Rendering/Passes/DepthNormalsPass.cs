using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Settings = DeferredLightsFeature.Settings;

class DepthNormalsPass : ScriptableRenderPass
{
    const string DEPTH_NORMAL_ID = "_DepthNormalsTexture";
    const string DEPTH_ID = "_Depth";

    Settings _settings;

    RenderTargetHandle depthHandle;

    ShaderTagId shaderTagId;
    FilteringSettings filteringSettings;
    RenderStateBlock renderStateBlock;

    ComputeShader _lightsCompute;

    public DepthNormalsPass(Settings settings)
    {
        _settings = settings;
        _lightsCompute = ComputeShaderUtils.LightsCompute;

        shaderTagId = new ShaderTagId("DepthNormal");
        renderPassEvent = RenderPassEvent.AfterRenderingPrePasses;

        filteringSettings = new FilteringSettings(RenderQueueRange.opaque);
        renderStateBlock = new RenderStateBlock(RenderStateMask.Nothing);

        depthHandle.Init(DEPTH_ID);
    }

    public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
    {
        int width = (int)((float)cameraTextureDescriptor.width * _settings.ResolutionMultiplier);
        int height = (int)((float)cameraTextureDescriptor.height * _settings.ResolutionMultiplier);

        var depthDescriptor = cameraTextureDescriptor;
        depthDescriptor.graphicsFormat = GraphicsFormat.R8G8B8A8_UNorm;
        depthDescriptor.depthBufferBits = 0;
        depthDescriptor.msaaSamples = 1;
        depthDescriptor.width = width;
        depthDescriptor.height = height;
        cmd.GetTemporaryRT(depthHandle.id, depthDescriptor, FilterMode.Point);

        cmd.SetComputeTextureParam(ComputeShaderUtils.TilesCompute, ComputeShaderUtils.TilesComputeKernels.ComputeLightTilesKernelID, DEPTH_NORMAL_ID, depthHandle.Identifier());
        cmd.SetComputeTextureParam(ComputeShaderUtils.LightsCompute, ComputeShaderUtils.LightsComputeKernels.ComputeLightsKernelID, DEPTH_NORMAL_ID, depthHandle.Identifier());

        ConfigureTarget(depthHandle.Identifier());
        ConfigureClear(ClearFlag.Color, Color.black);
    }

    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        var cmd = CommandBufferPool.Get();
        context.ExecuteCommandBuffer(cmd);
        cmd.Clear();

        using (new ProfilingScope(cmd, new ProfilingSampler("DeferredLightsPass: DepthNormalsPass")))
        {
            var sortFlags = renderingData.cameraData.defaultOpaqueSortFlags;

            var drawSettings = CreateDrawingSettings(shaderTagId, ref renderingData, sortFlags);
            drawSettings.enableDynamicBatching = true;
            drawSettings.enableInstancing = true;

            context.DrawRenderers(renderingData.cullResults, ref drawSettings, ref filteringSettings, ref renderStateBlock);

            cmd.SetGlobalTexture("_DeferredPass_DepthNormals_Texture", depthHandle.Identifier());
        }

        context.ExecuteCommandBuffer(cmd);
        CommandBufferPool.Release(cmd);
    }

    public override void FrameCleanup(CommandBuffer cmd)
    {
        cmd.ReleaseTemporaryRT(depthHandle.id);
    }
}