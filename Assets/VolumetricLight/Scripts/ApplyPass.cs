using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class ApplyPass : ScriptableRenderPass
{
    private readonly string k_BlitAddTag = "VolumetricLight Blit Add RenderPass";

    public VolumtericResolution m_VolumtericResolution;
    public VolumetricLightFeature m_Feature;

    private Material m_ApplyMaterial;
    private RenderTargetIdentifier m_Srouce;
    private RenderTargetHandle m_Dest;
    private CommandBuffer m_ApplyCommand;
    private RenderTargetHandle m_BlurTempTex;
    private RenderTargetHandle m_FullRayMarchTex;
    public ApplyPass()
    {
        renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
        m_ApplyMaterial = CoreUtils.CreateEngineMaterial("Hidden/Apply");
        int downSampleCout = m_VolumtericResolution == VolumtericResolution.Full ? 1 : m_VolumtericResolution == VolumtericResolution.Half ? 2 : 4;
        m_ApplyMaterial.SetInt("_DownSampleCount", downSampleCout);
        m_BlurTempTex.Init("BlurTempTex");
        m_FullRayMarchTex.Init("FullRayMarchTex");
    }

    public void Setup(RenderTargetIdentifier source, RenderTargetHandle dest)
    {
        m_Srouce = source;
        m_Dest = dest;
    }
    public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
    {
        base.Configure(cmd, cameraTextureDescriptor);
        cmd.GetTemporaryRT(m_FullRayMarchTex.id, cameraTextureDescriptor, FilterMode.Bilinear);

        RenderTextureDescriptor descriptor = cameraTextureDescriptor;
        if (m_VolumtericResolution == VolumtericResolution.Half)
        {
            descriptor.width /= 2;
            descriptor.height /= 2;
        }
        else if (m_VolumtericResolution == VolumtericResolution.Quarter) {
            descriptor.width /= 4;
            descriptor.height /= 4;
        }
        cmd.GetTemporaryRT(m_BlurTempTex.id, descriptor, FilterMode.Bilinear);
    }
    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        if (m_ApplyMaterial == null) return;
        m_ApplyCommand = CommandBufferPool.Get(k_BlitAddTag);
        context.ExecuteCommandBuffer(m_ApplyCommand);
        m_ApplyCommand.Clear();
        Render(m_ApplyCommand, ref renderingData);
        context.ExecuteCommandBuffer(m_ApplyCommand);
        CommandBufferPool.Release(m_ApplyCommand);
    }
    void Render(CommandBuffer cmd, ref RenderingData renderingData)
    {
        if (renderingData.cameraData.isSceneViewCamera) return;
        Texture nullTex = null;
        cmd.SetGlobalTexture("_SourceTex", m_Srouce);
        Blit(cmd, nullTex, m_Dest.Identifier(), m_ApplyMaterial);
    }
    public override void FrameCleanup(CommandBuffer cmd)
    {
        if (m_Dest == RenderTargetHandle.CameraTarget)
        {
            cmd.ReleaseTemporaryRT(m_BlurTempTex.id);
            cmd.ReleaseTemporaryRT(m_FullRayMarchTex.id);
        }
    }
}
