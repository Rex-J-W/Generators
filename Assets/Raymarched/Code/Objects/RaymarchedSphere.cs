using UnityEngine;

public class RaymarchedSphere : RaymarchedGameObject
{
    public float radius = 1f;

    public override RaymarchedObj GetObjectData() => new RaymarchedObj
    {
        type = 0,
        param0 = radius / 2,
    };
}
