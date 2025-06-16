![Uploading level_editor_ss.png…]()

# 🎮 Custom Level Editor for Unity

A **powerful and flexible level editor** designed for puzzle games, built as a Unity Editor extension.

---

## 🚀 Features

* 🎯 **Visual Grid Editor** – Real-time grid with coordinate system
* 🧩 **Multiple Tile Types** – Supports normal, special, and goal tiles
* 🎯 **Goal System** – Under, Cover, and Collectable types supported
* 🛠️ **Advanced Tools** – Row/Column/Rectangle fill, grid mirroring, random fill
* ↩️ **Undo/Redo System** – Safe editing with full history support
* 📦 **Multiple JSON Formats** – Export levels as Standard, SimpleCode, or CodeOnly
* 🧠 **Auto Format Detection** – Automatically detects and loads JSON formats
* ⌨️ **Keyboard Shortcuts** – Fast and efficient workflow
* 🎨 **Modern UI** – Clean and intuitive interface using Unity EditorGUI

---

## 📋 Requirements

* Unity **2022.3** or newer
* `Newtonsoft.Json` package (via Unity Package Manager)

---

## 🧰 Installation

1. Clone or download this repository
2. Copy the following files into your Unity project:

   ```
   Assets/Scripts/Editor/Test/CustomLevelEditor.cs  
   Assets/Scripts/Editor/Test/Level.cs  
   Assets/Scripts/Editor/Test/LevelEditorDataSO.cs
   ```
3. If not already installed, add `com.unity.nuget.newtonsoft-json` from Package Manager
4. Create a `LevelEditorData` asset:

   * Right-click in the Project window
   * Select `Create → ScriptableObjects → Level Editor → LevelEditorData`
   * Set up your tiles, goals, and special elements

---

## ⚙️ Usage

1. Open the editor from `Tools → Custom Level Editor`
2. Assign your `LevelEditorData` asset (including sprites and tile codes)
3. Set the grid size and level properties
4. Use the tools to place tiles and goals
5. Save or load levels as JSON files

---

## ⌨️ Keyboard Shortcuts

| Shortcut       | Function             |
| -------------- | -------------------- |
| `E`            | Toggle Eraser        |
| `Q`            | Toggle Empty Tile    |
| `1-4`          | Quick tile selection |
| `Ctrl + S`     | Save level           |
| `Ctrl + N`     | New level            |
| `Ctrl + Z / Y` | Undo / Redo          |

---

## 📁 File Structure

* `CustomLevelEditor.cs` – Main editor window and logic
* `Level.cs` – Level data structures and serialization
* `LevelEditorDataSO.cs` – ScriptableObject for editor settings

---

## 📤 JSON Export Formats

The editor supports 3 different export formats:

* **Standard:** Full object structure with types and codes
* **SimpleCode:** Simplified string-based layout
* **CodeOnly:** Minimal format with just tile codes

---

## ⚠️ Notes

* The sample assets shown in screenshots are from the Unity Asset Store
* Large grids may affect performance
* Always back up your level files before making big changes

---

## **Contact & Feedback**

If you find any issues, have suggestions, or think something is missing, feel free to reach out!

- **Email:** [ayberkturksoy97@gmail.com](mailto:ayberkturksoy97@gmail.com)
- **LinkedIn:** [https://www.linkedin.com/in/ayberkturksoy/](https://www.linkedin.com/in/ayberkturksoy/)
