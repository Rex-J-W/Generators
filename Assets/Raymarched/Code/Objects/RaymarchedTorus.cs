using UnityEngine;

public class RaymarchedTorus : RaymarchedGameObject
{
    public Vector2 size = Vector2.one;

    public override RaymarchedObj GetObjectData() => new RaymarchedObj
    {
        type = 3,
        param0 = size.x,
        param1 = size.y,
    };
}
