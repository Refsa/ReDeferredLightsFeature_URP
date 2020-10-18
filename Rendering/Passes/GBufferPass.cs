using System.Linq;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Settings = DeferredLightsFeature.Settings;

class GBufferPass : ScriptableRenderPass
{
    const string ALBEDO_HANDLE_ID = "_AlbedoTexture";
    const string SPECULAR_HANDLE_ID = "_SpecularTexture";
    const string WORLD_POSITION_HANDLE_ID = "_WorldPositionsTexture";
    const string DEPTH_NORMAL_HANDLE_ID = "_DepthNormalsTexture";

    Settings _settings;

    ShaderTagId shaderTagId;
    FilteringSettings filteringSettings;

    ShaderTagId[] shaderTagIds;
    RenderStateBlock[] renderStateBlocks;

    RenderTargetHandle[] handles;
    RenderTextureDescriptor[] descriptors;
    RenderTargetIdentifier[] identifiers;

    public GBufferPass(Settings settings)
    {
        _settings = settings;

        renderPassEvent = RenderPassEvent.AfterRenderingPrePasses;

        shaderTagId = new ShaderTagId("Refsa/Deferred Lit");
        filteringSettings = new FilteringSettings(RenderQueueRange.opaque, -1);

        shaderTagIds =
            new ShaderTagId[]
            {
                new ShaderTagId("AlbedoPass"),
                new ShaderTagId("SpecularPass"),
                new ShaderTagId("WorldPosition"),
                new ShaderTagId("DepthNormal"),
            };

        renderStateBlocks =
            new RenderStateBlock[]
            {
                new RenderStateBlock(RenderStateMask.Nothing),
                new RenderStateBlock(RenderStateMask.Nothing),
                new RenderStateBlock(RenderStateMask.Nothing),
                new RenderStateBlock(RenderStateMask.Nothing),
            };

        handles = new RenderTargetHandle[4];
        handles[0].Init(ALBEDO_HANDLE_ID);
        handles[1].Init(SPECULAR_HANDLE_ID);
        handles[2].Init(WORLD_POSITION_HANDLE_ID);
        handles[3].Init(DEPTH_NORMAL_HANDLE_ID);

        descriptors = new RenderTextureDescriptor[4];
        identifiers = new RenderTargetIdentifier[4];
    }

    public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
    {
        int width = cameraTextureDescriptor.width;
        int height = cameraTextureDescriptor.height;

        var baseDescriptor = new RenderTextureDescriptor(width, height);
        baseDescriptor.msaaSamples = 1;
        baseDescriptor.depthBufferBits = 0;
        baseDescriptor.enableRandomWrite = true;

        // ### ALBEDO ###
        baseDescriptor.colorFormat = RenderTextureFormat.ARGB32;
        descriptors[0] = baseDescriptor;
        cmd.GetTemporaryRT(handles[0].id, descriptors[0], 0);

        // ### SPECULAR ###
        baseDescriptor.colorFormat = RenderTextureFormat.ARGB32;
        descriptors[1] = baseDescriptor;
        cmd.GetTemporaryRT(handles[1].id, descriptors[1], 0);

        // ### WORLD POSITION ###
        baseDescriptor.colorFormat = RenderTextureFormat.ARGBFloat;
        descriptors[2] = baseDescriptor;
        cmd.GetTemporaryRT(handles[2].id, descriptors[2], 0);

        // ### DEPTH NORMAL ###
        baseDescriptor.colorFormat = RenderTextureFormat.ARGB32;
        descriptors[3] = baseDescriptor;
        cmd.GetTemporaryRT(handles[3].id, descriptors[3], 0);

        for (int i = 0; i < 4; i++)
        {
            identifiers[i] = handles[i].Identifier();
        }

        ConfigureTarget(identifiers);
        ConfigureClear(ClearFlag.Color, Color.white);
    }

    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        var cmd = CommandBufferPool.Get("GBufferPass");
        context.ExecuteCommandBuffer(cmd);
        cmd.Clear();

        using (new ProfilingScope(cmd, new ProfilingSampler("DeferredLightsPass: GBuffer Pass")))
        {
            var sortFlags = renderingData.cameraData.defaultOpaqueSortFlags;

            var drawSettings = CreateDrawingSettings(shaderTagIds.Reverse().ToList(), ref renderingData, sortFlags);

            NativeArray<ShaderTagId> stds = new NativeArray<ShaderTagId>(shaderTagIds, Allocator.Temp);
            NativeArray<RenderStateBlock> rsbs = new NativeArray<RenderStateBlock>(renderStateBlocks, Allocator.Temp);

            context.DrawRenderers(renderingData.cullResults, ref drawSettings, ref filteringSettings);//, stds, rsbs);

            stds.Dispose();
            rsbs.Dispose();

            var lightsCompute = ComputeShaderUtils.LightsCompute;
            cmd.SetComputeTextureParam(lightsCompute, ComputeShaderUtils.LightsComputeKernels.ComputeLightsKernelID, ALBEDO_HANDLE_ID, handles[0].Identifier());
            cmd.SetComputeTextureParam(lightsCompute, ComputeShaderUtils.LightsComputeKernels.ComputeLightsKernelID, SPECULAR_HANDLE_ID, handles[1].Identifier());
            cmd.SetComputeTextureParam(lightsCompute, ComputeShaderUtils.LightsComputeKernels.ComputeLightsKernelID, WORLD_POSITION_HANDLE_ID, handles[2].Identifier());
            cmd.SetComputeTextureParam(lightsCompute, ComputeShaderUtils.LightsComputeKernels.ComputeLightsKernelID, DEPTH_NORMAL_HANDLE_ID, handles[3].Identifier());

            var tilesCompute = ComputeShaderUtils.TilesCompute;
            cmd.SetComputeTextureParam(tilesCompute, ComputeShaderUtils.TilesComputeKernels.ComputeLightTilesKernelID, WORLD_POSITION_HANDLE_ID, handles[2].Identifier());
            cmd.SetComputeTextureParam(tilesCompute, ComputeShaderUtils.TilesComputeKernels.ComputeLightTilesKernelID, DEPTH_NORMAL_HANDLE_ID, handles[3].Identifier());

            cmd.SetGlobalTexture("_DeferredPass_Albedo_Texture", handles[0].Identifier());
            cmd.SetGlobalTexture("_DeferredPass_Specular_Texture", handles[1].Identifier());
            cmd.SetGlobalTexture("_DeferredPass_WorldPosition_Texture", handles[2].Identifier());
            cmd.SetGlobalTexture("_DeferredPass_DepthNormals_Texture", handles[3].Identifier());
        }

        context.ExecuteCommandBuffer(cmd);
        CommandBufferPool.Release(cmd);
    }

    public override void FrameCleanup(CommandBuffer cmd)
    {
        for (int i = 0; i < 4; i++)
        {
            cmd.ReleaseTemporaryRT(handles[i].id);
        }
    }
}