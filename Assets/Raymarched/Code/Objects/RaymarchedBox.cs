using UnityEngine;

public class RaymarchedBox : RaymarchedGameObject
{
    public Vector3 size = Vector3.one;

    public override RaymarchedObj GetObjectData() => new RaymarchedObj
    {
        type = 1,
        solidType = (int)solidType,
        repeating = repeating ? 1 : 0,
        repeatSize = repetitionSize,
        color = new Vector3(color.r, color.g, color.b),
        position = transform.position,
        param0 = size.x,
        param1 = size.y,
        param2 = size.z,
    };
}
