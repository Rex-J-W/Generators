using UnityEngine;

public class RaymarchedSphere : RaymarchedGameObject
{
    public float radius = 1f;

    public override RaymarchedObj GetObjectData() => new RaymarchedObj
    {
        type = 0,
        solidType = (int)solidType,
        repeating = repeating ? 1 : 0,
        repeatSize = repetitionSize,
        color = new Vector3(color.r, color.g, color.b),
        position = transform.position,
        param0 = radius,
    };
}
