using UnityEngine;

public class RaymarchedRoundBox : RaymarchedGameObject
{
    public Vector3 size = Vector3.one;
    public float radius = 0.1f;

    public override RaymarchedObj GetObjectData() => new RaymarchedObj
    {
        type = 2,
        param0 = size.x / 2,
        param1 = size.y / 2,
        param2 = size.z / 2,
        param3 = radius / 2,
    };
}
