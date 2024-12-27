using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using UnityEngine;
using Color = UnityEngine.Color;
using Random = Unity.Mathematics.Random;

/// <summary>
/// Represents a generated house object
/// </summary>
public class House : IInstanceObject
{
    /// <summary>
    /// Represents a side of an object
    /// </summary>
    public enum Side
    {
        Left = 0, Right = 1, Top = 2, Bottom = 3
    }

    /// <summary>
    /// Represents a physical wall
    /// </summary>
    public struct Wall
    {
        /// <summary>
        /// Minimum point of the wall
        /// </summary>
        public Vector2Int start;

        /// <summary>
        /// Length of the wall
        /// </summary>
        public int length;

        /// <summary>
        /// Used internally for storing wall hierarchy
        /// </summary>
        public int tree;

        /// <summary>
        /// Direction the wall faces
        /// </summary>
        public Side side;

        /// <summary>
        /// Maximum point of the wall
        /// </summary>
        public Vector2Int End
        {
            get
            {
                if (side == Side.Left || side == Side.Right)
                    return new Vector2Int(start.x, start.y + length);
                else return new Vector2Int(start.x + length, start.y);
            }
        }

        /// <summary>
        /// Creates a new wall
        /// </summary>
        /// <param name="startX">Minimum x postion of the wall</param>
        /// <param name="startY">Minium y position of the wall</param>
        /// <param name="length">Length of the wall</param>
        /// <param name="side">Which direction the wall faces</param>
        /// <param name="tree">What level of branching this wall represents</param>
        public Wall(int startX, int startY, int length, Side side, int tree)
        {
            start = new Vector2Int(startX, startY);
            this.length = length;
            this.side = side;
            this.tree = tree;
        }

        /// <summary>
        /// Checks if this wall contains a point
        /// </summary>
        /// <param name="pt">Point to check is in this wall</param>
        /// <returns>If the point is in the wall</returns>
        public bool Contains(Vector2Int pt)
        {
            if (side == Side.Left || side == Side.Right)
                return pt.x == start.x && pt.y >= start.y && pt.y < start.y + length;
            else return pt.y == start.y && pt.x >= start.x && pt.x < start.x + length;
        }
    }

    /// <summary>
    /// Describes the requirements for a house
    /// </summary>
    public struct Requirements
    {
        public float floorHeight, doorWidthMin, doorWidthMax;
        public int exteriorExpandMin, exteriorExpandMax, roomMin, roomMax;

        private readonly HashSet<RoomType> types;

        public Requirements(float floorHeight, int roomMin, int roomMax, int exteriorExpandMin, int exteriorExpandMax,
            float doorWidthMin, float doorWidthMax,
            params RoomType[] roomTypes)
        {
            this.floorHeight = floorHeight;
            this.roomMin = roomMin;
            this.roomMax = roomMax;
            this.exteriorExpandMin = exteriorExpandMin;
            this.exteriorExpandMax = exteriorExpandMax;
            this.doorWidthMin = doorWidthMin;
            this.doorWidthMax = doorWidthMax;
            types = new HashSet<RoomType>(roomTypes);
        }

        public readonly bool HasRoomType(RoomType type) => types.Contains(type);
    }

    public enum RoomType
    {
        Empty, Living, Dining, Kitchen, Bathroom, Hallway, Closet, Bedroom, Laundry, Guest, Basement, Garage, Gaming, Gym
    }

    /// <summary>
    /// Represents a physical room
    /// </summary>
    public struct Room
    {
        public int num;
        public RoomType type;
        public Vector2Int origin, min, max;
        public List<Vector2Int> addonCells;

        public RoomArchetype archetype;

        public RectInt Rect => new RectInt(min.x, min.y, max.x - min.x, max.y - min.y);

        /// <summary>
        /// The area the room takes up (includes non-rectangular rooms)
        /// </summary>
        public int Area => (max.x - min.x) * (max.y - min.y) + addonCells.Count;

        /// <summary>
        /// Creates a new room
        /// </summary>
        /// <param name="number">The index number of the room (add 2 in floorMap)</param>
        /// <param name="origin">Origin point of the room</param>
        public Room(int number, Vector2Int origin, RoomType type)
        {
            num = number;
            this.origin = origin;
            this.type = type;
            archetype = new RoomArchetype(type);
            min = origin;
            max = origin;

            addonCells = new List<Vector2Int>();
        }

        /// <summary>
        /// Expands one side of this room by an amount
        /// </summary>
        /// <param name="side">Side to expand</param>
        /// <param name="amount">Amount to expand side</param>
        public void Expand(Side side, int amount)
        {
            if (side == Side.Left) min.x -= amount;
            else if (side == Side.Right) max.x += amount;
            else if (side == Side.Bottom) min.y -= amount;
            else max.y += amount;
        }

        /// <summary>
        /// Expands all the sides of this room by an amount
        /// </summary>
        /// <param name="amount">Amount to expand each side</param>
        public void ExpandAllSides(int amount)
        {
            min.x -= amount;
            max.x += amount;
            min.y -= amount;
            max.y += amount;
        }

        public void CalcRoomArchetype(Random rand, bool biggest, bool smallest, float essentialScale,
            HashSet<RoomType> banned, Dictionary<RoomType, int> currentRooms)
        {
            // Remember to update the number of rooms if changing how many room types there are!!!
            (int, RoomType)[] bestMatches = new (int, RoomType)[14];

            // Calculates the similarity of each room type based on its archetype settings

            foreach (int i in Enum.GetValues(typeof(RoomType)))
            {
                int similarityPoints = 0;
                RoomType type = (RoomType)i;
                if (banned.Contains(type))
                {
                    bestMatches[i] = (int.MinValue, type);
                    continue;
                }
                RoomArchetype tempArchetype = new RoomArchetype(type);

                float aspectRatio1 = (max.x - min.x) / (max.y - min.y),
                    aspectRatio2 = (max.x - min.x) / (max.y - min.y),
                    aspect = math.max(aspectRatio1, aspectRatio2);
                if (tempArchetype.aspectRatioMin <= aspect && tempArchetype.aspectRatioMax >= aspect)
                    similarityPoints++;

                if (biggest && tempArchetype.isBiggest)
                    similarityPoints += 1;
                if (smallest && tempArchetype.isSmallest)
                    similarityPoints += 1;

                similarityPoints += (int)(tempArchetype.howEssential * essentialScale);

                if (currentRooms.TryGetValue(type, out int roomTypeNum) && roomTypeNum < tempArchetype.maxRoomType)
                    similarityPoints -= roomTypeNum;
                else if (roomTypeNum >= tempArchetype.maxRoomType)
                {
                    bestMatches[i] = (-10000, type);
                    continue;
                }

                bestMatches[i] = (similarityPoints, type);
            }

            // Sorts the room types based on their similarity to this room

            Array.Sort(bestMatches, (x, y) => y.Item1.CompareTo(x.Item1));

            // Debug
            //string roomTypes = "";
            //for (int i = 0; i < bestMatches.Length; i++)
            //{
            //    roomTypes += bestMatches[i].ToString() + " ";
            //}
            //Debug.Log(roomTypes);

            // Randomize the type if similarity levels are the same for greater diversity

            int highestScore = bestMatches[0].Item1;
            List<RoomType> randomType = new List<RoomType>();
            for (int i = 0; i < bestMatches.Length; i++)
                if (bestMatches[i].Item1 == highestScore)
                    randomType.Add(bestMatches[i].Item2);

            // Assigns the room archetype

            type = randomType[rand.NextInt(0, randomType.Count)];
            archetype = new RoomArchetype(type);
            //Debug.Log(type);
        }
    }

    /// <summary>
    /// Represents a doorway in the house;
    /// </summary>
    public struct Doorway
    {
        public Side side;
        public int roomAIndex, roomBIndex;
        public List<Vector2Int> points;

        public readonly int Width => points.Count;

        public Doorway(Side side, int roomAIndex, int roomBIndex, List<Vector2Int> points)
        {
            this.side = side;
            this.roomAIndex = roomAIndex;
            this.roomBIndex = roomBIndex;
            this.points = points;
        }
    }

    private readonly uint seed;
    private Texture2D floorPlan;
    private readonly int cellSize;
    private readonly float meshScaleFactor;
    private Random rand;

    private Rect floorBaseRect;
    private Wall[] exteriorWalls, interiorWalls;
    private Room[] baseRooms;
    private Doorway[] doors;

    /// <summary>
    /// Code 0 for empty, 1 for walls, above for room indices, -1 for doorway
    /// </summary>
    private int[,] floorMap;

    private Mesh exteriorWallMesh, interiorWallMesh, floorMesh;

    /// <summary>
    /// The generated visualization of the floor plan of the house
    /// </summary>
    public Texture2D FloorPlan
    {
        get
        {
            if (floorPlan == null) CalcFloorPlan();
            return floorPlan;
        }
    }

    /// <summary>
    /// Mesh of the generated exterior walls
    /// </summary>
    public Mesh ExteriorWalls
    {
        get
        {
            if (exteriorWallMesh == null) exteriorWallMesh = MeshWalls(exteriorWalls);
            return exteriorWallMesh;
        }
    }

    /// <summary>
    /// Mesh of the generated interior walls
    /// </summary>
    public Mesh InteriorWalls
    {
        get
        {
            if (interiorWallMesh == null) interiorWallMesh = MeshWalls(interiorWalls);
            return interiorWallMesh;
        }
    }

    /// <summary>
    /// Mesh of the generated floor
    /// </summary>
    public Mesh FloorMesh
    {
        get
        {
            if (floorMesh == null) MeshFloor();
            return floorMesh;
        }
    }

    /// <summary>
    /// Scaling ratio for house verticies and uvs
    /// </summary>
    public float MeshScaleRatio => cellSize / meshScaleFactor;

    private Requirements houseParams;

    /// <summary>
    /// The requirements for generating this house
    /// </summary>
    public Requirements HouseParams => houseParams;

    public House(uint seed, int cellSize, float meshScaleFactor) => 
        new House(seed, cellSize, meshScaleFactor, new Requirements(5f, 4, 8, 0, 6, 10, 15));

    public House(uint seed, int cellSize, float meshScaleFactor, Requirements requirements)
    {
        this.seed = seed;
        this.cellSize = cellSize;
        this.meshScaleFactor = meshScaleFactor;
        houseParams = requirements;

        // Setup house generation system

        floorMap = new int[cellSize, cellSize];
        rand = Generation.NewRand(seed);
        floorBaseRect = GetFloorBaseRect(rand, cellSize);

        // Calculate exterior walls

        CalcExteriorWalls();

        // Calculate rooms

        AddExteriorWallsToFloorMap();
        RoomExpansion();
        CalcRoomArchetypes(true, false, 1.8f);

        // Calculate interior walls

        CalcInteriorWalls();

        // Calculate doorways

        CalcDoorways();
        DivideDoorwayWalls();
    }

    /// <summary>
    /// Generates each exterior wall
    /// </summary>
    private void CalcExteriorWalls()
    {
        // Calculate min and max rectangle points

        int minX = cellSize, maxX = 0, minY = cellSize, maxY = 0;
        for (int x = 0; x < cellSize; x++)
        {
            for (int y = 0; y < cellSize; y++)
            {
                if (floorBaseRect.Contains(new Vector2(x, y)))
                {
                    minX = math.min(minX, x);
                    maxX = math.max(maxX, x);
                    minY = math.min(minY, y);
                    maxY = math.max(maxY, y);
                }
            }
        }

        // Add intial walls to expand

        int expandWalls = rand.NextInt(houseParams.exteriorExpandMin, houseParams.exteriorExpandMax);
        List<Wall> walls = new List<Wall>()
        {
            new Wall(minX, minY, maxY - minY, Side.Left, 0),
            new Wall(minX, minY, maxX - minX, Side.Bottom, 0),
            new Wall(minX, maxY, maxX - minX, Side.Top, 0),
            new Wall(maxX, minY, maxY - minY, Side.Right, 0),
        };

        // Expanding walls algorithm

        for (int i = 0; i < expandWalls; i++)
        {
            int wallIndex = rand.NextInt(0, 4);
            Wall cur = walls[wallIndex];
            if (cur.tree > 1)
            {
                if (expandWalls < 30) expandWalls++;
                continue;
            }

            int newStart = rand.NextInt(0, cur.length / 3),
                newLength = rand.NextInt(cur.length / 4, (int)(cur.length / 1.75f)),
                push = rand.NextInt(cellSize / 10 / 4, cellSize / 10);

            if (newLength > 3)
            {
                walls.RemoveAt(wallIndex);
                Wall[] newWalls = SegmentWall(cur, newStart, newLength, push);
                walls.AddRange(newWalls);
            }
            else
            {
                if (expandWalls < 30) expandWalls++;
                continue;
            }
        }

        // Remove non-euclidean walls

        for (int i = 0; i < walls.Count; i++)
        {
            if (walls[i].length < 1)
            {
                walls.RemoveAt(i);
                i--;
            }
        }

        // Assign wall array

        exteriorWalls = walls.ToArray();
    }

    /// <summary>
    /// Expands a wall based on start point, length, and expansion amount
    /// </summary>
    /// <param name="init">Wall to expand</param>
    /// <param name="start">Starting point of expansion on wall</param>
    /// <param name="length">Expansion length</param>
    /// <param name="push">Expansion amount</param>
    /// <returns>New wall segments created from expansion</returns>
    private static Wall[] SegmentWall(Wall init, int start, int length, int push)
    {
        Wall[] walls = new Wall[5];
        int newTree = init.tree + 1;
        if (init.side == Side.Left)
        {
            walls[0] = new Wall(init.start.x, init.start.y, start, Side.Left, newTree);
            walls[1] = new Wall(init.start.x - push, init.start.y + start, push, Side.Bottom, newTree + 10);
            walls[2] = new Wall(init.start.x - push, init.start.y + start, length, Side.Left, newTree);
            walls[3] = new Wall(init.start.x - push, init.start.y + start + length, push, Side.Top, newTree + 10);
            walls[4] = new Wall(init.start.x, init.start.y + start + length, init.length - length - start, Side.Left, newTree);
        }
        else if (init.side == Side.Right)
        {
            walls[0] = new Wall(init.start.x, init.start.y, start, Side.Right, newTree);
            walls[1] = new Wall(init.start.x, init.start.y + start, push, Side.Bottom, newTree + 10);
            walls[2] = new Wall(init.start.x + push, init.start.y + start, length, Side.Right, newTree);
            walls[3] = new Wall(init.start.x, init.start.y + start + length, push, Side.Top, newTree + 10);
            walls[4] = new Wall(init.start.x, init.start.y + start + length, init.length - length - start, Side.Right, newTree);
        }
        else if (init.side == Side.Top)
        {
            walls[0] = new Wall(init.start.x, init.start.y, start, Side.Top, newTree);
            walls[1] = new Wall(init.start.x + start, init.start.y, push, Side.Left, newTree + 10);
            walls[2] = new Wall(init.start.x + start, init.start.y + push, length, Side.Top, newTree);
            walls[3] = new Wall(init.start.x + start + length, init.start.y, push, Side.Right, newTree + 10);
            walls[4] = new Wall(init.start.x + start + length, init.start.y, init.length - length - start, Side.Top, newTree);
        }
        else
        {
            walls[0] = new Wall(init.start.x, init.start.y, start, Side.Bottom, newTree);
            walls[1] = new Wall(init.start.x + start, init.start.y - push, push, Side.Left, newTree + 10);
            walls[2] = new Wall(init.start.x + start, init.start.y - push, length, Side.Bottom, newTree);
            walls[3] = new Wall(init.start.x + start + length, init.start.y - push, push, Side.Right, newTree + 10);
            walls[4] = new Wall(init.start.x + start + length, init.start.y, init.length - length - start, Side.Bottom, newTree);
        }
        return walls;
    }

    /// <summary>
    /// Expands each room to fill the house
    /// </summary>
    private void RoomExpansion()
    {
        int roomCount = rand.NextInt(houseParams.roomMin, houseParams.roomMax);
        Room[] rooms = new Room[roomCount];

        // Create room points

        for (int i = 0; i < roomCount; i++)
        {
            Vector2Int roomOrigin = new Vector2Int(
                rand.NextInt((int)floorBaseRect.min.x + 3, (int)floorBaseRect.max.x),
                rand.NextInt((int)floorBaseRect.min.y + 3, (int)floorBaseRect.max.y));
            rooms[i] = new Room(i, roomOrigin, RoomType.Empty);
        }

        // Do room cell expansion

        bool floorFilled = false;
        while (!floorFilled)
        {
            floorFilled = true;
            for (int i = 0; i < roomCount; i++)
            {
                List<Side> sidesToExpand = CheckRoomExpansionSides(rooms[i]);
                if (sidesToExpand.Count > 0) floorFilled = false;
                for (int j = 0; j < sidesToExpand.Count; j++)
                    rooms[i].Expand(sidesToExpand[j], 1);
                FillFloorMapRoom(rooms[i]);
            }
        }

        // Fill remaining floor space with smallest rooms

        Array.Sort(rooms, (x, y) => x.Area.CompareTo(y.Area));
        for (int i = 0; i < roomCount; i++)
            rooms[i].addonCells = ExpandRoomFinalPass(rooms[i]);

        // Assign rooms

        baseRooms = rooms;
    }

    /// <summary>
    /// Finishes expansion of each room by doing paint fill on extra space
    /// </summary>
    /// <param name="room">The room to expand</param>
    /// <returns>List of points room expanded to</returns>
    private List<Vector2Int> ExpandRoomFinalPass(Room room)
    {
        // Setup hash table and queue

        List<Vector2Int> cellsAdded = new List<Vector2Int>();
        HashSet<Vector2Int> usedPts = new HashSet<Vector2Int>();
        Queue<Vector2Int> fillPts = new Queue<Vector2Int>();

        // Add room walls to expansion point queue

        for (int y = room.min.y; y < room.max.y; y++)
        {
            fillPts.Enqueue(new Vector2Int(room.min.x, y));
            fillPts.Enqueue(new Vector2Int(room.max.x - 1, y));
        }
        for (int x = room.min.x; x < room.max.x; x++)
        {
            fillPts.Enqueue(new Vector2Int(x, room.min.y));
            fillPts.Enqueue(new Vector2Int(x, room.max.y - 1));
        }

        // Recursive expand room

        int mapId = room.num + 2;
        while (fillPts.Count > 0)
        {
            Vector2Int pt = fillPts.Dequeue(), newPt;
            if (usedPts.Contains(pt)) continue;

            // Expand point left right

            newPt = new Vector2Int(pt.x - 1, pt.y);
            if (pt.x > 0 && floorMap[newPt.x, newPt.y] < 1)
            {
                floorMap[newPt.x, newPt.y] = mapId;
                cellsAdded.Add(newPt);
                fillPts.Enqueue(newPt);
            }
            newPt = new Vector2Int(pt.x, pt.y - 1);
            if (pt.y > 0 && floorMap[newPt.x, newPt.y] < 1)
            {
                floorMap[newPt.x, newPt.y] = mapId;
                cellsAdded.Add(newPt);
                fillPts.Enqueue(newPt);
            }

            // Expand point up down

            newPt = new Vector2Int(pt.x + 1, pt.y);
            if (pt.x < cellSize - 1 && floorMap[newPt.x, newPt.y] < 1)
            {
                floorMap[newPt.x, newPt.y] = mapId;
                cellsAdded.Add(newPt);
                fillPts.Enqueue(newPt);
            }
            newPt = new Vector2Int(pt.x, pt.y + 1);
            if (pt.y < cellSize - 1 && floorMap[newPt.x, newPt.y] < 1)
            {
                floorMap[newPt.x, newPt.y] = mapId;
                cellsAdded.Add(newPt);
                fillPts.Enqueue(newPt);
            }

            usedPts.Add(pt);
        }

        // Return all additional cells for room

        return cellsAdded;
    }

    /// <summary>
    /// Fills the floor map with the (altered) room index
    /// </summary>
    /// <param name="room">Room to add to map</param>
    private void FillFloorMapRoom(Room room)
    {
        for (int x = room.min.x; x < room.max.x; x++)
        {
            for (int y = room.min.y; y < room.max.y; y++)
            {
                // Add 2 for true room index
                floorMap[x, y] = room.num + 2;
            }
        }
    }

    /// <summary>
    /// Checks each side of a room to see if it can be expanded
    /// </summary>
    /// <param name="room">The room to check</param>
    /// <returns></returns>
    private List<Side> CheckRoomExpansionSides(Room room)
    {
        bool left = true, right = true, bottom = true, top = true;

        // Check if already outside bounds

        if (room.min.x <= 0) left = false;
        if (room.max.x >= cellSize - 1) right = false;
        if (room.min.y <= 0) bottom = false;
        if (room.max.y >= cellSize - 1) top = false;

        // Check left and right

        for (int y = room.min.y; y < room.max.y; y++)
        {
            y = math.clamp(y, 0, cellSize);
            if (left && floorMap[room.min.x - 1, y] > 0)
                left = false;
            if (right && floorMap[room.max.x, y] > 0)
                right = false;
        }

        // Check bottom and top

        for (int x = room.min.x; x < room.max.x; x++)
        {
            x = math.clamp(x, 0, cellSize);
            if (bottom && floorMap[x, room.min.y - 1] > 0)
                bottom = false;
            if (top && floorMap[x, room.max.y] > 0)
                top = false;
        }

        // Return sides that can be expanded

        List<Side> okaySidesToExpand = new List<Side>();
        if (left) okaySidesToExpand.Add(Side.Left);
        if (right) okaySidesToExpand.Add(Side.Right);
        if (bottom) okaySidesToExpand.Add(Side.Bottom);
        if (top) okaySidesToExpand.Add(Side.Top);

        return okaySidesToExpand;
    }

    /// <summary>
    /// Calculates the type of room each room is
    /// </summary>
    private void CalcRoomArchetypes(bool noEmptyRooms, bool basement, float essentialScale)
    {
        // Setup variables

        Dictionary<RoomType, int> curRoomTypes = new Dictionary<RoomType, int>();
        int biggest = 0, smallest = 0, biggestArea = 0, smallestArea = int.MaxValue;

        // Find biggest and smallest rooms

        for (int i = 0; i < baseRooms.Length; i++)
        {
            if (baseRooms[i].Area > biggestArea)
            {
                biggest = i;
                biggestArea = baseRooms[i].Area;
            }
            if (baseRooms[i].Area < smallestArea)
            {
                smallest = i;
                smallestArea = baseRooms[i].Area;
            }
        }

        // Check for banned room types

        HashSet<RoomType> bannedRooms = new HashSet<RoomType>();
        if (noEmptyRooms) bannedRooms.Add(RoomType.Empty);
        if (!basement) bannedRooms.Add(RoomType.Basement);
        bannedRooms.Add(RoomType.Garage);

        // Recalculate room archetypes

        for (int i = 0; i < baseRooms.Length; i++)
        {
            if (baseRooms[i].type == RoomType.Empty)
            {
                baseRooms[i].CalcRoomArchetype(rand, i == biggest, i == smallest, essentialScale, bannedRooms, curRoomTypes);
                if (curRoomTypes.TryGetValue(baseRooms[i].type, out int roomTypeCount))
                    curRoomTypes[baseRooms[i].type] = roomTypeCount + 1;
                else curRoomTypes.Add(baseRooms[i].type, roomTypeCount + 1);
            }
        }
    }

    /// <summary>
    /// Adds the exterior walls that have already been generated to the floor map for room cell expansion
    /// </summary>
    private void AddExteriorWallsToFloorMap()
    {
        for (int i = 0; i < exteriorWalls.Length; i++)
        {
            Wall wall = exteriorWalls[i];
            Vector2Int curPt = wall.start;
            if (wall.side == Side.Left || wall.side == Side.Right)
            {
                for (int j = 0; j <= wall.length; j++)
                {
                    floorMap[curPt.x, curPt.y] = 1;
                    curPt.y++;
                }
            }
            else
            {
                for (int j = 0; j <= wall.length; j++)
                {
                    floorMap[curPt.x, curPt.y] = 1;
                    curPt.x++;
                }
            }
        }
    }

    /// <summary>
    /// Generates the interior walls
    /// </summary>
    private void CalcInteriorWalls()
    {
        // Setup data storage

        List<Vector2Int> interiorWallPts = new List<Vector2Int>();
        HashSet<Vector2Int> interiorSet = new HashSet<Vector2Int>();

        // Find wall points using edge detection

        for (int x = 1; x < cellSize - 1; x++)
        {
            for (int y = 1; y < cellSize - 1; y++)
            {
                int floorVal = floorMap[x, y],
                    floorLeft = floorMap[x - 1, y],
                    floorRight = floorMap[x + 1, y],
                    floorUp = floorMap[x, y + 1],
                    floorRightUp = floorMap[x + 1, y + 1],
                    floorUpLeft = floorMap[x - 1, y + 1];

                if (floorVal <= 1) continue;
                else if ((floorRight > 1 && floorRight != floorVal) ||
                    (floorUp > 1 && floorUp != floorVal && floorLeft == floorVal) ||
                    (floorRightUp > 1 && floorRightUp != floorVal))
                {
                    Vector2Int pt = new Vector2Int(x, y);
                    interiorWallPts.Add(pt);
                    interiorSet.Add(pt);
                }
                else if (floorUp > 1 && floorUp != floorVal && floorLeft != floorVal && floorUpLeft != floorVal)
                {
                    Vector2Int pt = new Vector2Int(x - 1, y + 1);
                    interiorWallPts.Add(pt);
                    interiorSet.Add(pt);
                }
            }
        }

        // Setup north-south east-west wall point classification

        List<Vector2Int> wallPtsEW = new List<Vector2Int>(),
            wallPtsNS = new List<Vector2Int>();
        HashSet<Vector2Int> setEW = new HashSet<Vector2Int>(),
            setNS = new HashSet<Vector2Int>();

        // Find east west wall points and north south wall points

        for (int i = 0; i < interiorWallPts.Count; i++)
        {
            Vector2Int pt = interiorWallPts[i],
                ptLeft = new Vector2Int(pt.x - 1, pt.y),
                ptRight = new Vector2Int(pt.x + 1, pt.y),
                ptUp = new Vector2Int(pt.x, pt.y + 1),
                ptDown = new Vector2Int(pt.x, pt.y - 1);

            if (interiorSet.Contains(ptLeft) || interiorSet.Contains(ptRight))
            {
                wallPtsEW.Add(pt);
                setEW.Add(pt);
            }
            if (interiorSet.Contains(ptUp) || interiorSet.Contains(ptDown))
            {
                wallPtsNS.Add(pt);
                setNS.Add(pt);
            }
        }

        // Build east-west walls

        Dictionary<Vector2Int, Wall> interiorWallsEW = new Dictionary<Vector2Int, Wall>();
        for (int i = 0; i < wallPtsEW.Count; i++)
        {
            Vector2Int curPt, wallMin = wallPtsEW[i];
            for (int j = 0; j < cellSize; j++)
            {
                curPt = new Vector2Int(wallPtsEW[i].x - j, wallPtsEW[i].y);
                if (!setEW.Contains(curPt)) break;
                else wallMin = curPt;
            }

            if (!interiorWallsEW.ContainsKey(wallMin))
            {
                Vector2Int wallMax = wallPtsEW[i];
                for (int j = 0; j < cellSize; j++)
                {
                    curPt = new Vector2Int(wallPtsEW[i].x + j, wallPtsEW[i].y);
                    if (!setEW.Contains(curPt)) break;
                    else wallMax = curPt;
                }

                Wall interiorWall = new Wall(wallMin.x, wallMin.y, wallMax.x - wallMin.x, Side.Bottom, -1);
                interiorWallsEW.Add(wallMin, interiorWall);
            }
        }

        // Build north-south walls

        Dictionary<Vector2Int, Wall> interiorWallsNS = new Dictionary<Vector2Int, Wall>();
        for (int i = 0; i < wallPtsNS.Count; i++)
        {
            Vector2Int curPt, wallMin = wallPtsNS[i];
            for (int j = 0; j < cellSize; j++)
            {
                curPt = new Vector2Int(wallPtsNS[i].x, wallPtsNS[i].y - j);
                if (!setNS.Contains(curPt)) break;
                else wallMin = curPt;
            }

            if (!interiorWallsNS.ContainsKey(wallMin))
            {
                Vector2Int wallMax = wallPtsNS[i];
                for (int j = 0; j < cellSize; j++)
                {
                    curPt = new Vector2Int(wallPtsNS[i].x, wallPtsNS[i].y + j);
                    if (!setNS.Contains(curPt)) break;
                    else wallMax = curPt;
                }

                Wall interiorWall = new Wall(wallMin.x, wallMin.y, wallMax.y - wallMin.y, Side.Left, -1);
                interiorWallsNS.Add(wallMin, interiorWall);
            }
        }

        // Combine all walls and merge to array

        interiorWalls = interiorWallsEW.Values.ToArray().Concat(interiorWallsNS.Values.ToArray()).ToArray();
    }

    /// <summary>
    /// Generates all the doorways connecting rooms
    /// </summary>
    private void CalcDoorways()
    {
        // Storage for temp floor map

        int[,] newFloorMap = (int[,])floorMap.Clone();

        // Label the available doorway areas on the floormap using edge detection

        for (int x = 1; x < cellSize - 1; x++)
        {
            for (int y = 1; y < cellSize - 1; y++)
            {
                int floorVal = floorMap[x, y],
                    floorRight = floorMap[x + 1, y],
                    floorUp = floorMap[x, y + 1];

                // previous detectio algorithm
                //if ((floorVal != floorRight && floorVal == floorUp && floorVal == floorDown) ||
                //    (floorVal != floorUp && floorVal == floorLeft && floorVal == floorRight && floorVal == floorDown))

                // Checks if any floor value is not a room ( < 2 ), then continue
                // At the same time checks each side to see if it differs from the current value (except the current value)

                int sideTest = 0;
                Dictionary<int, int> roomVals = new Dictionary<int, int>
                {
                    { floorVal, 1 }
                };

                bool floorValLessTwo = false;
                for (int i = -1; i < 2; i++)
                {
                    for (int j = -1; j < 2; j++)
                    {
                        int curFloorVal = floorMap[x + i, y + j];
                        if (curFloorVal < 2)
                        {
                            floorValLessTwo = true;
                            break;
                        }
                        else if ((i != 0 || j != 0) && !roomVals.ContainsKey(curFloorVal))
                        {
                            sideTest++;
                            roomVals.Add(curFloorVal, 1);
                        }
                        else if (roomVals.ContainsKey(curFloorVal))
                            roomVals[curFloorVal]++;
                    }
                    if (floorValLessTwo) break;
                }

                // Continue if not in a room

                if (floorValLessTwo) continue;

                // Final checks to make sure doorways are on walls, not on corners

                if ((floorVal != floorRight || floorVal != floorUp) && sideTest < 2)
                {
                    bool canSetFloorVal = true;
                    foreach (int item in roomVals.Values)
                    {
                        if (item == 1 || item == 8)
                        {
                            canSetFloorVal = false;
                            break;
                        }
                    }

                    if (canSetFloorVal) newFloorMap[x, y] = -1;
                }
            }
        }

        // Check for inconsistencies in corner detection and correct them

        for (int x = 1; x < cellSize - 1; x++)
        {
            for (int y = 1; y < cellSize - 1; y++)
            {
                if (newFloorMap[x, y] == -1 && newFloorMap[x - 1, y] == -1 && newFloorMap[x, y - 1] == -1)
                    newFloorMap[x, y] = floorMap[x, y];
                else if (newFloorMap[x, y] == -1 && newFloorMap[x + 1, y] == -1 && newFloorMap[x, y - 1] == -1)
                {
                    newFloorMap[x, y] = floorMap[x, y];
                    newFloorMap[x, y - 1] = floorMap[x, y - 1];
                }
            }
        }

        // Create room connection map based on generated floor map

        Vector2Int[,] roomConnectMap = new Vector2Int[cellSize, cellSize];
        for (int x = 0; x < cellSize; x++)
            for (int y = 0; y < cellSize; y++)
                roomConnectMap[x, y] = new Vector2Int(-1, -1);

        for (int x = 1; x < cellSize - 1; x++)
        {
            for (int y = 1; y < cellSize - 1; y++)
            {
                if (newFloorMap[x, y] == -1)
                {
                    int floorVal = floorMap[x, y],
                        floorRight = floorMap[x + 1, y],
                        floorUp = floorMap[x, y + 1];
                    if (floorVal > 1 && floorVal != floorRight)
                        roomConnectMap[x, y] = new Vector2Int(floorVal, floorRight);
                    else if (floorVal > 1 && floorVal != floorUp)
                        roomConnectMap[x, y] = new Vector2Int(floorVal, floorUp);
                }
            }
        }

        // Create doorway objects

        List<Doorway> doorways = new List<Doorway>();
        HashSet<Vector2Int> roomsConnected = new HashSet<Vector2Int>();
        int minWidth = (int)(houseParams.doorWidthMin * MeshScaleRatio),
            maxWidth = (int)(houseParams.doorWidthMax * MeshScaleRatio);

        for (int i = 0; i < baseRooms.Length; i++)
        {
            Room r = baseRooms[i];

            // Check room for all potential doorway starting points
            
            int checkRoomNum = r.num + 2;
            List<Vector2Int> potentialDoorwayPts = new List<Vector2Int>();
            for (int x = 1; x < cellSize - 1; x++)
            {
                for (int y = 1; y < cellSize - 1; y++)
                {
                    Vector2Int mapVal = roomConnectMap[x, y];
                    if (mapVal.x == -1) continue;
                    else if (!roomsConnected.Contains(mapVal) && !roomsConnected.Contains(new Vector2Int(mapVal.y, mapVal.x)) &&
                        (mapVal.x == checkRoomNum || mapVal.y == checkRoomNum))
                        potentialDoorwayPts.Add(new Vector2Int(x, y));
                }
            }

            // Create multiple doorways to other rooms

            int doorwayCount = rand.NextInt(1, r.archetype.maxDoors + 1);
            for (int j = 0; j < doorwayCount; j++)
            {
                if (potentialDoorwayPts.Count < 1) break;
                int randomPt = rand.NextInt(0, potentialDoorwayPts.Count);
                Vector2Int newDoorwayPt = potentialDoorwayPts[randomPt];

                // Calculates the position and size of each doorway

                List<Vector2Int> finalPoints = new List<Vector2Int>(),
                    testPts = new List<Vector2Int>() { newDoorwayPt };
                Vector2Int doorwayRooms = roomConnectMap[newDoorwayPt.x, newDoorwayPt.y];

                // Add check to make sure door covers one side

                Side curSide = Side.Bottom;
                int leftRight = 0, upDown = 0;
                if (newFloorMap[newDoorwayPt.x - 1, newDoorwayPt.y] == -1) leftRight++;
                if (newFloorMap[newDoorwayPt.x + 1, newDoorwayPt.y] == -1) leftRight++;
                if (newFloorMap[newDoorwayPt.x, newDoorwayPt.y - 1] == -1) upDown++;
                if (newFloorMap[newDoorwayPt.x, newDoorwayPt.y + 1] == -1) upDown++;
                if (upDown > leftRight) curSide = Side.Left;

                // Create doorways

                if (!roomsConnected.Contains(doorwayRooms) &&
                    !roomsConnected.Contains(new Vector2Int(doorwayRooms.y, doorwayRooms.x)))
                {
                    finalPoints.Add(newDoorwayPt);
                    potentialDoorwayPts.RemoveAt(randomPt);

                    // Expands the doorways

                    int doorwayWidth = rand.NextInt(minWidth, maxWidth);
                    for (int k = 0; k < potentialDoorwayPts.Count; k++)
                    {
                        Vector2Int curDoorwayPt = potentialDoorwayPts[k];
                        doorwayRooms = roomConnectMap[curDoorwayPt.x, curDoorwayPt.y];
                        for (int t = 0; t < testPts.Count; t++)
                        {
                            if (!finalPoints.Contains(curDoorwayPt) && !roomsConnected.Contains(doorwayRooms) ||
                                !roomsConnected.Contains(new Vector2Int(doorwayRooms.y, doorwayRooms.x)))
                            {
                                if (curSide == Side.Bottom && math.abs(curDoorwayPt.x - testPts[t].x) == 1 && math.abs(curDoorwayPt.y - newDoorwayPt.y) == 0)
                                {
                                    testPts.Add(curDoorwayPt);
                                    finalPoints.Add(curDoorwayPt);
                                    doorwayWidth--;
                                }
                                else if (curSide == Side.Left && math.abs(curDoorwayPt.y - testPts[t].y) == 1 && math.abs(curDoorwayPt.x - newDoorwayPt.x) == 0)
                                {
                                    testPts.Add(curDoorwayPt);
                                    finalPoints.Add(curDoorwayPt);
                                    doorwayWidth--;
                                }

                                if (doorwayWidth < 1) break;
                            }
                        }

                        if (doorwayWidth < 1) break;
                    }
                }

                // Checks if doorway width is within boundary then creates doorway

                if (finalPoints.Count > 0 && finalPoints.Count >= minWidth && finalPoints.Count <= maxWidth)
                {
                    Vector2Int doorwayRoomsFinal = roomConnectMap[newDoorwayPt.x, newDoorwayPt.y];
                    Doorway doorway = new Doorway(curSide, doorwayRoomsFinal.x, doorwayRoomsFinal.y, finalPoints);
                    doorways.Add(doorway);
                    roomsConnected.Add(doorwayRoomsFinal);
                }
            }
        }

        // Assigns doorway array and uodates floor map

        floorMap = newFloorMap;
        doors = doorways.ToArray();
    }

    /// <summary>
    /// Divides walls for doorways
    /// </summary>
    private void DivideDoorwayWalls()
    {
        // Setup wall recursive divide queue

        List<Wall> newInteriorWalls = new List<Wall>();
        Queue<Wall> interiorWallQueue = new Queue<Wall>(interiorWalls);

        // Loop through old and new interior walls to divide doorways out

        while(interiorWallQueue.Count > 0)
        {
            bool hadDoorway = false;
            Wall w = interiorWallQueue.Dequeue();
            for (int j = 0; j < doors.Length; j++)
            {
                // Check if wall contains doorway

                Doorway doorway = doors[j];
                bool containsDoorway = true;
                for (int k = 0; k < doorway.points.Count; k++)
                {
                    if (!w.Contains(doorway.points[k]))
                    {
                        containsDoorway = false;
                        break;
                    }
                }

                if (containsDoorway)
                {
                    int alongAxis = 0;
                    Wall newWallA, newWallB;

                    if (w.side == Side.Left || w.side == Side.Right)
                    {
                        // Divide east-west wall

                        for (int k = w.start.y; k < w.End.y; k++)
                        {
                            if (doorway.points.Contains(new Vector2Int(w.start.x, k)))
                            {
                                alongAxis = k;
                                break;
                            }
                        }

                        int newY = alongAxis + doorway.points.Count;
                        newWallA = new Wall(w.start.x, w.start.y, alongAxis - w.start.y, w.side, -1);
                        newWallB = new Wall(w.start.x, newY, w.End.y - newY, w.side, -1);
                    }
                    else
                    {
                        // Divide north-south wall

                        for (int k = w.start.x; k < w.End.x; k++)
                        {
                            if (doorway.points.Contains(new Vector2Int(k, w.start.y)))
                            {
                                alongAxis = k;
                                break;
                            }
                        }

                        int newX = alongAxis + doorway.points.Count;
                        newWallA = new Wall(w.start.x, w.start.y, alongAxis - w.start.x, w.side, -1);
                        newWallB = new Wall(newX, w.start.y, w.End.x - newX, w.side, -1);
                    }

                    // Enqueue new walls to check if division necessary

                    if (newWallA.length > 0) interiorWallQueue.Enqueue(newWallA);
                    if (newWallB.length > 0) interiorWallQueue.Enqueue(newWallB);
                    hadDoorway = true;
                    break;
                }
            }

            if (!hadDoorway) newInteriorWalls.Add(w);
        }

        // Set new interior divided walls to interior wall storage

        interiorWalls = newInteriorWalls.ToArray();
    }

    /// <summary>
    /// Gets the base rectangle for a floor plan
    /// </summary>
    /// <param name="r">The random object in use</param>
    /// <param name="size">Size of the floor plan</param>
    /// <returns>Floor plan base rectangle</returns>
    private static Rect GetFloorBaseRect(Random r, int size) => new Rect
    {
        min = new Vector2(r.NextFloat(0.15f, 0.3f) * size,
            r.NextFloat(0.15f, 0.3f) * size),
        max = new Vector2(r.NextFloat(0.7f, 0.85f) * size,
            r.NextFloat(0.7f, 0.85f) * size)
    };

    /// <summary>
    /// Turns an array of walls into a mesh
    /// </summary>
    /// <param name="wallData">Walls to mesh</param>
    /// <returns>Generated wall mesh</returns>
    private Mesh MeshWalls(Wall[] wallData)
    {
        // Setup mesh data

        Mesh wallMesh = new Mesh();
        List<Vector3> verts = new List<Vector3>();
        List<int> tris = new List<int>();
        List<Vector2> uvs = new List<Vector2>();

        // Mesh walls

        float floorHeight = houseParams.floorHeight;
        int t = 0;
        for (int i = 0; i < wallData.Length; i++)
        {
            Wall w = wallData[i];
            int x = w.start.x, y = w.start.y, length = w.length + 1;

            if (w.side == Side.Bottom || w.side == Side.Top)
            {
                verts.Add(new Vector3(x, 0, y));
                verts.Add(new Vector3(x, floorHeight, y));
                verts.Add(new Vector3(x + length, 0, y));
                verts.Add(new Vector3(x + length, floorHeight, y));

                uvs.Add(new Vector2(x, 0));
                uvs.Add(new Vector2(x, floorHeight));
                uvs.Add(new Vector2(x + length, 0));
                uvs.Add(new Vector2(x + length, floorHeight));

                tris.Add(0 + t);
                tris.Add(1 + t);
                tris.Add(2 + t);

                tris.Add(1 + t);
                tris.Add(3 + t);
                tris.Add(2 + t);

                t += 4;

                verts.Add(new Vector3(x, 0, y + 1));
                verts.Add(new Vector3(x, floorHeight, y + 1));
                verts.Add(new Vector3(x + length, 0, y + 1));
                verts.Add(new Vector3(x + length, floorHeight, y + 1));

                uvs.Add(new Vector2(x, 0));
                uvs.Add(new Vector2(x, floorHeight));
                uvs.Add(new Vector2(x + length, 0));
                uvs.Add(new Vector2(x + length, floorHeight));

                tris.Add(2 + t);
                tris.Add(1 + t);
                tris.Add(0 + t);

                tris.Add(2 + t);
                tris.Add(3 + t);
                tris.Add(1 + t);

                t += 4;
            }
            else if (w.side == Side.Left || w.side == Side.Right)
            {
                verts.Add(new Vector3(x, 0, y));
                verts.Add(new Vector3(x, floorHeight, y));
                verts.Add(new Vector3(x, 0, y + length));
                verts.Add(new Vector3(x, floorHeight, y + length));

                uvs.Add(new Vector2(y, 0));
                uvs.Add(new Vector2(y, floorHeight));
                uvs.Add(new Vector2(y + length, 0));
                uvs.Add(new Vector2(y + length, floorHeight));

                tris.Add(2 + t);
                tris.Add(1 + t);
                tris.Add(0 + t);

                tris.Add(2 + t);
                tris.Add(3 + t);
                tris.Add(1 + t);

                t += 4;

                verts.Add(new Vector3(x + 1, 0, y));
                verts.Add(new Vector3(x + 1, floorHeight, y));
                verts.Add(new Vector3(x + 1, 0, y + length));
                verts.Add(new Vector3(x + 1, floorHeight, y + length));

                uvs.Add(new Vector2(y, 0));
                uvs.Add(new Vector2(y, floorHeight));
                uvs.Add(new Vector2(y + length, 0));
                uvs.Add(new Vector2(y + length, floorHeight));

                tris.Add(0 + t);
                tris.Add(1 + t);
                tris.Add(2 + t);

                tris.Add(1 + t);
                tris.Add(3 + t);
                tris.Add(2 + t);

                t += 4;
            }
        }

        // Mesh wall ends

        for (int i = 0; i < wallData.Length; i++)
        {
            Wall w = wallData[i];
            int x = w.start.x, y = w.start.y, length = w.length + 1;

            if (w.side == Side.Bottom || w.side == Side.Top)
            {
                verts.Add(new Vector3(x + 0.001f, 0, y));
                verts.Add(new Vector3(x + 0.001f, floorHeight, y));
                verts.Add(new Vector3(x + 0.001f, 0, y + 1));
                verts.Add(new Vector3(x + 0.001f, floorHeight, y + 1));

                uvs.Add(new Vector2(x, 0));
                uvs.Add(new Vector2(x, floorHeight));
                uvs.Add(new Vector2(x + 1, 0));
                uvs.Add(new Vector2(x + 1, floorHeight));

                tris.Add(2 + t);
                tris.Add(3 + t);
                tris.Add(1 + t);

                tris.Add(2 + t);
                tris.Add(1 + t);
                tris.Add(0 + t);

                t += 4;

                verts.Add(new Vector3(x + length - 0.001f, 0, y));
                verts.Add(new Vector3(x + length - 0.001f, floorHeight, y));
                verts.Add(new Vector3(x + length - 0.001f, 0, y + 1));
                verts.Add(new Vector3(x + length - 0.001f, floorHeight, y + 1));

                uvs.Add(new Vector2(x, 0));
                uvs.Add(new Vector2(x, floorHeight));
                uvs.Add(new Vector2(x + 1, 0));
                uvs.Add(new Vector2(x + 1, floorHeight));

                tris.Add(1 + t);
                tris.Add(3 + t);
                tris.Add(2 + t);

                tris.Add(0 + t);
                tris.Add(1 + t);
                tris.Add(2 + t);

                t += 4;
            }
            else if (w.side == Side.Left || w.side == Side.Right)
            {
                verts.Add(new Vector3(x, 0, y + 0.001f));
                verts.Add(new Vector3(x, floorHeight, y + 0.001f));
                verts.Add(new Vector3(x + 1, 0, y + 0.001f));
                verts.Add(new Vector3(x + 1, floorHeight, y + 0.001f));

                uvs.Add(new Vector2(y, 0));
                uvs.Add(new Vector2(y, floorHeight));
                uvs.Add(new Vector2(y + 1, 0));
                uvs.Add(new Vector2(y + 1, floorHeight));

                tris.Add(0 + t);
                tris.Add(1 + t);
                tris.Add(2 + t);

                tris.Add(1 + t);
                tris.Add(3 + t);
                tris.Add(2 + t);

                t += 4;

                verts.Add(new Vector3(x, 0, y + length - 0.001f));
                verts.Add(new Vector3(x, floorHeight, y + length - 0.001f));
                verts.Add(new Vector3(x + 1, 0, y + length - 0.001f));
                verts.Add(new Vector3(x + 1, floorHeight, y + length - 0.001f));

                uvs.Add(new Vector2(y, 0));
                uvs.Add(new Vector2(y, floorHeight));
                uvs.Add(new Vector2(y + 1, 0));
                uvs.Add(new Vector2(y + 1, floorHeight));

                tris.Add(2 + t);
                tris.Add(1 + t);
                tris.Add(0 + t);

                tris.Add(2 + t);
                tris.Add(3 + t);
                tris.Add(1 + t);

                t += 4;
            }
        }

        // Rescale vertices and uvs

        float ratio = MeshScaleRatio;
        for (int j = 0; j < verts.Count; j++)
            verts[j] = new Vector3(verts[j].x / ratio, verts[j].y, verts[j].z / ratio);
        for (int j = 0; j < uvs.Count; j++)
            uvs[j] = new Vector3(uvs[j].x / ratio, uvs[j].y);

        // Finish mesh building

        wallMesh.SetVertices(verts);
        wallMesh.SetTriangles(tris, 0);
        wallMesh.SetUVs(0, uvs);
        wallMesh.RecalculateBounds();
        wallMesh.RecalculateNormals();
        wallMesh.RecalculateTangents();
        wallMesh.Optimize();

        // Return wall mesh

        return wallMesh;
    }

    /// <summary>
    /// Builds a mesh for a floor of the house
    /// </summary>
    /// <returns>The generated floor mesh</returns>
    public Mesh MeshFloor()
    {
        // Setup mesh

        floorMesh = new Mesh
        {
            indexFormat = UnityEngine.Rendering.IndexFormat.UInt32
        };
        List<Vector3> verts = new List<Vector3>();
        List<int> tris = new List<int>();
        List<Vector2> uvs = new List<Vector2>();

        // Gather floor rectangles

        List<RectInt> rectangles = new List<RectInt>();
        for (int i = 0; i < baseRooms.Length; i++)
        {
            rectangles.Add(baseRooms[i].Rect);
            for (int j = 0; j < baseRooms[i].addonCells.Count; j++)
                rectangles.Add(new RectInt(baseRooms[i].addonCells[j], Vector2Int.one));
        }

        // Generate mesh from rectangles

        int t = 0;
        for (int i = 0; i < rectangles.Count; i++)
        {
            RectInt rect = rectangles[i];
            int x1 = rect.min.x, y1 = rect.min.y, x2 = rect.max.x, y2 = rect.max.y;
            
            verts.Add(new Vector3(x1, 0, y1));
            verts.Add(new Vector3(x2, 0, y1));
            verts.Add(new Vector3(x1, 0, y2));
            verts.Add(new Vector3(x2, 0, y2));

            uvs.Add(new Vector2(x1, y1));
            uvs.Add(new Vector2(x2, y1));
            uvs.Add(new Vector2(x1, y2));
            uvs.Add(new Vector2(x2, y2));

            tris.Add(2 + t);
            tris.Add(1 + t);
            tris.Add(0 + t);

            tris.Add(2 + t);
            tris.Add(3 + t);
            tris.Add(1 + t);

            t += 4;
        }

        // Rescale verticies and uvs

        float ratio = MeshScaleRatio;
        for (int j = 0; j < verts.Count; j++)
            verts[j] = new Vector3(verts[j].x / ratio, verts[j].y, verts[j].z / ratio);
        for (int j = 0; j < uvs.Count; j++)
            uvs[j] = new Vector3(uvs[j].x / ratio, uvs[j].y / ratio);

        // Finish mesh building

        floorMesh.SetVertices(verts);
        floorMesh.SetTriangles(tris, 0);
        floorMesh.SetUVs(0, uvs);
        floorMesh.RecalculateBounds();
        floorMesh.RecalculateNormals();
        floorMesh.RecalculateTangents();
        floorMesh.Optimize();

        // Returns generated floor mesh

        return floorMesh;
    }

    public Mesh MeshDoorways()
    {
        return null;
    }

    /// <summary>
    /// Creates a floor plan texture visualization for this house
    /// </summary>
    private void CalcFloorPlan()
    {
        // Create texture

        floorPlan = Generation.GetColorTexture(new Color32(255, 255, 255, 255), cellSize, cellSize);

        // Add rooms to texture

        Color[] floorColors = new Color[baseRooms.Length];
        for (int i = 0; i < baseRooms.Length; i++)
        {
            //Room r = baseRooms[i];
            //Color c = Color.HSVToRGB(rand.NextFloat(), rand.NextFloat(0.8f, 1f), rand.NextFloat(0f, 1f));
            floorColors[i] = baseRooms[i].archetype.archetypeColor;
            //for (int x = r.min.x; x < r.max.x; x++)
            //    for (int y = r.min.y; y < r.max.y; y++)
            //        floorPlan.SetPixel(x, y, c);
            //for (int j = 0; j < r.addonCells.Count; j++)
            //    floorPlan.SetPixel(r.addonCells[j].x, r.addonCells[j].y, c);
        }

        // Must use this method as there are slight
        // inconsistencies with what some rooms perceive
        // as the boundaries of their orginal rectangle
        for (int x = 0; x < cellSize; x++)
        {
            for (int y = 0; y < cellSize; y++)
            {
                int mapVal = floorMap[x, y];
                if (mapVal > 1)
                    floorPlan.SetPixel(x, y, floorColors[mapVal - 2]);
            }
        }

        // Add walls to texture

        for (int i = 0; i < 2; i++)
        {
            Wall[] tempWalls;
            if (i == 0) tempWalls = exteriorWalls;
            else tempWalls = interiorWalls;

            for (int j = 0; j < tempWalls.Length; j++)
            {
                Wall wall = tempWalls[j];
                Vector2Int curPt = wall.start;

                if (wall.side == Side.Left)
                {
                    for (int k = 0; k <= wall.length; k++)
                    {
                        floorPlan.SetPixel(curPt.x, curPt.y, Color.red);
                        curPt.y++;
                    }
                }
                else if (wall.side == Side.Right)
                {
                    for (int k = 0; k <= wall.length; k++)
                    {
                        floorPlan.SetPixel(curPt.x, curPt.y, Color.green);
                        curPt.y++;
                    }
                }
                else if (wall.side == Side.Bottom)
                {
                    for (int k = 0; k <= wall.length; k++)
                    {
                        floorPlan.SetPixel(curPt.x, curPt.y, Color.blue);
                        curPt.x++;
                    }
                }
                else
                {
                    for (int k = 0; k <= wall.length; k++)
                    {
                        floorPlan.SetPixel(curPt.x, curPt.y, Color.magenta);
                        curPt.x++;
                    }
                }
            }
        }
        
        // Add doorways

        for (int i = 0; i < doors.Length; i++)
        {
            for (int j = 0; j < doors[i].points.Count; j++)
            {
                floorPlan.SetPixel(doors[i].points[j].x, doors[i].points[j].y, Color.gray);
            }
        }

        // Apply texture changes

        floorPlan.Apply();
    }

    public GameObject GetInstance()
    {
        GameObject house = new GameObject("House " + seed),
            exteriorWalls = new GameObject("Exterior Walls"),
            interiorWalls = new GameObject("Interior Walls");
        exteriorWalls.transform.parent = house.transform;
        interiorWalls.transform.parent = house.transform;
        exteriorWalls.AddComponent<MeshFilter>().mesh = ExteriorWalls;
        interiorWalls.AddComponent<MeshFilter>().mesh = InteriorWalls;
        exteriorWalls.AddComponent<MeshRenderer>();
        interiorWalls.AddComponent<MeshRenderer>();
        return house;
    }
}
