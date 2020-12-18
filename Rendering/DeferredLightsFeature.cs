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

        public bool DeferredPassOn = true;

        [Header("Debug")]
        public DebugMode DebugMode = DebugMode.None;
        public bool DebugModeInSceneView;
        public bool ShowInSceneView = true;
    }

    DeferredLightsPass lightsPass;
    Material blitLightsMaterial;
    DeferredTilesPass tilesPass;

    DepthNormalsPass depthNormalsPass;
    Material depthNormalsMaterial;

    WorldPositionPass worldPositionPass;
    Material worldPositionMaterial;

    AlbedoGrabPass albedoGrabPass;

    SpecularGrabPass specularGrabPass;

    CullLightsHandler cullLightsHandler;

    GBufferPass gBufferPass;

    DebugPass debugPass;
    Material debugMaterial;

    BlitLightsPass blitLightsPass;
    CopyColorPass copyColorPass;


    [SerializeField] Settings settings;

    bool error = false;

    public override void Create()
    {
        error = ComputeShaderUtils.Prepare();
        if (error)
        {
            throw new System.Exception("DeferredLightsFeature setup was not successful");
        }

        depthNormalsMaterial = CoreUtils.CreateEngineMaterial("Hidden/DepthNormal");
        worldPositionMaterial = new Material(Shader.Find("Hidden/WorldPosition"));
        debugMaterial = new Material(Shader.Find("Hidden/DebugGBuffer"));

        cullLightsHandler = new CullLightsHandler();

        lightsPass = new DeferredLightsPass(settings);
        tilesPass = new DeferredTilesPass(settings);

        gBufferPass = new GBufferPass(settings);

        worldPositionPass = new WorldPositionPass(settings, worldPositionMaterial);
        depthNormalsPass = new DepthNormalsPass(settings, depthNormalsMaterial);
        albedoGrabPass = new AlbedoGrabPass(settings);
        specularGrabPass = new SpecularGrabPass(settings);

        blitLightsPass = new BlitLightsPass(settings);
        copyColorPass = new CopyColorPass(settings);

        debugPass = new DebugPass(settings, debugMaterial);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (error)
        {
            return;
        }

        // Reinit materials if they are lost in unity internal state change
        {
            if (debugMaterial == null) debugMaterial = new Material(Shader.Find("Hidden/DebugGBuffer"));
            debugPass.SetMaterial(debugMaterial);

            if (worldPositionMaterial == null) worldPositionMaterial = new Material(Shader.Find("Hidden/WorldPosition"));
            worldPositionPass.SetMaterial(worldPositionMaterial);

            if (depthNormalsMaterial == null) depthNormalsMaterial = new Material(Shader.Find("Hidden/DepthNormal"));
            depthNormalsPass.SetMaterial(depthNormalsMaterial);

            if (blitLightsMaterial == null) blitLightsMaterial = new Material(Shader.Find("Hidden/BlitLights"));
            lightsPass.SetMaterial(blitLightsMaterial);
        }

        cullLightsHandler.CullLights(renderingData.cameraData.camera);

        // GBuffer
        {
            renderer.EnqueuePass(albedoGrabPass);
            renderer.EnqueuePass(depthNormalsPass);
            renderer.EnqueuePass(worldPositionPass);
            renderer.EnqueuePass(specularGrabPass);

            // renderer.EnqueuePass(gBufferPass);
        }

        renderer.EnqueuePass(tilesPass);

        lightsPass.Setup(renderer.cameraColorTarget);
        renderer.EnqueuePass(lightsPass);

        // copyColorPass.Setup(renderer.cameraColorTarget);
        // renderer.EnqueuePass(copyColorPass);

        // blitLightsPass.Setup(renderer.cameraColorTarget, blitLightsMaterial);
        // renderer.EnqueuePass(blitLightsPass);

        debugPass.Setup(renderer.cameraColorTarget);
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


