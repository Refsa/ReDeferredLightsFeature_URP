using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Settings = DeferredLightsFeature.Settings;

class WorldPositionPass : ScriptableRenderPass
{
    const string WORLD_POSITIONS_ID = "_WorldPositionsTexture";
    const string HANDLE_ID = "_WorldPositions";

    Settings _settings;

    RenderTargetHandle wpHandle;
    RenderTextureDescriptor wpDescriptor;

    ShaderTagId shaderTagId;
    FilteringSettings filteringSettings;

    ComputeShader _lightsCompute;
    Material _worldPositionMaterial;

    public WorldPositionPass(Settings settings, Material worldPositionMaterial)
    {
        _settings = settings;
        _lightsCompute = ComputeShaderUtils.LightsCompute;
        _worldPositionMaterial = worldPositionMaterial;

        shaderTagId = new ShaderTagId("DepthOnly");
        renderPassEvent = RenderPassEvent.AfterRenderingPrePasses;
        filteringSettings = new FilteringSettings(RenderQueueRange.opaque, -1);

        wpHandle.Init(HANDLE_ID);
    }

    public void SetMaterial(Material worldPositionMaterial)
    {
        _worldPositionMaterial = worldPositionMaterial;
    }

    public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
    {
        int width = (int)((float)cameraTextureDescriptor.width * _settings.ResolutionMultiplier);
        int height = (int)((float)cameraTextureDescriptor.height * _settings.ResolutionMultiplier);

        wpDescriptor = cameraTextureDescriptor;
        wpDescriptor.colorFormat = RenderTextureFormat.ARGBFloat;
        wpDescriptor.depthBufferBits = 32;
        wpDescriptor.msaaSamples = 1;
        wpDescriptor.width = width;
        wpDescriptor.height = height;
        cmd.GetTemporaryRT(wpHandle.id, wpDescriptor, FilterMode.Point);

        ConfigureTarget(wpHandle.Identifier());
        ConfigureClear(ClearFlag.All, Color.clear);
    }

    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        var cmd = CommandBufferPool.Get();
        context.ExecuteCommandBuffer(cmd);
        cmd.Clear();

        using (new ProfilingScope(cmd, new ProfilingSampler("DeferredLightsPass: WorldPositionPass Render")))
        {
            var sortFlags = renderingData.cameraData.defaultOpaqueSortFlags;

            var drawSettings = CreateDrawingSettings(shaderTagId, ref renderingData, sortFlags);
            drawSettings.perObjectData = PerObjectData.None;
            drawSettings.overrideMaterial = _worldPositionMaterial;

            context.DrawRenderers(renderingData.cullResults, ref drawSettings, ref filteringSettings);

            cmd.SetComputeTextureParam(ComputeShaderUtils.TilesCompute, ComputeShaderUtils.TilesComputeKernels.ComputeLightTilesKernelID, WORLD_POSITIONS_ID, wpHandle.Identifier());
            cmd.SetComputeTextureParam(_lightsCompute, ComputeShaderUtils.LightsComputeKernels.ComputeLightsKernelID, WORLD_POSITIONS_ID, wpHandle.Identifier());
            cmd.SetGlobalTexture("_DeferredPass_WorldPosition_Texture", wpHandle.Identifier());
        }

        context.ExecuteCommandBuffer(cmd);
        CommandBufferPool.Release(cmd);
    }

    public override void FrameCleanup(CommandBuffer cmd)
    {
        cmd.ReleaseTemporaryRT(wpHandle.id);
    }
}