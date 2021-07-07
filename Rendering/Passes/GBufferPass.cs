using Unity.Collections;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class GBufferPass : ScriptableRenderPass
{
    const string ALBEDO_HANDLE_ID = "_AlbedoTexture";
    const string SPECULAR_HANDLE_ID = "_SpecularTexture";
    const string WORLD_POSITION_HANDLE_ID = "_WorldPositionsTexture";
    const string DEPTH_NORMAL_HANDLE_ID = "_DepthNormalsTexture";

    const string ALBEDO_ID = "_AlbedoTexture";
    const string SPECULAR_ID = "_SpecularTexture";
    const string WORLD_POSITIONS_ID = "_WorldPositionsTexture";
    const string DEPTH_NORMAL_ID = "_DepthNormalsTexture";

    static readonly ShaderTagId gBufferShaderTag = new ShaderTagId("R_GBuffer");

    readonly RenderTargetHandle albedoHandle;
    readonly RenderTargetHandle specularHandle;
    readonly RenderTargetHandle worldPosHandle;
    readonly RenderTargetHandle depthNormalHandle;

    FilteringSettings filteringSettings;
    RenderStateBlock renderStateBlock;

    RenderTargetIdentifier colorTarget;

    DeferredLightsFeature.Settings _settings;

    public ref readonly RenderTargetHandle AlbedoHandle => ref albedoHandle;
    public ref readonly RenderTargetHandle SpecularHandle => ref specularHandle;
    public ref readonly RenderTargetHandle WorldPosHandle => ref worldPosHandle;
    public ref readonly RenderTargetHandle DepthNormalHandle => ref depthNormalHandle;

    public GBufferPass(DeferredLightsFeature.Settings settings)
    {
        _settings = settings;

        renderPassEvent = RenderPassEvent.AfterRenderingPrePasses;

        filteringSettings = new FilteringSettings(RenderQueueRange.opaque);
        renderStateBlock = new RenderStateBlock(RenderStateMask.Nothing);

        albedoHandle.Init(ALBEDO_HANDLE_ID);
        specularHandle.Init(SPECULAR_HANDLE_ID);
        worldPosHandle.Init(WORLD_POSITION_HANDLE_ID);
        depthNormalHandle.Init(DEPTH_NORMAL_HANDLE_ID);
    }

    public void Setup(RenderTargetIdentifier colorTarget)
    {
        this.colorTarget = colorTarget;
    }

    public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
    {
        int width = (int)((float)cameraTextureDescriptor.width * _settings.ResolutionMultiplier);
        int height = (int)((float)cameraTextureDescriptor.height * _settings.ResolutionMultiplier);

        // ALBEDO
        {
            var rtd = cameraTextureDescriptor;
            rtd.graphicsFormat = QualitySettings.activeColorSpace == ColorSpace.Linear ? GraphicsFormat.R8G8B8A8_SRGB : GraphicsFormat.R8G8B8A8_UNorm;
            rtd.width = width;
            rtd.height = height;
            rtd.depthBufferBits = 0;
            rtd.msaaSamples = 1;
            cmd.GetTemporaryRT(albedoHandle.id, rtd, FilterMode.Bilinear);

            cmd.SetComputeTextureParam(ComputeShaderUtils.LightsCompute, ComputeShaderUtils.LightsComputeKernels.ComputeLightsKernelID, ALBEDO_ID, albedoHandle.Identifier());
        }

        // SPECULAR
        {
            var rtd = cameraTextureDescriptor;
            rtd.graphicsFormat = GraphicsFormat.R8G8B8A8_UNorm;
            rtd.width = width;
            rtd.height = height;
            rtd.depthBufferBits = 0;
            rtd.msaaSamples = 1;
            cmd.GetTemporaryRT(specularHandle.id, rtd, FilterMode.Bilinear);
            
            cmd.SetComputeTextureParam(ComputeShaderUtils.LightsCompute, ComputeShaderUtils.LightsComputeKernels.ComputeLightsKernelID, SPECULAR_ID, specularHandle.Identifier());
        }

        // WORLD_POS
        {
            var rtd = cameraTextureDescriptor;
            rtd.graphicsFormat = GraphicsFormat.R32G32B32A32_SFloat;
            rtd.depthBufferBits = 0;
            rtd.msaaSamples = 1;
            rtd.width = width;
            rtd.height = height;
            cmd.GetTemporaryRT(worldPosHandle.id, rtd, FilterMode.Bilinear);

            cmd.SetComputeTextureParam(ComputeShaderUtils.TilesCompute, ComputeShaderUtils.TilesComputeKernels.ComputeLightTilesKernelID, WORLD_POSITIONS_ID, worldPosHandle.Identifier());
            cmd.SetComputeTextureParam(ComputeShaderUtils.LightsCompute, ComputeShaderUtils.LightsComputeKernels.ComputeLightsKernelID, WORLD_POSITIONS_ID, worldPosHandle.Identifier());
        }

        // DEPTH_NORMAL
        {
            var rtd = cameraTextureDescriptor;
            rtd.graphicsFormat = GraphicsFormat.R8G8B8A8_UNorm;
            rtd.depthBufferBits = 0;
            rtd.msaaSamples = 1;
            rtd.width = width;
            rtd.height = height;
            cmd.GetTemporaryRT(depthNormalHandle.id, rtd, FilterMode.Bilinear);

            cmd.SetComputeTextureParam(ComputeShaderUtils.TilesCompute, ComputeShaderUtils.TilesComputeKernels.ComputeLightTilesKernelID, DEPTH_NORMAL_ID, depthNormalHandle.Identifier());
            cmd.SetComputeTextureParam(ComputeShaderUtils.LightsCompute, ComputeShaderUtils.LightsComputeKernels.ComputeLightsKernelID, DEPTH_NORMAL_ID, depthNormalHandle.Identifier());
        }

        ConfigureTarget(new RenderTargetIdentifier[] {
                albedoHandle.Identifier(),
                specularHandle.Identifier(),
                worldPosHandle.Identifier(),
                depthNormalHandle.Identifier(),
            });
        ConfigureClear(ClearFlag.All, Color.black);
    }

    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        var cmd = CommandBufferPool.Get();
        context.ExecuteCommandBuffer(cmd);
        cmd.Clear();

        using (new ProfilingScope(cmd, new ProfilingSampler("DeferredLights::GBufferPass")))
        {
            var sortFlags = renderingData.cameraData.defaultOpaqueSortFlags;
            var drawSettings = CreateDrawingSettings(gBufferShaderTag, ref renderingData, sortFlags);
            drawSettings.enableDynamicBatching = true;
            drawSettings.enableInstancing = true;

            context.DrawRenderers(renderingData.cullResults, ref drawSettings, ref filteringSettings, ref renderStateBlock);

            cmd.SetGlobalTexture("_DeferredPass_Albedo_Texture", albedoHandle.Identifier());
            cmd.SetGlobalTexture("_DeferredPass_Specular_Texture", specularHandle.Identifier());
            cmd.SetGlobalTexture("_DeferredPass_WorldPosition_Texture", worldPosHandle.Identifier());
            cmd.SetGlobalTexture("_DeferredPass_DepthNormals_Texture", depthNormalHandle.Identifier());
        }

        CoreUtils.SetRenderTarget(cmd, colorTarget, ClearFlag.All, Color.black);

        context.ExecuteCommandBuffer(cmd);
        CommandBufferPool.Release(cmd);
    }

    public override void FrameCleanup(CommandBuffer cmd)
    {
        cmd.ReleaseTemporaryRT(albedoHandle.id);
        cmd.ReleaseTemporaryRT(specularHandle.id);
        cmd.ReleaseTemporaryRT(worldPosHandle.id);
        cmd.ReleaseTemporaryRT(depthNormalHandle.id);
    }
}
