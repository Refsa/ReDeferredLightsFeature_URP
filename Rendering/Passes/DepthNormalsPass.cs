using UnityEngine;
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

    Material _depthNormalsMaterial;
    ComputeShader _lightsCompute;

    public DepthNormalsPass(Settings settings, Material depthNormalMaterial)
    {
        _settings = settings;
        _lightsCompute = ComputeShaderUtils.LightsCompute;
        _depthNormalsMaterial = depthNormalMaterial;

        shaderTagId = new ShaderTagId("DepthOnly");
        renderPassEvent = RenderPassEvent.AfterRenderingPrePasses;
        filteringSettings = new FilteringSettings(RenderQueueRange.opaque, -1);

        depthHandle.Init(DEPTH_ID);
    }

    public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
    {
        int width = (int)((float)cameraTextureDescriptor.width * _settings.ResolutionMultiplier);
        int height = (int)((float)cameraTextureDescriptor.height * _settings.ResolutionMultiplier);

        var depthDescriptor = cameraTextureDescriptor;
        depthDescriptor.colorFormat = RenderTextureFormat.ARGB32;
        depthDescriptor.depthBufferBits = 32;
        depthDescriptor.msaaSamples = 1;
        depthDescriptor.width = width;
        depthDescriptor.height = height;
        cmd.GetTemporaryRT(depthHandle.id, depthDescriptor, FilterMode.Point);

        ConfigureTarget(depthHandle.Identifier());
        ConfigureClear(ClearFlag.All, Color.black);

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
            drawSettings.perObjectData = PerObjectData.None; 
            drawSettings.overrideMaterial = _depthNormalsMaterial;

            context.DrawRenderers(renderingData.cullResults, ref drawSettings, ref filteringSettings);

            cmd.SetComputeTextureParam(ComputeShaderUtils.TilesCompute, ComputeShaderUtils.TilesComputeKernels.ComputeLightTilesKernelID, DEPTH_NORMAL_ID, depthHandle.Identifier());
            // cmd.SetComputeTextureParam(_lightsCompute, ComputeShaderUtils.LightsComputeKernels.ComputePixelDataKernelID, DEPTH_NORMAL_ID, depthHandle.Identifier());
            cmd.SetComputeTextureParam(_lightsCompute, ComputeShaderUtils.LightsComputeKernels.ComputeLightsKernelID, DEPTH_NORMAL_ID, depthHandle.Identifier());
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