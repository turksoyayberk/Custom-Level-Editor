using System;
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using System.Text;

public enum GridFormat
{
    Standard,
    SimpleCode,
    CodeOnly
}

[Serializable]
public struct JsonFormatConfig
{
    public GridFormat gridFormat;
    public string objectiveTypeField; // "type"
    public string targetObjectField; // "targetObject" 
    public string targetCountField; // "targetCount"
    public bool useObjTypes;
    public string customGridStructure;
}

public class CustomLevelEditor : EditorWindow
{
    #region Theme Configuration

    private struct EditorTheme
    {
        public static readonly Color Primary = new Color(0.25f, 0.5f, 0.9f);
        public static readonly Color Secondary = new Color(0.3f, 0.3f, 0.4f);
        public static readonly Color Success = new Color(0.2f, 0.8f, 0.4f);
        public static readonly Color Warning = new Color(0.9f, 0.7f, 0.2f);
        public static readonly Color Danger = new Color(0.8f, 0.3f, 0.3f);
        public static readonly Color Active = new Color(1f, 0.8f, 0.2f);
        public static readonly Color Inactive = new Color(0.7f, 0.7f, 0.7f);
        public static readonly Color Panel = new Color(0.9f, 0.9f, 0.9f, 0.3f);
        public static readonly Color GridLine = new Color(0.4f, 0.4f, 0.4f, 0.6f);
        public static readonly Color Selection = new Color(1f, 1f, 0f, 0.4f);
    }

    #endregion

    #region Constants

    private const float Padding = 5f;
    private const int MaxUndoHistory = 10;
    private const string SavePath = "Assets/Data/Levels";
    private const string TileDataPath = "Assets/Resources/LevelEditorData.asset";
    private const string AllColorCode = "all";
    private const string RandomCode = "random";

    #endregion

    #region Core Data

    private LevelEditorDataSO _spritesData;
    private TileData[,] _gridData;
    private string[] _levelFiles;

    #endregion

    #region Grid Settings

    private int _gridSizeX = 6;
    private int _gridSizeY = 6;
    private float _cellSize = 50f;

    #endregion

    #region Level Configuration

    private int _selectedLevel = 1;
    private int _moveCount = 20;
    private LimitType _limitType = LimitType.Moves;
    private int _timerSeconds = 60;
    private DifficultyLevel _difficultyLevel = DifficultyLevel.Easy;
    private readonly List<string> _availableColors = new();

    // JSON Format System
    private GridFormat _selectedGridFormat = GridFormat.Standard;
    private JsonFormatConfig _jsonConfig;
    private bool _showFormatBuilder;

    #endregion

    #region Tool States

    private string _selectedTileType;
    private bool _isPlacingObjective;
    private string _selectedObjective;
    private bool _isPlacingCollectable;
    private string _selectedCollectable;
    private bool _isEmptyTile;
    private bool _isPlacingSpecial;
    private string _selectedSpecial;
    private bool _isErasing;

    #endregion

    #region Fill Tools

    private bool _isRowFillMode;
    private bool _isColumnFillMode;
    private bool _isRectangleFillMode;
    private bool _isSelecting;
    private Vector2Int _selectionStart;
    private Vector2Int _selectionEnd;

    #endregion

    #region Color Objectives

    private bool _isAddingColorObjective;
    private string _selectedColorObjective = "r";
    private int _colorObjectiveCount = 5;
    private readonly List<ObjectiveData> _colorObjectives = new();

    #endregion

    #region UI State

    private Vector2 _scrollPosition;
    private bool _showToolbar = true;
    private bool _showTileTools = true;
    private bool _showObjectiveTools = true;
    private bool _showLevelSettings = true;
    private bool _showGridSettings;
    private bool _showQuickActions;
    private bool _showGrid = true;
    private bool _showCoordinates = true;
    private float _gridOpacity = 0.1f;

    #endregion

    #region Undo System

    private readonly List<TileData[,]> _undoStack = new();
    private readonly List<TileData[,]> _redoStack = new();

    #endregion

    #region UI Resources

    private Texture2D _eraserIcon;

    #endregion

    #region Unity Events

    [MenuItem("Tools/Custom Level Editor")]
    public static void ShowWindow()
    {
        GetWindow<CustomLevelEditor>("Level Editor");
    }

    private void OnEnable()
    {
        LoadEditorResources();
        RefreshLevelList();
        InitializeDefaultSelections();
        InitializeGrid();
        CreateEraserIcon();
    }

    private void OnDisable()
    {
        CleanupResources();
    }

    #endregion

    #region Initialization

    private void LoadEditorResources()
    {
        _spritesData = AssetDatabase.LoadAssetAtPath<LevelEditorDataSO>(TileDataPath);
    }

    private void CreateEraserIcon()
    {
        _eraserIcon = new Texture2D(40, 40);
        for (int y = 0; y < 40; y++)
        {
            for (int x = 0; x < 40; x++)
            {
                bool isLine = (Mathf.Abs(x - y) < 5) || (Mathf.Abs(x - (39 - y)) < 5);
                _eraserIcon.SetPixel(x, y, isLine ? Color.red : new Color(0, 0, 0, 0));
            }
        }

        _eraserIcon.Apply();
    }

    private void CleanupResources()
    {
        ClearUndoRedoStacks();
        if (_eraserIcon != null)
        {
            DestroyImmediate(_eraserIcon);
        }
    }

    private void InitializeDefaultSelections()
    {
        if (_spritesData == null) return;

        SetupJsonFormatConfiguration();
        SetDefaultTileType();
        SetDefaultObjective();
        SetDefaultCollectable();
        SetDefaultSpecialTile();
        InitializeAvailableColors();
    }

    private void SetupJsonFormatConfiguration()
    {
        switch (_selectedGridFormat)
        {
            case GridFormat.Standard:
                _jsonConfig = new JsonFormatConfig
                {
                    gridFormat = GridFormat.Standard,
                    objectiveTypeField = "type",
                    targetObjectField = "targetObject",
                    targetCountField = "targetCount",
                    useObjTypes = true,
                    customGridStructure = ""
                };
                break;

            case GridFormat.SimpleCode:
                _jsonConfig = new JsonFormatConfig
                {
                    gridFormat = GridFormat.SimpleCode,
                    objectiveTypeField = "type",
                    targetObjectField = "target",
                    targetCountField = "count",
                    useObjTypes = false,
                    customGridStructure = "string"
                };
                break;

            case GridFormat.CodeOnly:
                _jsonConfig = new JsonFormatConfig
                {
                    gridFormat = GridFormat.CodeOnly,
                    objectiveTypeField = "type",
                    targetObjectField = "object",
                    targetCountField = "amount",
                    useObjTypes = false,
                    customGridStructure = "{\"tile\": \"value\"}"
                };
                break;
        }
    }

    private void SetDefaultTileType()
    {
        if (_spritesData.tileTypes?.Count > 0)
        {
            _selectedTileType = _spritesData.tileTypes[0].code;
        }
        }

    private void SetDefaultObjective()
    {
        var underObjectives = _spritesData.objectiveTypes?
            .Where(o => o.type == LevelEditorDataSO.ObjectiveType.Under).ToList();
        if (underObjectives?.Count > 0)
        {
            _selectedObjective = underObjectives[0].code;
        }
        }

    private void SetDefaultCollectable()
    {
        var collectables = _spritesData.objectiveTypes?
            .Where(o => o.type == LevelEditorDataSO.ObjectiveType.Collectable).ToList();
        if (collectables?.Count > 0)
        {
            _selectedCollectable = collectables[0].code;
        }
        }

    private void SetDefaultSpecialTile()
        {
        if (_spritesData.specialTiles?.Count > 0)
        {
            _selectedSpecial = _spritesData.specialTiles[0].code;
        }
    }

    private void InitializeAvailableColors()
    {
        _availableColors.Clear();
        
        if (_spritesData?.tileTypes != null)
        {
            foreach (var tileType in _spritesData.tileTypes)
            {
                _availableColors.Add(tileType.code);
            }
        }
        
        if (_availableColors.Count == 0)
        {
            _availableColors.Add("r");
        }
    }

    #endregion

    #region File Management

    private void RefreshLevelList()
    {
        if (!Directory.Exists(SavePath))
        {
            Directory.CreateDirectory(SavePath);
        }

        _levelFiles = Directory.GetFiles(SavePath, "level_*.json")
            .Select(Path.GetFileNameWithoutExtension)
            .OrderBy(f => int.Parse(f.Split('_')[1]))
            .ToArray();
    }

    #endregion

    #region Grid Management

    private void InitializeGrid()
    {
        try
        {
            ClearUndoRedoStacks();
            CreateEmptyGrid();
            ValidateCollectableSelection();
            Repaint();
        }
        catch (Exception e)
        {
            Debug.LogError($"Grid initialization error: {e.Message}");
        }
    }

    private void CreateEmptyGrid()
    {
            _gridData = new TileData[_gridSizeY, _gridSizeX];
        string defaultTileCode = _selectedTileType ?? "r";

            for (int y = 0; y < _gridSizeY; y++)
            {
                for (int x = 0; x < _gridSizeX; x++)
                {
                    _gridData[y, x] = new TileData
                    {
                        code = defaultTileCode,
                        objTypes = new List<string>()
                    };
            }
                }
            }

    private void ValidateCollectableSelection()
    {
            if (_spritesData != null && _isPlacingCollectable)
            {
                var collectables = _spritesData.objectiveTypes
                    .Where(o => o.type == LevelEditorDataSO.ObjectiveType.Collectable).ToList();
                if (!collectables.Any(c => c.code == _selectedCollectable) && collectables.Count > 0)
                {
                    _selectedCollectable = collectables[0].code;
                }
        }
    }

    private void ResizeGrid()
    {
        try
        {
            if (_gridData == null)
            {
                InitializeGrid();
                return;
            }

            var oldData = _gridData;
            int oldSizeY = oldData.GetLength(0);
            int oldSizeX = oldData.GetLength(1);

            CreateEmptyGrid();
            CopyOldDataToNewGrid(oldData, oldSizeX, oldSizeY);

            Debug.Log($"Grid resized from ({oldSizeX},{oldSizeY}) to ({_gridSizeX},{_gridSizeY})");
            Repaint();
        }
        catch (Exception e)
        {
            Debug.LogError($"Grid resize error: {e.Message}");
            InitializeGrid();
        }
    }

    private void CopyOldDataToNewGrid(TileData[,] oldData, int oldSizeX, int oldSizeY)
    {
            int copyY = Math.Min(oldSizeY, _gridSizeY);
            int copyX = Math.Min(oldSizeX, _gridSizeX);

            for (int y = 0; y < copyY; y++)
            {
                for (int x = 0; x < copyX; x++)
                {
                    _gridData[y, x] = new TileData
                    {
                        code = oldData[y, x].code,
                        objTypes = new List<string>(oldData[y, x].objTypes)
                    };
                }
        }
    }

    #endregion

    #region Main GUI

    private void OnGUI()
    {
        try
        {
            HandleKeyboardShortcuts();

            if (_spritesData == null)
            {
                ShowMissingDataWarning();
                return;
            }

            DrawMainLayout();
        }
        catch (Exception e)
        {
            Debug.LogError($"GUI Error: {e.Message}");
            EditorGUIUtility.ExitGUI();
        }
    }

    private void ShowMissingDataWarning()
    {
        EditorGUILayout.HelpBox(
            "Please assign TileSpritesData asset in Resources folder!",
            MessageType.Error);
    }

    private void DrawMainLayout()
    {
            EditorGUILayout.BeginVertical();

            DrawModernHeader();
            EditorGUILayout.Space(5);

            EditorGUILayout.BeginHorizontal();
        DrawLeftControlPanels();
        EditorGUILayout.Space(10);
        DrawRightGridPanel();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(5);
        DrawBottomPanel();
        EditorGUILayout.Space(3);
        DrawStatusBar();

        EditorGUILayout.EndVertical();
    }

    private void DrawLeftControlPanels()
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(position.width * 0.6f));
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            DrawLevelSettingsPanel();
            EditorGUILayout.Space(3);
            DrawToolsPanel();
            EditorGUILayout.Space(3);
        DrawGridControlsPanel();
            EditorGUILayout.Space(3);
            DrawQuickActionsPanel();

            EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    private void DrawRightGridPanel()
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(position.width * 0.4f));
        GUILayout.FlexibleSpace();

        DrawCenteredGridTitle();
            EditorGUILayout.Space(10);
        DrawCenteredGrid();

            GUILayout.FlexibleSpace();
        EditorGUILayout.EndVertical();
    }

    private void DrawCenteredGridTitle()
    {
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label("🎮 Level Grid", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
    }

    private void DrawCenteredGrid()
    {
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
        DrawGrid();
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
    }

    #endregion

    #region Input Handling

    private void HandleKeyboardShortcuts()
    {
        Event e = Event.current;

        if (e.type == EventType.KeyDown)
        {
            HandleKeyInput(e);
        }
    }

    private void HandleKeyInput(Event e)
        {
            switch (e.keyCode)
            {
                case KeyCode.E:
                    ToggleEraser();
                    e.Use();
                    Repaint();
                    break;

                case KeyCode.Q:
                    ToggleEmptyTile();
                    e.Use();
                    Repaint();
                    break;

                case KeyCode.S when e.control:
                    SaveLevel();
                    e.Use();
                    break;

                case KeyCode.N when e.control:
                    InitializeGrid();
                    e.Use();
                    break;

                case KeyCode.Z when e.control:
                    Undo();
                    e.Use();
                    break;

                case KeyCode.Y when e.control:
                    Redo();
                    e.Use();
                    break;

                case KeyCode.Alpha1:
                case KeyCode.Alpha2:
                case KeyCode.Alpha3:
                case KeyCode.Alpha4:
                HandleTileSelection(e.keyCode);
                    e.Use();
                    Repaint();
                    break;
            }
        }

    private void HandleTileSelection(KeyCode keyCode)
    {
        int index = keyCode - KeyCode.Alpha1;
        SelectTileByIndex(index);
    }

    #endregion

    #region Tool State Management

    private void ToggleEraser()
    {
        _isErasing = !_isErasing;
        if (_isErasing)
        {
            ClearOtherToolStates();
        }
    }

    private void ToggleEmptyTile()
    {
        _isEmptyTile = !_isEmptyTile;
        if (_isEmptyTile)
        {
            ClearOtherToolStates();
            _isErasing = false;
        }
    }

    private void SelectTileByIndex(int index)
    {
        if (IsValidTileIndex(index))
        {
            _selectedTileType = _spritesData.tileTypes[index].code;
            ClearOtherToolStates();
            _isErasing = false;
            _isEmptyTile = false;
        }
    }

    private bool IsValidTileIndex(int index)
    {
        return _spritesData?.tileTypes != null &&
               index >= 0 &&
               index < _spritesData.tileTypes.Count;
    }

    private void ClearOtherToolStates()
    {
        _isPlacingObjective = false;
        _isPlacingCollectable = false;
        _isPlacingSpecial = false;
        _selectedTileType = null;
    }

    private void ToggleRowFillMode()
    {
        _isRowFillMode = !_isRowFillMode;
        if (_isRowFillMode)
        {
            DisableOtherFillModes();
        }
    }

    private void ToggleColumnFillMode()
    {
        _isColumnFillMode = !_isColumnFillMode;
        if (_isColumnFillMode)
        {
            DisableOtherFillModes();
            _isRowFillMode = false;
        }
    }

    private void ToggleRectangleFillMode()
    {
        _isRectangleFillMode = !_isRectangleFillMode;
        if (_isRectangleFillMode)
        {
            DisableOtherFillModes();
            _isRowFillMode = false;
            _isColumnFillMode = false;
        }
    }

    private void DisableOtherFillModes()
    {
        _isColumnFillMode = false;
        _isRectangleFillMode = false;
        _isSelecting = false;
    }

    #endregion

    #region Undo/Redo System

    private void SaveStateForUndo()
    {
        if (_gridData == null) return;

        var gridCopy = DeepCopyGrid(_gridData);
        _undoStack.Add(gridCopy);

        if (_undoStack.Count > MaxUndoHistory)
        {
            _undoStack.RemoveAt(0);
        }

        _redoStack.Clear();
    }

    private void Undo()
    {
        if (!CanUndo())
        {
            Debug.Log("Nothing to undo!");
            return;
        }

        _redoStack.Add(DeepCopyGrid(_gridData));
        RestoreLastState();
        Repaint();

        Debug.Log($"Undo successful! Remaining undo: {_undoStack.Count}");
    }

    private void Redo()
    {
        if (!CanRedo())
        {
            Debug.Log("Nothing to redo!");
            return;
        }

        _undoStack.Add(DeepCopyGrid(_gridData));
        RestoreRedoState();
        Repaint();

        Debug.Log($"Redo successful! Remaining redo: {_redoStack.Count}");
    }

    private bool CanUndo() => _undoStack.Count > 0;
    private bool CanRedo() => _redoStack.Count > 0;

    private void RestoreLastState()
    {
        var lastState = _undoStack[_undoStack.Count - 1];
        _undoStack.RemoveAt(_undoStack.Count - 1);
        _gridData = lastState;
    }

    private void RestoreRedoState()
    {
        var redoState = _redoStack[_redoStack.Count - 1];
        _redoStack.RemoveAt(_redoStack.Count - 1);
        _gridData = redoState;
    }

    private TileData[,] DeepCopyGrid(TileData[,] source)
    {
        int height = source.GetLength(0);
        int width = source.GetLength(1);
        var copy = new TileData[height, width];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                copy[y, x] = new TileData
                {
                    code = source[y, x].code,
                    objTypes = new List<string>(source[y, x].objTypes)
                };
            }
        }

        return copy;
    }

    private void ClearUndoRedoStacks()
    {
        int totalCleared = _undoStack.Count + _redoStack.Count;
        _undoStack.Clear();
        _redoStack.Clear();

        if (totalCleared > 0)
        {
            Debug.Log($"Undo/Redo history cleared! ({totalCleared} actions removed)");
        }
    }

    #endregion

    #region UI Drawing

    private void DrawModernHeader()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("🎮 Level Editor", EditorStyles.largeLabel);
        GUILayout.FlexibleSpace();

        DrawShortcutsInfo();
        DrawActiveModeStatus();

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
    }

    private void DrawShortcutsInfo()
    {
        EditorGUILayout.LabelField("💡 Shortcuts: E=Eraser | Q=Empty | 1-4=Tiles | Ctrl+S=Save | Ctrl+Z/Y=Undo/Redo",
            EditorStyles.miniLabel);
    }

    private void DrawActiveModeStatus()
    {
        if (HasActiveFillMode())
        {
            string activeMode = GetActiveFillModeName();
            EditorGUILayout.LabelField($"🎯 Active: {activeMode} Mode", EditorStyles.miniLabel);
        }
    }

    private bool HasActiveFillMode()
    {
        return _isRowFillMode || _isColumnFillMode || _isRectangleFillMode;
    }

    private string GetActiveFillModeName()
    {
        if (_isRowFillMode) return "Row Fill";
        if (_isColumnFillMode) return "Column Fill";
        return "Rectangle Fill";
    }

    private void DrawGridSizeControls()
    {
            EditorGUILayout.BeginVertical(GUILayout.Width(120));
            EditorGUILayout.LabelField("Grid Size:", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.LabelField("W:", GUILayout.Width(20));
            if (int.TryParse(EditorGUILayout.TextField(_gridSizeX.ToString(), GUILayout.Width(40)), out int newX))
            {
                if (newX > 0 && newX <= 20 && newX != _gridSizeX)
                {
                    _gridSizeX = newX;
                ResizeGrid();
                }
            }

            EditorGUILayout.LabelField("H:", GUILayout.Width(20));
            if (int.TryParse(EditorGUILayout.TextField(_gridSizeY.ToString(), GUILayout.Width(40)), out int newY))
            {
                if (newY > 0 && newY <= 20 && newY != _gridSizeY)
                {
                    _gridSizeY = newY;
                ResizeGrid();
                }
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
    }

    private void DrawGameLimitsControls()
    {
            EditorGUILayout.BeginVertical(GUILayout.Width(150));
            EditorGUILayout.LabelField("Game Limits:", EditorStyles.boldLabel);
            _limitType = (LimitType)EditorGUILayout.EnumPopup("Type:", _limitType);

            if (_limitType == LimitType.Moves)
            {
                _moveCount = EditorGUILayout.IntField("Moves:", _moveCount);
            }
            else
            {
                _timerSeconds = EditorGUILayout.IntField("Seconds:", _timerSeconds);
            }

            EditorGUILayout.EndVertical();
    }

    private void DrawLevelInfoControls()
    {
            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField("Level Info:", EditorStyles.boldLabel);
            _selectedLevel = EditorGUILayout.IntField("Level ID:", _selectedLevel);
            _difficultyLevel = (DifficultyLevel)EditorGUILayout.EnumPopup("Difficulty:", _difficultyLevel);

        EditorGUILayout.Space(3);

        // JSON Format Builder
        EditorGUILayout.LabelField("JSON Format:", EditorStyles.boldLabel);
        var newFormat = (GridFormat)EditorGUILayout.EnumPopup("Grid Format:", _selectedGridFormat);
        if (newFormat != _selectedGridFormat)
        {
            _selectedGridFormat = newFormat;
            SetupJsonFormatConfiguration();
        }

        if (GUILayout.Button("🔧 Format Builder", GUILayout.Height(25)))
        {
            _showFormatBuilder = !_showFormatBuilder;
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawLevelSettingsPanel()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        _showLevelSettings =
            EditorGUILayout.Foldout(_showLevelSettings, "⚙️ Level Settings", true, EditorStyles.foldoutHeader);

        if (_showLevelSettings)
        {
            // Main settings layout
            EditorGUILayout.BeginVertical();

            // First row - Grid and Level information
        EditorGUILayout.BeginHorizontal();
            DrawGridSizeControls();
            EditorGUILayout.Space(10);
            DrawLevelInfoControls();
        EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(3);

            // Second row - Game Limits
            EditorGUILayout.BeginHorizontal();
            DrawGameLimitsControls();
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(5);

            // Available Colors section
            DrawCompactAvailableTilesSection();

            // JSON Format Builder Panel
            if (_showFormatBuilder)
            {
                DrawJsonFormatBuilder();
            }
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawCompactAvailableTilesSection()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("🎨 Available Colors:", EditorStyles.boldLabel, GUILayout.Width(120));

        // Quick action buttons in header
        if (GUILayout.Button("All", GUILayout.Width(35), GUILayout.Height(20)))
            {
                SelectAllColors();
            }

        if (GUILayout.Button("Clear", GUILayout.Width(40), GUILayout.Height(20)))
            {
                ClearAllColors();
            }

        GUILayout.FlexibleSpace();
        EditorGUILayout.LabelField($"({_availableColors.Count}/{_spritesData?.tileTypes?.Count ?? 0})",
                EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();

        if (_spritesData != null && _spritesData.tileTypes != null)
        {
            // Color buttons in single row
            EditorGUILayout.BeginHorizontal();

            foreach (var tileType in _spritesData.tileTypes)
            {
                bool isAvailable = _availableColors.Contains(tileType.code);
                GUI.backgroundColor = isAvailable ? EditorTheme.Success : EditorTheme.Inactive;

                var buttonContent = new GUIContent(tileType.sprite.texture,
                    isAvailable ? $"{tileType.code} - Available" : $"{tileType.code} - Disabled");

                if (GUILayout.Button(buttonContent, GUILayout.Width(30), GUILayout.Height(30)))
                {
                    ToggleAvailableColor(tileType.code);
                }
            }

            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();
        }
        else
        {
            EditorGUILayout.HelpBox("TileSpritesData not found!", MessageType.Warning);
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawToolsPanel()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        _showToolbar = EditorGUILayout.Foldout(_showToolbar, "🛠️ Tools & Tiles", true, EditorStyles.foldoutHeader);

        if (_showToolbar)
        {
            // Basic tools
            DrawBasicTools();
            EditorGUILayout.Space(3);

            // Tile tools section
            _showTileTools = EditorGUILayout.Foldout(_showTileTools, "🎨 Tiles & Special", true);
            if (_showTileTools)
            {
                DrawTileSelector();
                EditorGUILayout.Space(3);
            }

            EditorGUILayout.Space(3);

            // Objective tools section  
            _showObjectiveTools = EditorGUILayout.Foldout(_showObjectiveTools, "🎯 Objectives", true);
            if (_showObjectiveTools)
            {
                DrawObjectiveSelector();
            }
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawBasicTools()
    {
        EditorGUILayout.LabelField("Basic Tools:", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();

        // Eraser tool with professional styling
        GUI.backgroundColor = _isErasing ? EditorTheme.Active : EditorTheme.Inactive;
        if (GUILayout.Button(new GUIContent("🗑️ Eraser (E)", _eraserIcon), GUILayout.Width(90), GUILayout.Height(35)))
        {
            ToggleEraser();
        }

        // Empty tile tool with professional styling
        GUI.backgroundColor = _isEmptyTile ? EditorTheme.Active : EditorTheme.Inactive;
        if (GUILayout.Button(new GUIContent("⭕ Empty (Q)"), GUILayout.Width(90), GUILayout.Height(35)))
        {
            ToggleEmptyTile();
        }

        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();
    }

    private void DrawGridControlsPanel()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        _showGridSettings =
            EditorGUILayout.Foldout(_showGridSettings, "🎯 Grid Settings", true, EditorStyles.foldoutHeader);

        if (_showGridSettings)
        {
            // Compact horizontal layout for grid settings
            EditorGUILayout.BeginHorizontal();

            // View options
            EditorGUILayout.BeginVertical(GUILayout.Width(120));
            EditorGUILayout.LabelField("View:", EditorStyles.boldLabel);
            _showGrid = EditorGUILayout.Toggle("Grid Lines", _showGrid, GUILayout.Width(100));
            _showCoordinates = EditorGUILayout.Toggle("Coords", _showCoordinates, GUILayout.Width(100));
            EditorGUILayout.EndVertical();

            // Zoom controls
            EditorGUILayout.BeginVertical(GUILayout.Width(100));
            EditorGUILayout.LabelField("Zoom:", EditorStyles.boldLabel);
            if (GUILayout.Button("🔍+", GUILayout.Height(25), GUILayout.Width(45)))
                _cellSize = Mathf.Min(_cellSize + 10, 100);
            if (GUILayout.Button("🔍-", GUILayout.Height(25), GUILayout.Width(45)))
                _cellSize = Mathf.Max(_cellSize - 10, 20);
            EditorGUILayout.EndVertical();

            // Opacity slider
            EditorGUILayout.BeginVertical(GUILayout.Width(80));
            EditorGUILayout.LabelField("Opacity:", EditorStyles.boldLabel);
            _gridOpacity = EditorGUILayout.Slider("", _gridOpacity, 0.1f, 1f);
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawQuickActionsPanel()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        _showQuickActions =
            EditorGUILayout.Foldout(_showQuickActions, "⚡ Quick Actions", true, EditorStyles.foldoutHeader);

        if (_showQuickActions)
        {
            // Fill Tools Section
            EditorGUILayout.LabelField("Fill Tools:", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();

            GUI.backgroundColor = _isRowFillMode ? EditorTheme.Active : EditorTheme.Inactive;
            if (GUILayout.Button("📏 Row Fill", GUILayout.Height(30), GUILayout.Width(80)))
            {
                ToggleRowFillMode();
            }

            GUI.backgroundColor = _isColumnFillMode ? EditorTheme.Active : EditorTheme.Inactive;
            if (GUILayout.Button("📐 Column Fill", GUILayout.Height(30), GUILayout.Width(80)))
            {
                ToggleColumnFillMode();
            }

            GUI.backgroundColor = _isRectangleFillMode ? EditorTheme.Active : EditorTheme.Inactive;
            if (GUILayout.Button("⬛ Rectangle Fill", GUILayout.Height(30), GUILayout.Width(100)))
            {
                ToggleRectangleFillMode();
            }

            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            // Quick Actions Section
            EditorGUILayout.Space(3);
            EditorGUILayout.LabelField("Quick Actions:", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("🎲 Random Fill", GUILayout.Height(30)))
                RandomFillGrid();

            if (GUILayout.Button("🧹 Clear All", GUILayout.Height(30)))
                ClearGrid();

            if (GUILayout.Button("🔄 Mirror H", GUILayout.Height(30)))
                MirrorGridHorizontal();

            if (GUILayout.Button("🔃 Mirror V", GUILayout.Height(30)))
                MirrorGridVertical();

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawBottomPanel()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        EditorGUILayout.BeginHorizontal();

        // Save button - Success color
        GUI.backgroundColor = EditorTheme.Success;
        if (GUILayout.Button("💾 Save (Ctrl+S)", GUILayout.Height(30)))
        {
            SaveLevel();
        }

        GUI.backgroundColor = Color.white;

        // Load button - Primary color
        GUI.backgroundColor = EditorTheme.Primary;
        if (GUILayout.Button("📁 Load", GUILayout.Height(30)))
        {
            ShowLoadLevelMenu();
        }

        GUI.backgroundColor = Color.white;

        // New button - Warning color
        GUI.backgroundColor = EditorTheme.Warning;
        if (GUILayout.Button("🆕 New (Ctrl+N)", GUILayout.Height(30)))
        {
            InitializeGrid();
        }

        GUI.backgroundColor = Color.white;

        // Debug button - Secondary color
        GUI.backgroundColor = EditorTheme.Secondary;
        if (GUILayout.Button("🔍 Debug", GUILayout.Height(30)))
        {
            DebugPrintGrid();
        }

        GUI.backgroundColor = Color.white;

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
    }

    private void DrawStatusBar()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        // Status bar background with theme color
        var statusRect = GUILayoutUtility.GetRect(0, 25, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(statusRect, EditorTheme.Panel);

        EditorGUILayout.BeginHorizontal();

        // Level info
        GUI.color = EditorTheme.Primary;
        EditorGUILayout.LabelField($"📋 Level: {_selectedLevel}", EditorStyles.boldLabel, GUILayout.Width(80));
        GUI.color = Color.white;

        EditorGUILayout.LabelField("|", GUILayout.Width(10));

        // Grid stats
        if (_gridData != null)
        {
            int totalTiles = _gridSizeX * _gridSizeY;
            int objectiveCount = GetTotalObjectiveCount();

            GUI.color = EditorTheme.Secondary;
            EditorGUILayout.LabelField($"🎯 Grid: {_gridSizeX}x{_gridSizeY} ({totalTiles} tiles)",
                EditorStyles.miniLabel, GUILayout.Width(120));
            EditorGUILayout.LabelField($"📦 Objectives: {objectiveCount}", EditorStyles.miniLabel, GUILayout.Width(80));
            GUI.color = Color.white;
        }

        GUILayout.FlexibleSpace();

        // Undo/Redo status
        GUI.color = EditorTheme.Warning;
        EditorGUILayout.LabelField($"↶ Undo: {_undoStack.Count}", EditorStyles.miniLabel, GUILayout.Width(60));
        EditorGUILayout.LabelField($"↷ Redo: {_redoStack.Count}", EditorStyles.miniLabel, GUILayout.Width(60));
        GUI.color = Color.white;

        EditorGUILayout.LabelField("|", GUILayout.Width(10));

        // Available colors status
        GUI.color = EditorTheme.Success;
        EditorGUILayout.LabelField($"🎨 Colors: {_availableColors.Count}", EditorStyles.miniLabel, GUILayout.Width(60));
        GUI.color = Color.white;

        EditorGUILayout.LabelField("|", GUILayout.Width(10));

        // Format status
        GUI.color = EditorTheme.Primary;
        EditorGUILayout.LabelField($"📋 Format: {_selectedGridFormat}", EditorStyles.miniLabel, GUILayout.Width(100));
        GUI.color = Color.white;

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
    }

    private int GetTotalObjectiveCount()
    {
        if (_gridData == null) return 0;

        int count = 0;
        for (int y = 0; y < _gridSizeY; y++)
        {
            for (int x = 0; x < _gridSizeX; x++)
            {
                count += _gridData[y, x].objTypes.Count;
            }
        }

        return count + _colorObjectives.Count;
    }


    private void ShowLoadLevelMenu()
    {
        var menu = new GenericMenu();

        foreach (var levelFile in _levelFiles)
        {
            var level = int.Parse(levelFile.Split('_')[1]);
            menu.AddItem(new GUIContent($"Level {level}"), false, () => { LoadLevel(level); });
        }

        menu.ShowAsContext();
    }

    // Quick Actions implementations
    private void RandomFillGrid()
    {
        if (_gridData == null || _spritesData == null || _spritesData.tileTypes == null) return;

        SaveStateForUndo();

        // Use only available colors
        var availableTiles = _spritesData.tileTypes.Where(t => _availableColors.Contains(t.code)).ToList();

        if (availableTiles.Count == 0)
        {
            EditorUtility.DisplayDialog("Warning", "No available colors selected! Please select some colors first.",
                "OK");
            return;
        }

        for (int y = 0; y < _gridSizeY; y++)
        {
            for (int x = 0; x < _gridSizeX; x++)
            {
                var randomTile = availableTiles[UnityEngine.Random.Range(0, availableTiles.Count)];
                _gridData[y, x].code = randomTile.code;
                _gridData[y, x].objTypes.Clear();
            }
        }

        Repaint();
    }

    private void ClearGrid()
    {
        if (EditorUtility.DisplayDialog("Clear Grid", "Are you sure you want to clear the entire grid?", "Yes",
                "Cancel"))
        {
            SaveStateForUndo();
            InitializeGrid();
        }
    }

    private void MirrorGridHorizontal()
    {
        if (_gridData == null) return;

        SaveStateForUndo();

        for (int y = 0; y < _gridSizeY; y++)
        {
            for (int x = 0; x < _gridSizeX / 2; x++)
            {
                int mirrorX = _gridSizeX - 1 - x;
                var temp = _gridData[y, x];
                _gridData[y, x] = _gridData[y, mirrorX];
                _gridData[y, mirrorX] = temp;
            }
        }

        Repaint();
    }

    private void MirrorGridVertical()
    {
        if (_gridData == null) return;

        SaveStateForUndo();

        for (int y = 0; y < _gridSizeY / 2; y++)
        {
            for (int x = 0; x < _gridSizeX; x++)
            {
                int mirrorY = _gridSizeY - 1 - y;
                var temp = _gridData[y, x];
                _gridData[y, x] = _gridData[mirrorY, x];
                _gridData[mirrorY, x] = temp;
            }
        }

        Repaint();
        EditorUtility.DisplayDialog("Mirror", "Grid mirrored vertically!", "OK");
    }

    private void FillRow(int row)
    {
        if (_gridData == null || row < 0 || row >= _gridSizeY) return;

        for (int x = 0; x < _gridSizeX; x++)
        {
            ApplyCurrentToolToCell(x, row);
        }

        Repaint();
        //EditorUtility.DisplayDialog("Row Fill", $"Row {row} filled with current tool!", "OK");
    }

    private void FillColumn(int column)
    {
        if (_gridData == null || column < 0 || column >= _gridSizeX) return;

        for (int y = 0; y < _gridSizeY; y++)
        {
            ApplyCurrentToolToCell(column, y);
        }

        Repaint();
        //EditorUtility.DisplayDialog("Column Fill", $"Column {column} filled with current tool!", "OK");
    }

    private void FillRectangle()
    {
        if (_gridData == null) return;

        int startX = Math.Min(_selectionStart.x, _selectionEnd.x);
        int endX = Math.Max(_selectionStart.x, _selectionEnd.x);
        int startY = Math.Min(_selectionStart.y, _selectionEnd.y);
        int endY = Math.Max(_selectionStart.y, _selectionEnd.y);

        // Bounds check
        startX = Math.Max(0, startX);
        endX = Math.Min(_gridSizeX - 1, endX);
        startY = Math.Max(0, startY);
        endY = Math.Min(_gridSizeY - 1, endY);

        for (int y = startY; y <= endY; y++)
        {
            for (int x = startX; x <= endX; x++)
            {
                ApplyCurrentToolToCell(x, y);
            }
        }

        Repaint();
    }

    private void ApplyCurrentToolToCell(int x, int y)
    {
        // Bounds check
        if (y >= _gridData.GetLength(0) || x >= _gridData.GetLength(1) || y < 0 || x < 0)
            return;

        var tile = _gridData[y, x];

        if (_isErasing)
        {
            // Use dynamic default color
            string defaultTileCode = _selectedTileType ?? "r";
            tile.code = defaultTileCode;
            tile.objTypes.Clear();
        }
        else if (_isEmptyTile)
        {
            tile.code = "null";
            tile.objTypes.Clear();
        }
        else if (_isPlacingSpecial)
        {
            tile.code = _selectedSpecial;

            // Preserve under objectives
            List<string> underObjectives = new List<string>();
            foreach (var objType in tile.objTypes)
            {
                var objectiveData = _spritesData.objectiveTypes.Find(t => t.code == objType);
                if (objectiveData != null && objectiveData.type == LevelEditorDataSO.ObjectiveType.Under)
                {
                    underObjectives.Add(objType);
                }
            }

            tile.objTypes.Clear();
            foreach (var under in underObjectives)
            {
                tile.objTypes.Add(under);
            }
        }
        else if (_isPlacingObjective)
            {
                if (!tile.objTypes.Contains(_selectedObjective))
                {
                    tile.objTypes.Add(_selectedObjective);
            }
        }
        else if (_isPlacingCollectable)
        {
            tile.code = "random";

            // Remove other collectables
                var allCollectables = _spritesData.objectiveTypes
                    .Where(o => o.type == LevelEditorDataSO.ObjectiveType.Collectable)
                    .Select(o => o.code)
                    .ToArray();

                foreach (var collectable in allCollectables)
                {
                    tile.objTypes.Remove(collectable);
                }

                tile.objTypes.Add(_selectedCollectable);
        }
        else if (_selectedTileType != null)
        {
            // Apply color to non-collectable tiles
            var collectables = _spritesData.objectiveTypes
                .Where(o => o.type == LevelEditorDataSO.ObjectiveType.Collectable)
                .Select(o => o.code)
                .ToArray();

            bool hasCollectable = tile.objTypes.Any(obj => collectables.Contains(obj));

            if (!hasCollectable)
            {
                tile.code = _selectedTileType;

                // Preserve under objectives
                List<string> underObjectives = new List<string>();
                foreach (var objType in tile.objTypes)
                {
                    var objectiveData = _spritesData.objectiveTypes.Find(t => t.code == objType);
                    if (objectiveData != null && objectiveData.type == LevelEditorDataSO.ObjectiveType.Under)
                    {
                        underObjectives.Add(objType);
                    }
                }

                tile.objTypes.Clear();
                foreach (var under in underObjectives)
                {
                    tile.objTypes.Add(under);
                }
            }
        }
    }

    private void SaveLevel()
    {
        try
        {
            // Create level data according to format
            var levelData = CreateLevelDataByFormat();

            // Check and create save directory
            if (!Directory.Exists(SavePath))
            {
                Directory.CreateDirectory(SavePath);
            }

            string filePath = $"{SavePath}/level_{_selectedLevel}.json";
            var json = JsonConvert.SerializeObject(levelData, Formatting.Indented);
            File.WriteAllText(filePath, json);

            AssetDatabase.Refresh();
            RefreshLevelList();

            EditorUtility.DisplayDialog("Success",
                $"Level {_selectedLevel} saved with {_selectedGridFormat} format to {filePath}", "OK");
        }
        catch (Exception e)
        {
            Debug.LogError($"Save error: {e.Message}\nStack trace: {e.StackTrace}");
            EditorUtility.DisplayDialog("Error", $"Failed to save level: {e.Message}", "OK");
        }
    }

    private object CreateLevelDataByFormat()
    {
        switch (_selectedGridFormat)
        {
            case GridFormat.Standard:
                return CreateStandardFormat();

            case GridFormat.SimpleCode:
                return CreateSimpleCodeFormat();

            case GridFormat.CodeOnly:
                return CreateCodeOnlyFormat();

            default:
                return CreateStandardFormat();
        }
    }

    private object CreateStandardFormat()
    {
        return new
        {
            level = _selectedLevel,
            gridX = _gridSizeX,
            gridY = _gridSizeY,
            moveCount = _moveCount,
            limitType = _limitType.ToString(),
            timerSeconds = _timerSeconds,
            difficulty = _difficultyLevel,
            availableColors = _availableColors.ToArray(),
            objectives = GenerateObjectives(),
            grid = ConvertGridToStandardFormat()
        };
    }

    private object CreateSimpleCodeFormat()
    {
        return new
        {
            level = _selectedLevel,
            gridX = _gridSizeX,
            gridY = _gridSizeY,
            moveCount = _moveCount,
            limitType = _limitType.ToString(),
            timerSeconds = _timerSeconds,
            difficulty = _difficultyLevel,
            availableColors = _availableColors.ToArray(),
            objectives = GenerateObjectivesForSimpleFormat(),
            grid = ConvertGridToSimpleCodeFormat()
        };
    }

    private object CreateCodeOnlyFormat()
    {
        return new
        {
            level = _selectedLevel,
            size = new { x = _gridSizeX, y = _gridSizeY },
            limits = new
            {
                moves = _moveCount,
                type = _limitType.ToString(),
                timer = _timerSeconds
            },
            difficulty = _difficultyLevel,
            colors = _availableColors.ToArray(),
            targets = GenerateTargetsForCodeOnlyFormat(),
            tiles = ConvertGridToCodeOnlyFormat()
        };
    }


    private List<ObjectiveData> GenerateObjectives()
    {
        var objectives = new Dictionary<string, int>();

        // Count all objectives in grid
        for (int y = 0; y < _gridSizeY; y++)
        {
            for (int x = 0; x < _gridSizeX; x++)
            {
                foreach (var objType in _gridData[y, x].objTypes)
                {
                    if (!objectives.ContainsKey(objType))
                        objectives[objType] = 0;
                    objectives[objType]++;
                }
            }
        }

        // Convert to ObjectiveData list - determine type according to format
        var result = objectives.Select(kvp => new ObjectiveData
        {
            type = _jsonConfig.objectiveTypeField,
            targetObject = kvp.Key,
            targetCount = kvp.Value
        }).ToList();

        // Add Color Tile Objectives
        result.AddRange(_colorObjectives);

        return result;
    }

    private List<List<TileData>> ConvertGridToList()
    {
        var result = new List<List<TileData>>();

        for (int y = 0; y < _gridSizeY; y++)
        {
            var row = new List<TileData>();
            for (int x = 0; x < _gridSizeX; x++)
            {
                var originalTile = _gridData[y, x];

                // Arrange objectives in this order: Cover -> Collectable -> Under
                var sortedObjectives = new List<string>();

                foreach (var objType in originalTile.objTypes)
                {
                    var objectiveData = _spritesData.objectiveTypes.Find(t => t.code == objType);
                    if (objectiveData != null && objectiveData.type == LevelEditorDataSO.ObjectiveType.Cover)
                    {
                        sortedObjectives.Add(objType);
                    }
                }

                foreach (var objType in originalTile.objTypes)
                {
                    var objectiveData = _spritesData.objectiveTypes.Find(t => t.code == objType);
                    if (objectiveData != null && objectiveData.type == LevelEditorDataSO.ObjectiveType.Collectable)
                    {
                        sortedObjectives.Add(objType);
                    }
                }

                foreach (var objType in originalTile.objTypes)
                {
                    var objectiveData = _spritesData.objectiveTypes.Find(t => t.code == objType);
                    if (objectiveData != null && objectiveData.type == LevelEditorDataSO.ObjectiveType.Under)
                    {
                        sortedObjectives.Add(objType);
                    }
                }

                string finalCode = originalTile.code;
                if (originalTile.code == "null")
                {
                    finalCode = GetNullTileCode();
                }
                else if (originalTile.code == "random")
                {
                    finalCode = GetRandomTileCode();
                }
                else
                {
                    var specialTileData = _spritesData.specialTiles?.Find(s => s.code == originalTile.code);
                    if (specialTileData != null)
                    {
                        // Determine special tile format according to format
                        finalCode = GetSpecialTileCode(specialTileData.code);
                    }
                }

                row.Add(new TileData
                {
                    code = finalCode,
                    objTypes = sortedObjectives
                });
            }

            result.Add(row);
        }

        return result;
    }

    // Debug method to print grid contents
    private void DebugPrintGrid()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"Grid contents ({_gridSizeX}x{_gridSizeY}):");

        for (var y = _gridSizeY - 1; y >= 0; y--)
        {
            for (var x = 0; x < _gridSizeX; x++)
            {
                if (y < _gridData.GetLength(0) && x < _gridData.GetLength(1))
                {
                    var tileCode = _gridData[y, x].code;
                    sb.Append($"[{tileCode}] ");
                }
            }

            sb.AppendLine();
        }

        Debug.Log(sb.ToString());
    }

    private void LoadLevel(int level)
    {
        try
        {
            // Clear undo history when loading new level
            ClearUndoRedoStacks();

            var filePath = $"{SavePath}/level_{level}.json";
            if (!File.Exists(filePath))
            {
                EditorUtility.DisplayDialog("Error", $"Level {level} does not exist!", "OK");
                return;
            }

            var json = File.ReadAllText(filePath);

            // Auto-detect format and load
            DetectFormatAndLoad(json, level);

            Repaint();
        }
        catch (Exception e)
        {
            Debug.LogError($"Load error: {e.Message}");
            EditorUtility.DisplayDialog("Error",
                "Failed to load level! Try changing the Grid Format in Level Settings.", "OK");
        }
    }

    private void DetectFormatAndLoad(string json, int level)
    {
        // First check which fields exist in JSON
        var jsonObj = JsonConvert.DeserializeObject<dynamic>(json);

        GridFormat detectedFormat = DetectJsonFormat(jsonObj);

        // Set detected format
        if (detectedFormat != _selectedGridFormat)
        {
            _selectedGridFormat = detectedFormat;
            SetupJsonFormatConfiguration();
            Debug.Log($"🔍 Format auto-detected and switched to: {_selectedGridFormat}");
        }

        // Load according to format
        switch (detectedFormat)
        {
            case GridFormat.Standard:
                LoadStandardFormat(json, level);
                break;

            case GridFormat.SimpleCode:
                LoadSimpleCodeFormat(json, level);
                break;

            case GridFormat.CodeOnly:
                LoadCodeOnlyFormat(json, level);
                break;

            default:
                LoadStandardFormat(json, level); // Fallback
                break;
        }
    }

    private GridFormat DetectJsonFormat(dynamic jsonObj)
    {
        // Check grid structure
        if (jsonObj.grid != null)
        {
            var firstRow = jsonObj.grid[0];
            if (firstRow != null && firstRow.Count > 0)
            {
                var firstCell = firstRow[0];


                if (firstCell.Type == Newtonsoft.Json.Linq.JTokenType.String)
                {
                    return GridFormat.SimpleCode;
                }

                if (firstCell.tile != null)
                {
                    return GridFormat.CodeOnly;
                }
                
                if (firstCell.code != null)
                {
                    return GridFormat.Standard;
                }
            }
        }


        if (jsonObj.tiles != null)
        {
            return GridFormat.CodeOnly;
        }

        return GridFormat.Standard; // Default
    }

    private void LoadStandardFormat(string json, int level)
    {
        // Standard format deserialization
        var levelData = JsonConvert.DeserializeObject<Level>(json);

        _selectedLevel = level;
        _gridSizeX = levelData.gridX;
        _gridSizeY = levelData.gridY;
        _moveCount = levelData.moveCount;

        // Load limit type and timer duration
        if (!string.IsNullOrEmpty(levelData.limitType))
        {
            if (Enum.TryParse<LimitType>(levelData.limitType, out var parsedLimitType))
            {
                _limitType = parsedLimitType;
            }
        }

        _timerSeconds = levelData.timerSeconds;

        // Load difficulty level
        _difficultyLevel = levelData.difficulty;

        // Load Color Objectives - dynamic according to format
        _colorObjectives.Clear();
        foreach (var objective in levelData.objectives)
        {
            // Detect color objectives (colorMatch, matchColor etc.)
            if (IsColorObjective(objective.type))
            {
                _colorObjectives.Add(new ObjectiveData
                {
                    type = objective.type,
                    targetObject = objective.targetObject,
                    targetCount = objective.targetCount
                });
            }
        }

        // Load available colors
        _availableColors.Clear();
        if (levelData.availableColors != null && levelData.availableColors.Count > 0)
        {
            _availableColors.AddRange(levelData.availableColors);
        }
        else
        {
            // If no color list exists, add all colors as default
            foreach (var tileType in _spritesData.tileTypes)
            {
                _availableColors.Add(tileType.code);
            }
        }

        InitializeGrid();

        // Load grid data
        for (var y = 0; y < _gridSizeY; y++)
        {
            for (var x = 0; x < _gridSizeX; x++)
            {
                if (y < levelData.grid.Count && x < levelData.grid[y].Count)
                {
                    var tileCode = levelData.grid[y][x].code;

                    // Special tile check - process codes starting with special_
                    if (tileCode != null && tileCode.StartsWith("special_"))
                    {
                        var specialTypeStr = tileCode.Substring(8);

                        var editorSpecialCode = GetEditorSpecialCodeFromSpecialCode(specialTypeStr);

                        if (!string.IsNullOrEmpty(editorSpecialCode))
                        {
                            _gridData[y, x].code = editorSpecialCode;
                        }
                        else
                        {
                            // Unknown special type, use original code as default
                            _gridData[y, x].code = tileCode;
                        }
                    }
                    else
                    {
                        // Normal tile code
                        _gridData[y, x].code = tileCode;
                    }

                    _gridData[y, x].objTypes = new List<string>(levelData.grid[y][x].objTypes);
                }
            }
        }
    }

    private void LoadSimpleCodeFormat(string json, int level)
    {
        // SimpleCode format deserialization
        var jsonObj = JsonConvert.DeserializeObject<dynamic>(json);

        _selectedLevel = level;
        _gridSizeX = (int)jsonObj.gridX;
        _gridSizeY = (int)jsonObj.gridY;
        _moveCount = (int)jsonObj.moveCount;

        // Load limit type and timer duration
        string limitTypeStr = (string)jsonObj.limitType;
        if (!string.IsNullOrEmpty(limitTypeStr))
        {
            if (Enum.TryParse<LimitType>(limitTypeStr, out var parsedLimitType))
            {
                _limitType = parsedLimitType;
            }
        }

        _timerSeconds = (int)jsonObj.timerSeconds;

        // Load difficulty level
        _difficultyLevel = (DifficultyLevel)jsonObj.difficulty;

        // Load Color Objectives - dynamic according to format
        _colorObjectives.Clear();
        if (jsonObj.objectives != null)
        {
            foreach (var objective in jsonObj.objectives)
            {
                string objType = (string)objective.type ?? (string)objective.target ?? "colorMatch";
                // Detect color objectives (colorMatch, matchColor etc.)
                if (IsColorObjective(objType))
                {
                    _colorObjectives.Add(new ObjectiveData
                    {
                        type = objType,
                        targetObject = (string)objective.target ?? (string)objective.targetObject,
                        targetCount = (int)(objective.count ?? objective.targetCount)
                    });
                }
            }
        }

        // Load available colors
        _availableColors.Clear();
        if (jsonObj.availableColors != null)
        {
            foreach (var color in jsonObj.availableColors)
            {
                _availableColors.Add((string)color);
            }
        }
        else
        {
            // If no color list exists, add all colors as default
            foreach (var tileType in _spritesData.tileTypes)
            {
                _availableColors.Add(tileType.code);
            }
        }

        InitializeGrid();

        // Load grid data - special parsing for SimpleCode format
        if (jsonObj.grid != null)
        {
            for (int y = 0; y < _gridSizeY && y < jsonObj.grid.Count; y++)
            {
                var row = jsonObj.grid[y];
                for (int x = 0; x < _gridSizeX && x < row.Count; x++)
                {
                    string cellValue = (string)row[x];
                    ParseSimpleCodeCell(x, y, cellValue);
                }
            }
        }
    }

    private void ParseSimpleCodeCell(int x, int y, string cellValue)
    {
        if (string.IsNullOrEmpty(cellValue))
        {
            _gridData[y, x].code = "null";
            _gridData[y, x].objTypes.Clear();
            return;
        }

        // Handle different cases in SimpleCode format
        if (cellValue.StartsWith("objective_"))
        {
            // Objective code - example: "objective_ice"
            string objCode = cellValue.Substring(10);

            // Find objective data
            var objectiveData = _spritesData.objectiveTypes.Find(o => o.code == objCode);
            if (objectiveData != null)
            {
                // Place in grid according to objective type
                if (objectiveData.type == LevelEditorDataSO.ObjectiveType.Collectable)
                {
                    _gridData[y, x].code = "random";
                    _gridData[y, x].objTypes = new List<string> { objCode };
                }
                else
                {
                    // Default color + objective for Under or Cover objectives
                    _gridData[y, x].code = _spritesData.tileTypes[0].code;
                    _gridData[y, x].objTypes = new List<string> { objCode };
                }
            }
            else
            {
                // Objective not found, process as normal tile
                _gridData[y, x].code = cellValue;
                _gridData[y, x].objTypes.Clear();
            }
        }
        else if (cellValue.StartsWith("special_"))
        {
            // Special tile code
            string specialCode = cellValue.Substring(8);
            var editorSpecialCode = GetEditorSpecialCodeFromSpecialCode(specialCode);

            _gridData[y, x].code = !string.IsNullOrEmpty(editorSpecialCode) ? editorSpecialCode : cellValue;
            _gridData[y, x].objTypes.Clear();
        }
        else if (cellValue == "empty" || cellValue == "null")
        {
            // Empty cell
            _gridData[y, x].code = "null";
            _gridData[y, x].objTypes.Clear();
        }
        else if (cellValue == "any" || cellValue == "random")
        {
            // Random cell
            _gridData[y, x].code = "random";
            _gridData[y, x].objTypes.Clear();
        }
        else
        {
            // Normal tile code
            _gridData[y, x].code = cellValue;
            _gridData[y, x].objTypes.Clear();
        }
    }

    private void LoadCodeOnlyFormat(string json, int level)
    {
        try
        {
            // CodeOnly format deserialization
            var jsonObj = JsonConvert.DeserializeObject<dynamic>(json);

            _selectedLevel = level;

            // Get size information - different structure in CodeOnly format
            if (jsonObj.size != null)
            {
                _gridSizeX = (int)jsonObj.size.x;
                _gridSizeY = (int)jsonObj.size.y;
            }
            else if (jsonObj.gridX != null)
            {
                // Fallback - standard format fields
                _gridSizeX = (int)jsonObj.gridX;
                _gridSizeY = (int)jsonObj.gridY;
            }

            // Get limits information
            if (jsonObj.limits != null)
            {
                _moveCount = (int)(jsonObj.limits.moves ?? 20);
                _timerSeconds = (int)(jsonObj.limits.timer ?? 60);

                string limitTypeStr = (string)(jsonObj.limits.type ?? "Moves");
                if (Enum.TryParse<LimitType>(limitTypeStr, out var parsedLimitType))
                {
                    _limitType = parsedLimitType;
                }
            }
            else
            {
                // Fallback - standard format
                _moveCount = (int)(jsonObj.moveCount ?? 20);
                _timerSeconds = (int)(jsonObj.timerSeconds ?? 60);

                string limitTypeStr = (string)(jsonObj.limitType ?? "Moves");
                if (Enum.TryParse<LimitType>(limitTypeStr, out var parsedLimitType))
                {
                    _limitType = parsedLimitType;
                }
            }

            // Difficulty
            if (jsonObj.difficulty != null)
            {
                _difficultyLevel = (DifficultyLevel)jsonObj.difficulty;
            }

            // Color Objectives
            _colorObjectives.Clear();
            if (jsonObj.targets != null)
            {
                foreach (var target in jsonObj.targets)
                {
                    string objType = (string)(target.object_type ?? target.type ?? "color");
                    if (IsColorObjective(objType))
                    {
                        _colorObjectives.Add(new ObjectiveData
                        {
                            type = objType,
                            targetObject = (string)(target.object_type ?? target.targetObject),
                            targetCount = (int)(target.amount ?? target.targetCount ?? 1)
                        });
                    }
                }
            }
            else if (jsonObj.objectives != null)
            {
                // Fallback - standard format objectives
                foreach (var objective in jsonObj.objectives)
                {
                    string objType = (string)objective.type;
                    if (IsColorObjective(objType))
                    {
                        _colorObjectives.Add(new ObjectiveData
                        {
                            type = objType,
                            targetObject = (string)objective.targetObject,
                            targetCount = (int)objective.targetCount
                        });
                    }
                }
            }

            // Available colors
            _availableColors.Clear();
            if (jsonObj.colors != null)
            {
                foreach (var color in jsonObj.colors)
                {
                    _availableColors.Add((string)color);
                }
            }
            else if (jsonObj.availableColors != null)
            {
                foreach (var color in jsonObj.availableColors)
                {
                    _availableColors.Add((string)color);
                }
            }
            else
            {
                // Default - all colors
                foreach (var tileType in _spritesData.tileTypes)
                {
                    _availableColors.Add(tileType.code);
                }
            }

            InitializeGrid();

            // Load grid data - special for CodeOnly format
            if (jsonObj.tiles != null)
            {
                LoadCodeOnlyGridData(jsonObj.tiles);
            }
            else if (jsonObj.grid != null)
            {
                // Fallback - standard format grid
                LoadCodeOnlyGridFromStandardFormat(jsonObj.grid);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"CodeOnly format load error: {e.Message}\nStack trace: {e.StackTrace}");
            throw;
        }
    }

    private void LoadCodeOnlyGridData(dynamic tilesData)
    {
        for (int y = 0; y < _gridSizeY && y < tilesData.Count; y++)
        {
            var row = tilesData[y];
            for (int x = 0; x < _gridSizeX && x < row.Count; x++)
            {
                var cellData = row[x];
                string tileValue = (string)(cellData.tile ?? cellData.code ?? "r");

                ParseCodeOnlyCell(x, y, tileValue);
            }
        }
    }

    private void LoadCodeOnlyGridFromStandardFormat(dynamic gridData)
    {
        // Parse standard format grid as CodeOnly
        for (int y = 0; y < _gridSizeY && y < gridData.Count; y++)
        {
            var row = gridData[y];
            for (int x = 0; x < _gridSizeX && x < row.Count; x++)
            {
                var cellData = row[x];
                string tileCode = (string)(cellData.code ?? "r");

                _gridData[y, x].code = tileCode;
                _gridData[y, x].objTypes.Clear();

                if (cellData.objTypes != null)
                {
                    foreach (var objType in cellData.objTypes)
                    {
                        _gridData[y, x].objTypes.Add((string)objType);
                    }
                }
            }
        }
    }

    private void ParseCodeOnlyCell(int x, int y, string tileValue)
    {
        if (string.IsNullOrEmpty(tileValue))
        {
            _gridData[y, x].code = "null";
            _gridData[y, x].objTypes.Clear();
            return;
        }

        // In CodeOnly format, tile values can be combined: "red_ice" etc.
        if (tileValue.Contains("_"))
        {
            // Combined format - example: "red_ice", "blue_wood"
            var parts = tileValue.Split('_');
            string baseColor = parts[0];
            string objective = parts.Length > 1 ? parts[1] : "";

            _gridData[y, x].code = baseColor;
            _gridData[y, x].objTypes.Clear();

            if (!string.IsNullOrEmpty(objective))
            {
                _gridData[y, x].objTypes.Add(objective);
            }
        }
        else if (tileValue == "empty" || tileValue == "null")
        {
            _gridData[y, x].code = "null";
            _gridData[y, x].objTypes.Clear();
        }
        else if (tileValue == "any" || tileValue == "random")
        {
            _gridData[y, x].code = "random";
            _gridData[y, x].objTypes.Clear();
        }
        else
        {
            // Simple tile code
            _gridData[y, x].code = tileValue;
            _gridData[y, x].objTypes.Clear();
        }
    }

    private string GetEditorSpecialCodeFromSpecialCode(string specialCode)
    {
        if (_spritesData?.specialTiles == null) return null;

        // Find special tile matching the code
        var specialTile = _spritesData.specialTiles.Find(s => s.code == specialCode);
        if (specialTile != null)
        {
            return specialTile.code;
        }

        // Special case check - if code not found, try lowercase conversion
        var lowerSpecialCode = specialCode.ToLower();
        foreach (var special in _spritesData.specialTiles)
        {
            if (special.code.ToLower() == lowerSpecialCode)
            {
                return special.code;
            }
        }

        return null;
    }

    private void AddColorObjective(string colorCode, int count)
    {
        // If same color already exists, update it
        var existingObjective = _colorObjectives.FirstOrDefault(o => o.targetObject == colorCode);
        if (existingObjective != null)
        {
            existingObjective.targetCount = count;
        }
        else
        {
            // Add new objective using current format configuration
            _colorObjectives.Add(new ObjectiveData
            {
                type = GetColorObjectiveType(),
                targetObject = colorCode,
                targetCount = count
            });
        }
    }

    // Simple fallback color for special tiles when sprite is not available
    private Color GetSpecialColor(string specialType)
    {
        return Color.gray;
    }

    // JSON Format Helper Functions
    private string GetNullTileCode()
    {
        switch (_selectedGridFormat)
        {
            case GridFormat.Standard:
                return "null";
            case GridFormat.SimpleCode:
                return "empty";
            case GridFormat.CodeOnly:
                return "empty";
            default:
                return "null";
        }
    }

    private string GetRandomTileCode()
    {
        switch (_selectedGridFormat)
        {
            case GridFormat.Standard:
                return "random";
            case GridFormat.SimpleCode:
                return "any";
            case GridFormat.CodeOnly:
                return "any";
            default:
                return "random";
        }
    }

    private string GetSpecialTileCode(string specialCode)
    {
        switch (_selectedGridFormat)
        {
            case GridFormat.Standard:
                return "special_" + specialCode;
            case GridFormat.SimpleCode:
                return "special_" + specialCode;
            case GridFormat.CodeOnly:
                return specialCode;
            default:
                return "special_" + specialCode;
        }
    }

    private string GetColorObjectiveType()
    {
        switch (_selectedGridFormat)
        {
            case GridFormat.Standard:
                return "colorMatch";
            case GridFormat.SimpleCode:
                return "matchColor";
            case GridFormat.CodeOnly:
                return "color";
            default:
                return "colorMatch";
        }
    }

    private bool IsColorObjective(string objectiveType)
    {
        // Common color objective types
        return objectiveType == "colorMatch" ||
               objectiveType == "matchColor" ||
               objectiveType == "color" ||
               objectiveType == "colorTile";
    }

    #endregion

    private void ToggleAvailableColor(string colorCode)
    {
        if (_availableColors.Contains(colorCode))
        {
            _availableColors.Remove(colorCode);
        }
        else
        {
            _availableColors.Add(colorCode);
        }

        EnsureAtLeastOneColorSelected(colorCode);
    }

    private void EnsureAtLeastOneColorSelected(string fallbackColor)
    {
        if (_availableColors.Count == 0)
        {
            _availableColors.Add(fallbackColor);
            EditorUtility.DisplayDialog("Warning", "At least one color must be available!", "OK");
        }
    }

    private void SelectAllColors()
    {
        _availableColors.Clear();
        if (_spritesData != null && _spritesData.tileTypes != null)
        {
            foreach (var tileType in _spritesData.tileTypes)
            {
                _availableColors.Add(tileType.code);
            }
        }
    }

    private void ClearAllColors()
    {
        if (EditorUtility.DisplayDialog("Clear All Colors",
                "This will disable all colors except the first one. Continue?", "Yes", "Cancel"))
        {
            _availableColors.Clear();
            if (_spritesData != null && _spritesData.tileTypes != null && _spritesData.tileTypes.Count > 0)
            {
                _availableColors.Add(_spritesData.tileTypes[0].code);
            }
        }
    }


    private void DrawJsonFormatBuilder()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        EditorGUILayout.LabelField("🔧 JSON Format Builder", EditorStyles.boldLabel);
        EditorGUILayout.Space(3);

        // Grid Format Preview
        EditorGUILayout.LabelField("Grid Format Preview:", EditorStyles.boldLabel);

        string preview = GetGridFormatPreview();
        EditorGUILayout.TextArea(preview, EditorStyles.textArea, GUILayout.Height(80));

        EditorGUILayout.Space(3);

        // Objective Fields Configuration
        EditorGUILayout.LabelField("Objective Fields:", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Type Field:", GUILayout.Width(80));
        _jsonConfig.objectiveTypeField = EditorGUILayout.TextField(_jsonConfig.objectiveTypeField);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Target Field:", GUILayout.Width(80));
        _jsonConfig.targetObjectField = EditorGUILayout.TextField(_jsonConfig.targetObjectField);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Count Field:", GUILayout.Width(80));
        _jsonConfig.targetCountField = EditorGUILayout.TextField(_jsonConfig.targetCountField);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(3);

        // Test Export Button
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("📋 Test Export", GUILayout.Height(25)))
        {
            TestExportFormat();
        }

        if (GUILayout.Button("📥 Load Sample", GUILayout.Height(25)))
        {
            LoadSampleData();
        }

        if (GUILayout.Button("❌ Close", GUILayout.Height(25), GUILayout.Width(60)))
        {
            _showFormatBuilder = false;
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
    }

    private string GetGridFormatPreview()
    {
        switch (_selectedGridFormat)
        {
            case GridFormat.Standard:
                return @"// Standard Format
[
  [
    {
      ""code"": ""r"",
      ""objTypes"": [""ice"", ""wood""]
    },
    {
      ""code"": ""g"",
      ""objTypes"": []
    }
  ]
]";

            case GridFormat.SimpleCode:
                return @"// Simple Code Format
[
  [""r"", ""g"", ""b""],
  [""objective_ice"", ""y"", ""p""]
]";

            case GridFormat.CodeOnly:
                return @"// Code Only Format
[
  [
    {""tile"": ""red_ice""},
    {""tile"": ""green""}
  ],
  [
    {""tile"": ""blue_wood""},
    {""tile"": ""yellow""}
  ]
]";

            default:
                return "Unknown format";
        }
    }

    private void TestExportFormat()
    {
        try
        {
            // Test export with current grid
            var testGrid = CreateLevelDataByFormat();
            var json = JsonConvert.SerializeObject(testGrid, Formatting.Indented);

            Debug.Log($"🧪 Test Export Result ({_selectedGridFormat} format):\n" + json);
            EditorUtility.DisplayDialog("Test Export",
                $"Test export completed with {_selectedGridFormat} format! Check Console for results.", "OK");
        }
        catch (Exception e)
        {
            Debug.LogError("Test export failed: " + e.Message);
            EditorUtility.DisplayDialog("Error", "Test export failed: " + e.Message, "OK");
        }
    }
    
    private void LoadSampleData()
    {
        var menu = new GenericMenu();

        menu.AddItem(new GUIContent("Standard Format Sample"), false, () =>
        {
            _selectedGridFormat = GridFormat.Standard;
            SetupJsonFormatConfiguration();
        });

        menu.AddItem(new GUIContent("Simple Code Sample"), false, () =>
        {
            _selectedGridFormat = GridFormat.SimpleCode;
            SetupJsonFormatConfiguration();
        });

        menu.AddItem(new GUIContent("Code Only Sample"), false, () =>
        {
            _selectedGridFormat = GridFormat.CodeOnly;
            SetupJsonFormatConfiguration();
        });

        menu.ShowAsContext();
    }

    // Format-specific grid conversion functions
    private object ConvertGridToStandardFormat()
    {
        // Current TileData format
        return ConvertGridToList();
    }

    private object ConvertGridToSimpleCodeFormat()
    {
        var result = new List<List<string>>();

        for (int y = 0; y < _gridSizeY; y++)
        {
            var row = new List<string>();
            for (int x = 0; x < _gridSizeX; x++)
            {
                var tile = _gridData[y, x];
                string tileCode;

                // If objective exists, use objective code directly
                if (tile.objTypes.Count > 0)
                {
                    // Use first objective
                    tileCode = "objective_" + tile.objTypes[0];
                }
                else
                {
                    // Normal tile code
                    if (tile.code == "null")
                        tileCode = GetNullTileCode();
                    else if (tile.code == "random")
                        tileCode = GetRandomTileCode();
                    else
                        tileCode = tile.code;
                }

                row.Add(tileCode);
            }

            result.Add(row);
        }

        return result;
    }

    private object ConvertGridToCodeOnlyFormat()
    {
        var result = new List<List<object>>();

        for (int y = 0; y < _gridSizeY; y++)
        {
            var row = new List<object>();
            for (int x = 0; x < _gridSizeX; x++)
            {
                var tile = _gridData[y, x];
                string tileValue;

                // Combine if objective exists
                if (tile.objTypes.Count > 0)
                {
                    tileValue = tile.code + "_" + tile.objTypes[0];
                }
                else
                {
                    if (tile.code == "null")
                        tileValue = GetNullTileCode();
                    else if (tile.code == "random")
                        tileValue = GetRandomTileCode();
                    else
                        tileValue = tile.code;
                }

                row.Add(new { tile = tileValue });
            }

            result.Add(row);
        }

        return result;
    }

    // Format-specific objective functions
    private object GenerateObjectivesForSimpleFormat()
    {
        var objectives = new Dictionary<string, int>();

        // Count objectives in grid
        for (int y = 0; y < _gridSizeY; y++)
        {
            for (int x = 0; x < _gridSizeX; x++)
            {
                foreach (var objType in _gridData[y, x].objTypes)
                {
                    if (!objectives.ContainsKey(objType))
                        objectives[objType] = 0;
                    objectives[objType]++;
                }
            }
        }

        // Different structure for SimpleCode format
        var result = objectives.Select(kvp => new
        {
            type = _jsonConfig.objectiveTypeField,
            target = kvp.Key,
            count = kvp.Value
        }).ToList();

        // Add color objectives
        foreach (var colorObj in _colorObjectives)
        {
            result.Add(new
            {
                type = colorObj.type,
                target = colorObj.targetObject,
                count = colorObj.targetCount
            });
        }

        return result;
    }

    private object GenerateTargetsForCodeOnlyFormat()
    {
        var objectives = new Dictionary<string, int>();

        // Count objectives in grid
        for (int y = 0; y < _gridSizeY; y++)
        {
            for (int x = 0; x < _gridSizeX; x++)
            {
                foreach (var objType in _gridData[y, x].objTypes)
                {
                    if (!objectives.ContainsKey(objType))
                        objectives[objType] = 0;
                    objectives[objType]++;
                }
            }
        }

        // Different structure for CodeOnly format
        var result = objectives.Select(kvp => new
        {
            object_type = kvp.Key,
            amount = kvp.Value
        }).ToList();

        // Add color objectives
        foreach (var colorObj in _colorObjectives)
        {
            result.Add(new
            {
                object_type = colorObj.targetObject,
                amount = colorObj.targetCount
            });
        }

        return result;
    }

    private void DrawTileSelector()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Color Tiles:", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();

        foreach (var tileType in _spritesData.tileTypes)
        {
            GUI.backgroundColor = _selectedTileType == tileType.code ? EditorTheme.Primary : EditorTheme.Inactive;
            if (GUILayout.Button(new GUIContent(tileType.sprite.texture), GUILayout.Width(40), GUILayout.Height(40)))
            {
                _selectedTileType = tileType.code;
                _isPlacingObjective = false;
                _isPlacingSpecial = false;
            }
        }

        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        // Special Tiles section
        EditorGUILayout.Space();

        // Special tile header and description
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Special Tiles:", EditorStyles.boldLabel, GUILayout.Width(90));
        EditorGUILayout.EndHorizontal();

        // Special Tiles - Get dynamically from LevelEditorTileDataSO
        if (_spritesData.specialTiles != null && _spritesData.specialTiles.Count > 0)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField("Special Tiles:", EditorStyles.boldLabel);

                EditorGUILayout.BeginHorizontal();
            foreach (var specialTile in _spritesData.specialTiles)
                {
                    Texture2D specialIcon = null;
                    if (specialTile.sprite != null)
                    {
                        specialIcon = specialTile.sprite.texture;
                    }
                    else
                    {
                    // Create default icon
                        specialIcon = new Texture2D(1, 1);
                        specialIcon.SetPixel(0, 0, GetSpecialColor(specialTile.code));
                        specialIcon.Apply();
                    }

                    GUI.backgroundColor = (_isPlacingSpecial && _selectedSpecial == specialTile.code)
                        ? EditorTheme.Active
                        : EditorTheme.Inactive;

                    if (GUILayout.Button(new GUIContent(specialTile.name, specialIcon), GUILayout.Width(70),
                            GUILayout.Height(40)))
                    {
                        _isPlacingSpecial = true;
                        _selectedSpecial = specialTile.code;
                        _isPlacingObjective = false;
                        _selectedTileType = null;
                    }
                }

                GUI.backgroundColor = Color.white;
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
            }

        EditorGUILayout.EndVertical();
    }

    private void DrawObjectiveSelector()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Objectives:", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Under:", GUILayout.Width(100));

        // Under objectives
        foreach (var obj in _spritesData.objectiveTypes.Where(o => o.type == LevelEditorDataSO.ObjectiveType.Under))
        {
            GUI.backgroundColor = (_isPlacingObjective && _selectedObjective == obj.code)
                ? EditorTheme.Active
                : EditorTheme.Inactive;
            if (GUILayout.Button(new GUIContent(obj.sprite.texture), GUILayout.Width(40), GUILayout.Height(40)))
            {
                _isPlacingObjective = true;
                _selectedObjective = obj.code;
                _isPlacingCollectable = false;
                _isAddingColorObjective = false;
            }
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Cover:", GUILayout.Width(100));

        // Cover objectives
        foreach (var obj in _spritesData.objectiveTypes.Where(o => o.type == LevelEditorDataSO.ObjectiveType.Cover))
        {
            GUI.backgroundColor = (_isPlacingObjective && _selectedObjective == obj.code)
                ? EditorTheme.Active
                : EditorTheme.Inactive;
            if (GUILayout.Button(new GUIContent(obj.sprite.texture), GUILayout.Width(40), GUILayout.Height(40)))
            {
                _isPlacingObjective = true;
                _selectedObjective = obj.code;
                _isPlacingCollectable = false;
                _isAddingColorObjective = false;
            }
        }

        EditorGUILayout.EndHorizontal();

        // Collectables
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Collectables:", GUILayout.Width(100));

        // Collectable items - Get all items marked as collectable from LevelEditorTileDataSO
        var collectables = _spritesData.objectiveTypes
            .Where(o => o.type == LevelEditorDataSO.ObjectiveType.Collectable).ToList();

        if (collectables.Count == 0)
        {
            EditorGUILayout.HelpBox("No Collectable type objectives found in TileSpritesData asset!",
                MessageType.Warning);
        }
        else
        {
            foreach (var collectableObj in collectables)
            {
                GUI.backgroundColor = (_isPlacingCollectable && _selectedCollectable == collectableObj.code)
                    ? EditorTheme.Active
                    : EditorTheme.Inactive;
                if (GUILayout.Button(new GUIContent(collectableObj.sprite.texture), GUILayout.Width(40),
                        GUILayout.Height(40)))
                {
                    _isPlacingCollectable = true;
                    _selectedCollectable = collectableObj.code;
                    _isPlacingObjective = false;
                    _isAddingColorObjective = false;
                    _isEmptyTile = false;
                    _isErasing = false;
                    _selectedTileType = null;
                }
            }
        }

        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        // Color Tile Objectives
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Color Objectives:", GUILayout.Width(100));

        // Color Tile Objective add button
        GUI.backgroundColor = _isAddingColorObjective ? EditorTheme.Active : EditorTheme.Inactive;
        if (GUILayout.Button("➕ Add Color Objective", GUILayout.Height(25)))
        {
            _isAddingColorObjective = !_isAddingColorObjective;
            _isPlacingObjective = false;
            _isPlacingCollectable = false;
        }

        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        // If Color Objective adding mode is active, show color and count selection
        DrawColorObjectiveSection();

        // Current Color Objectives list
        if (_colorObjectives.Count > 0)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Current Color Objectives:", EditorStyles.boldLabel);

            for (int i = 0; i < _colorObjectives.Count; i++)
            {
                var objective = _colorObjectives[i];
                EditorGUILayout.BeginHorizontal();

                if (objective.targetObject == AllColorCode)
                {
                    // Special view for "All" color option
                    GUI.backgroundColor = new Color(0.8f, 0.8f, 0.8f); // Gray color
                    GUILayout.Box("ALL", GUILayout.Width(30), GUILayout.Height(30));
                    GUI.backgroundColor = Color.white;
                    EditorGUILayout.LabelField($"Match {objective.targetCount} any color tiles");
                }
                else
                {
                    // View for normal color
                    var tileData = _spritesData.tileTypes.Find(t => t.code == objective.targetObject);
                    if (tileData != null)
                    {
                        GUILayout.Box(new GUIContent(tileData.sprite.texture), GUILayout.Width(30),
                            GUILayout.Height(30));
                    }

                    EditorGUILayout.LabelField($"Match {objective.targetCount} {objective.targetObject} tiles");
                }

                if (GUILayout.Button("X", GUILayout.Width(25)))
                {
                    _colorObjectives.RemoveAt(i);
                    i--;
                }

                EditorGUILayout.EndHorizontal();
            }
        }

        // Available colors section
        EditorGUILayout.Space();

        EditorGUILayout.EndVertical();
    }

    private void DrawColorObjectiveSection()
    {
        if (_isAddingColorObjective)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Color Objective Settings:", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Select Color:", GUILayout.Width(100));

            // All color option
            GUI.backgroundColor =
                _selectedColorObjective == AllColorCode ? EditorTheme.Primary : EditorTheme.Inactive;
            if (GUILayout.Button("ALL", GUILayout.Width(40), GUILayout.Height(40)))
            {
                _selectedColorObjective = AllColorCode;
            }

            // Normal colors
            foreach (var tileType in _spritesData.tileTypes)
            {
                GUI.backgroundColor =
                    _selectedColorObjective == tileType.code ? EditorTheme.Primary : EditorTheme.Inactive;
                if (GUILayout.Button(new GUIContent(tileType.sprite.texture), GUILayout.Width(40),
                        GUILayout.Height(40)))
                {
                    _selectedColorObjective = tileType.code;
                }
            }

            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Count:", GUILayout.Width(100));
            _colorObjectiveCount = EditorGUILayout.IntField(_colorObjectiveCount, GUILayout.Width(60));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = EditorTheme.Success;
            if (GUILayout.Button("➕ Add Color Objective", GUILayout.Height(25)))
            {
                AddColorObjective(_selectedColorObjective, _colorObjectiveCount);
            }

            GUI.backgroundColor = Color.white;

            GUI.backgroundColor = EditorTheme.Danger;
            if (GUILayout.Button("❌ Cancel", GUILayout.Height(25), GUILayout.Width(60)))
            {
                _isAddingColorObjective = false;
            }

            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }
    }

    private void DrawGrid()
    {
        if (_gridData == null)
        {
            InitializeGrid();
            return;
        }

        try
        {
            var gridRect = GUILayoutUtility.GetRect(
                _gridSizeX * (_cellSize + Padding) + 30,
                _gridSizeY * (_cellSize + Padding) + 30);

            // Grid background
            EditorGUI.DrawRect(new Rect(gridRect.x - 2, gridRect.y - 2, gridRect.width + 4, gridRect.height + 4),
                new Color(0.2f, 0.2f, 0.2f, 0.3f));

            for (int y = _gridSizeY - 1; y >= 0; y--)
            {
                for (int x = 0; x < _gridSizeX; x++)
                {
                    if (y >= _gridData.GetLength(0) || x >= _gridData.GetLength(1))
                        continue;

                    var cellRect = new Rect(
                        gridRect.x + x * (_cellSize + Padding) + 15,
                        gridRect.y + (_gridSizeY - 1 - y) * (_cellSize + Padding) + 15,
                        _cellSize,
                        _cellSize
                    );

                    if (_showGrid)
                    {
                        EditorGUI.DrawRect(cellRect,
                            new Color(EditorTheme.GridLine.r, EditorTheme.GridLine.g, EditorTheme.GridLine.b,
                                _gridOpacity * 0.3f));
                        Handles.color = new Color(EditorTheme.GridLine.r, EditorTheme.GridLine.g,
                            EditorTheme.GridLine.b, _gridOpacity);
                        Handles.DrawWireDisc(new Vector3(cellRect.center.x, cellRect.center.y, 0), Vector3.forward, 1f);
                    }

                    // Cell frame
                    GUI.Box(cellRect, "");

                    // Draw cell content
                    DrawCell(cellRect, x, y);

                    // Show coordinates
                    if (_showCoordinates)
                    {
                        var coordStyle = new GUIStyle(EditorStyles.miniLabel);
                        coordStyle.normal.textColor = Color.white;
                        coordStyle.fontSize = 8;
                        coordStyle.alignment = TextAnchor.UpperLeft;

                        GUI.Label(new Rect(cellRect.x + 1, cellRect.y + 1, 20, 10), $"{x},{y}", coordStyle);
                    }

                    // Check mouse click
                    if (Event.current.type == EventType.MouseDown &&
                        cellRect.Contains(Event.current.mousePosition))
                    {
                        HandleCellClick(x, y);
                        Repaint();
                        Event.current.Use();
                    }

                    // Mouse drag handling for rectangle fill
                    if (_isRectangleFillMode && Event.current.type == EventType.MouseDrag &&
                        cellRect.Contains(Event.current.mousePosition))
                    {
                        if (_isSelecting)
                        {
                            _selectionEnd = new Vector2Int(x, y);
                            Repaint();
                        }
                    }
                }
            }

            // Draw grid coordinate labels
            if (_showCoordinates)
            {
                DrawGridLabels(gridRect);
            }

            // Draw rectangle fill selection
            if (_isRectangleFillMode && _isSelecting)
            {
                DrawSelectionRectangle(gridRect);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Grid drawing error: {e.Message}");
        }
    }

    private void DrawGridLabels(Rect gridRect)
    {
        var labelStyle = new GUIStyle(EditorStyles.miniLabel);
        labelStyle.normal.textColor = Color.gray;
        labelStyle.fontSize = 9;
        labelStyle.alignment = TextAnchor.MiddleCenter;

        // X labels (top)
        for (int x = 0; x < _gridSizeX; x++)
        {
            var labelRect = new Rect(
                gridRect.x + x * (_cellSize + Padding) + 15,
                gridRect.y - 15,
                _cellSize,
                12
            );
            GUI.Label(labelRect, x.ToString(), labelStyle);
        }

        // Y labels (left)
        for (int y = 0; y < _gridSizeY; y++)
        {
            var labelRect = new Rect(
                gridRect.x - 15,
                gridRect.y + (_gridSizeY - 1 - y) * (_cellSize + Padding) + 15,
                12,
                _cellSize
            );
            GUI.Label(labelRect, y.ToString(), labelStyle);
        }
    }

    private void DrawSelectionRectangle(Rect gridRect)
    {
        int startX = Math.Min(_selectionStart.x, _selectionEnd.x);
        int endX = Math.Max(_selectionStart.x, _selectionEnd.x);
        int startY = Math.Min(_selectionStart.y, _selectionEnd.y);
        int endY = Math.Max(_selectionStart.y, _selectionEnd.y);

        // Bounds check
        startX = Math.Max(0, startX);
        endX = Math.Min(_gridSizeX - 1, endX);
        startY = Math.Max(0, startY);
        endY = Math.Min(_gridSizeY - 1, endY);

        var selectionRect = new Rect(
            gridRect.x + startX * (_cellSize + Padding) + 15,
            gridRect.y + (_gridSizeY - 1 - endY) * (_cellSize + Padding) + 15,
            (endX - startX + 1) * (_cellSize + Padding) - Padding,
            (endY - startY + 1) * (_cellSize + Padding) - Padding
        );

        // Draw selection outline - Professional theme
        Handles.color = new Color(EditorTheme.Active.r, EditorTheme.Active.g, EditorTheme.Active.b, 0.8f);
        Vector3[] corners = new Vector3[5];
        corners[0] = new Vector3(selectionRect.xMin, selectionRect.yMin, 0);
        corners[1] = new Vector3(selectionRect.xMax, selectionRect.yMin, 0);
        corners[2] = new Vector3(selectionRect.xMax, selectionRect.yMax, 0);
        corners[3] = new Vector3(selectionRect.xMin, selectionRect.yMax, 0);
        corners[4] = corners[0]; // Close the rectangle

        Handles.DrawPolyLine(corners);

        // Draw selection fill (transparent) - Professional theme
        EditorGUI.DrawRect(selectionRect, EditorTheme.Selection);
    }

    private void DrawCell(Rect rect, int x, int y)
    {
        // Add bounds check
        if (y >= _gridData.GetLength(0) || x >= _gridData.GetLength(1) || y < 0 || x < 0)
        {
            // Draw default tile for outside grid
            GUI.Box(rect, "");
            GUI.Label(rect, "N/A", EditorStyles.centeredGreyMiniLabel);
            return;
        }

        var tile = _gridData[y, x];

        // Draw cell frame independent of cell type
        GUI.Box(rect, "");

        // Preparation for Collectables and Objectives
        var collectables = _spritesData.objectiveTypes
            .Where(o => o.type == LevelEditorDataSO.ObjectiveType.Collectable)
            .Select(o => o.code)
            .ToArray();

        bool hasCollectable = tile.objTypes.Any(obj => collectables.Contains(obj));

        // DRAWING ORDER (bottom one is drawn first):
        // 1. Under (bottom)
        // 2. Tile 
        // 3. Collectable
        // 4. Cover (top)

        // Standard drawing dimensions
        var fullRect = new Rect(
            rect.x + (rect.width * 0.1f),
            rect.y + (rect.height * 0.1f),
            rect.width * 0.8f,
            rect.height * 0.8f
        );

        // 1. Under objectives (bottom) - WITH LARGE SIZE
        foreach (var objType in tile.objTypes)
        {
            var objectiveData = _spritesData.objectiveTypes.Find(t => t.code == objType);
            if (objectiveData != null && objectiveData.type == LevelEditorDataSO.ObjectiveType.Under)
            {
                // Larger size and less margin for Under objects
                var bgRect = new Rect(
                    rect.x + (rect.width * 0.05f), // Reduce margin
                    rect.y + (rect.height * 0.05f), // Reduce margin
                    rect.width, // Increase size
                    rect.height
                );

                // Draw dark colored frame for better visibility
                EditorGUI.DrawRect(bgRect, new Color(0.2f, 0.2f, 0.2f, 0.1f));

                GUI.DrawTextureWithTexCoords(bgRect, objectiveData.sprite.texture,
                    new Rect(0, 0, 1, 1));
            }
        }

        // 2. Draw Tile (above Under)
        // - No special handling for "random" (collectable), only draw non-null normal tiles
        if (tile.code != "null" && tile.code != "random")
        {
            Sprite tileSprite = null;

            // Check if it's a special tile
            var specialTileData = _spritesData.specialTiles?.Find(s => s.code == tile.code);
            if (specialTileData != null)
            {
                if (specialTileData.sprite != null)
                {
                    tileSprite = specialTileData.sprite;
                }
                else
                {
                    // If we can't find sprite, draw as text
                    GUI.color = GetSpecialColor(tile.code);
                    GUI.Label(fullRect, specialTileData.name, EditorStyles.boldLabel);
                    GUI.color = Color.white;
                }
            }
            else
            {
                // Search in normal tiles
                    var tileData = _spritesData.tileTypes.Find(t => t.code == tile.code);
                    if (tileData != null)
                    {
                        tileSprite = tileData.sprite;
                }
            }

            if (tileSprite != null)
            {
                GUI.DrawTextureWithTexCoords(fullRect, tileSprite.texture,
                    new Rect(0, 0, 1, 1));
            }
        }

        // 3. Collectables (above Tile)
        if (hasCollectable)
        {
            string collectableCode = tile.objTypes.Find(obj => collectables.Contains(obj));
            var collectableData = _spritesData.objectiveTypes.Find(t => t.code == collectableCode);

            if (collectableData != null)
            {
                GUI.DrawTextureWithTexCoords(fullRect, collectableData.sprite.texture,
                    new Rect(0, 0, 1, 1));
            }
        }

        // 4. Cover (top - above everything)
        foreach (var objType in tile.objTypes)
        {
            var objectiveData = _spritesData.objectiveTypes.Find(t => t.code == objType);
            if (objectiveData != null && objectiveData.type == LevelEditorDataSO.ObjectiveType.Cover)
            {
                GUI.DrawTextureWithTexCoords(fullRect, objectiveData.sprite.texture,
                    new Rect(0, 0, 1, 1));
            }
        }
    }

    private void HandleCellClick(int x, int y)
    {
        // Bounds check
        if (_gridData == null || y >= _gridData.GetLength(0) || x >= _gridData.GetLength(1) || y < 0 || x < 0)
        {
            Debug.LogWarning(
                $"Cell click out of bounds: ({x},{y}), Grid size: ({(_gridData != null ? _gridData.GetLength(1) : 0)},{(_gridData != null ? _gridData.GetLength(0) : 0)})");
            return;
        }

        // Check Fill Modes
        if (_isRowFillMode)
        {
            SaveStateForUndo(); // Save state for undo
            FillRow(y);
            return;
        }

        if (_isColumnFillMode)
        {
            SaveStateForUndo(); // Save state for undo
            FillColumn(x);
            return;
        }

        if (_isRectangleFillMode)
        {
            if (!_isSelecting)
            {
                // Start selection
                _selectionStart = new Vector2Int(x, y);
                _selectionEnd = new Vector2Int(x, y);
                _isSelecting = true;
            }
            else
            {
                // Complete selection and fill
                SaveStateForUndo(); // Save state for undo
                _selectionEnd = new Vector2Int(x, y);
                FillRectangle();
                _isSelecting = false;
            }

            return;
        }

        // Save undo state for normal cell click
        SaveStateForUndo();

        var tile = _gridData[y, x];

        if (_isErasing)
        {
            // Use dynamic default color
            string defaultTileCode = _selectedTileType ?? "r"; // Fallback as "r"
            tile.code = defaultTileCode;
            tile.objTypes.Clear();
        }
        else if (_isEmptyTile)
        {
            // Create completely empty grid cell
            tile.code = "null"; // Special code for empty tile
            tile.objTypes.Clear();
        }

        else if (_isPlacingSpecial)
        {
            // Special tile placement
            tile.code = _selectedSpecial;

            // Clear objectives
            List<string> underObjectives = new List<string>();

            foreach (var objType in tile.objTypes)
            {
                var objectiveData = _spritesData.objectiveTypes.Find(t => t.code == objType);
                if (objectiveData != null && objectiveData.type == LevelEditorDataSO.ObjectiveType.Under)
                {
                    underObjectives.Add(objType);
                }
            }

            tile.objTypes.Clear();

            // Put back Under objects
            foreach (var under in underObjectives)
            {
                tile.objTypes.Add(under);
            }
        }
        else if (_isPlacingObjective)
        {
            // Get objective type
            var objectiveData = _spritesData.objectiveTypes.Find(t => t.code == _selectedObjective);
            if (objectiveData == null)
            {
                EditorUtility.DisplayDialog("Error", "Selected objective not found in data!", "OK");
                return;
            }

            // Determine objective type
            bool isUnder = objectiveData.type == LevelEditorDataSO.ObjectiveType.Under;
            bool isCover = objectiveData.type == LevelEditorDataSO.ObjectiveType.Cover;

            // Check if collectable exists
            var collectables = _spritesData.objectiveTypes
                .Where(o => o.type == LevelEditorDataSO.ObjectiveType.Collectable)
                .Select(o => o.code)
                .ToArray();

            bool hasCollectable = tile.objTypes.Any(obj => collectables.Contains(obj));

            // Block if not Under or Cover and collectable exists
            if (!isUnder && !isCover && hasCollectable)
            {
                EditorUtility.DisplayDialog("Invalid Placement",
                    "Only under or cover objectives can be placed on collectables!", "OK");
                return;
            }

            // If this objective already exists, remove it
            if (tile.objTypes.Contains(_selectedObjective))
            {
                tile.objTypes.Remove(_selectedObjective);
            }
            else
            {
                // Otherwise add it
                tile.objTypes.Add(_selectedObjective);
            }

            // IMPORTANT: NEVER change tile.code when adding objectives
        }
        else if (_isPlacingCollectable)
        {
            // Collectable placement

            // Use special code for collectable - mark as random color
            tile.code = "random"; // Random tile code

            // If this collectable already exists remove it, otherwise add it
            if (tile.objTypes.Contains(_selectedCollectable))
            {
                tile.objTypes.Remove(_selectedCollectable);
            }
            else
            {
                // First remove other collectables, keep unders
                var allCollectables = _spritesData.objectiveTypes
                    .Where(o => o.type == LevelEditorDataSO.ObjectiveType.Collectable)
                    .Select(o => o.code)
                    .ToArray();

                // Clear non-Under collectables
                foreach (var collectable in allCollectables)
                {
                    tile.objTypes.Remove(collectable);
                }

                // Under objectives are preserved

                // Add new collectable
                tile.objTypes.Add(_selectedCollectable);
            }
        }
        else if (_selectedTileType != null)
        {
            // Check if collectable exists
            var collectables = _spritesData.objectiveTypes
                .Where(o => o.type == LevelEditorDataSO.ObjectiveType.Collectable)
                .Select(o => o.code)
                .ToArray();

            bool hasCollectable = tile.objTypes.Any(obj => collectables.Contains(obj));

            // Don't allow color change for places with collectibles
            if (hasCollectable)
            {
                EditorUtility.DisplayDialog("Invalid Placement",
                    "Cannot change the color of a tile with collectables!", "OK");
                return;
            }

            // Normal tile placement
            tile.code = _selectedTileType;

            // Clear all non-Under objTypes
            List<string> underObjectives = new List<string>();

            foreach (var objType in tile.objTypes)
            {
                var objectiveData = _spritesData.objectiveTypes.Find(t => t.code == objType);
                if (objectiveData != null && objectiveData.type == LevelEditorDataSO.ObjectiveType.Under)
                {
                    underObjectives.Add(objType);
                }
            }

            tile.objTypes.Clear();

            // Put back Under objects
            foreach (var under in underObjectives)
            {
                tile.objTypes.Add(under);
            }
        }
    }
}
