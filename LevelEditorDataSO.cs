using System;
using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "LevelEditorData", menuName = "ScriptableObjects/Level Editor/LevelEditorData")]
public class LevelEditorDataSO : ScriptableObject
{
    [Serializable]
    public class TileTypeData
    {
        public string code;
        public Sprite sprite;
    }

    [Serializable]
    public class ObjectiveTypeData
    {
        public string name;
        public string code;
        public Sprite sprite;
        public ObjectiveType type;
    }

    [Serializable]
    public class SpecialTileData
    {
        public string name;
        public string code;
        public Sprite sprite;
    }

    public enum ObjectiveType
    {
        Under,
        Cover,
        Collectable
    }

    [Header("Tile Types")] public List<TileTypeData> tileTypes = new();
    [Header("Objective Types")] public List<ObjectiveTypeData> objectiveTypes = new();
    [Header("Special Tiles")] public List<SpecialTileData> specialTiles = new();
}