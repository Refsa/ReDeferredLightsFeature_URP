using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Settings = DeferredLightsFeature.Settings;
using LightData = DeferredLightsFeature.LightData;

class DeferredLightsPass : ScriptableRenderPass, System.IDisposable
{
    const string lightsID = "_DeferredLightsTexture";
    const string outputID = "_DeferredOutputTexture";

    const string colorID = "_DeferredColorTexture";
    const string colorFullscreenID = "_DownsampleColorInput";

    const string depthID = "_DepthTexture";
    const string depthFullscreenID = "_DownsampleDepthInput";

    RenderTargetHandle colorHandle;
    RenderTargetHandle lightsHandle;
    RenderTargetHandle outputHandle;
    RenderTargetHandle colorFullscreenHandle;

    RenderTargetHandle depthHandle;
    RenderTargetHandle depthFullscreenHandle;

    ComputeShader _lightsCompute;
    Settings _settings;

    Vector2 passSize;
    Vector2 renderSize;

    ComputeBuffer lightDataBuffer;
    LightData[] lightDatas;

    public DeferredLightsPass(Settings settings, ComputeShader lightsCompute)
    {
        renderPassEvent = RenderPassEvent.AfterRenderingTransparents;

        _settings = settings;
        _lightsCompute = lightsCompute;

        // ### SETUP RENDER TEXTURE HANDLES ###
        {
            outputHandle.Init(outputID);
            lightsHandle.Init(lightsID);

            colorHandle.Init(colorID);
            colorFullscreenHandle.Init(colorFullscreenID);

            depthHandle.Init(depthID);
            depthFullscreenHandle.Init(depthFullscreenID);
        }

        PrepareBuffers();
    }

    public void PrepareBuffers()
    {
        lightDataBuffer = new ComputeBuffer(DeferredLightsFeature.MAX_LIGHTS, LightData.SizeBytes);
        lightDatas = new LightData[DeferredLightsFeature.MAX_LIGHTS];
    }

    public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
    {
        int width = (int)((float)cameraTextureDescriptor.width * _settings.ResolutionMultiplier);
        int height = (int)((float)cameraTextureDescriptor.height * _settings.ResolutionMultiplier);

        passSize = new Vector2(width, height);
        renderSize = new Vector2(cameraTextureDescriptor.width, cameraTextureDescriptor.height);

        cmd.BeginSample("DeferredLightsPass: Setup");

        // Color RT
        {
            RenderTextureDescriptor rtd = new RenderTextureDescriptor(width, height);
            rtd.colorFormat = cameraTextureDescriptor.colorFormat;
            rtd.enableRandomWrite = true;
            cmd.GetTemporaryRT(colorHandle.id, rtd);

            rtd.width = 2048;
            rtd.height = 2048;
            rtd.colorFormat = RenderTextureFormat.DefaultHDR;
            rtd.depthBufferBits = 0;
            cmd.GetTemporaryRT(lightsHandle.id, rtd);
        }

        // Depth RT
        {
            var rtd = cameraTextureDescriptor;
            rtd.colorFormat = RenderTextureFormat.Depth;
            rtd.msaaSamples = 1;
            rtd.depthBufferBits = 32;
            cmd.GetTemporaryRT(depthFullscreenHandle.id, rtd);

            rtd.colorFormat = RenderTextureFormat.R8;
            rtd.depthBufferBits = 32;
            rtd.msaaSamples = 1;
            rtd.enableRandomWrite = true;
            rtd.width = width;
            rtd.height = height;
            cmd.GetTemporaryRT(depthHandle.id, rtd);
        }

        // Full size RT
        {
            var rtd = cameraTextureDescriptor;

            cmd.GetTemporaryRT(colorFullscreenHandle.id, rtd);

            rtd.enableRandomWrite = true;
            cmd.GetTemporaryRT(outputHandle.id, rtd);
        }

        // ### GET ALL LIGHTS IN SCENE ###
        int ldIndex = 0;
        foreach (var ld in GameObject.FindObjectsOfType<DeferredLightsData>())
        {
            lightDatas[ldIndex].Position = ld.transform.position;
            lightDatas[ldIndex].Color = new Vector3(ld.Color.r, ld.Color.g, ld.Color.b);
            lightDatas[ldIndex].Intensity = ld.Intensity;
            lightDatas[ldIndex].Range = ld.Range;

            ldIndex++;
        }

        // UnityEngine.Debug.Log($"{ldIndex}");

        // ### COMPUTE GLOBALS ###
        {
            cmd.SetComputeVectorParam(_lightsCompute, "_InputSize", passSize);
            cmd.SetComputeVectorParam(_lightsCompute, "_OutputSize", renderSize);
            cmd.SetComputeFloatParam(_lightsCompute, "_RenderScale", _settings.ResolutionMultiplier);
        }

        // ### COLOR DOWNSAMPLE ###
        {
            cmd.Blit(colorAttachment, colorFullscreenHandle.Identifier());
            cmd.SetComputeTextureParam(_lightsCompute, DeferredLightsFeature.DownsampleInputKernelID, colorFullscreenID, colorFullscreenHandle.Identifier());
            cmd.DispatchCompute(_lightsCompute, DeferredLightsFeature.DownsampleInputKernelID, (int)renderSize.x / 32, (int)renderSize.y / 32, 1);
        }

        // ### DEPTH DOWNSAMPLE ###
        {
            cmd.Blit(depthAttachment, depthFullscreenHandle.Identifier());
            cmd.SetComputeTextureParam(_lightsCompute, DeferredLightsFeature.DownsampleDepthKernelID, depthFullscreenID, depthFullscreenHandle.Identifier());
            cmd.DispatchCompute(_lightsCompute, DeferredLightsFeature.DownsampleDepthKernelID, (int)renderSize.x / 32, (int)renderSize.y / 18, 1);
        }

        // ### OUTPUT UPSAMPLE ###
        {
            cmd.SetComputeTextureParam(_lightsCompute, DeferredLightsFeature.UpsampleOutputKernelID, colorID, colorHandle.Identifier());
            cmd.SetComputeTextureParam(_lightsCompute, DeferredLightsFeature.UpsampleOutputKernelID, outputID, outputHandle.Identifier());
            cmd.SetComputeTextureParam(_lightsCompute, DeferredLightsFeature.UpsampleOutputKernelID, lightsID, lightsHandle.Identifier());
            cmd.SetComputeTextureParam(_lightsCompute, DeferredLightsFeature.UpsampleOutputKernelID, colorFullscreenID, colorFullscreenHandle.Identifier());
        }

        // ### SETUP LIGHT COMPUTE ###
        {
            lightDataBuffer.SetData(lightDatas, 0, 0, ldIndex);
            cmd.SetComputeIntParam(_lightsCompute, "_LightCount", ldIndex);
            cmd.SetComputeBufferParam(_lightsCompute, DeferredLightsFeature.ComputeLightsKernelID, "_LightData", lightDataBuffer);

            cmd.SetComputeTextureParam(_lightsCompute, DeferredLightsFeature.ComputeLightsKernelID, colorID, colorHandle.Identifier());
            cmd.SetComputeTextureParam(_lightsCompute, DeferredLightsFeature.ComputeLightsKernelID, lightsID, lightsHandle.Identifier());

            cmd.SetComputeTextureParam(_lightsCompute, DeferredLightsFeature.BlurLightsKernelID, lightsID, lightsHandle.Identifier());
        }

        cmd.EndSample("DeferredLightsPass: Setup");
    }

    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        CommandBuffer cmd = CommandBufferPool.Get("DeferredLightsFeature");

        context.ExecuteCommandBuffer(cmd);
        cmd.Clear();

        cmd.BeginSample("DeferredLightsPass: Execute");

        ref CameraData cameraData = ref renderingData.cameraData;

        // ### SET CAMERA DATA ###
        {
            Camera camera = cameraData.camera;
            cmd.SetComputeVectorParam(_lightsCompute, "_CameraPos", camera.transform.position);
            cmd.SetComputeVectorParam(_lightsCompute, "_ProjectionParams", new Vector4(
                1f, camera.nearClipPlane, camera.farClipPlane, 1f / camera.farClipPlane
            ));
        }

        // ### DISPATCH LIGHT AND UPSAMPLE COMPUTE ###
        {
            cmd.BeginSample("DeferredLightsPass: Compute Lights");
            cmd.DispatchCompute(_lightsCompute, DeferredLightsFeature.ComputeLightsKernelID, (int)passSize.x / 32, (int)passSize.y / 18, 1);
            cmd.CreateGraphicsFence(GraphicsFenceType.AsyncQueueSynchronisation, SynchronisationStageFlags.ComputeProcessing);
            cmd.EndSample("DeferredLightsPass: Compute Lights");

            cmd.BeginSample("DeferredLightsPass: Blur Lights");
            cmd.DispatchCompute(_lightsCompute, DeferredLightsFeature.BlurLightsKernelID, (int)passSize.x / 32, (int)passSize.y / 18, 1);
            cmd.CreateGraphicsFence(GraphicsFenceType.AsyncQueueSynchronisation, SynchronisationStageFlags.ComputeProcessing);
            cmd.EndSample("DeferredLightsPass: Blur Lights");

            cmd.BeginSample("DeferredLightsPass: Upsample Output");
            cmd.DispatchCompute(_lightsCompute, DeferredLightsFeature.UpsampleOutputKernelID, (int)passSize.x / 32, (int)passSize.y / 18, 1);
            cmd.CreateGraphicsFence(GraphicsFenceType.AsyncQueueSynchronisation, SynchronisationStageFlags.ComputeProcessing);
            cmd.EndSample("DeferredLightsPass: Upsample Output");
        }

        // ### BLIT RESULTS BACK INTO RENDER BUFFER ###
        RenderTargetIdentifier cameraTarget =
            (cameraData.targetTexture != null) ?
                new RenderTargetIdentifier(cameraData.targetTexture)
                : BuiltinRenderTextureType.CurrentActive;

        if (cameraData.isSceneViewCamera || cameraData.isDefaultViewport)
        {
            cmd.SetRenderTarget(
                BuiltinRenderTextureType.CurrentActive,
                RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store, // color
                RenderBufferLoadAction.DontCare, RenderBufferStoreAction.DontCare); // depth
            cmd.Blit(outputHandle.id, BuiltinRenderTextureType.CurrentActive);
        }
        else
        {
            // CoreUtils.SetRenderTarget(cmd, cameraTarget);
            // CoreUtils.ClearRenderTarget(cmd, ClearFlag.All, Color.black);
// 
            // cmd.Blit(outputHandle.id, cameraTarget);
        }

        cmd.EndSample("DeferredLightsPass: Execute");

        context.ExecuteCommandBuffer(cmd);
        CommandBufferPool.Release(cmd);
    }

    public override void FrameCleanup(CommandBuffer cmd)
    {
        cmd.ReleaseTemporaryRT(outputHandle.id);
        cmd.ReleaseTemporaryRT(lightsHandle.id);

        cmd.ReleaseTemporaryRT(colorHandle.id);
        cmd.ReleaseTemporaryRT(colorFullscreenHandle.id);

        cmd.ReleaseTemporaryRT(depthHandle.id);
        cmd.ReleaseTemporaryRT(depthFullscreenHandle.id);
    }

    public void Dispose()
    {
        CoreUtils.SafeRelease(lightDataBuffer);
    }
}