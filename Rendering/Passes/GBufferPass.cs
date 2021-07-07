using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class GBufferPass : ScriptableRenderPass
{
    const string ALBEDO_HANDLE_ID = "_AlbedoTexture";
    const string SPECULAR_HANDLE_ID = "_SpecularTexture";
    const string WORLD_POSITION_HANDLE_ID = "_WorldPositionsTexture";
    const string DEPTH_NORMAL_HANDLE_ID = "_DepthNormalsTexture";
    const string DEPTH_TEXTURE_ID = "_DepthTexture";

    const string ALBEDO_ID = "_AlbedoTexture";
    const string SPECULAR_ID = "_SpecularTexture";
    const string WORLD_POSITIONS_ID = "_WorldPositionsTexture";
    const string DEPTH_NORMAL_ID = "_DepthNormalsTexture";

    static readonly ShaderTagId shaderTagLit = new ShaderTagId("Lit");
    static readonly ShaderTagId shaderTagSimpleLit = new ShaderTagId("SimpleLit");
    static readonly ShaderTagId shaderTagUnlit = new ShaderTagId("Unlit");
    static readonly ShaderTagId gBufferShaderTag = new ShaderTagId("R_GBuffer");

    readonly RenderTargetHandle albedoHandle;
    readonly RenderTargetHandle specularHandle;
    readonly RenderTargetHandle worldPosHandle;
    readonly RenderTargetHandle depthNormalHandle;

    FilteringSettings filteringSettings;
    RenderStateBlock renderStateBlock;

    RenderTargetIdentifier colorTarget;

    public ref readonly RenderTargetHandle AlbedoHandle => ref albedoHandle;
    public ref readonly RenderTargetHandle SpecularHandle => ref specularHandle;
    public ref readonly RenderTargetHandle WorldPosHandle => ref worldPosHandle;
    public ref readonly RenderTargetHandle DepthNormalHandle => ref depthNormalHandle;

    public GBufferPass()
    {
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
        int width = cameraTextureDescriptor.width;
        int height = cameraTextureDescriptor.height;

        // ALBEDO
        {
            var rtd = cameraTextureDescriptor;
            rtd.colorFormat = RenderTextureFormat.ARGB32;
            rtd.width = width;
            rtd.height = height;
            rtd.depthBufferBits = 0;
            rtd.msaaSamples = 1;
            cmd.GetTemporaryRT(albedoHandle.id, rtd, FilterMode.Point);

            cmd.SetComputeTextureParam(ComputeShaderUtils.LightsCompute, ComputeShaderUtils.LightsComputeKernels.ComputeLightsKernelID, ALBEDO_ID, albedoHandle.Identifier());
        }

        // SPECULAR
        {
            var rtd = cameraTextureDescriptor;
            rtd.colorFormat = RenderTextureFormat.ARGB32;
            rtd.width = width;
            rtd.height = height;
            rtd.depthBufferBits = 0;
            rtd.msaaSamples = 1;
            cmd.GetTemporaryRT(specularHandle.id, rtd, FilterMode.Point);
            
            cmd.SetComputeTextureParam(ComputeShaderUtils.LightsCompute, ComputeShaderUtils.LightsComputeKernels.ComputeLightsKernelID, SPECULAR_ID, specularHandle.Identifier());
        }

        // WORLD_POS
        {
            var rtd = cameraTextureDescriptor;
            rtd.colorFormat = RenderTextureFormat.ARGBFloat;
            rtd.depthBufferBits = 0;
            rtd.msaaSamples = 1;
            rtd.width = width;
            rtd.height = height;
            cmd.GetTemporaryRT(worldPosHandle.id, rtd, FilterMode.Point);

            cmd.SetComputeTextureParam(ComputeShaderUtils.TilesCompute, ComputeShaderUtils.TilesComputeKernels.ComputeLightTilesKernelID, WORLD_POSITIONS_ID, worldPosHandle.Identifier());
            cmd.SetComputeTextureParam(ComputeShaderUtils.LightsCompute, ComputeShaderUtils.LightsComputeKernels.ComputeLightsKernelID, WORLD_POSITIONS_ID, worldPosHandle.Identifier());
        }

        // DEPTH_NORMAL
        {
            var rtd = cameraTextureDescriptor;
            rtd.colorFormat = RenderTextureFormat.ARGB32;
            rtd.depthBufferBits = 24;
            rtd.msaaSamples = 1;
            rtd.width = width;
            rtd.height = height;
            cmd.GetTemporaryRT(depthNormalHandle.id, rtd, FilterMode.Point);

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
