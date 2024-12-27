using UnityEngine;

/// <summary>
/// Describes different archetypes used for rooms
/// </summary>
public class RoomArchetype
{
    public House.RoomType type;
    public float aspectRatioMin = 0f, aspectRatioMax = float.MaxValue,
        howEssential = 0f;
    public int maxRoomType = int.MaxValue;
    public bool isBiggest = false, isSmallest = false;

    public Color archetypeColor = Color.white;

    public int maxDoors = 4;
    //public House.RoomType[] doorRoomPriority;

    public RoomArchetype(House.RoomType type)
    {
        this.type = type;
        switch (type)
        {
            case House.RoomType.Empty:
                break;
            case House.RoomType.Living:
                aspectRatioMax = 2f;
                howEssential = 0.6f;
                maxRoomType = 1;
                isBiggest = true;
                archetypeColor = Color.yellow;
                break;
            case House.RoomType.Dining:
                aspectRatioMax = 3f;
                howEssential = 0.3f;
                maxRoomType = 2;
                isBiggest = true;
                archetypeColor = Color.cyan;
                break;
            case House.RoomType.Kitchen:
                aspectRatioMax = 3f;
                howEssential = 0.9f;
                maxRoomType = 1;
                isBiggest = true;
                archetypeColor = Color.HSVToRGB(0.2f, 0.5f, 0.5f);
                break;
            case House.RoomType.Bathroom:
                aspectRatioMax = 4f;
                howEssential = 1f;
                maxRoomType = 4;
                archetypeColor = Color.HSVToRGB(1f, 0.5f, 0.5f);
                maxDoors = 1;
                break;
            case House.RoomType.Hallway:
                aspectRatioMin = 3f;
                archetypeColor = Color.HSVToRGB(0.2f, 0.8f, 0.2f);
                break;
            case House.RoomType.Closet:
                isSmallest = true;
                maxRoomType = 2;
                archetypeColor = Color.HSVToRGB(0.7f, 0.6f, 0.6f);
                maxDoors = 1;
                break;
            case House.RoomType.Bedroom:
                aspectRatioMax = 4f;
                maxRoomType = 5;
                archetypeColor = Color.HSVToRGB(0.6f, 0.2f, 0.7f);
                maxDoors = 2;
                break;
            case House.RoomType.Laundry:
                aspectRatioMin = 1.5f;
                howEssential = 0.8f;
                maxRoomType = 1;
                archetypeColor = Color.HSVToRGB(0.5f, 0.2f, 1f);
                maxDoors = 1;
                break;
            case House.RoomType.Guest:
                aspectRatioMax = 3f;
                maxRoomType = 2;
                archetypeColor = Color.HSVToRGB(0.8f, 0.8f, 0.5f);
                maxDoors = 2;
                break;
            case House.RoomType.Basement:
                isBiggest = true;
                maxRoomType = 1;
                archetypeColor = Color.gray;
                break;
            case House.RoomType.Garage:
                aspectRatioMax = 1.5f;
                howEssential = 0.5f;
                maxRoomType = 2;
                break;
            case House.RoomType.Gaming:
                aspectRatioMax = 4f;
                maxRoomType = 2;
                maxDoors = 2;
                break;
            case House.RoomType.Gym:
                howEssential = 0.2f;
                maxRoomType = 2;
                maxDoors = 2;
                break;
            default:
                break;
        }
    }
}
