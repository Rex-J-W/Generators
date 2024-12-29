using UnityEditor;
using UnityEngine;

public class CreateRaymarchedObjectsMenu
{
    [MenuItem("GameObject/3D Object/Raymarched Sphere")]
    public static void CreateObject()
    {
        GameObject newObj = new GameObject("Raymarched Sphere");
        newObj.AddComponent<RaymarchedSphere>();
    }
}
