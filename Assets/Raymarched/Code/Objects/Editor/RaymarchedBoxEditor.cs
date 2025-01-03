using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

[CustomEditor(typeof(RaymarchedBox))]
public class RaymarchedBoxEditor : Editor
{
    private void OnSceneGUI()
    {
        RaymarchedBox obj = (RaymarchedBox)target;
        Color.RGBToHSV(obj.color, out float h, out float s, out float v);
        Handles.color = Color.HSVToRGB(h, s, 1f - v);

        Handles.DrawWireCube(obj.transform.position, obj.size);
    }
}
