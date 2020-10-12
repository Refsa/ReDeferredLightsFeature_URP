using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class DeferredLightsFeature : ScriptableRendererFeature
{
    public const int MAX_LIGHTS = 1 << 16;

    public enum DebugMode : int { None = 0, Normals = 1, NormalWorld = 6, Depth = 2, Positions = 3, Albedo = 4, Specular = 5, Smoothness = 7, TileData = 8 };

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
        public Vector2 Attenuation;
        public float RangeSqr;
    }

    public struct PixelData
    {
        public Vector3 Diffuse;
        public Vector3 Normal;
        public float Depth;
        public Vector3 ViewDir;
        public Vector3 Position;
        public Vector3 Specular;
        public float Roughness;
        public float Roughness2;
        public float Roughness2MinusOne;
        public float NormalizationTerm;
    };

    DeferredLightsPass lightsPass;
    DeferredTilesPass tilesPass;

    DepthNormalsPass depthNormalsPass;
    Material depthNormalsMaterial;

    WorldPositionPass worldPositionPass;
    Material worldPositionMaterial;

    AlbedoGrabPass albedoGrabPass;

    SpecularGrabPass specularGrabPass;

    DebugPass debugPass;
    Material debugMaterial;

    [SerializeField] Settings settings;

    ComputeBuffer lightsDataBuffer;
    ComputeBuffer pixelDataBuffer;
    DeferredTilesPass.DeferredTilesBuffers deferredTilesBuffers;

    bool error = false;

    public override void Create()
    {
        error = ComputeShaderUtils.Prepare();
        if (error)
        {
            throw new System.Exception("DeferredLightsFeature setup was not successful");
        }

        depthNormalsMaterial = CoreUtils.CreateEngineMaterial("Hidden/Internal-DepthNormalsTexture");
        worldPositionMaterial = new Material(Shader.Find("Hidden/WorldPosition"));
        debugMaterial = new Material(Shader.Find("Hidden/DebugGBuffer"));

        // lightsDataBuffer = new ComputeBuffer(MAX_LIGHTS, System.Runtime.InteropServices.Marshal.SizeOf<LightData>());
        // pixelDataBuffer = new ComputeBuffer(2560*1440, System.Runtime.InteropServices.Marshal.SizeOf<PixelData>());

        lightsPass = new DeferredLightsPass(settings);
        tilesPass = new DeferredTilesPass(settings);

        worldPositionPass = new WorldPositionPass(settings, worldPositionMaterial);
        depthNormalsPass = new DepthNormalsPass(settings, depthNormalsMaterial);
        albedoGrabPass = new AlbedoGrabPass(settings);
        specularGrabPass = new SpecularGrabPass(settings);

        debugPass = new DebugPass(settings, debugMaterial);
    }
 
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (error)
        {
            return;
        }

        if (lightsDataBuffer == null)
        {
            lightsDataBuffer = new ComputeBuffer(MAX_LIGHTS, System.Runtime.InteropServices.Marshal.SizeOf<LightData>());
            lightsDataBuffer.name = "LightsDataBuffer";
        }
        if (pixelDataBuffer == null)
        {
            pixelDataBuffer = new ComputeBuffer(2560*1440, System.Runtime.InteropServices.Marshal.SizeOf<PixelData>());
            pixelDataBuffer.name = "PixelDataBuffer";
        }
        if (deferredTilesBuffers == null) deferredTilesBuffers = new DeferredTilesPass.DeferredTilesBuffers();

        tilesPass.SetBuffers(ref lightsDataBuffer, ref deferredTilesBuffers);
        lightsPass.SetBuffers(ref lightsDataBuffer, ref pixelDataBuffer);
        lightsPass.PrepareLightDataBuffer();

        renderer.EnqueuePass(albedoGrabPass);
        renderer.EnqueuePass(depthNormalsPass);
        renderer.EnqueuePass(worldPositionPass);
        renderer.EnqueuePass(specularGrabPass);

        renderer.EnqueuePass(tilesPass);
        renderer.EnqueuePass(lightsPass);

        renderer.EnqueuePass(debugPass);
    }
 
    void OnDisable()
    {
        lightsDataBuffer?.Release();
        pixelDataBuffer?.Release();
        deferredTilesBuffers?.Dispose();
    }

    void OnEnable()
    {
        if (error) return;
        lightsDataBuffer?.Release();
        pixelDataBuffer?.Release();
        deferredTilesBuffers?.Dispose();
    }
}


