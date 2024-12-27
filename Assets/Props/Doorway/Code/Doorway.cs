using Unity.Mathematics;
using UnityEngine;
using Random = Unity.Mathematics.Random;

public class Doorway : IInstanceObject
{
    public enum DoorType
    {
        Flush, HingePanel, French, Sliding, 
    }

    public struct Params
    {
        public bool doubleDoor;
        public DoorType type;
        public float doorHeight;
        public float doorWidth;

        public Material glass, wood, metal;

        public Params(Material glass, Material wood, Material metal, bool doubleDoor = false, float doorHeight = 5f, float doorWidth = 2f)
        {
            this.glass = glass;
            this.wood = wood;
            this.metal = metal;
            this.doubleDoor = doubleDoor;
            this.doorHeight = doorHeight;
            this.doorWidth = doorWidth;
            type = DoorType.Flush;
        }
    }

    private uint seed;
    private bool doubleDoor;
    private Random rand;
    private Params parameters;
    private House.Doorway houseDoorData;

    public Doorway(uint seed, Params parameters)
    {
        this.seed = seed;
        this.parameters = parameters;
        rand = Generation.NewRand(seed);
    }

    public Doorway(uint seed, House.Doorway doorwayData, Params parameters)
    {
        this.seed = seed;
        this.parameters = parameters;
        houseDoorData = doorwayData;
        rand = Generation.NewRand(seed);
    }

    private void MeshDoor()
    {
        
    }

    private void MeshHingePanel()
    {
        
    }

    public GameObject GetInstance()
    {
        throw new System.NotImplementedException();
    }
}
