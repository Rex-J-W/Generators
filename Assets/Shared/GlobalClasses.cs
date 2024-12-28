using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.VFX;
using Random = Unity.Mathematics.Random;

/// <summary>
/// Functions for generating things procedurally
/// </summary>
public static class Generation
{
    /// <summary>
    /// Gets a new seeded random
    /// </summary>
    /// <param name="seed">Seed for intialization</param>
    /// <returns>Seeded random object</returns>
    public static Random NewRand(uint seed)
    {
        if (seed == 0) seed = 1;
        return new Random(seed);
    }
    
    /// <summary>
    /// Gets a texture of a certain size filled with a color
    /// </summary>
    /// <param name="c">The color to fill</param>
    /// <param name="width">Width of the texture</param>
    /// <param name="height">Height of the texture</param>
    /// <returns>Colored texture</returns>
    public static Texture2D GetColorTexture(Color32 c, int width, int height)
    {
        Texture2D tex = new Texture2D(width, height, TextureFormat.ARGB32, false)
        {
            filterMode = FilterMode.Point
        };

        Color32[] pixels = Enumerable.Repeat(c, width * height).ToArray();
        tex.SetPixels32(pixels);
        tex.Apply();
        return tex;
    }
}

/// <summary>
/// Specifies if an object can create a GameObject instance of itself
/// </summary>
public interface IInstanceObject
{
    /// <summary>
    /// Turn into a physical GameObject
    /// </summary>
    /// <returns>The physical instance of this data</returns>
    public GameObject GetInstance();
}

/// <summary>
/// Functionality for simulation-based objects
/// </summary>
public static class Simulation
{
    /// <summary>
    /// Create a vfx-graph based visualization for a simulation
    /// </summary>
    /// <param name="parent">The parent to attach the vfx object to</param>
    /// <param name="vfxResourcePath">The path in the resource folder to find this vfx asset</param>
    /// <returns>Instatiated visual effect</returns>
    public static VisualEffect CreateVFXForSim(Transform parent, string vfxResourcePath)
    {
        VisualEffectAsset vfxAsset = Resources.Load<VisualEffectAsset>(vfxResourcePath);
        GameObject vfx = new GameObject(parent.gameObject.name + "_vfx");
        vfx.transform.SetParent(parent);
        vfx.AddComponent<VisualEffect>().visualEffectAsset = vfxAsset;
        return vfx.GetComponent<VisualEffect>();
    }

    /// <summary>
    /// Create a fullscreen shader based visualization for a simulation
    /// </summary>
    /// <typeparam name="T">Volume component type to return</typeparam>
    /// <param name="callingObj">The object to attach the volume to</param>
    /// <param name="volumeResourcePath">The path in the resource folder to find the volume profile asset</param>
    /// <returns>Type T volume component of this post process effect</returns>
    public static T CreatePostProcessVolumeForSim<T>(GameObject callingObj, string volumeResourcePath) where T : VolumeComponent
    {
        VolumeProfile profile = Resources.Load<VolumeProfile>(volumeResourcePath);
        Volume rendVol = callingObj.AddComponent<Volume>();
        rendVol.sharedProfile = profile;
        rendVol.isGlobal = true;
        rendVol.priority = 100;
        rendVol.sharedProfile.TryGet(out T postProcessEffect);
        return postProcessEffect;
    }
}
