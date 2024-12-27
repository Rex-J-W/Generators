using UnityEngine;

[ExecuteInEditMode]
public class TextureTest : MonoBehaviour
{
    public uint seed = 1234;
    public int cellSize = 512;

    [Space(10)]
    public float floorHeight = 5f, meshScale = 32f;
    public int roomMin = 2, roomMax = 7;
    public int exteriorExpandMin = 0, exteriorExpandMax = 6;
    public float doorWidthMin = 10f, doorWidthMax = 15f;

    [Space(10)]
    public Texture2D tex;
    public Material mat;
    public MeshFilter filterExterior;
    public MeshFilter filterInterior;
    public MeshFilter filterFloor;

    [Space(10)]
    public bool generate;
    public bool randomSeed;

    private void Update()
    {
        if (generate)
        {
            if (randomSeed) seed = (uint)Random.Range(0, int.MaxValue);

            House.Requirements requirements = new House.Requirements(
                floorHeight, roomMin, roomMax, exteriorExpandMin, exteriorExpandMax, doorWidthMin, doorWidthMax,
                House.RoomType.Bedroom,
                House.RoomType.Bathroom,
                House.RoomType.Living);

            House h = new House(seed, cellSize, meshScale, requirements);
            tex = h.FloorPlan;
            mat.mainTexture = tex;

            filterExterior.mesh = h.ExteriorWalls;
            filterInterior.mesh = h.InteriorWalls;
            filterFloor.mesh = h.FloorMesh;
            generate = false;
        }
    }
}
