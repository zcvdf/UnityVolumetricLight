using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class RayMarchLightPass : ScriptableRenderPass
{
    private readonly string k_RayMarchLightTag = "VolumetricLight Ray March Light RenderPass";

    public VolumetricLightFeature m_Feature;
    public VolumtericResolution m_VolumtericResolution;

    private Material m_RayMarchLightMaterial;
    private Material m_BilateralBlurMaterial;

    private RenderTargetIdentifier m_Srouce;
    private RenderTargetHandle m_Dest;
    private RenderTargetHandle m_RayMarchTexture;
    private RenderTargetHandle m_TempBlurTexture_1;
    private RenderTargetHandle m_TempBlurTexture_2;
    private CommandBuffer m_RayMarchLightCommand;

    private VolumetricLightFeature.Settings m_Settings;
    private Vector4[] _frustumCorners = new Vector4[4];

    public RayMarchLightPass()
    {
        renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
        m_RayMarchTexture.Init("RayMarchTexture");
        m_TempBlurTexture_1.Init("TempBlurTexture_1");
        m_TempBlurTexture_2.Init("TempBlurTexture_2");
        m_RayMarchLightMaterial = CoreUtils.CreateEngineMaterial("Hidden/RayMarchLight");
        m_BilateralBlurMaterial = CoreUtils.CreateEngineMaterial("Hidden/BilateralBlur");

    }
    private Camera _mainCamera;
    private Light _mainLight;
    public Camera mainCamera {
        get {
            if (_mainCamera == null) {
                _mainCamera = GameObject.Find("Main Camera").GetComponent<Camera>();
            }
            return _mainCamera;
        }
    }
    public Light mainLight {
        get {
            if (_mainLight == null) {
                _mainLight = GameObject.Find("Directional Light").GetComponent<Light>(); 
            }
            return _mainLight;
        }
    }

    public void Setup(RenderTargetIdentifier source, RenderTargetHandle dest, VolumetricLightFeature.Settings settings)
    {
        m_Srouce = source;
        m_Dest = dest;
        m_Settings = settings;
    }
    public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
    {
        base.Configure(cmd, cameraTextureDescriptor);
        GenerateDitherTexture();
        RenderTextureDescriptor descriptor = cameraTextureDescriptor;
        cmd.GetTemporaryRT(m_RayMarchTexture.id, descriptor, FilterMode.Bilinear);
        if (m_VolumtericResolution == VolumtericResolution.Half)
        {
            descriptor.width /= 2;
            descriptor.height /= 2;
        }
        else if (m_VolumtericResolution == VolumtericResolution.Quarter)
        {
            descriptor.width /= 4;
            descriptor.height /= 4;
        }
        cmd.GetTemporaryRT(m_TempBlurTexture_2.id, descriptor, FilterMode.Bilinear);
        cmd.GetTemporaryRT(m_TempBlurTexture_1.id, descriptor, FilterMode.Bilinear);
        Vector3 camDir = GameObject.Find("Main Camera").transform.forward;
        m_RayMarchLightMaterial.SetVector("_CameraForward", camDir);
        m_RayMarchLightMaterial.SetInt("_SampleCount", m_Settings.SampleCount);
        m_RayMarchLightMaterial.SetFloat("_ShadowProjCoef", m_Settings.ShadowProjCoef);
        m_RayMarchLightMaterial.SetFloat("_DensityCoef", m_Settings.DensityCoef);
        m_RayMarchLightMaterial.SetFloat("_FogHeightCoef", m_Settings.FogHeightCoef);
        m_RayMarchLightMaterial.SetFloat("_ExtinctionCoef", m_Settings.ExtinctionCoef);
        m_RayMarchLightMaterial.SetTexture("_DitherTex", _ditheringTexture);
        m_RayMarchLightMaterial.SetVector("_MieG", new Vector4(1 - (m_Settings.MieG * m_Settings.MieG), 1 + (m_Settings.MieG * m_Settings.MieG), 2 * m_Settings.MieG, 1.0f / (4.0f * Mathf.PI)));
        Light light = mainLight;
        m_RayMarchLightMaterial.SetVector("_LightDir", new Vector4(light.transform.forward.x, light.transform.forward.y, light.transform.forward.z, 1.0f / (light.range * light.range)));

        cmd.SetGlobalTexture("_CameraDepthTexture", m_Feature.m_SampleDepthTexPass.CurrentCameraDepthTexture.Identifier());

        m_RayMarchLightMaterial.SetFloat("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
        Camera cam = mainCamera;
        _frustumCorners[0] = cam.ViewportToWorldPoint(new Vector3(0, 0, cam.farClipPlane));
        // top left
        _frustumCorners[2] = cam.ViewportToWorldPoint(new Vector3(0, 1, cam.farClipPlane));
        // top right
        _frustumCorners[3] = cam.ViewportToWorldPoint(new Vector3(1, 1, cam.farClipPlane));
        // bottom right
        _frustumCorners[1] = cam.ViewportToWorldPoint(new Vector3(1, 0, cam.farClipPlane));

        m_RayMarchLightMaterial.SetVectorArray("_FrustumCorners", _frustumCorners);
    }

    private void ConfigDirectionalLight(CommandBuffer cmd, ref RenderingData renderingData)
    {

        int pass = 0;

        m_RayMarchLightMaterial.SetPass(pass);


        Texture nullTex = null;
        cmd.SetGlobalTexture("_SourceTex", nullTex);
        Blit(cmd, nullTex, m_TempBlurTexture_1.Identifier(), m_RayMarchLightMaterial, pass);
    }
    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        if (m_RayMarchLightMaterial == null) return;
        m_RayMarchLightCommand = CommandBufferPool.Get(k_RayMarchLightTag);
        context.ExecuteCommandBuffer(m_RayMarchLightCommand);
        m_RayMarchLightCommand.Clear();

        ConfigDirectionalLight(m_RayMarchLightCommand, ref renderingData);
        RenderBlur(m_RayMarchLightCommand, ref renderingData);

        context.ExecuteCommandBuffer(m_RayMarchLightCommand);
        CommandBufferPool.Release(m_RayMarchLightCommand);
    }
    private void RenderBlur(CommandBuffer cmd, ref RenderingData renderingData)
    {
        Blit(cmd, m_TempBlurTexture_1.Identifier(), m_TempBlurTexture_2.Identifier(), m_BilateralBlurMaterial, 0);
        Blit(cmd, m_TempBlurTexture_2.Identifier(), m_TempBlurTexture_1.Identifier(), m_BilateralBlurMaterial, 1);
        Blit(cmd, m_TempBlurTexture_1.Identifier(), m_RayMarchTexture.Identifier(), m_BilateralBlurMaterial, 2);
        cmd.SetGlobalTexture("_RayMarchTex", m_RayMarchTexture.Identifier());
    }

    public override void FrameCleanup(CommandBuffer cmd)
    {
        if (m_Dest == RenderTargetHandle.CameraTarget)
        {
            cmd.ReleaseTemporaryRT(m_TempBlurTexture_1.id);
            cmd.ReleaseTemporaryRT(m_TempBlurTexture_2.id);
            cmd.ReleaseTemporaryRT(m_RayMarchTexture.id);
        }
    }
    private Texture2D _ditheringTexture;
    private void GenerateDitherTexture()
    {
        if (_ditheringTexture != null)
        {
            return;
        }

        int size = 8;
        _ditheringTexture = new Texture2D(size, size, TextureFormat.Alpha8, false, true);
        _ditheringTexture.filterMode = FilterMode.Point;
        Color32[] c = new Color32[size * size];

        byte b;
        int i = 0;
        b = (byte)(1.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(49.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(13.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(61.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(4.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(52.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(16.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(64.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);

        b = (byte)(33.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(17.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(45.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(29.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(36.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(20.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(48.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(32.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);

        b = (byte)(9.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(57.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(5.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(53.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(12.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(60.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(8.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(56.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);

        b = (byte)(41.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(25.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(37.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(21.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(44.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(28.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(40.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(24.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);

        b = (byte)(3.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(51.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(15.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(63.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(2.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(50.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(14.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(62.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);

        b = (byte)(35.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(19.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(47.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(31.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(34.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(18.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(46.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(30.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);

        b = (byte)(11.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(59.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(7.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(55.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(10.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(58.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(6.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(54.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);

        b = (byte)(43.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(27.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(39.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(23.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(42.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(26.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(38.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        b = (byte)(22.0f / 65.0f * 255); c[i++] = new Color32(b, b, b, b);
        _ditheringTexture.SetPixels32(c);
        _ditheringTexture.Apply();
    }

}
