using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering;
using UnityEngine;

public enum VolumtericResolution {
    Full,
    Half,
    Quarter
}

public class VolumetricLightFeature : ScriptableRendererFeature
{
    [System.Serializable, ReloadGroup]
    public class Settings
    {
        public VolumtericResolution resolution;
        [Range(0.0f, 1.0f)]
        public float FogHeightCoef = 0.0f;
        [Range(0.0f, 1.0f)]
        public float ShadowProjCoef = 0.03f;
        [Range(0.0f, 1.0f)]
        public float DensityCoef = 0.15f;
        [Range(0.0f, 1.0f)]
        public float ExtinctionCoef = 0.18f;
        [Range(1, 64)]
        public int SampleCount = 6;
        [Range(0.0f, 0.999f)]
        public float MieG = 0.32f;
    }
    public Settings setting = new Settings();
    private RenderTargetHandle dest;


    public SampleDepthTexPass m_SampleDepthTexPass { get; private set; }
    public RayMarchLightPass m_RayMarchLightPass { get; private set; }
    public ApplyPass m_BlitAddPass { get; private set; }


    public override void Create()
    {
        //renderPass = new VolumetricLightsRenderPass();

        m_SampleDepthTexPass = new SampleDepthTexPass();
        m_SampleDepthTexPass.m_VolumtericResolution = setting.resolution;

        m_RayMarchLightPass = new RayMarchLightPass();
        m_RayMarchLightPass.m_VolumtericResolution = setting.resolution;
        m_RayMarchLightPass.m_Feature = this;

        m_BlitAddPass = new ApplyPass();
        m_BlitAddPass.m_VolumtericResolution = setting.resolution;
        m_BlitAddPass.m_Feature = this;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        var src = renderer.cameraColorTarget;
        dest = RenderTargetHandle.CameraTarget;

        m_SampleDepthTexPass.Setup(src, dest);
        renderer.EnqueuePass(m_SampleDepthTexPass);

        m_RayMarchLightPass.Setup(src, dest, setting);
        renderer.EnqueuePass(m_RayMarchLightPass);

        m_BlitAddPass.Setup(src, dest);
        renderer.EnqueuePass(m_BlitAddPass);
    }
}
