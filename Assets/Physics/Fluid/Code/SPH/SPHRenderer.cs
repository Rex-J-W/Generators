using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using System;

/// <summary>
/// Renders an SPH fluid volume
/// </summary>
[Serializable, VolumeComponentMenu("Fluid/SPHRenderer")]
public sealed class SPHRenderer : CustomPostProcessVolumeComponent, IPostProcessComponent
{
    public BoolParameter enabled = new BoolParameter(false);
    public MinIntParameter iterations = new MinIntParameter(128, 16);

    private float testRad;
    private Bounds fluidBounds;
    private GraphicsBuffer particleBuffer;
    private Material overlayMat;
    private ComputeShader sphRenderShader;
    private int sphRenderKernel;

    public bool IsActive() => enabled.value;

    // Do not forget to add this post process in the Custom Post Process Orders list (Project Settings > Graphics > HDRP Global Settings).
    public override CustomPostProcessInjectionPoint injectionPoint => CustomPostProcessInjectionPoint.AfterPostProcess;

    const string kShaderName = "Hidden/Shader/OverlayTextureOnScreen";

    public void SetFluidVars(GraphicsBuffer particles, Bounds fluidBounds, float testRadius)
    {
        particleBuffer = particles;
        this.fluidBounds = fluidBounds;
        testRad = testRadius;
    }

    /// <summary>
    /// Initializes this effect
    /// </summary>
    public override void Setup()
    {
        sphRenderShader = Resources.Load<ComputeShader>("SPHRenderShader");
        sphRenderKernel = sphRenderShader.FindKernel("Render");

        if (Shader.Find(kShaderName) != null)
            overlayMat = new Material(Shader.Find(kShaderName));
        else
            Debug.LogError($"Unable to find shader '{kShaderName}'. Post Process Volume SPHRenderer is unable to load. To fix this, " +
                $"please edit the 'kShaderName' constant in SPHRenderer.cs or change the name of your custom post process shader.");
    }

    public override void Render(CommandBuffer cmd, HDCamera cam, RTHandle source, RTHandle destination)
    {
        if (!enabled.value || particleBuffer == null || overlayMat == null)
            return;

        Debug.Log(fluidBounds);
        // Creates a temporary render texture

        RenderTexture output = RenderTexture.GetTemporary(
            source.rt.width - (source.rt.width % 8), source.rt.height - (source.rt.height % 8),
            0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
        output.enableRandomWrite = true;
        output.Create();

        // Setup size parameters

        sphRenderShader.SetVector("size", new Vector2(output.width, output.height));
        sphRenderShader.SetInt("iterations", iterations.value);

        // Setup camera variables

        Camera camera = cam.camera;
        //sphRenderShader.SetFloat("farPlane", camera.farClipPlane);
        sphRenderShader.SetMatrix("camToWorld", camera.cameraToWorldMatrix);
        sphRenderShader.SetMatrix("camInverseProjection", camera.projectionMatrix.inverse);

        // Setup particle variables

        sphRenderShader.SetVector("leftBottomAABB", fluidBounds.min);
        sphRenderShader.SetVector("rightTopAABB", fluidBounds.max);
        sphRenderShader.SetFloat("testRad", testRad);
        sphRenderShader.SetInt("particleCount", particleBuffer.count);

        // Dispatches fluid rendering

        sphRenderShader.SetBuffer(sphRenderKernel, "particleBuffer", particleBuffer);
        sphRenderShader.SetTexture(sphRenderKernel, "result", output);
        sphRenderShader.Dispatch(sphRenderKernel, output.width / 8, output.height / 8, 1);

        // Sends information to overlay shader and renders result

        overlayMat.SetFloat("intensity", 1f);
        overlayMat.SetTexture("mainTex", source);
        overlayMat.SetTexture("overlayTex", output);
        HDUtils.DrawFullScreen(cmd, overlayMat, destination, shaderPassId: 0);

        // Releases the render texture

        RenderTexture.ReleaseTemporary(output);
    }

    public override void Cleanup()
    {
        CoreUtils.Destroy(overlayMat);
    }
}
