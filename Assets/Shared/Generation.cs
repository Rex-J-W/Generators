using System.Linq;
using UnityEngine;
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

public interface IInstanceObject
{
    public GameObject GetInstance();
}
