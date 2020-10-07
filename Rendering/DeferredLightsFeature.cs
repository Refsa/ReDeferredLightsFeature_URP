using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class DeferredLightsFeature : ScriptableRendererFeature
{
    public const int MAX_LIGHTS = 1 << 12;

    public enum DebugMode : int { None = 0, Normals = 1, NormalWorld = 6, Depth = 2, Positions = 3, Albedo = 4, Specular = 5, Smoothness = 7 };

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

        lightsDataBuffer = new ComputeBuffer(MAX_LIGHTS, System.Runtime.InteropServices.Marshal.SizeOf<LightData>());
        pixelDataBuffer = new ComputeBuffer(2560*1440, System.Runtime.InteropServices.Marshal.SizeOf<PixelData>());

        worldPositionPass = new WorldPositionPass(settings, worldPositionMaterial);
        lightsPass = new DeferredLightsPass(settings);
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

        lightsPass.SetBuffer(ref lightsDataBuffer, ref pixelDataBuffer);

        renderer.EnqueuePass(albedoGrabPass);
        renderer.EnqueuePass(depthNormalsPass);
        renderer.EnqueuePass(worldPositionPass);
        renderer.EnqueuePass(specularGrabPass);
        renderer.EnqueuePass(lightsPass);

        renderer.EnqueuePass(debugPass);
    }

    void OnDisable()
    {
        lightsDataBuffer?.Dispose();
        pixelDataBuffer?.Dispose();
    }

    void OnEnable()
    {
        if (error) return;
        lightsDataBuffer = new ComputeBuffer(MAX_LIGHTS, System.Runtime.InteropServices.Marshal.SizeOf<LightData>());
        pixelDataBuffer = new ComputeBuffer(2560*1440, System.Runtime.InteropServices.Marshal.SizeOf<PixelData>());
    }

    bool PrepareCompute(string path, ref ComputeShader field)
    {
        if (field == null)
        {
            field = Resources.Load<ComputeShader>(path);
        }
        if (field == null)
        {
            UnityEngine.Debug.LogError($"Could not find compute shader at path: {path}");
            return false;
        }

        return true;
    }
}


