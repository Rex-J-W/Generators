using UnityEngine;

[ExecuteInEditMode]
public class MaterialTest : MonoBehaviour
{
    private Material cur;
    public Material mat;
    [Space(20)]
    public MeshRenderer[] objects;

    private void Update()
    {
        if (cur != mat)
        {
            for (int i = 0; i < objects.Length; i++)
                objects[i].sharedMaterial = mat;
            cur = mat;
        }
    }
}
