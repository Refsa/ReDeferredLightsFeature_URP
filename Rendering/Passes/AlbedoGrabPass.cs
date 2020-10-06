﻿using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Settings = DeferredLightsFeature.Settings;

class AlbedoGrabPass : ScriptableRenderPass
{
    const string ALBEDO_ID = "_AlbedoTexture";

    static readonly ShaderTagId ShaderTagDepthOnly = new ShaderTagId("DepthOnly");
    static readonly ShaderTagId ShaderTagUniversalForward = new ShaderTagId("UniversalForward");

    RenderTargetHandle albedoHandle;

    FilteringSettings filteringSettings;
    RenderStateBlock renderStateBlock;

    Settings _settings;
    ComputeShader _lightsCompute;
    Material _albedoGrabMaterial;

    public AlbedoGrabPass(Settings settings, Material albedoGrabMaterial)
    {
        _settings = settings;
        _lightsCompute = ComputeShaderUtils.LightsCompute;
        _albedoGrabMaterial = albedoGrabMaterial;

        renderPassEvent = RenderPassEvent.BeforeRenderingOpaques;
        filteringSettings = new FilteringSettings(RenderQueueRange.opaque, -1);

        renderStateBlock = new RenderStateBlock(RenderStateMask.Nothing);

        albedoHandle.Init(ALBEDO_ID);
    }

    public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
    {
        int width = (int)((float)cameraTextureDescriptor.width * _settings.ResolutionMultiplier);
        int height = (int)((float)cameraTextureDescriptor.height * _settings.ResolutionMultiplier);

        var rtd = cameraTextureDescriptor;
        rtd.colorFormat = RenderTextureFormat.ARGB32;
        rtd.width = width;
        rtd.height = height;
        rtd.depthBufferBits = 32;
        rtd.msaaSamples = 1;
        rtd.enableRandomWrite = true;
        cmd.GetTemporaryRT(albedoHandle.id, rtd, FilterMode.Point);

        // ComputeShaderUtils.Utils.DispatchClear(cmd, albedoHandle.Identifier(), width / 32, height / 18, Color.black);
        // cmd.Blit(colorAttachment, albedoHandle.Identifier());

        ConfigureTarget(albedoHandle.Identifier());
        ConfigureClear(ClearFlag.Color, Color.black);
    }

    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        var cmd = CommandBufferPool.Get();
        context.ExecuteCommandBuffer(cmd);
        cmd.Clear();

        using (new ProfilingScope(cmd, new ProfilingSampler("DeferredLightsPass: Grab Albedo")))
        {
            var sortFlags = renderingData.cameraData.defaultOpaqueSortFlags;
            var drawSettings = CreateDrawingSettings(ShaderTagUniversalForward, ref renderingData, sortFlags);

            context.DrawRenderers(renderingData.cullResults, ref drawSettings, ref filteringSettings, ref renderStateBlock);

            cmd.SetGlobalTexture("_DeferredPass_Albedo_Texture", albedoHandle.Identifier());
            cmd.SetComputeTextureParam(_lightsCompute, ComputeShaderUtils.LightsComputeKernels.ComputeLightsKernelID, ALBEDO_ID, albedoHandle.Identifier());
        }

        context.ExecuteCommandBuffer(cmd);
        CommandBufferPool.Release(cmd);
    }

    public override void FrameCleanup(CommandBuffer cmd)
    {
        if (albedoHandle != RenderTargetHandle.CameraTarget)
        {
            cmd.ReleaseTemporaryRT(albedoHandle.id);
        }
    }
}