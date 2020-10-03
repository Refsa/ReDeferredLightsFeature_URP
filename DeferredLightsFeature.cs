using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class DeferredLightsFeature : ScriptableRendererFeature
{
    const int MAX_LIGHTS = 1024;

    enum DebugMode : int { None = 0, Normals = 1, Depth = 2, Positions = 3 };

    [System.Serializable]
    class Settings
    {
        [Range(0.1f, 1f)] public float ResolutionMultiplier = 0.5f;

        [Header("Debug")]
        public DebugMode DebugMode = DebugMode.None;
    }

    struct LightData
    {
        public Vector3 Position;
        public Color Color;
        public float Intensity;
        public float Range;

        public static int SizeBytes => 36;
    }

    class DepthNormalsPass : ScriptableRenderPass
    {
        const string DEPTH_NORMAL_ID = "_DepthNormalsTexture";
        const string DEPTH_ID = "_Depth";

        Settings _settings;

        RenderTargetHandle depthHandle;
        RenderTextureDescriptor depthDescriptor;

        ShaderTagId shaderTagId;
        FilteringSettings filteringSettings;

        Material _depthNormalsMaterial;
        ComputeShader _lightsCompute;

        public DepthNormalsPass(Settings settings, ComputeShader lightsCompute, Material depthNormalMaterial)
        {
            _settings = settings;
            _lightsCompute = lightsCompute;
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

            depthDescriptor = cameraTextureDescriptor;
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

            var sortFlags = renderingData.cameraData.defaultOpaqueSortFlags;

            var drawSettings = CreateDrawingSettings(shaderTagId, ref renderingData, sortFlags);
            drawSettings.perObjectData = PerObjectData.None;
            drawSettings.overrideMaterial = _depthNormalsMaterial;

            context.DrawRenderers(renderingData.cullResults, ref drawSettings, ref filteringSettings);

            cmd.SetComputeTextureParam(_lightsCompute, ComputeLightsKernelID, DEPTH_NORMAL_ID, depthHandle.Identifier());

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public override void FrameCleanup(CommandBuffer cmd)
        {
            if (depthHandle != RenderTargetHandle.CameraTarget)
            {
                cmd.ReleaseTemporaryRT(depthHandle.id);
                // depthHandle = RenderTargetHandle.CameraTarget;
            }
        }
    }

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

        public WorldPositionPass(Settings settings, ComputeShader lightsCompute, Material worldPositionMaterial)
        {
            _settings = settings;
            _lightsCompute = lightsCompute;
            _worldPositionMaterial = worldPositionMaterial;

            shaderTagId = new ShaderTagId("DepthOnly");
            renderPassEvent = RenderPassEvent.AfterRenderingPrePasses;
            filteringSettings = new FilteringSettings(RenderQueueRange.opaque, -1);

            wpHandle.Init(HANDLE_ID);
        }

        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            int width = (int)((float)cameraTextureDescriptor.width * _settings.ResolutionMultiplier);
            int height = (int)((float)cameraTextureDescriptor.height * _settings.ResolutionMultiplier);

            wpDescriptor = cameraTextureDescriptor;
            wpDescriptor.colorFormat = RenderTextureFormat.ARGB32;
            wpDescriptor.depthBufferBits = 0;
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

            var sortFlags = renderingData.cameraData.defaultOpaqueSortFlags;

            var drawSettings = CreateDrawingSettings(shaderTagId, ref renderingData, sortFlags);
            drawSettings.perObjectData = PerObjectData.None;
            drawSettings.overrideMaterial = _worldPositionMaterial;

            context.DrawRenderers(renderingData.cullResults, ref drawSettings, ref filteringSettings);

            cmd.SetComputeTextureParam(_lightsCompute, ComputeLightsKernelID, WORLD_POSITIONS_ID, wpHandle.Identifier());

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public override void FrameCleanup(CommandBuffer cmd)
        {
            if (wpHandle != RenderTargetHandle.CameraTarget)
            {
                cmd.ReleaseTemporaryRT(wpHandle.id);
                // depthHandle = RenderTargetHandle.CameraTarget;
            }
        }
    }

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
            lightDataBuffer = new ComputeBuffer(MAX_LIGHTS, LightData.SizeBytes);
            lightDatas = new LightData[MAX_LIGHTS];
        }

        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            int width = (int)((float)cameraTextureDescriptor.width * _settings.ResolutionMultiplier);
            int height = (int)((float)cameraTextureDescriptor.height * _settings.ResolutionMultiplier);

            passSize = new Vector2(width, height);
            renderSize = new Vector2(cameraTextureDescriptor.width, cameraTextureDescriptor.height);

            // Color RT
            {
                RenderTextureDescriptor rtd = new RenderTextureDescriptor(width, height);
                rtd.colorFormat = cameraTextureDescriptor.colorFormat;
                rtd.enableRandomWrite = true;
                cmd.GetTemporaryRT(colorHandle.id, rtd);

                rtd.colorFormat = RenderTextureFormat.ARGB32;
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

            int ldIndex = 0;
            foreach (var ld in Resources.FindObjectsOfTypeAll<DeferredLightsData>())
            {
                lightDatas[ldIndex].Position = ld.transform.position;
                lightDatas[ldIndex].Color = ld.Color;
                lightDatas[ldIndex].Intensity = ld.Intensity;
                lightDatas[ldIndex].Range = ld.Range;

                ldIndex++;
            }

            // ### COMPUTE GLOBALS ###
            {
                cmd.SetComputeVectorParam(_lightsCompute, "_InputSize", passSize);
                cmd.SetComputeVectorParam(_lightsCompute, "_OutputSize", renderSize);
                cmd.SetComputeFloatParam(_lightsCompute, "_RenderScale", _settings.ResolutionMultiplier);
            }

            // ### COLOR DOWNSAMPLE ###
            {
                cmd.Blit(colorAttachment, colorFullscreenHandle.Identifier());
                cmd.SetComputeTextureParam(_lightsCompute, DownsampleInputKernelID, colorFullscreenID, colorFullscreenHandle.Identifier());
                cmd.DispatchCompute(_lightsCompute, DownsampleInputKernelID, (int)renderSize.x / 32, (int)renderSize.y / 18, 1);
            }

            // ### DEPTH DOWNSAMPLE ###
            {
                cmd.Blit(depthAttachment, depthFullscreenHandle.Identifier());
                cmd.SetComputeTextureParam(_lightsCompute, DownsampleDepthKernelID, depthFullscreenID, depthFullscreenHandle.Identifier());
                cmd.DispatchCompute(_lightsCompute, DownsampleDepthKernelID, (int)renderSize.x / 32, (int)renderSize.y / 18, 1);
            }

            // ### OUTPUT UPSAMPLE ###
            {
                cmd.SetComputeTextureParam(_lightsCompute, UpsampleOutputKernelID, colorID, colorHandle.Identifier());
                cmd.SetComputeTextureParam(_lightsCompute, UpsampleOutputKernelID, outputID, outputHandle.Identifier());
                cmd.SetComputeTextureParam(_lightsCompute, UpsampleOutputKernelID, lightsID, lightsHandle.Identifier());
                cmd.SetComputeTextureParam(_lightsCompute, UpsampleOutputKernelID, colorFullscreenID, colorFullscreenHandle.Identifier());
            }

            // ### COMPUTE LIGHT ###
            {
                lightDataBuffer.SetData(lightDatas, 0, 0, ldIndex);
                cmd.SetComputeIntParam(_lightsCompute, "_LightCount", ldIndex);
                cmd.SetComputeBufferParam(_lightsCompute, ComputeLightsKernelID, "_LightData", lightDataBuffer);

                cmd.SetComputeTextureParam(_lightsCompute, ComputeLightsKernelID, colorID, colorHandle.Identifier());
                cmd.SetComputeTextureParam(_lightsCompute, ComputeLightsKernelID, lightsID, lightsHandle.Identifier());
            }
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            CommandBuffer cmd = CommandBufferPool.Get("DeferredLightsFeature");
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();

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
                cmd.DispatchCompute(_lightsCompute, ComputeLightsKernelID, (int)passSize.x / 32, (int)passSize.y / 18, 1);
                cmd.DispatchCompute(_lightsCompute, UpsampleOutputKernelID, (int)passSize.x / 32, (int)passSize.y / 18, 1);
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
                CoreUtils.SetRenderTarget(cmd, cameraTarget);
                CoreUtils.ClearRenderTarget(cmd, ClearFlag.All, Color.black);

                cmd.Blit(outputHandle.id, cameraTarget);
            }

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

    static int UpsampleOutputKernelID = -1;
    static int DownsampleInputKernelID = -1;
    static int DownsampleDepthKernelID = -1;
    static int ComputeLightsKernelID = -1;

    DeferredLightsPass lightsPass;

    DepthNormalsPass depthNormalsPass;
    Material depthNormalsMaterial;

    WorldPositionPass worldPositionPass;
    Material worldPositionMaterial;

    [SerializeField] Settings settings;
    [SerializeField, HideInInspector] ComputeShader lightsCompute;

    public override void Create()
    {
        UpsampleOutputKernelID = lightsCompute.FindKernel("UpsampleOutput");
        DownsampleInputKernelID = lightsCompute.FindKernel("DownsampleInput");
        ComputeLightsKernelID = lightsCompute.FindKernel("ComputeLights");
        DownsampleDepthKernelID = lightsCompute.FindKernel("DownsampleDepth");

        depthNormalsMaterial = CoreUtils.CreateEngineMaterial("Hidden/Internal-DepthNormalsTexture");
        worldPositionMaterial = CoreUtils.CreateEngineMaterial("Hidden/WorldPosition");

        depthNormalsPass = new DepthNormalsPass(settings, lightsCompute, depthNormalsMaterial);
        worldPositionPass = new WorldPositionPass(settings, lightsCompute, worldPositionMaterial);
        lightsPass = new DeferredLightsPass(settings, lightsCompute);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        lightsCompute.SetInt("_DebugMode", (int)settings.DebugMode);

        renderer.EnqueuePass(depthNormalsPass);
        renderer.EnqueuePass(worldPositionPass);
        renderer.EnqueuePass(lightsPass);
    }

    void OnDestroy()
    {
        lightsPass?.Dispose();
    }

    void OnEnable()
    {
        lightsPass?.PrepareBuffers();
    }

    void OnDisable()
    {
        lightsPass?.Dispose();
    }
}


