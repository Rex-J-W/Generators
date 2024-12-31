using UnityEditor;
using UnityEngine;

public class CreateRaymarchedObjectsMenu
{
    [MenuItem("GameObject/3D Object/Raymarched Sphere")]
    public static void CreateSphere()
    {
        new GameObject("Raymarched Sphere", typeof(RaymarchedSphere));
    }

    [MenuItem("GameObject/3D Object/Raymarched Box")]
    public static void CreateBox()
    {
        new GameObject("Raymarched Box", typeof(RaymarchedBox));
    }
}
