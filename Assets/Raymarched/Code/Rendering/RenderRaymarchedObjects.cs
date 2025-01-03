using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using System;

/// <summary>
/// Data used to render raymarched game objects
/// </summary>
public struct RaymarchedObj
{
    public int type;
    public int solidType;
    public Vector3 position;
    public Vector4 rotation;
    public Vector3 scale;
    public Vector3 repeatSize;
    public Vector3 color;
    public float param0;
    public float param1;
    public float param2;
    public float param3;
    public float param4;

    public static int Compare(RaymarchedObj x, RaymarchedObj y)
    {
        if (x.solidType < y.solidType) return -1;
        return x.solidType > y.solidType ? 1 : 0;
    }
}

/// <summary>
/// Custom post process to render raymarched game objects
/// </summary>
[Serializable, VolumeComponentMenu("Rendering/Raymarched Objects")]
public sealed class RenderRaymarchedObjects : CustomPostProcessVolumeComponent, IPostProcessComponent
{
    [Header("Rendering")]
    [Tooltip("Whether raymarched objects are enabled or not")]
    public BoolParameter enabled = new BoolParameter(false);
    public ClampedFloatParameter resolution = new ClampedFloatParameter(1e-05f, 0.0000001f, 0.0001f);
    public MinIntParameter maxIterations = new MinIntParameter(128, 8);
    public MinIntParameter conePlaneDivisions = new MinIntParameter(16, 2);
    [Space(7f)]
    [Header("Lighting")]
    public FloatParameter ambientLight = new FloatParameter(0.1f);
    public MinFloatParameter shadowRayMaxLength = new MinFloatParameter(10f, 0.1f);
    public MinFloatParameter penumbraSize = new MinFloatParameter(0.1f, 0f);
    public MinIntParameter maxLightIterations = new MinIntParameter(128, 8);
    [Space(7f)]
    [Header("Debug")]
    public BoolParameter debugRayCount = new BoolParameter(false);
    public BoolParameter enableConeTracing = new BoolParameter(false);
    public BoolParameter debugObjectArray = new BoolParameter(false);

    private Material overlayMat;
    private ComputeShader raymarchShader;
    private int raymarchKernel;
    private Transform sun;

    private RaymarchedGameObject[] raymarchedObjs;
    private ComputeBuffer objectsBuffer;
    private int prevBufferLength;

    public bool IsActive() => enabled.value;

    // Do not forget to add this post process in the Custom Post Process Orders list (Project Settings > Graphics > HDRP Global Settings).
    public override CustomPostProcessInjectionPoint injectionPoint => CustomPostProcessInjectionPoint.AfterPostProcess;

    private const string kShaderName = "Hidden/Shader/OverlayTextureOnScreen";

    /// <summary>
    /// Runs on shader start
    /// </summary>
    public override void Setup()
    {
        // Load raymarching compute shader

        raymarchShader = Resources.Load<ComputeShader>("RaymarchObjects");
        raymarchKernel = raymarchShader.FindKernel("RenderRaymarch");
        prevBufferLength = 0;

        // Find sun light

        Light[] sceneLights = FindObjectsByType<Light>(FindObjectsSortMode.InstanceID);
        for (int i = 0; i < sceneLights.Length; i++)
        {
            if (sceneLights[i].type == LightType.Directional)
            {
                sun = sceneLights[i].transform;
                break;
            }
        }

        // Load overlay shader

        if (Shader.Find(kShaderName) != null)
            overlayMat = new Material(Shader.Find(kShaderName));
        else
            Debug.LogError($"Unable to find shader '{kShaderName}'. Post Process Volume RenderRaymarchedObjects is unable to load. " +
                $"To fix this, please edit the 'kShaderName' constant in RenderRaymarchedObjcts.cs or change the name of your custom post process shader.");
    }

    public override void Render(CommandBuffer cmd, HDCamera camera, RTHandle source, RTHandle destination)
    {
        if (overlayMat == null)
            return;

        // Get output texture for raymarching

        RenderTexture output = Rendering.GetTempComputeTexture(source);

        // Set debug vars

        raymarchShader.SetInt("debugRayCount", debugRayCount.value ? 1 : 0);
        raymarchShader.SetInt("enableConeTracing", enableConeTracing.value ? 1 : 0);

        // Set camera vars

        Camera cam = camera.camera;
        raymarchShader.SetMatrix("camToWorld", cam.cameraToWorldMatrix);
        raymarchShader.SetMatrix("camInverseProjection", cam.projectionMatrix.inverse);
        raymarchShader.SetFloat("camFarPlane", cam.farClipPlane);
        raymarchShader.SetFloat("camNearPlane", cam.nearClipPlane);
        raymarchShader.SetVector("size", new Vector2(output.width, output.height));
        raymarchShader.SetVector("cameraPos", cam.transform.position);

        // Used for cone-tracing

        raymarchShader.SetFloat("camTanFov", Mathf.Tan(Mathf.Deg2Rad * (cam.fieldOfView / 2f)));
        raymarchShader.SetFloat("camPlaneSubdivisions", conePlaneDivisions.value);

        // Find raymarched objects

        raymarchedObjs = FindObjectsByType<RaymarchedGameObject>(FindObjectsInactive.Exclude, FindObjectsSortMode.InstanceID);
        //if (raymarchedObjs.Length > 0 && prevBufferLength != raymarchedObjs.Length)
        //{
            objectsBuffer?.Release();
            objectsBuffer = new ComputeBuffer(raymarchedObjs.Length, 92);
            prevBufferLength = raymarchedObjs.Length;
        //}

        // Setup raymarching objects

        RaymarchedObj[] raymarchObjData = new RaymarchedObj[raymarchedObjs.Length];
        for (int i = 0; i < raymarchedObjs.Length; i++)
        {
            raymarchObjData[i] = raymarchedObjs[i].GetObjectData();
            raymarchObjData[i].solidType = (int)raymarchedObjs[i].solidType;
            raymarchObjData[i].repeatSize = raymarchedObjs[i].repetitionSize / 2f;
            raymarchObjData[i].color = new Vector3(raymarchedObjs[i].color.r, raymarchedObjs[i].color.g, raymarchedObjs[i].color.b);
            raymarchObjData[i].position = raymarchedObjs[i].transform.position;

            Vector3 scale = raymarchedObjs[i].transform.lossyScale;
            raymarchObjData[i].scale = new Vector3(1f / scale.x, 1f / scale.y, 1f / scale.z);

            Quaternion rot = Quaternion.Inverse(raymarchedObjs[i].transform.rotation);
            raymarchObjData[i].rotation = new Vector4(rot.x, rot.y, rot.z, rot.w);
        }

        // Sort objects by solid type

        Array.Sort(raymarchObjData, RaymarchedObj.Compare);
        objectsBuffer.SetData(raymarchObjData);

        if (debugObjectArray.value) DebugArray(raymarchObjData);

        raymarchShader.SetInt("objectCount", raymarchedObjs.Length);
        raymarchShader.SetBuffer(raymarchKernel, "objects", objectsBuffer);

        // Dispatch raymarching

        raymarchShader.SetVector("sunDir", -sun.forward);
        raymarchShader.SetFloat("ambientLight", ambientLight.value);
        raymarchShader.SetFloat("shadowRayMaxLength", shadowRayMaxLength.value);
        raymarchShader.SetFloat("penumbraSize", penumbraSize.value);
        raymarchShader.SetFloat("resolution", resolution.value);
        raymarchShader.SetInt("maxIterations", maxIterations.value);
        raymarchShader.SetInt("maxLightIterations", maxLightIterations.value);
        raymarchShader.SetTexture(raymarchKernel, "result", output);
        raymarchShader.SetTextureFromGlobal(raymarchKernel, "DepthTexture", "_CameraDepthTexture");
        raymarchShader.Dispatch(raymarchKernel, output.width / 8, output.height / 8, 1);

        // Overlay raymarched texture onto scene

        overlayMat.SetFloat("intensity", 1f);
        overlayMat.SetTexture("mainTex", source);
        overlayMat.SetTexture("overlayTex", output);
        HDUtils.DrawFullScreen(cmd, overlayMat, destination, shaderPassId: 0);

        // Release raymarched output texture

        RenderTexture.ReleaseTemporary(output);
    }

    /// <summary>
    /// Debugs the raymarchedObj array
    /// </summary>
    /// <param name="objs">Debug array</param>
    public void DebugArray(RaymarchedObj[] objs)
    {
        string arr = "";
        for (int i = 0; i < objs.Length; i++)
        {
            arr += " (" + objs[i].type + " : " + objs[i].solidType + ") ";
        }
        Debug.Log(arr);
    }

    /// <summary>
    /// Memory management
    /// </summary>
    public override void Cleanup()
    {
        objectsBuffer?.Release();
        CoreUtils.Destroy(overlayMat);
    }
}
