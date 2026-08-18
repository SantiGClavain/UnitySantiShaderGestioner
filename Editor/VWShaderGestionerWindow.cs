using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace VWShaderGestioner.Editor
{
    public sealed class VWShaderGestionerWindow : EditorWindow
    {
        private const string WindowTitle = "VW Shader Gestioner";
        private const string MaterialsRoot = "Assets";
        private const string ScenesRoot = "Assets/Scenes";
        private const string ProjectOption = "Project";
        private const string CurrentSceneOption = "Current_Scene";
        private const string AllScenesOption = "AllScenes";
        private const string NoSceneOption = "NoScene";
        private const string BugsOption = "Bugs";
        private const string BugsInSceneOption = "Bugs_In_Scene";
        private const string DonePrefsKey = "VWShaderGestioner.DoneMaterialPaths";
        private const int ProjectOptionIndex = 0;
        private const int CurrentSceneOptionIndex = 1;
        private const int AllScenesOptionIndex = 2;
        private const int NoSceneOptionIndex = 3;
        private const int BugsOptionIndex = 4;
        private const int BugsInSceneOptionIndex = 5;
        private const int FirstSceneOptionIndex = 6;
        private const float DoneColumnWidth = 42f;
        private const float InstColumnWidth = 32f;
        private const float MaterialColumnWidth = 190f;
        private const float ShaderColumnWidth = 190f;
        private const float MeshRendererColumnWidth = 92f;
        private const float TextureColumnWidth = 230f;
        private const float TextureAssetsColumnWidth = 52f;
        private const float TextureInputColumnWidth = 190f;
        private const float TextureReferenceColumnWidth = 190f;
        private const float TextureCopyColumnWidth = 48f;
        private const float TextureThumbnailSize = 18f;
        private const float TextureRowVerticalPadding = 1f;
        private const float SceneColumnWidth = 190f;
        private const float SceneOpenColumnWidth = 48f;
        private const float CheckNameColumnWidth = 210f;
        private const float CheckStatusColumnWidth = 70f;
        private const float SplitterHeight = 6f;
        private const float MinMaterialsPanelHeight = 140f;
        private const float MinTexturesPanelHeight = 120f;
        private const float MinScenesPanelHeight = 90f;
        private const float MinChecksPanelHeight = 92f;
        private const float MinPathColumnWidth = 40f;

        private readonly List<MaterialEntry> materials = new List<MaterialEntry>();
        private readonly List<SceneEntry> scenes = new List<SceneEntry>();
        private readonly List<MaterialEntry> filteredMaterials = new List<MaterialEntry>();
        private readonly List<TextureEntry> materialTextures = new List<TextureEntry>();
        private readonly List<MaterialSceneEntry> materialScenes = new List<MaterialSceneEntry>();
        private readonly Dictionary<Texture, int> textureMaterialUsageCounts = new Dictionary<Texture, int>();
        private readonly HashSet<string> selectedMaterialPaths = new HashSet<string>();
        private readonly HashSet<string> doneMaterialPaths = new HashSet<string>();
        private readonly HashSet<string> currentSceneMaterialPaths = new HashSet<string>();

        private readonly string[] doneOptions =
        {
            "None",
            "Exclusive",
            "NoExclusive"
        };

        private readonly string[] shaderGraphOptions =
        {
            "None",
            "Only",
            "Exclusive",
            "NoExclusive"
        };

        private Vector2 materialsScrollPosition;
        private Vector2 texturesScrollPosition;
        private Vector2 materialScenesScrollPosition;
        private float materialsPanelHeight = 260f;
        private float materialScenesPanelHeight = 140f;
        private float materialChecksPanelHeight = 118f;
        private int selectedSceneIndex;
        private int selectedDoneIndex;
        private int selectedShaderGraphIndex;
        private bool onlySelectedScene;
        private bool findReferencesInScene;
        private bool findReferencesQueued;
        private bool texturesLocked;
        private bool scenesLocked;
        private bool textureMaterialUsageCountsDirty = true;
        private string activeMaterialPath;
        private string materialTexturesMaterialName = "No material selected";
        private string materialScenesMaterialName = "No material selected";
        private MaterialSortColumn sortColumn = MaterialSortColumn.Material;
        private bool sortDescending;

        [MenuItem("Tools/VW Shader Gestioner")]
        private static void OpenWindow()
        {
            VWShaderGestionerWindow window = GetWindow<VWShaderGestionerWindow>();
            window.titleContent = new GUIContent(WindowTitle);
            window.minSize = new Vector2(560f, 560f);
            window.Show();
        }

        private void OnEnable()
        {
            Selection.selectionChanged += OnUnitySelectionChanged;
            LoadDoneMaterials();
            RefreshProjectData();
            UpdateActiveMaterialFromUnitySelection();
        }

        private void OnDisable()
        {
            Selection.selectionChanged -= OnUnitySelectionChanged;
        }

        private void OnProjectChange()
        {
            RefreshProjectData();
            UpdateActiveMaterialFromUnitySelection();
            Repaint();
        }

        private void OnHierarchyChange()
        {
            RefreshCurrentSceneMaterialUsage();
            Repaint();
        }

        private void OnInspectorUpdate()
        {
            if (!texturesLocked)
            {
                Repaint();
            }
        }

        private void OnGUI()
        {
            DrawFilters();
            EditorGUILayout.Space(8f);

            ClampMaterialsPanelHeight();
            ClampMaterialScenesPanelHeight();
            ClampMaterialChecksPanelHeight();

            using (new EditorGUILayout.VerticalScope())
            {
                using (new EditorGUILayout.VerticalScope(GUILayout.Height(materialsPanelHeight)))
                {
                    DrawMaterialsProject();
                }

                DrawVerticalSplitter();

                using (new EditorGUILayout.VerticalScope(GUILayout.ExpandHeight(true)))
                {
                    DrawMaterialTextures();
                    DrawMaterialScenesSplitter();

                    using (new EditorGUILayout.VerticalScope(GUILayout.Height(materialScenesPanelHeight)))
                    {
                        DrawMaterialScenes();
                    }

                    DrawMaterialChecksSplitter();

                    using (new EditorGUILayout.VerticalScope(GUILayout.Height(materialChecksPanelHeight)))
                    {
                        DrawMaterialChecks();
                    }
                }
            }
        }

        private void DrawFilters()
        {
            float previousLabelWidth = EditorGUIUtility.labelWidth;

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUIUtility.labelWidth = 48f;
                selectedSceneIndex = EditorGUILayout.Popup(
                    "Scenes",
                    selectedSceneIndex,
                    BuildSceneOptions(),
                    GUILayout.Width(260f));

                using (new EditorGUI.DisabledScope(selectedSceneIndex < FirstSceneOptionIndex || HasActiveSecondaryFilter()))
                {
                    EditorGUIUtility.labelWidth = 32f;
                    onlySelectedScene = EditorGUILayout.Toggle(
                        "Only",
                        onlySelectedScene,
                        GUILayout.Width(58f));
                }
            }

            EditorGUILayout.Space(2f);

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUIUtility.labelWidth = 48f;
                selectedDoneIndex = EditorGUILayout.Popup(
                    "Done",
                    selectedDoneIndex,
                    BuildDoneOptions(),
                    GUILayout.Width(200f));

                GUILayout.Space(8f);

                int selectedVisibleMaterialsCount = CountSelectedVisibleMaterials();

                using (new EditorGUI.DisabledScope(selectedVisibleMaterialsCount == 0))
                {
                    if (GUILayout.Button("Mark/Unmark as Done", GUILayout.Width(170f)))
                    {
                        ToggleDoneForSelectedMaterials();
                    }
                }

                if (GUILayout.Button("Clear_Checks/Select_Dones", GUILayout.MinWidth(120f), GUILayout.MaxWidth(170f)))
                {
                    ToggleDoneMaterialChecks();
                }
            }

            EditorGUILayout.Space(2f);

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUIUtility.labelWidth = 150f;
                bool newFindReferencesInScene = EditorGUILayout.Toggle(
                    "Find References in Scene",
                    findReferencesInScene,
                    GUILayout.Width(180f));

                if (newFindReferencesInScene != findReferencesInScene)
                {
                    findReferencesInScene = newFindReferencesInScene;

                    if (findReferencesInScene)
                    {
                        QueueFindReferencesInScene();
                    }
                    else
                    {
                        ClearFindReferencesInSceneSearch();
                    }
                }

                GUILayout.Space(8f);

                EditorGUIUtility.labelWidth = 92f;
                selectedShaderGraphIndex = EditorGUILayout.Popup(
                    "ShaderGraphs",
                    selectedShaderGraphIndex,
                    BuildShaderGraphOptions(),
                    GUILayout.Width(230f));

                if (GUILayout.Button(new GUIContent("Isolate in Scene", "Isolates scene GameObjects using the checked materials."), GUILayout.Width(120f)))
                {
                    IsolateSelectedMaterialsInScene();
                }
            }

            EditorGUIUtility.labelWidth = previousLabelWidth;
        }

        private void DrawMaterialsProject()
        {
            RebuildFilteredMaterials();
            int selectedVisibleMaterialsCount = CountSelectedVisibleMaterials();

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Materials Project", EditorStyles.boldLabel);
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.ExpandHeight(true)))
            {
                EditorGUILayout.LabelField(
                    $"Materials: {filteredMaterials.Count} | Selected: {selectedVisibleMaterialsCount}",
                    EditorStyles.miniLabel);

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Space(18f);
                    GUILayout.Space(DoneColumnWidth);
                    EditorGUILayout.LabelField(
                        new GUIContent("Inst", "Indicates whether GPU Instancing is enabled for this material."),
                        EditorStyles.miniLabel,
                        GUILayout.Width(InstColumnWidth));
                    DrawSortHeader(
                        MaterialSortColumn.Material,
                        new GUIContent("Material", "Sorts materials alphabetically by material name."),
                        GUILayout.Width(MaterialColumnWidth));
                    DrawSortHeader(
                        MaterialSortColumn.Shader,
                        new GUIContent("Shader", "Sorts materials alphabetically by shader name."),
                        GUILayout.Width(ShaderColumnWidth));
                    DrawSortHeader(
                        MaterialSortColumn.MeshRenderer,
                        new GUIContent("Mesh_Renderer", "Indicates whether Mesh Renderers using this material are enabled in the current scene."),
                        GUILayout.Width(MeshRendererColumnWidth));
                    DrawFlexibleSortHeader(
                        MaterialSortColumn.Path,
                        new GUIContent("Path", "Sorts materials alphabetically by project path."));
                }

                materialsScrollPosition = EditorGUILayout.BeginScrollView(materialsScrollPosition, GUILayout.ExpandHeight(true));

                foreach (MaterialEntry materialEntry in filteredMaterials)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        bool isSelected = selectedMaterialPaths.Contains(materialEntry.Path);
                        bool newSelection = EditorGUILayout.Toggle(isSelected, GUILayout.Width(18f));

                        if (newSelection != isSelected)
                        {
                            SetMaterialSelection(materialEntry.Path, newSelection);
                        }

                        EditorGUILayout.LabelField(
                            doneMaterialPaths.Contains(materialEntry.Path) ? "Done" : string.Empty,
                            GUILayout.Width(DoneColumnWidth));
                        EditorGUILayout.LabelField(
                            materialEntry.Material.enableInstancing ? "On" : "-",
                            GUILayout.Width(InstColumnWidth));
                        DrawMaterialProjectField(materialEntry);
                        EditorGUILayout.LabelField(materialEntry.ShaderName, EditorStyles.miniLabel, GUILayout.Width(ShaderColumnWidth));
                        EditorGUILayout.LabelField(GetMeshRendererDisplayValue(materialEntry), EditorStyles.miniLabel, GUILayout.Width(MeshRendererColumnWidth));
                        DrawFlexibleMiniLabel(materialEntry.Path);
                    }
                }

                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawVerticalSplitter()
        {
            int controlId = GUIUtility.GetControlID(FocusType.Passive);
            Rect splitterRect = GUILayoutUtility.GetRect(0f, SplitterHeight, GUILayout.ExpandWidth(true));
            EditorGUIUtility.AddCursorRect(splitterRect, MouseCursor.ResizeVertical);

            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(splitterRect, new Color(0.22f, 0.22f, 0.22f, 1f));
                Rect handleRect = new Rect(splitterRect.x, splitterRect.y + 2f, splitterRect.width, 1f);
                EditorGUI.DrawRect(handleRect, new Color(0.45f, 0.45f, 0.45f, 1f));
            }

            if (Event.current.type == EventType.MouseDown && splitterRect.Contains(Event.current.mousePosition))
            {
                GUIUtility.hotControl = controlId;
                Event.current.Use();
            }

            if (GUIUtility.hotControl == controlId && Event.current.type == EventType.MouseDrag)
            {
                materialsPanelHeight += Event.current.delta.y;
                ClampMaterialsPanelHeight();
                Repaint();
                Event.current.Use();
            }

            if (GUIUtility.hotControl == controlId && Event.current.type == EventType.MouseUp)
            {
                GUIUtility.hotControl = 0;
                Event.current.Use();
            }
        }

        private void DrawMaterialScenesSplitter()
        {
            int controlId = GUIUtility.GetControlID(FocusType.Passive);
            Rect splitterRect = GUILayoutUtility.GetRect(0f, SplitterHeight, GUILayout.ExpandWidth(true));
            EditorGUIUtility.AddCursorRect(splitterRect, MouseCursor.ResizeVertical);

            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(splitterRect, new Color(0.22f, 0.22f, 0.22f, 1f));
                Rect handleRect = new Rect(splitterRect.x, splitterRect.y + 2f, splitterRect.width, 1f);
                EditorGUI.DrawRect(handleRect, new Color(0.45f, 0.45f, 0.45f, 1f));
            }

            if (Event.current.type == EventType.MouseDown && splitterRect.Contains(Event.current.mousePosition))
            {
                GUIUtility.hotControl = controlId;
                Event.current.Use();
            }

            if (GUIUtility.hotControl == controlId && Event.current.type == EventType.MouseDrag)
            {
                materialScenesPanelHeight -= Event.current.delta.y;
                ClampMaterialScenesPanelHeight();
                Repaint();
                Event.current.Use();
            }

            if (GUIUtility.hotControl == controlId && Event.current.type == EventType.MouseUp)
            {
                GUIUtility.hotControl = 0;
                Event.current.Use();
            }
        }

        private void DrawMaterialChecksSplitter()
        {
            int controlId = GUIUtility.GetControlID(FocusType.Passive);
            Rect splitterRect = GUILayoutUtility.GetRect(0f, SplitterHeight, GUILayout.ExpandWidth(true));
            EditorGUIUtility.AddCursorRect(splitterRect, MouseCursor.ResizeVertical);

            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(splitterRect, new Color(0.22f, 0.22f, 0.22f, 1f));
                Rect handleRect = new Rect(splitterRect.x, splitterRect.y + 2f, splitterRect.width, 1f);
                EditorGUI.DrawRect(handleRect, new Color(0.45f, 0.45f, 0.45f, 1f));
            }

            if (Event.current.type == EventType.MouseDown && splitterRect.Contains(Event.current.mousePosition))
            {
                GUIUtility.hotControl = controlId;
                Event.current.Use();
            }

            if (GUIUtility.hotControl == controlId && Event.current.type == EventType.MouseDrag)
            {
                materialChecksPanelHeight -= Event.current.delta.y;
                ClampMaterialChecksPanelHeight();
                Repaint();
                Event.current.Use();
            }

            if (GUIUtility.hotControl == controlId && Event.current.type == EventType.MouseUp)
            {
                GUIUtility.hotControl = 0;
                Event.current.Use();
            }
        }

        private void DrawMaterialProjectField(MaterialEntry materialEntry)
        {
            Rect fieldRect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight, GUILayout.Width(MaterialColumnWidth));
            if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && fieldRect.Contains(Event.current.mousePosition))
            {
                SetActiveMaterial(materialEntry.Path);
                Selection.activeObject = materialEntry.Material;
                Event.current.Use();
            }

            EditorGUI.ObjectField(fieldRect, materialEntry.Material, typeof(Material), false);
        }

        private void ClampMaterialsPanelHeight()
        {
            float maxMaterialsPanelHeight = Mathf.Max(
                MinMaterialsPanelHeight,
                position.height - MinTexturesPanelHeight - MinScenesPanelHeight - MinChecksPanelHeight - SplitterHeight * 3f - 120f);

            materialsPanelHeight = Mathf.Clamp(
                materialsPanelHeight,
                MinMaterialsPanelHeight,
                maxMaterialsPanelHeight);
        }

        private void ClampMaterialScenesPanelHeight()
        {
            float lowerPanelHeight = Mathf.Max(
                MinTexturesPanelHeight + MinScenesPanelHeight + MinChecksPanelHeight + SplitterHeight * 2f,
                position.height - materialsPanelHeight - SplitterHeight - 120f);
            float maxScenesPanelHeight = Mathf.Max(
                MinScenesPanelHeight,
                lowerPanelHeight - MinTexturesPanelHeight - MinChecksPanelHeight - SplitterHeight * 2f);

            materialScenesPanelHeight = Mathf.Clamp(
                materialScenesPanelHeight,
                MinScenesPanelHeight,
                maxScenesPanelHeight);
        }

        private void ClampMaterialChecksPanelHeight()
        {
            float lowerPanelHeight = Mathf.Max(
                MinTexturesPanelHeight + MinScenesPanelHeight + MinChecksPanelHeight + SplitterHeight * 2f,
                position.height - materialsPanelHeight - SplitterHeight - 120f);
            float maxChecksPanelHeight = Mathf.Max(
                MinChecksPanelHeight,
                lowerPanelHeight - MinTexturesPanelHeight - MinScenesPanelHeight - SplitterHeight * 2f);

            materialChecksPanelHeight = Mathf.Clamp(
                materialChecksPanelHeight,
                MinChecksPanelHeight,
                maxChecksPanelHeight);
        }

        private void DrawSortHeader(MaterialSortColumn headerSortColumn, GUIContent content, params GUILayoutOption[] options)
        {
            string sortIndicator = sortColumn == headerSortColumn ? sortDescending ? " v" : " ^" : string.Empty;
            GUIContent headerContent = new GUIContent(content.text + sortIndicator, content.tooltip);

            if (GUILayout.Button(headerContent, EditorStyles.miniButton, options))
            {
                ApplySortHeaderClick(headerSortColumn);
            }
        }

        private void DrawFlexibleSortHeader(MaterialSortColumn headerSortColumn, GUIContent content)
        {
            string sortIndicator = sortColumn == headerSortColumn ? sortDescending ? " v" : " ^" : string.Empty;
            GUIContent headerContent = new GUIContent(content.text + sortIndicator, content.tooltip);
            Rect rect = EditorGUILayout.GetControlRect(
                false,
                EditorGUIUtility.singleLineHeight,
                GUILayout.MinWidth(MinPathColumnWidth),
                GUILayout.ExpandWidth(true));

            if (GUI.Button(rect, headerContent, EditorStyles.miniButton))
            {
                ApplySortHeaderClick(headerSortColumn);
            }
        }

        private void ApplySortHeaderClick(MaterialSortColumn headerSortColumn)
        {
            if (sortColumn == headerSortColumn)
            {
                sortDescending = !sortDescending;
            }
            else
            {
                sortColumn = headerSortColumn;
                sortDescending = false;
            }

            RebuildFilteredMaterials();
        }

        private static void DrawFlexibleMiniLabel(string text)
        {
            Rect rect = EditorGUILayout.GetControlRect(
                false,
                EditorGUIUtility.singleLineHeight,
                GUILayout.MinWidth(MinPathColumnWidth),
                GUILayout.ExpandWidth(true));
            EditorGUI.LabelField(rect, text, EditorStyles.miniLabel);
        }

        private void DrawMaterialTextures()
        {
            MaterialEntry activeMaterialEntry = GetActiveMaterialEntry();

            if (!texturesLocked)
            {
                RebuildMaterialTextures(activeMaterialEntry);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                bool newTexturesLocked = GUILayout.Toggle(
                    texturesLocked,
                    new GUIContent(string.Empty, "Locks the texture list so it does not refresh when materials, shaders, textures, or selection change."),
                    "IN LockButton",
                    GUILayout.Width(20f));

                if (newTexturesLocked != texturesLocked)
                {
                    texturesLocked = newTexturesLocked;

                    if (!texturesLocked)
                    {
                        UpdateActiveMaterialFromUnitySelection();
                        activeMaterialEntry = GetActiveMaterialEntry();
                        RebuildMaterialTextures(activeMaterialEntry);
                    }
                }

                EditorGUILayout.LabelField($"Material Inputs - {materialTexturesMaterialName}", EditorStyles.boldLabel);
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.ExpandHeight(true)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Space(TextureThumbnailSize + 4f);
                    EditorGUILayout.LabelField("Texture / Value", EditorStyles.miniLabel, GUILayout.Width(TextureColumnWidth));
                    EditorGUILayout.LabelField("Assets", EditorStyles.miniLabel, GUILayout.Width(TextureAssetsColumnWidth));
                    EditorGUILayout.LabelField("Input", EditorStyles.miniLabel, GUILayout.Width(TextureInputColumnWidth));
                    EditorGUILayout.LabelField("Reference", EditorStyles.miniLabel, GUILayout.Width(TextureReferenceColumnWidth));
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.LabelField("Copy", EditorStyles.miniLabel, GUILayout.Width(TextureCopyColumnWidth));
                }

                texturesScrollPosition = EditorGUILayout.BeginScrollView(texturesScrollPosition, GUILayout.ExpandHeight(true));

                for (int textureIndex = 0; textureIndex < materialTextures.Count; textureIndex++)
                {
                    TextureEntry textureEntry = materialTextures[textureIndex];

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        DrawTextureValueField(textureEntry, TextureColumnWidth + TextureThumbnailSize + 4f, textureIndex);
                        EditorGUILayout.LabelField(textureEntry.AssetsCountText, EditorStyles.miniLabel, GUILayout.Width(TextureAssetsColumnWidth));
                        EditorGUILayout.LabelField(textureEntry.Input, EditorStyles.miniLabel, GUILayout.Width(TextureInputColumnWidth));
                        EditorGUILayout.LabelField(textureEntry.Reference, EditorStyles.miniLabel, GUILayout.Width(TextureReferenceColumnWidth));
                        GUILayout.FlexibleSpace();

                        using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(textureEntry.CopyValue)))
                        {
                            if (GUILayout.Button(new GUIContent("Copy", "Copies this material parameter value to the system clipboard."), EditorStyles.miniButton, GUILayout.Width(TextureCopyColumnWidth)))
                            {
                                EditorGUIUtility.systemCopyBuffer = textureEntry.CopyValue;
                            }
                        }
                    }
                }

                if (materialTextures.Count == 0)
                {
                    EditorGUILayout.LabelField("No material inputs found.", EditorStyles.miniLabel);
                }

                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawMaterialScenes()
        {
            MaterialEntry activeMaterialEntry = GetActiveMaterialEntry();

            if (!scenesLocked)
            {
                RebuildMaterialScenes(activeMaterialEntry);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                bool newScenesLocked = GUILayout.Toggle(
                    scenesLocked,
                    new GUIContent(string.Empty, "Locks the scene list so it does not refresh when the selected material changes."),
                    "IN LockButton",
                    GUILayout.Width(20f));

                if (newScenesLocked != scenesLocked)
                {
                    scenesLocked = newScenesLocked;

                    if (!scenesLocked)
                    {
                        UpdateActiveMaterialFromUnitySelection();
                        activeMaterialEntry = GetActiveMaterialEntry();
                        RebuildMaterialScenes(activeMaterialEntry);
                    }
                }

                EditorGUILayout.LabelField($"Material Scenes - {materialScenesMaterialName}", EditorStyles.boldLabel);
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.ExpandHeight(true)))
            {
                EditorGUILayout.LabelField(
                    $"Scenes: {materialScenes.Count}",
                    EditorStyles.miniLabel);

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Scene", EditorStyles.miniLabel, GUILayout.Width(SceneColumnWidth));
                    DrawFlexibleMiniLabel("Path");
                    EditorGUILayout.LabelField("Open", EditorStyles.miniLabel, GUILayout.Width(SceneOpenColumnWidth));
                }

                materialScenesScrollPosition = EditorGUILayout.BeginScrollView(materialScenesScrollPosition, GUILayout.ExpandHeight(true));

                for (int sceneIndex = 0; sceneIndex < materialScenes.Count; sceneIndex++)
                {
                    MaterialSceneEntry sceneEntry = materialScenes[sceneIndex];

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        DrawSceneProjectField(sceneEntry, SceneColumnWidth);
                        DrawFlexibleMiniLabel(sceneEntry.Path);

                        using (new EditorGUI.DisabledScope(sceneEntry.SceneAsset == null))
                        {
                            if (GUILayout.Button(new GUIContent("Open", "Opens this scene asset."), EditorStyles.miniButton, GUILayout.Width(SceneOpenColumnWidth)))
                            {
                                AssetDatabase.OpenAsset(sceneEntry.SceneAsset);
                            }
                        }
                    }
                }

                if (materialScenes.Count == 0)
                {
                    EditorGUILayout.LabelField("No scene usage found.", EditorStyles.miniLabel);
                }

                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawSceneProjectField(MaterialSceneEntry sceneEntry, float width)
        {
            Rect fieldRect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight, GUILayout.Width(width));

            if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && fieldRect.Contains(Event.current.mousePosition))
            {
                if (sceneEntry.SceneAsset != null)
                {
                    Selection.activeObject = sceneEntry.SceneAsset;
                    EditorGUIUtility.PingObject(sceneEntry.SceneAsset);
                }

                Event.current.Use();
            }

            EditorGUI.ObjectField(fieldRect, sceneEntry.SceneAsset, typeof(SceneAsset), false);
        }

        private void DrawMaterialChecks()
        {
            MaterialEntry activeMaterialEntry = GetActiveMaterialEntry();
            MaterialCheckEntry[] checks = BuildMaterialChecks(activeMaterialEntry);
            string materialName = activeMaterialEntry != null && activeMaterialEntry.Material != null
                ? activeMaterialEntry.Material.name
                : "No material selected";

            EditorGUILayout.LabelField($"Material Checks - {materialName}", EditorStyles.boldLabel);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.ExpandHeight(true)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Check", EditorStyles.miniLabel, GUILayout.Width(CheckNameColumnWidth));
                    EditorGUILayout.LabelField("Status", EditorStyles.miniLabel, GUILayout.Width(CheckStatusColumnWidth));
                    DrawFlexibleMiniLabel("Details");
                }

                foreach (MaterialCheckEntry check in checks)
                {
                    DrawMaterialCheckRow(check);
                }
            }
        }

        private static void DrawMaterialCheckRow(MaterialCheckEntry check)
        {
            Color statusColor = check.HasProblem
                ? new Color(0.9f, 0.18f, 0.18f, 1f)
                : new Color(0.16f, 0.65f, 0.24f, 1f);
            GUIStyle statusStyle = new GUIStyle(EditorStyles.miniLabel);
            statusStyle.normal.textColor = statusColor;

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(check.Name, statusStyle, GUILayout.Width(CheckNameColumnWidth));
                EditorGUILayout.LabelField(check.HasProblem ? "Error" : "OK", statusStyle, GUILayout.Width(CheckStatusColumnWidth));
                DrawFlexibleMiniLabel(check.Details);
            }
        }

        private static MaterialCheckEntry[] BuildMaterialChecks(MaterialEntry materialEntry)
        {
            Material material = materialEntry != null ? materialEntry.Material : null;
            Shader shader = material != null ? material.shader : null;
            bool shaderIsNull = shader == null;

            MaterialCheckEntry shaderNullCheck = new MaterialCheckEntry(
                "Shader is null",
                shaderIsNull,
                shaderIsNull ? material == null ? "No material selected." : "Material has no shader assigned." : "Shader assigned.");

            MaterialCheckEntry shaderSupportedCheck = new MaterialCheckEntry(
                "Shader is supported",
                shaderIsNull || !shader.isSupported,
                shaderIsNull ? "Cannot check because shader is null." : shader.isSupported ? "Shader reports supported." : "Shader reports unsupported on this editor/platform.");

            bool hasShaderErrors = false;
            string shaderErrorsDetail = string.Empty;
            bool shaderErrorsKnown = !shaderIsNull && TryGetShaderErrorState(shader, out hasShaderErrors, out shaderErrorsDetail);
            MaterialCheckEntry shaderErrorsCheck = new MaterialCheckEntry(
                "Shader has errors",
                shaderIsNull || hasShaderErrors,
                shaderIsNull
                    ? "Cannot check because shader is null."
                    : shaderErrorsKnown
                        ? hasShaderErrors ? shaderErrorsDetail : "No shader errors reported."
                        : "Unity ShaderUtil error API was not available; no confirmed shader errors.");

            bool pipelineCompatible = true;
            string pipelineDetail = string.Empty;
            bool pipelineKnown = !shaderIsNull && TryGetPipelineCompatibility(material, out pipelineCompatible, out pipelineDetail);
            MaterialCheckEntry pipelineTagCheck = new MaterialCheckEntry(
                "Pipeline tag compatible",
                shaderIsNull || !pipelineCompatible,
                shaderIsNull
                    ? "Cannot check because shader is null."
                    : pipelineKnown
                        ? pipelineDetail
                        : "Could not determine active render pipeline; no confirmed tag incompatibility.");

            return new[]
            {
                shaderNullCheck,
                shaderSupportedCheck,
                shaderErrorsCheck,
                pipelineTagCheck
            };
        }

        private static bool TryGetShaderErrorState(Shader shader, out bool hasErrors, out string detail)
        {
            hasErrors = false;
            detail = string.Empty;

            MethodInfo shaderHasErrorMethod =
                GetShaderUtilMethod("ShaderHasError", typeof(Shader))
                ?? GetShaderUtilMethod("ShaderHasErrors", typeof(Shader))
                ?? GetShaderUtilMethod("HasShaderError", typeof(Shader))
                ?? GetShaderUtilMethod("HasShaderErrors", typeof(Shader));

            if (shaderHasErrorMethod == null || shaderHasErrorMethod.ReturnType != typeof(bool))
            {
                return false;
            }

            hasErrors = (bool)shaderHasErrorMethod.Invoke(null, new object[] { shader });
            detail = hasErrors ? GetShaderErrorDetails(shader) : string.Empty;

            if (hasErrors && string.IsNullOrEmpty(detail))
            {
                detail = "ShaderUtil reports shader errors.";
            }

            return true;
        }

        private static string GetShaderErrorDetails(Shader shader)
        {
            MethodInfo getShaderMessagesMethod = GetShaderUtilMethod("GetShaderMessages", typeof(Shader));

            if (getShaderMessagesMethod == null)
            {
                return string.Empty;
            }

            object messagesObject = getShaderMessagesMethod.Invoke(null, new object[] { shader });
            System.Array messages = messagesObject as System.Array;

            if (messages == null || messages.Length == 0)
            {
                return string.Empty;
            }

            StringBuilder builder = new StringBuilder();
            int appendedMessages = 0;

            foreach (object message in messages)
            {
                if (message == null)
                {
                    continue;
                }

                string messageText = ReadShaderMessageText(message);

                if (string.IsNullOrEmpty(messageText))
                {
                    continue;
                }

                if (builder.Length > 0)
                {
                    builder.Append(" | ");
                }

                builder.Append(messageText);
                appendedMessages++;

                if (appendedMessages >= 2)
                {
                    break;
                }
            }

            return builder.ToString();
        }

        private static string ReadShaderMessageText(object message)
        {
            System.Type messageType = message.GetType();
            string[] memberNames =
            {
                "message",
                "messageDetails",
                "file"
            };
            StringBuilder builder = new StringBuilder();

            foreach (string memberName in memberNames)
            {
                FieldInfo field = messageType.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                PropertyInfo property = messageType.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                object value = field != null ? field.GetValue(message) : property != null ? property.GetValue(message, null) : null;
                string text = value as string;

                if (string.IsNullOrEmpty(text))
                {
                    continue;
                }

                if (builder.Length > 0)
                {
                    builder.Append(" - ");
                }

                builder.Append(text);
            }

            return builder.Length > 0 ? builder.ToString() : message.ToString();
        }

        private static bool TryGetPipelineCompatibility(Material material, out bool isCompatible, out string detail)
        {
            isCompatible = true;
            string shaderRenderPipelineTag = material.GetTag("RenderPipeline", false, string.Empty);
            string activePipelineName = GetActiveRenderPipelineName();
            string expectedPipelineTag = GetExpectedRenderPipelineTag(activePipelineName);

            if (activePipelineName == null)
            {
                detail = string.Empty;
                return false;
            }

            if (expectedPipelineTag == null)
            {
                detail = $"Active pipeline '{activePipelineName}' is not mapped to a known RenderPipeline tag.";
                return false;
            }

            if (string.IsNullOrEmpty(expectedPipelineTag))
            {
                isCompatible = string.IsNullOrEmpty(shaderRenderPipelineTag);
                detail = isCompatible
                    ? "Built-in pipeline active and shader has no RenderPipeline tag."
                    : $"Built-in pipeline active but shader tag is '{shaderRenderPipelineTag}'.";
                return true;
            }

            isCompatible = string.Equals(shaderRenderPipelineTag, expectedPipelineTag, System.StringComparison.OrdinalIgnoreCase);
            detail = isCompatible
                ? $"Active pipeline '{activePipelineName}' matches shader tag '{shaderRenderPipelineTag}'."
                : $"Active pipeline '{activePipelineName}' expects tag '{expectedPipelineTag}', shader tag is '{FormatEmptyTag(shaderRenderPipelineTag)}'.";
            return true;
        }

        private static string GetActiveRenderPipelineName()
        {
            System.Type graphicsSettingsType = typeof(UnityEngine.Rendering.GraphicsSettings);
            PropertyInfo currentRenderPipelineProperty =
                graphicsSettingsType.GetProperty("currentRenderPipeline", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                ?? graphicsSettingsType.GetProperty("renderPipelineAsset", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            object renderPipelineAsset = currentRenderPipelineProperty != null
                ? currentRenderPipelineProperty.GetValue(null, null)
                : null;

            if (renderPipelineAsset == null)
            {
                return string.Empty;
            }

            System.Type pipelineType = renderPipelineAsset.GetType();
            return !string.IsNullOrEmpty(pipelineType.FullName) ? pipelineType.FullName : pipelineType.Name;
        }

        private static string GetExpectedRenderPipelineTag(string activePipelineName)
        {
            if (activePipelineName == null)
            {
                return null;
            }

            if (string.IsNullOrEmpty(activePipelineName))
            {
                return string.Empty;
            }

            if (activePipelineName.IndexOf("Universal", System.StringComparison.OrdinalIgnoreCase) >= 0
                || activePipelineName.IndexOf("URP", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "UniversalPipeline";
            }

            if (activePipelineName.IndexOf("HDRenderPipeline", System.StringComparison.OrdinalIgnoreCase) >= 0
                || activePipelineName.IndexOf("HighDefinition", System.StringComparison.OrdinalIgnoreCase) >= 0
                || activePipelineName.IndexOf("HDRP", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "HDRenderPipeline";
            }

            return null;
        }

        private static string FormatEmptyTag(string shaderRenderPipelineTag)
        {
            return string.IsNullOrEmpty(shaderRenderPipelineTag) ? "<empty>" : shaderRenderPipelineTag;
        }

        private void DrawTextureValueField(TextureEntry textureEntry, float width, int rowIndex)
        {
            if (textureEntry.Texture != null)
            {
                DrawTextureProjectField(textureEntry.Texture, width, rowIndex);
                return;
            }

            Rect rowRect = EditorGUILayout.GetControlRect(false, TextureThumbnailSize, GUILayout.Width(width));
            DrawTextureRowBackground(rowRect, rowIndex);
            Rect valueRect = new Rect(rowRect.x + TextureThumbnailSize + 4f, rowRect.y, rowRect.width - TextureThumbnailSize - 4f, rowRect.height);

            if (textureEntry.HasColor)
            {
                Rect swatchRect = new Rect(rowRect.x, rowRect.y + 1f, TextureThumbnailSize, TextureThumbnailSize - 2f);
                EditorGUI.DrawRect(swatchRect, textureEntry.ColorValue);
            }

            EditorGUI.LabelField(valueRect, textureEntry.DisplayValue, EditorStyles.miniLabel);
        }

        private void DrawTextureProjectField(Texture texture, float width, int rowIndex)
        {
            Rect rowRect = EditorGUILayout.GetControlRect(false, TextureThumbnailSize, GUILayout.Width(width));
            DrawTextureRowBackground(rowRect, rowIndex);
            Rect thumbnailRect = new Rect(rowRect.x, rowRect.y, TextureThumbnailSize, TextureThumbnailSize);
            Rect labelRect = new Rect(rowRect.x + TextureThumbnailSize + 4f, rowRect.y, rowRect.width - TextureThumbnailSize - 4f, rowRect.height);
            Texture thumbnail = GetTextureThumbnail(texture);

            if (Selection.activeObject == texture)
            {
                EditorGUI.DrawRect(rowRect, new Color(0.24f, 0.49f, 0.90f, 0.35f));
            }

            if (thumbnail != null)
            {
                GUI.DrawTexture(thumbnailRect, thumbnail, ScaleMode.ScaleToFit, false);
            }

            EditorGUI.LabelField(labelRect, texture != null ? texture.name : string.Empty, EditorStyles.miniLabel);

            if (texture == null)
            {
                return;
            }

            Event currentEvent = Event.current;

            if (currentEvent.type == EventType.MouseDown && currentEvent.button == 0 && rowRect.Contains(currentEvent.mousePosition))
            {
                Selection.activeObject = texture;
                EditorGUIUtility.PingObject(texture);
                Repaint();
                currentEvent.Use();
            }
            else if (currentEvent.type == EventType.MouseDrag && rowRect.Contains(currentEvent.mousePosition))
            {
                string texturePath = AssetDatabase.GetAssetPath(texture);
                DragAndDrop.PrepareStartDrag();
                DragAndDrop.objectReferences = new Object[] { texture };
                DragAndDrop.paths = string.IsNullOrEmpty(texturePath) ? new string[0] : new[] { texturePath };
                DragAndDrop.StartDrag(texture.name);
                currentEvent.Use();
            }
        }

        private static void DrawTextureRowBackground(Rect firstCellRect, int rowIndex)
        {
            if (Event.current.type != EventType.Repaint || rowIndex % 2 != 1)
            {
                return;
            }

            Rect rowRect = firstCellRect;
            rowRect.x = 0f;
            rowRect.width = EditorGUIUtility.currentViewWidth;
            rowRect.y -= TextureRowVerticalPadding;
            rowRect.height += TextureRowVerticalPadding * 2f;
            EditorGUI.DrawRect(rowRect, new Color(1f, 1f, 1f, EditorGUIUtility.isProSkin ? 0.04f : 0.12f));
        }

        private Texture GetTextureThumbnail(Texture texture)
        {
            if (texture == null)
            {
                return null;
            }

            Texture preview = AssetPreview.GetAssetPreview(texture);

            if (preview != null)
            {
                return preview;
            }

            if (AssetPreview.IsLoadingAssetPreview(texture.GetInstanceID()))
            {
                Repaint();
            }

            return AssetPreview.GetMiniThumbnail(texture);
        }

        private void RebuildMaterialTextures(MaterialEntry materialEntry)
        {
            materialTextures.Clear();
            materialTexturesMaterialName = materialEntry != null && materialEntry.Material != null
                ? materialEntry.Material.name
                : "No material selected";

            if (materialEntry == null || materialEntry.Material == null || materialEntry.Material.shader == null)
            {
                return;
            }

            Shader shader = materialEntry.Material.shader;
            int propertyCount = ShaderUtil.GetPropertyCount(shader);
            MaterialInputGroup currentInputGroup = MaterialInputGroup.Other;
            EnsureTextureMaterialUsageCounts();

            for (int propertyIndex = 0; propertyIndex < propertyCount; propertyIndex++)
            {
                if (!IsInspectorVisibleShaderProperty(shader, propertyIndex))
                {
                    continue;
                }

                ShaderUtil.ShaderPropertyType propertyType = ShaderUtil.GetPropertyType(shader, propertyIndex);
                string propertyName = ShaderUtil.GetPropertyName(shader, propertyIndex);
                string propertyDescription = ShaderUtil.GetPropertyDescription(shader, propertyIndex);
                string input = string.IsNullOrEmpty(propertyDescription) ? propertyName : propertyDescription;
                string[] propertyAttributes = GetShaderPropertyAttributes(shader, propertyIndex);
                currentInputGroup = ResolveMaterialInputGroup(
                    propertyName,
                    input,
                    propertyAttributes,
                    currentInputGroup);

                if (propertyType == ShaderUtil.ShaderPropertyType.TexEnv)
                {
                    Texture texture = materialEntry.Material.GetTexture(propertyName);
                    string textureCopyValue = BuildTextureCopyValue(texture);
                    int textureAssetsCount = texture != null && textureMaterialUsageCounts.ContainsKey(texture)
                        ? textureMaterialUsageCounts[texture]
                        : 0;

                    materialTextures.Add(new TextureEntry(
                        texture,
                        texture != null ? texture.name : "Null",
                        texture != null ? textureAssetsCount.ToString(CultureInfo.InvariantCulture) : string.Empty,
                        input,
                        propertyName,
                        textureCopyValue,
                        false,
                        Color.clear,
                        currentInputGroup,
                        propertyIndex));
                    continue;
                }

                string copyValue;
                bool hasColor;
                Color colorValue;
                string value = GetMaterialPropertyDisplayValue(
                    materialEntry.Material,
                    propertyName,
                    propertyType,
                    propertyAttributes,
                    out copyValue,
                    out hasColor,
                    out colorValue);

                materialTextures.Add(new TextureEntry(
                    null,
                    value,
                    string.Empty,
                    input,
                    propertyName,
                    copyValue,
                    hasColor,
                    colorValue,
                    currentInputGroup,
                    propertyIndex));
            }

            materialTextures.Sort(CompareTextureEntries);
        }

        private void RebuildMaterialScenes(MaterialEntry materialEntry)
        {
            materialScenes.Clear();
            materialScenesMaterialName = materialEntry != null && materialEntry.Material != null
                ? materialEntry.Material.name
                : "No material selected";

            if (materialEntry == null || materialEntry.Material == null)
            {
                return;
            }

            foreach (string scenePath in materialEntry.ScenePaths)
            {
                SceneAsset sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath);
                string sceneName = Path.GetFileNameWithoutExtension(scenePath);
                materialScenes.Add(new MaterialSceneEntry(sceneName, scenePath, sceneAsset));
            }

            materialScenes.Sort((left, right) => string.Compare(left.Name, right.Name, System.StringComparison.OrdinalIgnoreCase));
        }

        private static int CompareTextureEntries(TextureEntry left, TextureEntry right)
        {
            int result = left.InputGroup.CompareTo(right.InputGroup);

            if (result != 0)
            {
                return result;
            }

            return left.OriginalIndex.CompareTo(right.OriginalIndex);
        }

        private static MaterialInputGroup ResolveMaterialInputGroup(
            string propertyName,
            string input,
            string[] propertyAttributes,
            MaterialInputGroup currentInputGroup)
        {
            MaterialInputGroup attributeGroup;

            if (TryResolveMaterialInputGroupFromText(string.Join(" ", propertyAttributes), out attributeGroup))
            {
                return attributeGroup;
            }

            MaterialInputGroup nameGroup;

            if (TryResolveMaterialInputGroupFromText(propertyName + " " + input, out nameGroup))
            {
                return nameGroup;
            }

            return currentInputGroup;
        }

        private static bool TryResolveMaterialInputGroupFromText(string text, out MaterialInputGroup inputGroup)
        {
            string normalizedText = NormalizeMaterialInputGroupText(text);

            if (normalizedText.Contains("detailinputs") || normalizedText.Contains("detail"))
            {
                inputGroup = MaterialInputGroup.DetailInputs;
                return true;
            }

            if (normalizedText.Contains("surfaceinputs")
                || normalizedText.Contains("basemap")
                || normalizedText.Contains("basecolor")
                || normalizedText.Contains("albed")
                || normalizedText.Contains("metallic")
                || normalizedText.Contains("smoothness")
                || normalizedText.Contains("normalmap")
                || normalizedText.Contains("bumpmap")
                || normalizedText.Contains("occlusion")
                || normalizedText.Contains("emission"))
            {
                inputGroup = MaterialInputGroup.SurfaceInputs;
                return true;
            }

            if (normalizedText.Contains("surfaceoptions")
                || normalizedText.Contains("surface")
                || normalizedText.Contains("blend")
                || normalizedText.Contains("alphaclip")
                || normalizedText.Contains("cull")
                || normalizedText.Contains("receiveshadows"))
            {
                inputGroup = MaterialInputGroup.SurfaceOptions;
                return true;
            }

            if (normalizedText.Contains("advancedinputs")
                || normalizedText.Contains("advanced")
                || normalizedText.Contains("queue")
                || normalizedText.Contains("renderqueue")
                || normalizedText.Contains("priority")
                || normalizedText.Contains("stencil")
                || normalizedText.Contains("zwrite")
                || normalizedText.Contains("ztest"))
            {
                inputGroup = MaterialInputGroup.AdvancedInputs;
                return true;
            }

            inputGroup = MaterialInputGroup.Other;
            return false;
        }

        private static string NormalizeMaterialInputGroupText(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            StringBuilder builder = new StringBuilder(text.Length);

            foreach (char character in text)
            {
                if (char.IsLetterOrDigit(character))
                {
                    builder.Append(char.ToLowerInvariant(character));
                }
            }

            return builder.ToString();
        }

        private void EnsureTextureMaterialUsageCounts()
        {
            if (!textureMaterialUsageCountsDirty)
            {
                return;
            }

            textureMaterialUsageCounts.Clear();

            foreach (MaterialEntry projectMaterialEntry in materials)
            {
                if (projectMaterialEntry.Material == null || projectMaterialEntry.Material.shader == null)
                {
                    continue;
                }

                HashSet<Texture> materialTexturesFound = new HashSet<Texture>();
                Shader shader = projectMaterialEntry.Material.shader;
                int propertyCount = ShaderUtil.GetPropertyCount(shader);

                for (int propertyIndex = 0; propertyIndex < propertyCount; propertyIndex++)
                {
                    if (ShaderUtil.GetPropertyType(shader, propertyIndex) != ShaderUtil.ShaderPropertyType.TexEnv)
                    {
                        continue;
                    }

                    Texture texture = projectMaterialEntry.Material.GetTexture(ShaderUtil.GetPropertyName(shader, propertyIndex));

                    if (texture != null)
                    {
                        materialTexturesFound.Add(texture);
                    }
                }

                foreach (Texture texture in materialTexturesFound)
                {
                    int currentCount;
                    textureMaterialUsageCounts.TryGetValue(texture, out currentCount);
                    textureMaterialUsageCounts[texture] = currentCount + 1;
                }
            }

            textureMaterialUsageCountsDirty = false;
        }

        private static bool IsInspectorVisibleShaderProperty(Shader shader, int propertyIndex)
        {
            if (HasNonUserEditablePropertyFlag(shader, propertyIndex))
            {
                return false;
            }

            string[] attributes = GetShaderPropertyAttributes(shader, propertyIndex);

            foreach (string attribute in attributes)
            {
                if (IsNonUserEditablePropertyMarker(attribute))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool HasNonUserEditablePropertyFlag(Shader shader, int propertyIndex)
        {
            MethodInfo getPropertyFlagsMethod = GetShaderUtilMethod("GetPropertyFlags", typeof(Shader), typeof(int));

            if (getPropertyFlagsMethod == null)
            {
                return false;
            }

            object flags = getPropertyFlagsMethod.Invoke(null, new object[] { shader, propertyIndex });
            return flags != null && IsNonUserEditablePropertyMarker(flags.ToString());
        }

        private static string[] GetShaderPropertyAttributes(Shader shader, int propertyIndex)
        {
            MethodInfo getPropertyAttributesMethod = GetShaderUtilMethod("GetPropertyAttributes", typeof(Shader), typeof(int));

            if (getPropertyAttributesMethod == null)
            {
                return new string[0];
            }

            object attributes = getPropertyAttributesMethod.Invoke(null, new object[] { shader, propertyIndex });
            return attributes as string[] ?? new string[0];
        }

        private static bool IsNonUserEditablePropertyMarker(string marker)
        {
            return marker.IndexOf("HideInInspector", System.StringComparison.OrdinalIgnoreCase) >= 0
                || marker.IndexOf("PerRendererData", System.StringComparison.OrdinalIgnoreCase) >= 0
                || marker.IndexOf("NonModifiableTextureData", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static MethodInfo GetShaderUtilMethod(string methodName, params System.Type[] parameterTypes)
        {
            MethodInfo[] methods = typeof(ShaderUtil).GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

            foreach (MethodInfo method in methods)
            {
                if (method.Name != methodName)
                {
                    continue;
                }

                ParameterInfo[] parameters = method.GetParameters();

                if (parameters.Length != parameterTypes.Length)
                {
                    continue;
                }

                bool matches = true;

                for (int i = 0; i < parameters.Length; i++)
                {
                    if (parameters[i].ParameterType != parameterTypes[i])
                    {
                        matches = false;
                        break;
                    }
                }

                if (matches)
                {
                    return method;
                }
            }

            return null;
        }

        private static string BuildTextureCopyValue(Texture texture)
        {
            if (texture == null)
            {
                return "null";
            }

            string texturePath = AssetDatabase.GetAssetPath(texture);

            if (!string.IsNullOrEmpty(texturePath))
            {
                return Path.GetFileNameWithoutExtension(texturePath);
            }

            return texture.name;
        }

        private static string GetMaterialPropertyDisplayValue(
            Material material,
            string propertyName,
            ShaderUtil.ShaderPropertyType propertyType,
            string[] propertyAttributes,
            out string copyValue,
            out bool hasColor,
            out Color colorValue)
        {
            hasColor = false;
            colorValue = Color.clear;

            switch (propertyType)
            {
                case ShaderUtil.ShaderPropertyType.Color:
                    colorValue = material.GetColor(propertyName);
                    copyValue = FormatColorHex(colorValue);
                    hasColor = true;
                    return copyValue;
                case ShaderUtil.ShaderPropertyType.Vector:
                    copyValue = FormatVector(material.GetVector(propertyName));
                    return copyValue;
                case ShaderUtil.ShaderPropertyType.Float:
                case ShaderUtil.ShaderPropertyType.Range:
                default:
                    if (IsBooleanShaderProperty(propertyAttributes))
                    {
                        bool boolValue = !Mathf.Approximately(material.GetFloat(propertyName), 0f);
                        copyValue = boolValue ? "true" : "false";
                        return copyValue;
                    }

                    if (IsIntegerShaderProperty(propertyType))
                    {
                        copyValue = GetIntegerMaterialPropertyValue(material, propertyName).ToString(CultureInfo.InvariantCulture);
                        return copyValue;
                    }

                    copyValue = FormatFloat(material.GetFloat(propertyName));
                    return copyValue;
            }
        }

        private static bool IsBooleanShaderProperty(string[] propertyAttributes)
        {
            foreach (string attribute in propertyAttributes)
            {
                if (attribute.IndexOf("Toggle", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsIntegerShaderProperty(ShaderUtil.ShaderPropertyType propertyType)
        {
            return string.Equals(propertyType.ToString(), "Int", System.StringComparison.OrdinalIgnoreCase);
        }

        private static int GetIntegerMaterialPropertyValue(Material material, string propertyName)
        {
            MethodInfo getIntegerMethod = GetMaterialIntegerMethod("GetInteger") ?? GetMaterialIntegerMethod("GetInt");

            if (getIntegerMethod != null)
            {
                return (int)getIntegerMethod.Invoke(material, new object[] { propertyName });
            }

            return Mathf.RoundToInt(material.GetFloat(propertyName));
        }

        private static MethodInfo GetMaterialIntegerMethod(string methodName)
        {
            return typeof(Material).GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[] { typeof(string) },
                null);
        }

        private static string FormatColorHex(Color color)
        {
            return "#" + ColorUtility.ToHtmlStringRGBA(color);
        }

        private static string FormatVector(Vector4 vector)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "({0}, {1}, {2}, {3})",
                FormatFloat(vector.x),
                FormatFloat(vector.y),
                FormatFloat(vector.z),
                FormatFloat(vector.w));
        }

        private static string FormatFloat(float value)
        {
            return value.ToString("0.########", CultureInfo.InvariantCulture);
        }

        private void RefreshProjectData()
        {
            materials.Clear();
            scenes.Clear();
            textureMaterialUsageCountsDirty = true;

            Dictionary<string, MaterialEntry> materialsByPath = CollectMaterials();
            CollectScenes();
            CollectSceneMaterialUsage(materialsByPath);
            CollectCurrentSceneMaterialUsage(materialsByPath);
            RebuildFilteredMaterials();
        }

        private Dictionary<string, MaterialEntry> CollectMaterials()
        {
            Dictionary<string, MaterialEntry> materialsByPath = new Dictionary<string, MaterialEntry>();

            if (!AssetDatabase.IsValidFolder(MaterialsRoot))
            {
                return materialsByPath;
            }

            string[] materialGuids = AssetDatabase.FindAssets("t:Material", new[] { MaterialsRoot });

            foreach (string materialGuid in materialGuids)
            {
                string materialPath = AssetDatabase.GUIDToAssetPath(materialGuid);
                Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);

                if (material == null)
                {
                    continue;
                }

                MaterialEntry materialEntry = new MaterialEntry(materialPath, material);
                materials.Add(materialEntry);
                materialsByPath[materialPath] = materialEntry;
            }

            materials.Sort((left, right) => string.CompareOrdinal(left.Path, right.Path));
            return materialsByPath;
        }

        private void CollectScenes()
        {
            if (!AssetDatabase.IsValidFolder(ScenesRoot))
            {
                return;
            }

            string[] sceneGuids = AssetDatabase.FindAssets("t:Scene", new[] { ScenesRoot });

            foreach (string sceneGuid in sceneGuids)
            {
                string scenePath = AssetDatabase.GUIDToAssetPath(sceneGuid);
                string sceneName = Path.GetFileNameWithoutExtension(scenePath);
                scenes.Add(new SceneEntry(sceneName, scenePath));
            }

            scenes.Sort((left, right) => string.CompareOrdinal(left.Name, right.Name));
        }

        private void CollectSceneMaterialUsage(Dictionary<string, MaterialEntry> materialsByPath)
        {
            foreach (SceneEntry scene in scenes)
            {
                string[] dependencies = AssetDatabase.GetDependencies(scene.Path, true);

                foreach (string dependencyPath in dependencies)
                {
                    if (!materialsByPath.TryGetValue(dependencyPath, out MaterialEntry materialEntry))
                    {
                        continue;
                    }

                    materialEntry.ScenePaths.Add(scene.Path);
                }
            }
        }

        private void CollectCurrentSceneMaterialUsage(Dictionary<string, MaterialEntry> materialsByPath)
        {
            currentSceneMaterialPaths.Clear();

            foreach (MaterialEntry materialEntry in materialsByPath.Values)
            {
                materialEntry.ResetCurrentSceneMeshRendererState();
            }

            Scene activeScene = SceneManager.GetActiveScene();

            if (!activeScene.IsValid() || !activeScene.isLoaded)
            {
                return;
            }

            GameObject[] rootGameObjects = activeScene.GetRootGameObjects();

            foreach (GameObject rootGameObject in rootGameObjects)
            {
                Renderer[] renderers = rootGameObject.GetComponentsInChildren<Renderer>(true);

                foreach (Renderer renderer in renderers)
                {
                    foreach (Material sharedMaterial in renderer.sharedMaterials)
                    {
                        if (sharedMaterial == null)
                        {
                            continue;
                        }

                        string materialPath = AssetDatabase.GetAssetPath(sharedMaterial);

                        if (materialsByPath.TryGetValue(materialPath, out MaterialEntry materialEntry))
                        {
                            currentSceneMaterialPaths.Add(materialPath);

                            MeshRenderer meshRenderer = renderer as MeshRenderer;
                            if (meshRenderer != null)
                            {
                                materialEntry.AddCurrentSceneMeshRendererState(meshRenderer.enabled);
                            }
                        }
                    }
                }
            }
        }

        private void RefreshCurrentSceneMaterialUsage()
        {
            Dictionary<string, MaterialEntry> materialsByPath = new Dictionary<string, MaterialEntry>();

            foreach (MaterialEntry materialEntry in materials)
            {
                materialsByPath[materialEntry.Path] = materialEntry;
            }

            CollectCurrentSceneMaterialUsage(materialsByPath);
            RebuildFilteredMaterials();
        }

        private void RebuildFilteredMaterials()
        {
            filteredMaterials.Clear();

            foreach (MaterialEntry materialEntry in materials)
            {
                if (PassesFilters(materialEntry))
                {
                    filteredMaterials.Add(materialEntry);
                }
            }

            filteredMaterials.Sort(CompareMaterialEntries);
        }

        private int CompareMaterialEntries(MaterialEntry left, MaterialEntry right)
        {
            int result;

            switch (sortColumn)
            {
                case MaterialSortColumn.Material:
                    result = string.Compare(left.Material.name, right.Material.name, System.StringComparison.OrdinalIgnoreCase);
                    break;
                case MaterialSortColumn.Shader:
                    result = string.Compare(left.ShaderName, right.ShaderName, System.StringComparison.OrdinalIgnoreCase);
                    break;
                case MaterialSortColumn.MeshRenderer:
                    result = string.Compare(
                        GetMeshRendererDisplayValue(left),
                        GetMeshRendererDisplayValue(right),
                        System.StringComparison.OrdinalIgnoreCase);
                    break;
                case MaterialSortColumn.Path:
                default:
                    result = string.Compare(left.Path, right.Path, System.StringComparison.OrdinalIgnoreCase);
                    break;
            }

            if (result == 0)
            {
                result = string.Compare(left.Path, right.Path, System.StringComparison.OrdinalIgnoreCase);
            }

            return sortDescending ? -result : result;
        }

        private bool PassesFilters(MaterialEntry materialEntry)
        {
            if (!HasActiveSecondaryFilter())
            {
                return PassesSceneFilter(materialEntry, true);
            }

            if (selectedDoneIndex != 0 && !PassesDoneFilter(materialEntry))
            {
                return false;
            }

            if (selectedShaderGraphIndex != 0 && !PassesShaderGraphFilter(materialEntry))
            {
                return false;
            }

            return true;
        }

        private bool PassesSceneFilter(MaterialEntry materialEntry)
        {
            return PassesSceneFilter(materialEntry, true);
        }

        private bool PassesSceneFilter(MaterialEntry materialEntry, bool applyOnlyFilter)
        {
            if (selectedSceneIndex == ProjectOptionIndex)
            {
                return true;
            }

            if (selectedSceneIndex == CurrentSceneOptionIndex)
            {
                return currentSceneMaterialPaths.Contains(materialEntry.Path);
            }

            if (selectedSceneIndex == AllScenesOptionIndex)
            {
                return materialEntry.ScenePaths.Count > 0;
            }

            if (selectedSceneIndex == NoSceneOptionIndex)
            {
                return materialEntry.ScenePaths.Count == 0;
            }

            if (selectedSceneIndex == BugsOptionIndex)
            {
                return HasMaterialCheckProblems(materialEntry);
            }

            if (selectedSceneIndex == BugsInSceneOptionIndex)
            {
                return currentSceneMaterialPaths.Contains(materialEntry.Path) && HasMaterialCheckProblems(materialEntry);
            }

            int sceneIndex = selectedSceneIndex - FirstSceneOptionIndex;

            if (sceneIndex < 0 || sceneIndex >= scenes.Count)
            {
                return false;
            }

            string selectedScenePath = scenes[sceneIndex].Path;

            if (!materialEntry.ScenePaths.Contains(selectedScenePath))
            {
                return false;
            }

            return !applyOnlyFilter || !onlySelectedScene || materialEntry.ScenePaths.Count == 1;
        }

        private bool PassesDoneFilter(MaterialEntry materialEntry)
        {
            if (!doneMaterialPaths.Contains(materialEntry.Path))
            {
                return false;
            }

            if (selectedDoneIndex == 1)
            {
                return PassesExclusiveDoneFilter(materialEntry);
            }

            return PassesSceneFilter(materialEntry, false);
        }

        private bool PassesShaderGraphFilter(MaterialEntry materialEntry)
        {
            if (!materialEntry.IsShaderGraph)
            {
                return false;
            }

            if (selectedShaderGraphIndex == 1)
            {
                return PassesSceneFilter(materialEntry, true);
            }

            if (selectedShaderGraphIndex == 2)
            {
                return PassesExclusiveSceneFilter(materialEntry);
            }

            return PassesSceneFilter(materialEntry, false);
        }

        private bool PassesExclusiveDoneFilter(MaterialEntry materialEntry)
        {
            return PassesExclusiveSceneFilter(materialEntry);
        }

        private bool PassesExclusiveSceneFilter(MaterialEntry materialEntry)
        {
            if (selectedSceneIndex == ProjectOptionIndex || selectedSceneIndex == AllScenesOptionIndex)
            {
                return materialEntry.ScenePaths.Count == 1;
            }

            if (selectedSceneIndex == CurrentSceneOptionIndex)
            {
                return IsExclusiveToCurrentScene(materialEntry);
            }

            if (selectedSceneIndex == NoSceneOptionIndex)
            {
                return materialEntry.ScenePaths.Count == 0;
            }

            if (selectedSceneIndex == BugsOptionIndex)
            {
                return HasMaterialCheckProblems(materialEntry);
            }

            if (selectedSceneIndex == BugsInSceneOptionIndex)
            {
                return currentSceneMaterialPaths.Contains(materialEntry.Path) && HasMaterialCheckProblems(materialEntry);
            }

            int sceneIndex = selectedSceneIndex - FirstSceneOptionIndex;

            if (sceneIndex < 0 || sceneIndex >= scenes.Count)
            {
                return false;
            }

            string selectedScenePath = scenes[sceneIndex].Path;
            return materialEntry.ScenePaths.Count == 1 && materialEntry.ScenePaths.Contains(selectedScenePath);
        }

        private bool IsExclusiveToCurrentScene(MaterialEntry materialEntry)
        {
            if (!currentSceneMaterialPaths.Contains(materialEntry.Path))
            {
                return false;
            }

            string activeScenePath = SceneManager.GetActiveScene().path;

            foreach (string scenePath in materialEntry.ScenePaths)
            {
                if (scenePath != activeScenePath)
                {
                    return false;
                }
            }

            return true;
        }

        private bool HasActiveSecondaryFilter()
        {
            return selectedDoneIndex != 0 || selectedShaderGraphIndex != 0;
        }

        private static bool HasMaterialCheckProblems(MaterialEntry materialEntry)
        {
            MaterialCheckEntry[] checks = BuildMaterialChecks(materialEntry);

            foreach (MaterialCheckEntry check in checks)
            {
                if (check.HasProblem)
                {
                    return true;
                }
            }

            return false;
        }

        private string GetMeshRendererDisplayValue(MaterialEntry materialEntry)
        {
            if (selectedSceneIndex != CurrentSceneOptionIndex)
            {
                return "--";
            }

            return materialEntry.CurrentSceneMeshRendererDisplayValue;
        }

        private void SetMaterialSelection(string materialPath, bool isSelected)
        {
            if (isSelected)
            {
                selectedMaterialPaths.Add(materialPath);
            }
            else
            {
                selectedMaterialPaths.Remove(materialPath);
            }

            if (findReferencesInScene)
            {
                QueueFindReferencesInScene();
            }
        }

        private void SetActiveMaterial(string materialPath)
        {
            if (activeMaterialPath == materialPath)
            {
                return;
            }

            activeMaterialPath = materialPath;

            if (!texturesLocked)
            {
                RebuildMaterialTextures(GetActiveMaterialEntry());
                Repaint();
            }
        }

        private void OnUnitySelectionChanged()
        {
            bool changed = UpdateActiveMaterialFromUnitySelection();

            if (changed && !texturesLocked)
            {
                Repaint();
            }
        }

        private bool UpdateActiveMaterialFromUnitySelection()
        {
            Material selectedMaterial = Selection.activeObject as Material;

            if (selectedMaterial == null)
            {
                return false;
            }

            string selectedMaterialPath = AssetDatabase.GetAssetPath(selectedMaterial);

            if (string.IsNullOrEmpty(selectedMaterialPath) || activeMaterialPath == selectedMaterialPath)
            {
                return false;
            }

            activeMaterialPath = selectedMaterialPath;
            return true;
        }

        private MaterialEntry GetActiveMaterialEntry()
        {
            if (string.IsNullOrEmpty(activeMaterialPath))
            {
                return null;
            }

            foreach (MaterialEntry materialEntry in materials)
            {
                if (materialEntry.Path == activeMaterialPath)
                {
                    return materialEntry;
                }
            }

            return null;
        }

        private void QueueFindReferencesInScene()
        {
            if (findReferencesQueued || selectedMaterialPaths.Count == 0)
            {
                return;
            }

            findReferencesQueued = true;
            EditorApplication.delayCall += ExecuteFindReferencesInSceneForSelectedMaterials;
        }

        private void ExecuteFindReferencesInSceneForSelectedMaterials()
        {
            findReferencesQueued = false;

            List<Object> selectedMaterials = new List<Object>();

            foreach (MaterialEntry materialEntry in materials)
            {
                if (selectedMaterialPaths.Contains(materialEntry.Path))
                {
                    selectedMaterials.Add(materialEntry.Material);
                }
            }

            if (selectedMaterials.Count == 0)
            {
                return;
            }

            Selection.objects = selectedMaterials.ToArray();
            EditorApplication.ExecuteMenuItem("Assets/Find References In Scene");
        }

        private void IsolateSelectedMaterialsInScene()
        {
            if (SceneVisibilityManager.instance.IsCurrentStageIsolated())
            {
                SceneVisibilityManager.instance.ExitIsolation();
                SceneVisibilityManager.instance.ShowAll();
                SceneView.RepaintAll();
                return;
            }

            HashSet<Material> selectedMaterials = new HashSet<Material>();

            foreach (MaterialEntry materialEntry in materials)
            {
                if (selectedMaterialPaths.Contains(materialEntry.Path) && materialEntry.Material != null)
                {
                    selectedMaterials.Add(materialEntry.Material);
                }
            }

            if (selectedMaterials.Count == 0)
            {
                EditorUtility.DisplayDialog(
                    WindowTitle,
                    "Select at least one material with the check before isolating it in the scene.",
                    "OK");
                return;
            }

            Scene activeScene = SceneManager.GetActiveScene();

            if (!activeScene.IsValid() || !activeScene.isLoaded)
            {
                EditorUtility.DisplayDialog(WindowTitle, "There is no valid loaded active scene.", "OK");
                return;
            }

            List<GameObject> matchingGameObjects = new List<GameObject>();
            HashSet<GameObject> matchingGameObjectSet = new HashSet<GameObject>();
            GameObject[] rootGameObjects = activeScene.GetRootGameObjects();

            foreach (GameObject rootGameObject in rootGameObjects)
            {
                MeshRenderer[] meshRenderers = rootGameObject.GetComponentsInChildren<MeshRenderer>(true);

                foreach (MeshRenderer meshRenderer in meshRenderers)
                {
                    if (meshRenderer == null || !UsesAnyMaterial(meshRenderer, selectedMaterials))
                    {
                        continue;
                    }

                    GameObject matchingGameObject = meshRenderer.gameObject;

                    if (matchingGameObjectSet.Add(matchingGameObject))
                    {
                        matchingGameObjects.Add(matchingGameObject);
                    }
                }
            }

            if (matchingGameObjects.Count == 0)
            {
                EditorUtility.DisplayDialog(
                    WindowTitle,
                    "No MeshRenderer in the active scene uses the checked materials.",
                    "OK");
                return;
            }

            GameObject[] objectsToIsolate = matchingGameObjects.ToArray();
            SceneVisibilityManager.instance.ExitIsolation();
            SceneVisibilityManager.instance.ShowAll();
            Selection.objects = objectsToIsolate;
            SceneVisibilityManager.instance.Isolate(objectsToIsolate, true);
            SceneView.RepaintAll();
        }

        private static bool UsesAnyMaterial(Renderer renderer, HashSet<Material> materialsToMatch)
        {
            foreach (Material sharedMaterial in renderer.sharedMaterials)
            {
                if (sharedMaterial != null && materialsToMatch.Contains(sharedMaterial))
                {
                    return true;
                }
            }

            return false;
        }

        private static void ClearFindReferencesInSceneSearch()
        {
            System.Type hierarchyWindowType = typeof(EditorWindow).Assembly.GetType("UnityEditor.SceneHierarchyWindow");

            if (hierarchyWindowType == null)
            {
                return;
            }

            Object[] hierarchyWindows = Resources.FindObjectsOfTypeAll(hierarchyWindowType);

            foreach (Object hierarchyWindow in hierarchyWindows)
            {
                if (TryClearSearchFilter(hierarchyWindow))
                {
                    ((EditorWindow)hierarchyWindow).Repaint();
                }
            }
        }

        private static bool TryClearSearchFilter(Object hierarchyWindow)
        {
            System.Type currentType = hierarchyWindow.GetType();

            while (currentType != null)
            {
                MethodInfo[] methods = currentType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                foreach (MethodInfo method in methods)
                {
                    if (method.Name != "SetSearchFilter")
                    {
                        continue;
                    }

                    ParameterInfo[] parameters = method.GetParameters();

                    if (parameters.Length < 1 || parameters[0].ParameterType != typeof(string))
                    {
                        continue;
                    }

                    object[] arguments = BuildClearSearchArguments(parameters);

                    if (arguments == null)
                    {
                        continue;
                    }

                    method.Invoke(hierarchyWindow, arguments);
                    return true;
                }

                currentType = currentType.BaseType;
            }

            return false;
        }

        private static object[] BuildClearSearchArguments(ParameterInfo[] parameters)
        {
            object[] arguments = new object[parameters.Length];

            for (int i = 0; i < parameters.Length; i++)
            {
                System.Type parameterType = parameters[i].ParameterType;

                if (parameterType == typeof(string))
                {
                    arguments[i] = string.Empty;
                }
                else if (parameterType == typeof(bool))
                {
                    arguments[i] = true;
                }
                else if (parameterType == typeof(int))
                {
                    arguments[i] = 0;
                }
                else if (parameterType.IsEnum)
                {
                    arguments[i] = System.Enum.ToObject(parameterType, 0);
                }
                else
                {
                    return null;
                }
            }

            return arguments;
        }

        private void ToggleDoneForSelectedMaterials()
        {
            bool shouldUnmark = true;

            foreach (MaterialEntry materialEntry in filteredMaterials)
            {
                if (!selectedMaterialPaths.Contains(materialEntry.Path))
                {
                    continue;
                }

                if (!doneMaterialPaths.Contains(materialEntry.Path))
                {
                    shouldUnmark = false;
                    break;
                }
            }

            foreach (MaterialEntry materialEntry in filteredMaterials)
            {
                if (!selectedMaterialPaths.Contains(materialEntry.Path))
                {
                    continue;
                }

                if (shouldUnmark)
                {
                    doneMaterialPaths.Remove(materialEntry.Path);
                }
                else
                {
                    doneMaterialPaths.Add(materialEntry.Path);
                }
            }

            SaveDoneMaterials();
            RebuildFilteredMaterials();
        }

        private void ToggleDoneMaterialChecks()
        {
            if (selectedMaterialPaths.Count > 0)
            {
                selectedMaterialPaths.Clear();
            }
            else
            {
                foreach (MaterialEntry materialEntry in filteredMaterials)
                {
                    if (doneMaterialPaths.Contains(materialEntry.Path))
                    {
                        selectedMaterialPaths.Add(materialEntry.Path);
                    }
                }
            }

            RebuildFilteredMaterials();
            Repaint();
        }

        private int CountSelectedVisibleMaterials()
        {
            int count = 0;

            foreach (MaterialEntry materialEntry in filteredMaterials)
            {
                if (selectedMaterialPaths.Contains(materialEntry.Path))
                {
                    count++;
                }
            }

            return count;
        }

        private string[] BuildSceneOptions()
        {
            string[] options = new string[scenes.Count + FirstSceneOptionIndex];
            options[ProjectOptionIndex] = ProjectOption;
            options[CurrentSceneOptionIndex] = CurrentSceneOption;
            options[AllScenesOptionIndex] = AllScenesOption;
            options[NoSceneOptionIndex] = NoSceneOption;
            options[BugsOptionIndex] = BugsOption;
            options[BugsInSceneOptionIndex] = BugsInSceneOption;

            for (int i = 0; i < scenes.Count; i++)
            {
                options[i + FirstSceneOptionIndex] = scenes[i].Name;
            }

            if (selectedSceneIndex >= options.Length)
            {
                selectedSceneIndex = 0;
            }

            return options;
        }

        private string[] BuildDoneOptions()
        {
            if (selectedDoneIndex >= doneOptions.Length)
            {
                selectedDoneIndex = 0;
            }

            return doneOptions;
        }

        private string[] BuildShaderGraphOptions()
        {
            if (selectedShaderGraphIndex >= shaderGraphOptions.Length)
            {
                selectedShaderGraphIndex = 0;
            }

            return shaderGraphOptions;
        }

        private static bool IsShaderGraphMaterial(Material material)
        {
            if (material == null || material.shader == null)
            {
                return false;
            }

            string shaderPath = AssetDatabase.GetAssetPath(material.shader);

            if (string.IsNullOrEmpty(shaderPath))
            {
                return false;
            }

            if (shaderPath.EndsWith(".shadergraph", System.StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            AssetImporter shaderImporter = AssetImporter.GetAtPath(shaderPath);
            string importerTypeName = shaderImporter != null ? shaderImporter.GetType().FullName : string.Empty;
            return importerTypeName.Contains("ShaderGraph");
        }

        private void LoadDoneMaterials()
        {
            doneMaterialPaths.Clear();

            string serializedPaths = EditorPrefs.GetString(DonePrefsKey, string.Empty);

            if (string.IsNullOrEmpty(serializedPaths))
            {
                return;
            }

            string[] paths = serializedPaths.Split('\n');

            foreach (string path in paths)
            {
                if (!string.IsNullOrEmpty(path))
                {
                    doneMaterialPaths.Add(path);
                }
            }
        }

        private void SaveDoneMaterials()
        {
            StringBuilder builder = new StringBuilder();

            foreach (string doneMaterialPath in doneMaterialPaths)
            {
                builder.Append(doneMaterialPath);
                builder.Append('\n');
            }

            EditorPrefs.SetString(DonePrefsKey, builder.ToString());
        }

        private sealed class MaterialEntry
        {
            public MaterialEntry(string path, Material material)
            {
                Path = path;
                Material = material;
                ShaderName = material.shader != null ? material.shader.name : string.Empty;
                IsShaderGraph = IsShaderGraphMaterial(material);
            }

            public string Path { get; }
            public Material Material { get; }
            public string ShaderName { get; }
            public bool IsShaderGraph { get; }
            public HashSet<string> ScenePaths { get; } = new HashSet<string>();
            public bool HasCurrentSceneEnabledMeshRenderer { get; private set; }
            public bool HasCurrentSceneDisabledMeshRenderer { get; private set; }
            public string CurrentSceneMeshRendererDisplayValue
            {
                get
                {
                    if (HasCurrentSceneEnabledMeshRenderer && HasCurrentSceneDisabledMeshRenderer)
                    {
                        return "Mix";
                    }

                    if (HasCurrentSceneEnabledMeshRenderer)
                    {
                        return "On";
                    }

                    if (HasCurrentSceneDisabledMeshRenderer)
                    {
                        return "Off";
                    }

                    return "--";
                }
            }

            public void ResetCurrentSceneMeshRendererState()
            {
                HasCurrentSceneEnabledMeshRenderer = false;
                HasCurrentSceneDisabledMeshRenderer = false;
            }

            public void AddCurrentSceneMeshRendererState(bool isEnabled)
            {
                if (isEnabled)
                {
                    HasCurrentSceneEnabledMeshRenderer = true;
                }
                else
                {
                    HasCurrentSceneDisabledMeshRenderer = true;
                }
            }
        }

        private sealed class TextureEntry
        {
            public TextureEntry(
                Texture texture,
                string displayValue,
                string assetsCountText,
                string input,
                string reference,
                string copyValue,
                bool hasColor,
                Color colorValue,
                MaterialInputGroup inputGroup,
                int originalIndex)
            {
                Texture = texture;
                DisplayValue = displayValue;
                AssetsCountText = assetsCountText;
                Input = input;
                Reference = reference;
                CopyValue = copyValue;
                HasColor = hasColor;
                ColorValue = colorValue;
                InputGroup = inputGroup;
                OriginalIndex = originalIndex;
            }

            public Texture Texture { get; }
            public string DisplayValue { get; }
            public string AssetsCountText { get; }
            public string Input { get; }
            public string Reference { get; }
            public string CopyValue { get; }
            public bool HasColor { get; }
            public Color ColorValue { get; }
            public MaterialInputGroup InputGroup { get; }
            public int OriginalIndex { get; }
        }

        private sealed class MaterialSceneEntry
        {
            public MaterialSceneEntry(string name, string path, SceneAsset sceneAsset)
            {
                Name = name;
                Path = path;
                SceneAsset = sceneAsset;
            }

            public string Name { get; }
            public string Path { get; }
            public SceneAsset SceneAsset { get; }
        }

        private sealed class MaterialCheckEntry
        {
            public MaterialCheckEntry(string name, bool hasProblem, string details)
            {
                Name = name;
                HasProblem = hasProblem;
                Details = details;
            }

            public string Name { get; }
            public bool HasProblem { get; }
            public string Details { get; }
        }

        private enum MaterialInputGroup
        {
            SurfaceInputs,
            DetailInputs,
            SurfaceOptions,
            AdvancedInputs,
            Other
        }

        private enum MaterialSortColumn
        {
            Material,
            Shader,
            MeshRenderer,
            Path
        }

        private sealed class SceneEntry
        {
            public SceneEntry(string name, string path)
            {
                Name = name;
                Path = path;
            }

            public string Name { get; }
            public string Path { get; }
        }
    }
}
