using UnityEngine;

/// <summary>
/// Represents a raymarched game object
/// </summary>
public abstract class RaymarchedGameObject : MonoBehaviour
{
    /// <summary>
    /// Gets the data for raymarching this object
    /// </summary>
    /// <returns>Raymarching object data</returns>
    public abstract RaymarchedObj GetObjectData();
}
