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
    public MinIntParameter maxParticlesPerCell = new MinIntParameter(128, 100);
    public MinIntParameter cellsPerSide = new MinIntParameter(128, 64);

    private SPHFluid fluid;
    private Material overlayMat;
    private ComputeShader sphRenderShader;
    private ComputeBuffer cellsBuffer;
    private int sphRenderKernel, clearTexKernel, buildCellsKernel, clearCellsKernel;

    /// <summary>
    /// Checks if this volume is active
    /// </summary>
    /// <returns></returns>
    public bool IsActive() => enabled.value;

    // Do not forget to add this post process in the Custom Post Process Orders list (Project Settings > Graphics > HDRP Global Settings).
    public override CustomPostProcessInjectionPoint injectionPoint => CustomPostProcessInjectionPoint.AfterPostProcess;

    private const string kShaderName = "Hidden/Shader/OverlayTextureOnScreen";

    // NOTE: CANNOT HAVE ANY FUNCTIONS HERE THAT ARE NOT CALLED BY SETUP OR RENDER
    // All functions in custom post processes are 

    /// <summary>
    /// Gets the total number of cells in the buffer
    /// </summary>
    private int TotalCells => cellsPerSide.value * cellsPerSide.value * cellsPerSide.value;

    /// <summary>
    /// Initializes this effect
    /// </summary>
    public override void Setup()
    {
        sphRenderShader = Resources.Load<ComputeShader>("SPHRenderShader");
        sphRenderKernel = sphRenderShader.FindKernel("Render");
        clearTexKernel = sphRenderShader.FindKernel("ClearTexture");
        buildCellsKernel = sphRenderShader.FindKernel("BuildCells");
        clearCellsKernel = sphRenderShader.FindKernel("ClearCells");

        fluid = (SPHFluid)FindFirstObjectByType(typeof(SPHFluid));
        cellsBuffer = new ComputeBuffer(TotalCells * maxParticlesPerCell.value, 4);

        if (Shader.Find(kShaderName) != null)
            overlayMat = new Material(Shader.Find(kShaderName));
        else
            Debug.LogError($"Unable to find shader '{kShaderName}'. Post Process Volume SPHRenderer is unable to load. To fix this, " +
                $"please edit the 'kShaderName' constant in SPHRenderer.cs or change the name of your custom post process shader.");
    }

    public override void Render(CommandBuffer cmd, HDCamera cam, RTHandle source, RTHandle destination)
    {
        if (!enabled.value || fluid.particleBuffer == null || overlayMat == null)
            return;

        // Creates a temporary render texture

        RenderTexture output = Rendering.GetTempComputeTexture(source);

        // Setup size parameters

        sphRenderShader.SetVector("size", new Vector2(output.width, output.height));
        sphRenderShader.SetInt("iterations", 32);

        // Setup camera variables

        Camera camera = cam.camera;
        //sphRenderShader.SetFloat("farPlane", camera.farClipPlane);
        sphRenderShader.SetMatrix("camToWorld", camera.cameraToWorldMatrix);
        sphRenderShader.SetMatrix("camInverseProjection", camera.projectionMatrix.inverse);

        // Setup particle variables

        Bounds fluidBounds = fluid.FluidBounds;
        sphRenderShader.SetFloat("contributionAmount", 1f / 32f);
        sphRenderShader.SetVector("leftBottomAABB", fluidBounds.min);
        sphRenderShader.SetVector("rightTopAABB", fluidBounds.max);
        sphRenderShader.SetFloat("testRad", fluid.particleRad * 100f);
        sphRenderShader.SetInt("particleCount", fluid.TotalParticles);

        // Dispatches cell creation

        Vector3 cellSize = fluidBounds.size / cellsPerSide.value;
        sphRenderShader.SetVector("cellSize", cellSize);
        sphRenderShader.SetInt("cellDimension", cellsPerSide.value);
        sphRenderShader.SetInt("totalCells", cellsBuffer.count);
        sphRenderShader.SetInt("maxCellParticles", maxParticlesPerCell.value);

        sphRenderShader.SetBuffer(clearCellsKernel, "cells", cellsBuffer);
        sphRenderShader.Dispatch(clearCellsKernel, cellsPerSide.value / 8, cellsPerSide.value / 8, cellsPerSide.value / 8);

        sphRenderShader.SetBuffer(buildCellsKernel, "cells", cellsBuffer);
        sphRenderShader.SetBuffer(buildCellsKernel, "particles", fluid.particleBuffer);
        sphRenderShader.Dispatch(buildCellsKernel, fluid.TotalParticles / 256, 1, 1);

        // Dispatches fluid rendering

        sphRenderShader.SetTexture(clearTexKernel, "result", output);
        sphRenderShader.Dispatch(clearTexKernel, output.width / 8, output.height / 8, 1);

        sphRenderShader.SetBuffer(sphRenderKernel, "cells", cellsBuffer);
        sphRenderShader.SetBuffer(sphRenderKernel, "particles", fluid.particleBuffer);
        sphRenderShader.SetTexture(sphRenderKernel, "result", output);
        sphRenderShader.Dispatch(sphRenderKernel, output.width / 8, output.height / 8, 32 / 8);

        // Sends information to overlay shader and renders result

        overlayMat.SetFloat("intensity", 1f);
        overlayMat.SetTexture("mainTex", source);
        overlayMat.SetTexture("overlayTex", output);
        HDUtils.DrawFullScreen(cmd, overlayMat, destination, shaderPassId: 0);

        // Releases the render texture

        RenderTexture.ReleaseTemporary(output);
    }

    /// <summary>
    /// Memory management
    /// </summary>
    public override void Cleanup()
    {
        cellsBuffer.Release();
        CoreUtils.Destroy(overlayMat);
    }
}
