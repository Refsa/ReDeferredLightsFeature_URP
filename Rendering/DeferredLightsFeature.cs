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
        [Tooltip("Multiple Render Targets - More optimized GBuffer pass but support for fewer platforms:\nDX11+, OpenGL 3.2+, OpenGL ES 3+, Metal, Vulkan, PS4/XB1")]
        public bool UseMRT = true;

        [Header("Debug")]
        public DebugMode DebugMode = DebugMode.None;
        public bool DebugModeInSceneView;
        public bool ShowInSceneView = true;
    }

#region Deferred Lights Passes
    DeferredLightsPass lightsPass;
    DeferredTilesPass tilesPass;
    CullLightsHandler cullLightsHandler;
    Material blitLightsMaterial;
#endregion

#region Multi-Pass GBuffer
    DepthNormalsPass depthNormalsPass;
    WorldPositionPass worldPositionPass;
    AlbedoGrabPass albedoGrabPass;
    SpecularGrabPass specularGrabPass;
#endregion

    GBufferPass gBufferPass;

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

        debugMaterial = new Material(Shader.Find("Hidden/DebugGBuffer"));

        cullLightsHandler = new CullLightsHandler();

        lightsPass = new DeferredLightsPass(settings);
        tilesPass = new DeferredTilesPass(settings);

        gBufferPass = new GBufferPass(settings);

        worldPositionPass = new WorldPositionPass(settings);
        depthNormalsPass = new DepthNormalsPass(settings);
        albedoGrabPass = new AlbedoGrabPass(settings);
        specularGrabPass = new SpecularGrabPass(settings);

        debugPass = new DebugPass(settings, debugMaterial);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (error || !settings.DeferredPassOn)
        {
            return;
        }

        // Reinit materials if they are lost in unity internal state change
        {
            if (debugMaterial == null) debugMaterial = new Material(Shader.Find("Hidden/DebugGBuffer"));
            debugPass.SetMaterial(debugMaterial);

            if (blitLightsMaterial == null) blitLightsMaterial = new Material(Shader.Find("Hidden/BlitLights"));
            lightsPass.SetMaterial(blitLightsMaterial);
        }

        cullLightsHandler.CullLights(renderingData.cameraData.camera);

        // GBuffer
        {
            if (settings.UseMRT)
            {
                gBufferPass.Setup(renderer.cameraColorTarget);
                renderer.EnqueuePass(gBufferPass);
            }
            else
            {
                renderer.EnqueuePass(albedoGrabPass);
                renderer.EnqueuePass(depthNormalsPass);
                renderer.EnqueuePass(worldPositionPass);
                renderer.EnqueuePass(specularGrabPass);
            }
        }

        renderer.EnqueuePass(tilesPass);

        lightsPass.Setup(renderer.cameraColorTarget);
        renderer.EnqueuePass(lightsPass);

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


