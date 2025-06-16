using System;
using System.Collections.Generic;


[Serializable]
public class Level
{
    public int level;
    public int gridX;
    public int gridY;
    public int moveCount;
    public string limitType = "Moves";
    public int timerSeconds = 60;
    public DifficultyLevel difficulty = DifficultyLevel.Easy;
    public List<string> availableColors = new List<string>();
    public List<ObjectiveData> objectives;
    public List<List<TileData>> grid;
}

[Serializable]
public class TileData
{
    public string code;
    public List<string> objTypes = new();
}

[Serializable]
public class ObjectiveData
{
    public string type;
    public string targetObject;
    public int targetCount;
}

[Serializable]
public enum LimitType
{
    Moves,
    Timer
}

[Serializable]
public enum DifficultyLevel
{
    Easy = 0,
    Normal = 1,
    Hard = 2
}
