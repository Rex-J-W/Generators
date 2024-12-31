using UnityEngine;

/// <summary>
/// Represents a raymarched game object
/// </summary>
public abstract class RaymarchedGameObject : MonoBehaviour
{
    /// <summary>
    /// Defines raymarched solid types (Union currently does not work)
    /// </summary>
    public enum SolidType
    {
        Add = 0, Remove = 1, Union = 2
    }

    /// <summary>
    /// Color of the object
    /// </summary>
    [ColorUsage(showAlpha: false)]
    public Color color = Color.white;

    /// <summary>
    /// The type of solid this is;
    /// </summary>
    public SolidType solidType; 

    /// <summary>
    /// Does this object repeat infinitely;
    /// </summary>
    public bool repeating;

    /// <summary>
    /// How frequently the object repeats
    /// </summary>
    public Vector3 repetitionSize = Vector3.one * 5f;

    /// <summary>
    /// Gets the data for raymarching this object
    /// </summary>
    /// <returns>Raymarching object data</returns>
    public abstract RaymarchedObj GetObjectData();
}
