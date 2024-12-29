using UnityEngine;

public class RaymarchedSphere : RaymarchedGameObject
{
    public float radius = 1f;

    public override RaymarchedObj GetObjectData() => new RaymarchedObj
    {
        type = 0,
        position = transform.position,
        param0 = radius,
    };
}
