using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class DeferredLightsFeature : ScriptableRendererFeature
{
    public const int MAX_LIGHTS = 1 << 12;

    public enum DebugMode : int { None = 0, Normals = 1, Depth = 2, Positions = 3, Albedo = 4 };

    [System.Serializable]
    public class Settings
    {
        [Range(0.1f, 1f)] public float ResolutionMultiplier = 0.5f;

        [Header("Debug")]
        public DebugMode DebugMode = DebugMode.None;
    }

    public struct LightData
    {
        public Vector3 Position;
        public Vector3 Color;
        public float Intensity;
        public float Range;

        public static int SizeBytes => 32;
    }

    public static int UpsampleOutputKernelID {get; private set;} = -1;
    public static int DownsampleInputKernelID {get; private set;} = -1;
    public static int DownsampleDepthKernelID {get; private set;} = -1;
    public static int ComputeLightsKernelID {get; private set;} = -1;
    public static int BlurLightsKernelID {get; private set;} = -1;

    DeferredLightsPass lightsPass;

    DepthNormalsPass depthNormalsPass;
    Material depthNormalsMaterial;

    WorldPositionPass worldPositionPass;
    Material worldPositionMaterial;

    AlbedoGrabPass albedoGrabPass;
    Material albedoGrabMaterial;

    DebugPass debugPass;
    Material debugMaterial;

    [SerializeField] Settings settings;
    [SerializeField, HideInInspector] ComputeShader lightsCompute;

    ComputeBuffer lightsDataBuffer;

    public override void Create()
    {
        UpsampleOutputKernelID = lightsCompute.FindKernel("UpsampleOutput");
        DownsampleInputKernelID = lightsCompute.FindKernel("DownsampleInput");
        ComputeLightsKernelID = lightsCompute.FindKernel("ComputeLights");
        DownsampleDepthKernelID = lightsCompute.FindKernel("DownsampleDepth");
        BlurLightsKernelID = lightsCompute.FindKernel("BlurLightsTexture");

        depthNormalsMaterial = CoreUtils.CreateEngineMaterial("Hidden/Internal-DepthNormalsTexture"); 
        worldPositionMaterial = new Material(Shader.Find("Hidden/WorldPosition"));
        albedoGrabMaterial = new Material(Shader.Find("Hidden/GrabAlbedo"));
        debugMaterial = new Material(Shader.Find("Hidden/DebugGBuffer"));

        lightsDataBuffer = new ComputeBuffer(DeferredLightsFeature.MAX_LIGHTS, LightData.SizeBytes);

        worldPositionPass = new WorldPositionPass(settings, lightsCompute, worldPositionMaterial);
        lightsPass = new DeferredLightsPass(settings, lightsCompute);
        depthNormalsPass = new DepthNormalsPass(settings, lightsCompute, depthNormalsMaterial);
        // albedoGrabPass = new AlbedoGrabPass(settings, lightsCompute, albedoGrabMaterial);

        debugPass = new DebugPass(settings, debugMaterial); 
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        lightsPass.SetBuffer(ref lightsDataBuffer);
        lightsCompute.SetInt("_DebugMode", (int)settings.DebugMode);

        // renderer.EnqueuePass(albedoGrabPass);
        
        renderer.EnqueuePass(depthNormalsPass);
        renderer.EnqueuePass(worldPositionPass);
        renderer.EnqueuePass(lightsPass);

        renderer.EnqueuePass(debugPass);
    }

    void OnDisable() 
    {
        lightsDataBuffer?.Dispose();    
    }

    void OnEnable() 
    {
        lightsDataBuffer = new ComputeBuffer(DeferredLightsFeature.MAX_LIGHTS, LightData.SizeBytes);
    }
}


