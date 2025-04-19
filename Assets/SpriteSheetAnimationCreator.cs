using UnityEngine;
using UnityEditor;
using UnityEditor.Animations; // Add this to fix AnimatorStateMachine reference
using System.IO;
using System.Collections.Generic;
using System.Linq;

#if UNITY_EDITOR
public class SpriteSheetAnimationCreator : MonoBehaviour
{
    [Header("Input Settings")]
    [Tooltip("Path to the folder containing animation sprite sheets, relative to Assets folder")]
    public string spriteSheetsFolder = "AnimationFrames";
    
    [Tooltip("Base name of the output animation files")]
    public string animationBaseName = "Anim_";
    
    [Header("Animation Settings")]
    [Tooltip("Frames per second for the animations")]
    public float framesPerSecond = 24f;
    
    [Tooltip("Whether animations should loop")]
    public bool loopAnimations = true;
    
    [Header("Output Settings")]
    [Tooltip("Output folder for the generated animations")]
    public string outputFolder = "GeneratedAnimations";
    
    [Tooltip("Whether to organize animations by direction")]
    public bool organizeByDirection = true;
    
    [Tooltip("Whether to generate a single animator controller with all animations")]
    public bool generateAnimatorController = true;
    
    [Header("Sprite Slicing")]
    [Tooltip("Use fixed cell size for slicing (256x256 recommended)")]
    public bool useFixedCellSize = true;
    
    [Tooltip("Cell size for sprite slicing")]
    public Vector2 cellSize = new Vector2(256, 256);

    // Structure to hold metadata from JSON files
    [System.Serializable]
    private class AnimationMetadata
    {
        public string animationName;
        public string direction;
        public int frameCount;
        public int frameWidth;
        public int frameHeight;
        public int columns;
        public int rows;
        public int framesPerSecond;
    }

    [ContextMenu("Generate Animations From Sprite Sheets")]
    public void GenerateAnimations()
    {
        // Ensure the output directory exists
        string outputPath = Path.Combine("Assets", outputFolder);
        if (!Directory.Exists(outputPath))
        {
            Directory.CreateDirectory(outputPath);
        }

        // Get all sprite sheet files
        string spriteSheetPath = Path.Combine("Assets", spriteSheetsFolder);
        string[] animationFolders = Directory.GetDirectories(spriteSheetPath);

        // Dictionary to keep track of animations for animator controller
        Dictionary<string, Dictionary<string, AnimationClip>> allAnimations = 
            new Dictionary<string, Dictionary<string, AnimationClip>>();

        foreach (string animFolder in animationFolders)
        {
            string animationName = Path.GetFileName(animFolder);
            Debug.Log($"Processing animation: {animationName}");

            // Get all PNG files in this animation folder
            string[] spriteSheetFiles = Directory.GetFiles(animFolder, "*.png");
            
            // Dictionary to store direction-specific animation clips
            Dictionary<string, AnimationClip> directionClips = new Dictionary<string, AnimationClip>();

            foreach (string spriteSheetFile in spriteSheetFiles)
            {
                // Extract direction from filename
                string fileName = Path.GetFileNameWithoutExtension(spriteSheetFile);
                string[] parts = fileName.Split('_');
                
                // Default direction if none found
                string direction = "default";
                
                // Extract direction from the sprite sheet filename
                // Format expected: animation_AnimName_direction.png
                if (parts.Length >= 3)
                {
                    direction = parts[parts.Length - 1];
                }
                
                // Look for metadata JSON file
                string metadataFile = Path.Combine(animFolder, $"{Path.GetFileNameWithoutExtension(spriteSheetFile)}_metadata.json");
                AnimationMetadata metadata = null;
                
                if (File.Exists(metadataFile))
                {
                    string json = File.ReadAllText(metadataFile);
                    metadata = JsonUtility.FromJson<AnimationMetadata>(json);
                    Debug.Log($"Found metadata for {fileName}: {metadata.frameCount} frames, {metadata.columns}x{metadata.rows}");
                }
                
                // Process the sprite sheet
                AnimationClip clip = ProcessSpriteSheet(spriteSheetFile, animationName, direction, metadata);
                
                if (clip != null)
                {
                    directionClips[direction] = clip;
                    Debug.Log($"Created animation clip for {animationName} ({direction})");
                }
            }
            
            if (directionClips.Count > 0)
            {
                allAnimations[animationName] = directionClips;
            }
        }
        
        // Generate animator controller if requested
        if (generateAnimatorController && allAnimations.Count > 0)
        {
            CreateAnimatorController(allAnimations, outputPath);
        }
        
        Debug.Log("Animation generation complete!");
        AssetDatabase.Refresh();
    }
    
    private AnimationClip ProcessSpriteSheet(string spriteSheetPath, string animationName, string direction, AnimationMetadata metadata)
    {
        // Create relative path for AssetDatabase
        string assetPath = spriteSheetPath.Replace('\\', '/');

        // Import the sprite sheet as a sprite with multiple mode
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null)
        {
            Debug.LogError($"Failed to get importer for {assetPath}");
            return null;
        }
        
        // Set sprite sheet parameters based on metadata or fixed size
        int frameWidth = useFixedCellSize ? (int)cellSize.x : (metadata != null ? metadata.frameWidth : 256);
        int frameHeight = useFixedCellSize ? (int)cellSize.y : (metadata != null ? metadata.frameHeight : 256);
        int columns = metadata != null ? metadata.columns : 8;
        int rows = metadata != null ? metadata.rows : 1;
        int frameCount = metadata != null ? metadata.frameCount : (columns * rows);
        
        // Get sprite texture to determine its dimensions
        Texture2D spriteTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        if (spriteTexture == null)
        {
            Debug.LogError($"Failed to load texture at {assetPath}");
            return null;
        }
        
        int textureHeight = spriteTexture.height;
        int textureWidth = spriteTexture.width;
        
        // Calculate actual columns and rows based on the texture dimensions if metadata not available
        if (metadata == null)
        {
            columns = Mathf.FloorToInt(textureWidth / frameWidth);
            rows = Mathf.FloorToInt(textureHeight / frameHeight);
            frameCount = columns * rows;
        }
        
        // Use the Unity's "Grid By Cell Size" slicing method
        Debug.Log($"Setting up Grid by Cell Size slicing for {assetPath}: Cell size {frameWidth}x{frameHeight}");
        
        // Configure the texture importer for sprite sheet with grid slicing
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = 100;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Bilinear;
        importer.alphaIsTransparency = true;
        importer.isReadable = true;
        
        // Apply settings before slicing
        EditorUtility.SetDirty(importer);
        importer.SaveAndReimport();
        
        // After reimport, slice the texture using Grid by Cell Size method
        string sliceMethod = "Grid";
        int sliceAlignment = (int)SpriteAlignment.Center;
        Vector2 customOffset = Vector2.zero;
        
        // Call the sprite editor window's automatic slicing method
        object[] args = new object[] 
        { 
            textureWidth, 
            textureHeight, 
            frameWidth, 
            frameHeight, 
            0, // padding
            0, // offset X
            0, // offset Y
            sliceMethod, 
            sliceAlignment, 
            customOffset.x, 
            customOffset.y, 
            true, // cleanup
            true  // create
        };
        
        try
        {
            // This uses reflection to access Unity's internal sprite editor methods
            System.Type spriteUtilityType = System.Type.GetType("UnityEditor.SpriteUtility, UnityEditor");
            if (spriteUtilityType != null)
            {
                System.Reflection.MethodInfo method = spriteUtilityType.GetMethod("GenerateGridSpriteRectangles", 
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                
                if (method != null)
                {
                    // Generate the sprite rectangles
                    object result = method.Invoke(null, new object[] { textureWidth, textureHeight, frameWidth, frameHeight, 0, 0, 0 });
                    
                    if (result is Rect[])
                    {
                        Rect[] rects = (Rect[])result;
                        SpriteMetaData[] spriteSheet = new SpriteMetaData[rects.Length];
                        
                        for (int i = 0; i < rects.Length; i++)
                        {
                            spriteSheet[i] = new SpriteMetaData
                            {
                                name = $"{Path.GetFileNameWithoutExtension(assetPath)}_{i}",
                                rect = rects[i],
                                alignment = 9, // Center
                                pivot = new Vector2(0.5f, 0.5f)
                            };
                            Debug.Log($"Grid slicing: Added frame {i} at {rects[i].x},{rects[i].y} size {rects[i].width}x{rects[i].height}");
                        }
                        
                        importer.spritesheet = spriteSheet;
                    }
                    else
                    {
                        Debug.LogError("Failed to generate grid sprite rectangles");
                        // Fallback to manual slicing
                        CreateManualSlices(importer, textureWidth, textureHeight, frameWidth, frameHeight, columns, rows, assetPath);
                    }
                }
                else
                {
                    Debug.LogWarning("Could not find GenerateGridSpriteRectangles method. Falling back to manual slicing.");
                    // Fallback to manual slicing
                    CreateManualSlices(importer, textureWidth, textureHeight, frameWidth, frameHeight, columns, rows, assetPath);
                }
            }
            else
            {
                Debug.LogWarning("Could not find SpriteUtility type. Falling back to manual slicing.");
                // Fallback to manual slicing
                CreateManualSlices(importer, textureWidth, textureHeight, frameWidth, frameHeight, columns, rows, assetPath);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error during grid slicing: {e.Message}. Falling back to manual slicing.");
            // Fallback to manual slicing
            CreateManualSlices(importer, textureWidth, textureHeight, frameWidth, frameHeight, columns, rows, assetPath);
        }
        
        // Apply the changes to the importer
        EditorUtility.SetDirty(importer);
        importer.SaveAndReimport();
        
        // Get the sliced sprites
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
        List<Sprite> sprites = new List<Sprite>();
        
        foreach (Object asset in assets)
        {
            if (asset is Sprite sprite)
            {
                sprites.Add(sprite);
            }
        }
        
        // Sort sprites by frame number
        sprites = sprites.OrderBy(s => {
            string[] parts = s.name.Split('_');
            if (parts.Length > 0 && int.TryParse(parts[parts.Length - 1], out int frameNum))
                return frameNum;
            return 0;
        }).ToList();
        
        if (sprites.Count == 0)
        {
            Debug.LogError($"No sprites found in {assetPath} after slicing");
            return null;
        }
        
        Debug.Log($"Successfully sliced {sprites.Count} sprites from {assetPath}");
        
        // Create consolidated filename with animation name and direction
        string clipName = $"{animationBaseName}{animationName}_{direction}";
        string outputPath = Path.Combine("Assets", outputFolder);
        
        // Always put all animations in a single folder
        string clipPath = Path.Combine(outputPath, $"{clipName}.anim");
        clipPath = clipPath.Replace('\\', '/');
        
        // Create or get existing animation clip
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
        if (clip == null)
        {
            clip = new AnimationClip();
            clip.name = clipName;
            AssetDatabase.CreateAsset(clip, clipPath);
        }
        
        // Set up the animation clip
        EditorCurveBinding spriteBinding = new EditorCurveBinding();
        spriteBinding.type = typeof(SpriteRenderer);
        spriteBinding.path = "";
        spriteBinding.propertyName = "m_Sprite";
        
        // Create the keyframes
        ObjectReferenceKeyframe[] spriteKeyFrames = new ObjectReferenceKeyframe[sprites.Count];
        
        // Calculate time between frames
        float frameDuration = 1.0f / (metadata != null ? metadata.framesPerSecond : framesPerSecond);
        
        for (int i = 0; i < sprites.Count; i++)
        {
            spriteKeyFrames[i] = new ObjectReferenceKeyframe();
            spriteKeyFrames[i].time = i * frameDuration;
            spriteKeyFrames[i].value = sprites[i];
        }
        
        // Apply the keyframes to the animation
        AnimationUtility.SetObjectReferenceCurve(clip, spriteBinding, spriteKeyFrames);
        
        // Make the animation loop if needed
        var clipSettings = AnimationUtility.GetAnimationClipSettings(clip);
        clipSettings.loopTime = loopAnimations;
        AnimationUtility.SetAnimationClipSettings(clip, clipSettings);
        
        // Save the changes
        EditorUtility.SetDirty(clip);
        AssetDatabase.SaveAssets();
        
        return clip;
    }
    
    // Helper method for manual slicing as a fallback
    private void CreateManualSlices(TextureImporter importer, int textureWidth, int textureHeight, 
                                  int frameWidth, int frameHeight, int columns, int rows, string assetPath)
    {
        List<SpriteMetaData> spriteSheetData = new List<SpriteMetaData>();
        int frameCount = columns * rows;
        
        Debug.Log($"Manual slicing: {textureWidth}x{textureHeight}, Grid: {columns}x{rows}, Frame: {frameWidth}x{frameHeight}");
        
        for (int i = 0; i < frameCount; i++)
        {
            int row = i / columns;
            int col = i % columns;
            
            // Calculate correct Y position (flipped in Unity)
            int x = col * frameWidth;
            int y = textureHeight - ((row + 1) * frameHeight);
            
            // Ensure we don't go outside texture boundaries
            if (x + frameWidth > textureWidth || y < 0)
            {
                Debug.LogWarning($"Frame {i} would exceed texture bounds ({x}+{frameWidth}, {y}). Skipping.");
                continue;
            }
            
            SpriteMetaData smd = new SpriteMetaData
            {
                name = $"{Path.GetFileNameWithoutExtension(assetPath)}_{i}",
                rect = new Rect(x, y, frameWidth, frameHeight),
                alignment = 9, // Center
                pivot = new Vector2(0.5f, 0.5f)
            };
            
            spriteSheetData.Add(smd);
            Debug.Log($"Manual slicing: Added frame {i} at {x},{y}");
        }
        
        importer.spritesheet = spriteSheetData.ToArray();
    }
    
    private void CreateAnimatorController(Dictionary<string, Dictionary<string, AnimationClip>> allAnimations, string outputPath)
    {
        // Create animator controller
        string controllerPath = Path.Combine(outputPath, "SpriteAnimationController.controller");
        controllerPath = controllerPath.Replace('\\', '/');
        
        AnimatorController animatorController = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        
        // Get the root state machine
        AnimatorStateMachine rootStateMachine = animatorController.layers[0].stateMachine;
        
        // Start position for states
        Vector3 statePosition = new Vector3(300, 0, 0);
        float yOffset = 50;
        
        // Create parameters for direction
        animatorController.AddParameter("Direction", AnimatorControllerParameterType.Int);
        
        // Create sub-state machines for each animation type
        foreach (var animPair in allAnimations)
        {
            string animationName = animPair.Key;
            Dictionary<string, AnimationClip> directionClips = animPair.Value;
            
            // Create a sub-state machine for this animation
            AnimatorStateMachine subStateMachine = rootStateMachine.AddStateMachine(animationName, statePosition);
            statePosition.y += yOffset * 2;
            
            // Add animations for each direction
            Vector3 subStatePosition = new Vector3(300, 0, 0);
            
            // Associate directions with parameter values
            Dictionary<string, int> directionValues = new Dictionary<string, int>
            {
                { "down", 0 },
                { "downleft", 1 },
                { "left", 2 },
                { "upleft", 3 },
                { "up", 4 },
                { "upright", 5 },
                { "right", 6 },
                { "downright", 7 },
                { "default", 0 }
            };
            
            // Create transitions between animation states
            AnimatorState defaultState = null;
            
            foreach (var directionPair in directionClips)
            {
                string direction = directionPair.Key.ToLower();
                AnimationClip clip = directionPair.Value;
                
                AnimatorState state = subStateMachine.AddState(direction, subStatePosition);
                state.motion = clip;
                
                // Save the first state as default (or use "down" if available)
                if (defaultState == null || direction == "down")
                {
                    defaultState = state;
                }
                
                // Add transitions based on Direction parameter
                if (directionValues.ContainsKey(direction))
                {
                    int paramValue = directionValues[direction];
                    
                    // Create condition for this state
                    AnimatorStateTransition anyTransition = subStateMachine.AddAnyStateTransition(state);
                    anyTransition.AddCondition(AnimatorConditionMode.Equals, paramValue, "Direction");
                    anyTransition.hasExitTime = false;
                    anyTransition.duration = 0.1f; // Quick transition
                    anyTransition.canTransitionToSelf = false;
                }
                
                // Move position for next state
                subStatePosition.y += yOffset;
            }
            
            // Set default state
            if (defaultState != null)
            {
                subStateMachine.defaultState = defaultState;
            }
        }
        
        EditorUtility.SetDirty(animatorController);
        AssetDatabase.SaveAssets();
        Debug.Log($"Created animator controller at {controllerPath}");
    }

    [ContextMenu("Debug Sprite Sheet Grid")]
    public void DebugSpriteSheetGrid()
    {
        string[] spriteSheetPaths = EditorUtility.OpenFilePanel("Select Sprite Sheet", 
            Path.Combine(Application.dataPath, spriteSheetsFolder), "png").Split('\n');
        
        if (spriteSheetPaths == null || spriteSheetPaths.Length == 0 || string.IsNullOrEmpty(spriteSheetPaths[0]))
            return;
            
        string spriteSheetPath = spriteSheetPaths[0];
        if (!spriteSheetPath.StartsWith(Application.dataPath))
        {
            Debug.LogError("Selected file must be inside the Assets folder");
            return;
        }
        
        // Convert to relative path for AssetDatabase
        string assetPath = "Assets" + spriteSheetPath.Substring(Application.dataPath.Length);
        assetPath = assetPath.Replace('\\', '/');
        
        Texture2D spriteTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        if (spriteTexture == null)
        {
            Debug.LogError($"Failed to load texture at {assetPath}");
            return;
        }
        
        // Get existing sprite metadata if any
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null)
        {
            Debug.LogError($"Failed to get importer for {assetPath}");
            return;
        }
        
        // Look for metadata JSON file
        string fileName = Path.GetFileNameWithoutExtension(assetPath);
        string folderPath = Path.GetDirectoryName(assetPath);
        string metadataFile = Path.Combine(folderPath, $"{fileName}_metadata.json");
        metadataFile = metadataFile.Replace('\\', '/');
        
        AnimationMetadata metadata = null;
        if (File.Exists(metadataFile))
        {
            string json = File.ReadAllText(metadataFile);
            metadata = JsonUtility.FromJson<AnimationMetadata>(json);
            Debug.Log($"Found metadata for {fileName}: {metadata.frameCount} frames, {metadata.columns}x{metadata.rows}");
        }
        
        // Set sprite sheet parameters based on metadata or fixed size
        int frameWidth = useFixedCellSize ? (int)cellSize.x : (metadata != null ? metadata.frameWidth : 256);
        int frameHeight = useFixedCellSize ? (int)cellSize.y : (metadata != null ? metadata.frameHeight : 256);
        int columns = metadata != null ? metadata.columns : spriteTexture.width / frameWidth;
        int rows = metadata != null ? metadata.rows : spriteTexture.height / frameHeight;
        int frameCount = metadata != null ? metadata.frameCount : (columns * rows);
        
        // Create debug preview window
        EditorWindow window = EditorWindow.GetWindow(typeof(SpriteGridDebugWindow));
        SpriteGridDebugWindow debugWindow = window as SpriteGridDebugWindow;
        debugWindow.Initialize(spriteTexture, frameWidth, frameHeight, columns, rows);
        window.titleContent = new GUIContent("Sprite Grid Debug");
        window.Show();
        
        Debug.Log($"Opened debug window for {assetPath}: Grid {columns}x{rows}, Cell size {frameWidth}x{frameHeight}, Total frames: {frameCount}");
    }
    
    // Debug window class for visualizing sprite sheet grid
    private class SpriteGridDebugWindow : EditorWindow
    {
        private Texture2D texture;
        private int cellWidth;
        private int cellHeight;
        private int columns;
        private int rows;
        private Vector2 scrollPosition;
        private float zoom = 1.0f;
        
        public void Initialize(Texture2D texture, int cellWidth, int cellHeight, int columns, int rows)
        {
            this.texture = texture;
            this.cellWidth = cellWidth;
            this.cellHeight = cellHeight;
            this.columns = columns;
            this.rows = rows;
        }
        
        void OnGUI()
        {
            if (texture == null)
                return;
                
            // Add zoom control
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Zoom:", GUILayout.Width(50));
            zoom = EditorGUILayout.Slider(zoom, 0.1f, 3.0f);
            EditorGUILayout.EndHorizontal();
            
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            
            float scaledWidth = texture.width * zoom;
            float scaledHeight = texture.height * zoom;
            
            // Draw the texture
            Rect textureRect = GUILayoutUtility.GetRect(scaledWidth, scaledHeight);
            EditorGUI.DrawPreviewTexture(textureRect, texture);
            
            // Draw grid lines
            Handles.color = Color.red;
            
            // Draw vertical lines
            for (int x = 0; x <= columns; x++)
            {
                float xPos = textureRect.x + (x * cellWidth * zoom);
                Handles.DrawLine(new Vector3(xPos, textureRect.y, 0), 
                                new Vector3(xPos, textureRect.y + scaledHeight, 0));
            }
            
            // Draw horizontal lines
            for (int y = 0; y <= rows; y++)
            {
                float yPos = textureRect.y + (y * cellHeight * zoom);
                Handles.DrawLine(new Vector3(textureRect.x, yPos, 0), 
                                new Vector3(textureRect.x + scaledWidth, yPos, 0));
            }
            
            EditorGUILayout.EndScrollView();
            
            // Display info
            EditorGUILayout.LabelField($"Texture size: {texture.width} x {texture.height}");
            EditorGUILayout.LabelField($"Grid: {columns} x {rows} cells");
            EditorGUILayout.LabelField($"Cell size: {cellWidth} x {cellHeight}");
            
            Repaint();
        }
    }
}

// Helper class for animation clip settings
public class AnimationClipSettings
{
    public bool loopTime;
    public bool loopBlend;
    public bool loopBlendOrientation;
    public bool loopBlendPositionY;
    public bool loopBlendPositionXZ;
    public bool keepOriginalOrientation;
    public bool keepOriginalPositionY;
    public bool keepOriginalPositionXZ;
    public bool heightFromFeet;
    public bool mirror;
}
#endif