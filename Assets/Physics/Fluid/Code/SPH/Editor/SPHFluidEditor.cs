using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SPHFluid))]
public class FluidEditor : Editor
{
    private void OnSceneGUI()
    {
        SPHFluid fluid = (SPHFluid)target;
        fluid.transform.localScale = new Vector3(Mathf.Max(1f, fluid.transform.localScale.x), 
            Mathf.Max(1f, fluid.transform.localScale.y), 
            Mathf.Max(1f, fluid.transform.localScale.z));
        Handles.DrawWireCube(fluid.transform.position, fluid.transform.localScale);
        serializedObject.ApplyModifiedProperties();
    }
}
