using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class DeferredLightsFeature : ScriptableRendererFeature
{
    public const int MAX_LIGHTS = 1 << 16;

    public enum DebugMode : int { None = 0, Normals = 1, NormalWorld = 6, Depth = 2, Positions = 3, Albedo = 4, Specular = 5, Smoothness = 7, TileData = 8, TileDataOverlay = 9 };

    [System.Serializable]
    public class Settings
    {
        [Range(0.1f, 1f)] public float ResolutionMultiplier = 0.5f;

        [Header("Debug")]
        public DebugMode DebugMode = DebugMode.None;
    }

    DeferredLightsPass lightsPass;
    DeferredTilesPass tilesPass;

    DepthNormalsPass depthNormalsPass;
    Material depthNormalsMaterial;

    WorldPositionPass worldPositionPass;
    Material worldPositionMaterial;

    AlbedoGrabPass albedoGrabPass;

    SpecularGrabPass specularGrabPass;

    CullLightsHandler cullLightsHandler;

    DebugPass debugPass;
    Material debugMaterial;

    [SerializeField] Settings settings;

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

        cullLightsHandler = new CullLightsHandler();

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

        cullLightsHandler.CullLights(renderingData.cameraData.camera);

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
        ShaderData.instance.Dispose();
    }

    void OnValidate() 
    {
        ShaderData.instance.Dispose();
    }
}


