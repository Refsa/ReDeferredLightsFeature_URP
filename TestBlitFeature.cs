using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class TestBlitFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        [System.NonSerialized] public Material Material;
    }

    class CustomRenderPass : ScriptableRenderPass
    {
        Settings settings;
        RenderTargetIdentifier colorTarget;
        RenderTargetHandle blitHandle;

        public CustomRenderPass(Settings settings)
        {
            this.settings = settings;
            renderPassEvent = RenderPassEvent.AfterRendering;

            blitHandle.Init("_BlitTexture");
        }

        public void Setup(RenderTargetIdentifier colorTarget)
        {
            this.colorTarget = colorTarget;
        }

        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            cmd.GetTemporaryRT(blitHandle.id, cameraTextureDescriptor);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            var cmd = CommandBufferPool.Get();
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();

            // if (renderingData.cameraData.isSceneViewCamera)
            {
                cmd.Blit(colorTarget, blitHandle.Identifier(), settings.Material);
                cmd.Blit(blitHandle.Identifier(), colorTarget);
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public override void FrameCleanup(CommandBuffer cmd)
        {
            cmd.ReleaseTemporaryRT(blitHandle.id);
        }
    }

    CustomRenderPass m_ScriptablePass;

    [SerializeField] Settings settings;

    public override void Create()
    {
        settings.Material = new Material(Shader.Find("Unlit/InverseColorShader"));

        m_ScriptablePass = new CustomRenderPass(settings);

        m_ScriptablePass.renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (settings.Material == null)
        {
            settings.Material = new Material(Shader.Find("Unlit/InverseColorShader"));
        }

        m_ScriptablePass.Setup(renderer.cameraColorTarget);
        renderer.EnqueuePass(m_ScriptablePass);
    }
}


