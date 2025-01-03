using UnityEngine;

public class RaymarchedBox : RaymarchedGameObject
{
    public Vector3 size = Vector3.one;

    public override RaymarchedObj GetObjectData() => new RaymarchedObj
    {
        type = 1,
        param0 = size.x / 2,
        param1 = size.y / 2,
        param2 = size.z / 2,
    };
}
