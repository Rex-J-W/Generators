using UnityEditor;
using UnityEngine;

public class CreateRaymarchedObjectsMenu
{
    [MenuItem("GameObject/3D Object/Raymarched/Sphere")]
    public static void CreateSphere()
    {
        new GameObject("Sphere", typeof(RaymarchedSphere));
    }

    [MenuItem("GameObject/3D Object/Raymarched/Box")]
    public static void CreateBox()
    {
        new GameObject("Box", typeof(RaymarchedBox));
    }

    [MenuItem("GameObject/3D Object/Raymarched/Rounded Box")]
    public static void CreateRoundBox()
    {
        new GameObject("Rounded Box", typeof(RaymarchedRoundBox));
    }

    [MenuItem("GameObject/3D Object/Raymarched/Torus")]
    public static void CreateTorus()
    {
        new GameObject("Torus", typeof(RaymarchedTorus));
    }
}
