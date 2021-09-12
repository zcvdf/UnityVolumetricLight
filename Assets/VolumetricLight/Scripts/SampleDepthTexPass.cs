using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine;
public class SampleDepthTexPass : ScriptableRenderPass
{
    private readonly string k_SampleDepthTexTag = "VolumetricLight Sample Camera DepthTex RenderPass";

    private Material m_SampleDepthTexMaterial;
    private Material m_DownSampleMaterial;
    public VolumtericResolution m_VolumtericResolution;
    private CommandBuffer m_SampleDepthTexCommand;
    private RenderTargetIdentifier m_Srouce;
    private RenderTargetHandle m_Dest;
    private RenderTargetHandle m_FullCameraDepthTexture;
    private RenderTargetHandle m_HalfCameraDepthTexture;
    private RenderTargetHandle m_QuarterCameraDepthTexture;

    public RenderTargetHandle CurrentCameraDepthTexture {
        get {
            if (m_VolumtericResolution == VolumtericResolution.Half)
            {
                return m_HalfCameraDepthTexture;
            }
            else if (m_VolumtericResolution == VolumtericResolution.Quarter)
            {
                return m_QuarterCameraDepthTexture;
            }
            else {
                return m_FullCameraDepthTexture;
            }
        }
    }

    public SampleDepthTexPass() {
        renderPassEvent = RenderPassEvent.AfterRenderingOpaques+1;
        m_FullCameraDepthTexture.Init("FullCameraDepthTexture");
        m_HalfCameraDepthTexture.Init("HalfCameraDepthTexture");
        m_QuarterCameraDepthTexture.Init("QuarterCameraDepthTexture");
        m_SampleDepthTexMaterial = CoreUtils.CreateEngineMaterial("Hidden/SampleDepthTex");
        m_DownSampleMaterial = CoreUtils.CreateEngineMaterial("Hidden/Universal Render Pipeline/Sampling");
    }

    public void Setup(RenderTargetIdentifier source,RenderTargetHandle dest) {
        m_Srouce = source;
        m_Dest = dest;
    }
    public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
    {
        base.Configure(cmd, cameraTextureDescriptor);
        RenderTextureDescriptor descriptor = cameraTextureDescriptor;
        cmd.GetTemporaryRT(m_FullCameraDepthTexture.id, descriptor, FilterMode.Bilinear);
        if (m_VolumtericResolution == VolumtericResolution.Half || m_VolumtericResolution == VolumtericResolution.Quarter)
        {
            descriptor.height /= 2;
            descriptor.width /= 2;
            cmd.GetTemporaryRT(m_HalfCameraDepthTexture.id, descriptor, FilterMode.Bilinear);
            if (m_VolumtericResolution == VolumtericResolution.Quarter)
            {
                descriptor.height /= 2;
                descriptor.width /= 2;
                cmd.GetTemporaryRT(m_QuarterCameraDepthTexture.id, descriptor, FilterMode.Bilinear);
            }
        }
    }
    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        if (m_SampleDepthTexMaterial == null || m_DownSampleMaterial == null) return;
        m_SampleDepthTexCommand = CommandBufferPool.Get(k_SampleDepthTexTag);
        context.ExecuteCommandBuffer(m_SampleDepthTexCommand);
        m_SampleDepthTexCommand.Clear();
        Render(m_SampleDepthTexCommand, ref renderingData);
        context.ExecuteCommandBuffer(m_SampleDepthTexCommand);
        CommandBufferPool.Release(m_SampleDepthTexCommand);
    }
    void Render(CommandBuffer cmd, ref RenderingData renderingData)
    {
        if (renderingData.cameraData.isSceneViewCamera) return;
        //根据分辨率向下采样
        Texture nullTex = null;
        Blit(cmd, nullTex, m_FullCameraDepthTexture.Identifier(), m_SampleDepthTexMaterial);
        if (m_VolumtericResolution == VolumtericResolution.Half || m_VolumtericResolution == VolumtericResolution.Quarter)
        {
            //Blit第二个参数是为了支持old urp版本，设置_SourceTex是新版本
            //half
            cmd.SetGlobalTexture("_SourceTex", m_FullCameraDepthTexture.Identifier());
            Blit(cmd, m_FullCameraDepthTexture.Identifier(), m_HalfCameraDepthTexture.Identifier(), m_DownSampleMaterial);
            //quarter
            if (m_VolumtericResolution == VolumtericResolution.Quarter) {
                cmd.SetGlobalTexture("_SourceTex", m_HalfCameraDepthTexture.Identifier());
                Blit(cmd, m_HalfCameraDepthTexture.Identifier(), m_QuarterCameraDepthTexture.Identifier(), m_DownSampleMaterial);
            }
        }
        const string texName = "_SampleCameraDepthTexture";
        if (m_VolumtericResolution == VolumtericResolution.Full)
        {
            cmd.SetGlobalTexture(texName, m_FullCameraDepthTexture.Identifier());
        }
        else if (m_VolumtericResolution == VolumtericResolution.Half)
        {
            cmd.SetGlobalTexture(texName, m_HalfCameraDepthTexture.Identifier());
        }
        else if (m_VolumtericResolution == VolumtericResolution.Quarter) {
            cmd.SetGlobalTexture(texName, m_QuarterCameraDepthTexture.Identifier());
        }
    }
    public override void FrameCleanup(CommandBuffer cmd)
    {
        if (m_Dest == RenderTargetHandle.CameraTarget)
        {
            cmd.ReleaseTemporaryRT(m_FullCameraDepthTexture.id);
            cmd.ReleaseTemporaryRT(m_HalfCameraDepthTexture.id);
            cmd.ReleaseTemporaryRT(m_QuarterCameraDepthTexture.id);
        }
    }
}
