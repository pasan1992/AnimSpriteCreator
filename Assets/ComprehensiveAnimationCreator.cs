using UnityEngine;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Animations;
#endif

public class ComprehensiveAnimationCreator : MonoBehaviour
{
    [Header("Animation Settings")]
    [Tooltip("Reference to the Animator component")]
    public Animator targetAnimator;
    
    [Tooltip("Frames to capture per second")]
    public int captureFrameRate = 24;
    
    [Tooltip("Camera to use for capturing frames")]
    public Camera captureCamera;
    
    [Header("Camera Settings")]
    [Tooltip("Distance from camera to character (lower values = closer view)")]
    [Range(0.5f, 10f)]
    public float cameraDistance = 3f;
    
    [Tooltip("Vertical offset for camera target (to frame character properly)")]
    [Range(-3f, 3f)]
    public float verticalOffset = 0f;
    
    [Tooltip("Automatically frame character within view (adjusts camera position)")]
    public bool autoFrameCharacter = true;
    
    [Tooltip("Character bounds expansion factor (for auto-framing)")]
    [Range(1.0f, 2.0f)]
    public float boundsExpansion = 1.2f;
    
    [Header("Direction Settings")]
    [Tooltip("Whether to capture animation from multiple directions")]
    public bool captureMultipleDirections = true;
    
    [Tooltip("Game object to rotate for different directions")]
    public GameObject characterRoot;
    
    [Header("Capture Settings")]
    [Tooltip("Base directory for output files (sprites and animations)")]
    public string outputBaseDirectory = "GeneratedAnimations";
    
    [Tooltip("Base name for the captured sprite sheets and animations")]
    public string outputFileBaseName = "animation_";
    
    [Tooltip("Image format for the captured sprite sheets")]
    public CaptureFormat imageFormat = CaptureFormat.PNG;
    
    [Tooltip("Width of each frame in the sprite sheet")]
    public int frameWidth = 256;
    
    [Tooltip("Height of each frame in the sprite sheet")]
    public int frameHeight = 256;
    
    [Tooltip("Maximum number of frames per row in the sprite sheet")]
    public int maxFramesPerRow = 8;
    
    [Tooltip("Background color for the captured frames")]
    public Color backgroundColor = Color.clear;
    
    [Header("High Resolution Settings")]
    [Tooltip("Enable higher resolution capture (will be downscaled to frame size)")]
    public bool enableHighResCapture = false;
    
    [Tooltip("Capture resolution multiplier (higher = better quality but slower)")]
    [Range(1, 4)]
    public int resolutionMultiplier = 2;
    
    [Header("Animation Settings")]
    [Tooltip("Whether animations should loop")]
    public bool loopAnimations = true;
    
    [Tooltip("Whether to generate a single animator controller with all animations")]
    public bool generateAnimatorController = true;
    
    // Private variables
    private RenderTexture renderTexture;
    private RenderTexture highResRenderTexture;
    private bool isCapturing = false;
    private string fullOutputPath;
    private string spritesOutputPath;
    private string animationsOutputPath;
    private HashSet<string> processedAnimations = new HashSet<string>();
    private Vector3 originalCameraPosition;
    private Quaternion originalCameraRotation;
    private float originalCameraSize;
    
    // Direction-related variables
    private enum Direction { Down, DownLeft, Left, UpLeft, Up, UpRight, Right, DownRight }
    private readonly Dictionary<Direction, float> directionAngles = new Dictionary<Direction, float>
    {
        { Direction.Down, 0f },
        { Direction.DownLeft, -45f },
        { Direction.Left, -90f },
        { Direction.UpLeft, -135f },
        { Direction.Up, 180f }, // Changed to 180 for better up view
        { Direction.UpRight, -225f },
        { Direction.Right, 90f },
        { Direction.DownRight, 45f }
    };
    
    public enum CaptureFormat
    {
        PNG,
        JPG
    }
    
    // Structure to hold animation frames metadata
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
    
    void Start()
    {
        // Initialize if needed
        if (captureCamera == null)
        {
            captureCamera = Camera.main;
        }
        
        if (characterRoot == null && targetAnimator != null)
        {
            characterRoot = targetAnimator.gameObject;
        }
        
        // Save original camera properties
        if (captureCamera != null)
        {
            originalCameraPosition = captureCamera.transform.position;
            originalCameraRotation = captureCamera.transform.rotation;
            if (captureCamera.orthographic)
            {
                originalCameraSize = captureCamera.orthographicSize;
            }
        }
        
        // Create render textures
        renderTexture = new RenderTexture(frameWidth, frameHeight, 24);
        if (enableHighResCapture)
        {
            highResRenderTexture = new RenderTexture(
                frameWidth * resolutionMultiplier, 
                frameHeight * resolutionMultiplier, 24);
        }
        
        // Create output directories
        SetupDirectories();
    }
    
    private void SetupDirectories()
    {
        // Create base output directory
        fullOutputPath = Path.Combine(Application.dataPath, outputBaseDirectory);
        if (!Directory.Exists(fullOutputPath))
        {
            Directory.CreateDirectory(fullOutputPath);
        }
        
        // Create sprites and animations subdirectories
        spritesOutputPath = Path.Combine(fullOutputPath, "Sprites");
        if (!Directory.Exists(spritesOutputPath))
        {
            Directory.CreateDirectory(spritesOutputPath);
        }
        
        animationsOutputPath = Path.Combine(fullOutputPath, "Animations");
        if (!Directory.Exists(animationsOutputPath))
        {
            Directory.CreateDirectory(animationsOutputPath);
        }
    }
    
    public void StartCapture()
    {
        if (targetAnimator == null)
        {
            Debug.LogError("No target animator assigned!");
            return;
        }
        
        if (isCapturing)
        {
            Debug.LogWarning("Already capturing animation!");
            return;
        }
        
        // Create output directories if they don't exist
        SetupDirectories();
        
        // Start the automatic capture process
        StartCoroutine(CaptureAllAnimations());
    }
    
    private IEnumerator CaptureAllAnimations()
    {
        isCapturing = true;
        
        // Get all animation clips from the controller
        AnimationClip[] clips = targetAnimator.runtimeAnimatorController.animationClips;
        Debug.Log($"Found {clips.Length} animation clips");
        
        // Dictionary to store all created animations for animator controller
        Dictionary<string, Dictionary<string, AnimationClip>> allAnimations = 
            new Dictionary<string, Dictionary<string, AnimationClip>>();
        
        // Keep track of the original rotation
        Quaternion originalRotation = characterRoot != null ? characterRoot.transform.rotation : Quaternion.identity;
        
        // For each clip
        foreach (AnimationClip clip in clips)
        {
            string animName = clip.name;
            
            // Skip if we've already processed this animation
            if (processedAnimations.Contains(animName))
            {
                Debug.Log($"Skipping already processed animation: {animName}");
                continue;
            }
            
            Debug.Log($"Processing animation: {animName}");
            processedAnimations.Add(animName);
            
            // Create the state name directly in the Base Layer.StateName format
            string stateName = "Base Layer." + animName;
            Debug.Log($"Using state name: {stateName}");
            
            // Dictionary to store direction-specific animation clips
            Dictionary<string, AnimationClip> directionClips = new Dictionary<string, AnimationClip>();
            
            if (captureMultipleDirections && characterRoot != null)
            {
                // Capture the animation from multiple directions
                foreach (var directionPair in directionAngles)
                {
                    Direction direction = directionPair.Key;
                    float yRotation = directionPair.Value;
                    
                    // Set the character rotation for this direction
                    characterRoot.transform.rotation = Quaternion.Euler(0, yRotation, 0);
                    
                    // Position camera for better framing if autoFrameCharacter is enabled
                    if (autoFrameCharacter)
                    {
                        PositionCameraForCharacter();
                    }
                    else
                    {
                        // Just set the camera distance
                        //AdjustCameraDistance();
                    }
                    
                    // Give time for the camera to update
                    yield return new WaitForEndOfFrame();
                    
                    // Create animation folder for this specific animation
                    string animSpriteFolder = Path.Combine(spritesOutputPath, animName);
                    if (!Directory.Exists(animSpriteFolder))
                    {
                        Directory.CreateDirectory(animSpriteFolder);
                    }
                    
                    // Capture all frames for this animation and direction into a single sprite sheet
                    string dirName = direction.ToString().ToLower();
                    string spriteSheetPath = null;
                    
                    // Properly capture and handle the coroutine return
                    IEnumerator captureCoroutine = CaptureSpriteSheet(clip, stateName, animSpriteFolder, direction);
                    while (captureCoroutine.MoveNext())
                    {
                        object current = captureCoroutine.Current;
                        if (current is string path && !string.IsNullOrEmpty(path))
                        {
                            spriteSheetPath = path;
                        }
                        yield return current;
                    }
                    
                    if (!string.IsNullOrEmpty(spriteSheetPath))
                    {
                        // Process the sprite sheet to create animation
                        #if UNITY_EDITOR
                        AnimationClip animClip = ProcessSpriteSheet(spriteSheetPath, animName, dirName);
                        if (animClip != null)
                        {
                            directionClips[dirName] = animClip;
                            Debug.Log($"Created animation clip for {animName} ({dirName})");
                        }
                        #endif
                    }
                    
                    // Give time for the UI to update and showing progress
                    yield return new WaitForSeconds(0.1f);
                }
            }
            else
            {
                // Position camera for better framing if autoFrameCharacter is enabled
                if (autoFrameCharacter)
                {
                    PositionCameraForCharacter();
                }
                else
                {
                    // Just set the camera distance
                    //AdjustCameraDistance();
                }
                
                // Give time for the camera to update
                yield return new WaitForEndOfFrame();
                
                // Create animation folder for this specific animation
                string animSpriteFolder = Path.Combine(spritesOutputPath, animName);
                if (!Directory.Exists(animSpriteFolder))
                {
                    Directory.CreateDirectory(animSpriteFolder);
                }
                
                // Capture all frames for this animation into a single sprite sheet
                string spriteSheetPath = null;
                
                // Properly capture and handle the coroutine return
                IEnumerator captureCoroutine = CaptureSpriteSheet(clip, stateName, animSpriteFolder, null);
                while (captureCoroutine.MoveNext())
                {
                    object current = captureCoroutine.Current;
                    if (current is string path && !string.IsNullOrEmpty(path))
                    {
                        spriteSheetPath = path;
                    }
                    yield return current;
                }
                
                if (!string.IsNullOrEmpty(spriteSheetPath))
                {
                    // Process the sprite sheet to create animation
                    #if UNITY_EDITOR
                    AnimationClip animClip = ProcessSpriteSheet(spriteSheetPath, animName, "default");
                    if (animClip != null)
                    {
                        directionClips["default"] = animClip;
                        Debug.Log($"Created animation clip for {animName} (default)");
                    }
                    #endif
                }
                
                // Give time for the UI to update
                yield return new WaitForSeconds(0.1f);
            }
            
            if (directionClips.Count > 0)
            {
                allAnimations[animName] = directionClips;
            }
        }
        
        // Generate animator controller if requested
        #if UNITY_EDITOR
        if (generateAnimatorController && allAnimations.Count > 0)
        {
            CreateAnimatorController(allAnimations, animationsOutputPath);
        }
        #endif
        
        // Restore original rotation
        if (characterRoot != null)
        {
            characterRoot.transform.rotation = originalRotation;
        }
        
        // Restore original camera settings
        RestoreCameraSettings();
        
        isCapturing = false;
        Debug.Log("Complete animation pipeline finished! Sprite sheets captured, sliced, and animations created.");
    }
    
    private void PositionCameraForCharacter()
    {
        if (captureCamera == null || targetAnimator == null)
            return;
            
        // Get the character's renderer bounds
        Bounds characterBounds = GetCharacterBounds();
        
        // Apply bounds expansion
        characterBounds.size *= boundsExpansion;
        
        // Calculate camera position based on character bounds and camera type
        if (captureCamera.orthographic)
        {
            // For orthographic cameras, set size based on bounds height
            captureCamera.orthographicSize = characterBounds.size.y / 2;
            
            // Position camera at a distance directly in front of character
            Vector3 targetPosition = characterBounds.center;
            targetPosition.y += verticalOffset;
            
            // Position camera at proper distance and direction
            Vector3 cameraDirection = captureCamera.transform.forward;
            captureCamera.transform.position = targetPosition - cameraDirection * cameraDistance;
        }
        else
        {
            // For perspective cameras, calculate field of view and position
            // Position camera at proper distance to frame the character
            Vector3 targetPosition = characterBounds.center;
            targetPosition.y += verticalOffset;
            
            // Position camera at proper distance and direction
            Vector3 cameraDirection = captureCamera.transform.forward;
            captureCamera.transform.position = targetPosition - cameraDirection * cameraDistance;
            
            // Adjust camera to look at the target
            captureCamera.transform.LookAt(targetPosition);
        }
    }
    
    private Bounds GetCharacterBounds()
    {
        // Get all renderers on the character
        Renderer[] renderers = targetAnimator.gameObject.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            // Fallback if no renderers found
            return new Bounds(targetAnimator.transform.position, Vector3.one);
        }
        
        // Start with the first renderer's bounds
        Bounds bounds = renderers[0].bounds;
        
        // Expand to include all other renderers
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }
        
        return bounds;
    }
    
    private void AdjustCameraDistance()
    {
        if (captureCamera == null || targetAnimator == null)
            return;
            
        // Get character position
        Vector3 characterPosition = targetAnimator.transform.position;
        characterPosition.y += verticalOffset;
        
        // Position camera at the specified distance
        Vector3 cameraDirection = captureCamera.transform.forward;
        captureCamera.transform.position = characterPosition - cameraDirection * cameraDistance;
        
        // Make camera look at character
        captureCamera.transform.LookAt(characterPosition);
    }
    
    private void RestoreCameraSettings()
    {
        if (captureCamera != null)
        {
            captureCamera.transform.position = originalCameraPosition;
            captureCamera.transform.rotation = originalCameraRotation;
            if (captureCamera.orthographic)
            {
                captureCamera.orthographicSize = originalCameraSize;
            }
        }
    }
    
    private IEnumerator CaptureSpriteSheet(AnimationClip clip, string stateName, string animFolder, Direction? direction)
    {
        // Use the provided state name
        Debug.Log($"Using state name: {stateName} for clip: {clip.name}");
        
        // Prepare the animator - use the state name
        targetAnimator.Play(stateName, 0, 0f);
        targetAnimator.speed = 0; // Pause animation
        targetAnimator.Update(0.0f);
        
        float animationLength = clip.length;
        int frameCount = Mathf.CeilToInt(animationLength * captureFrameRate);
        
        if (direction.HasValue)
        {
            Debug.Log($"Capturing {clip.name} ({direction.Value} direction): {frameCount} frames, {animationLength} seconds");
        }
        else
        {
            Debug.Log($"Capturing {clip.name}: {frameCount} frames, {animationLength} seconds");
        }
        
        // Calculate sprite sheet dimensions
        int rows = Mathf.CeilToInt((float)frameCount / maxFramesPerRow);
        int cols = Mathf.Min(frameCount, maxFramesPerRow);
        
        // Create the sprite sheet texture
        int sheetWidth = cols * frameWidth;
        int sheetHeight = rows * frameHeight;
        Texture2D spriteSheet = new Texture2D(sheetWidth, sheetHeight, TextureFormat.RGBA32, false);
        
        // Fill with transparent pixels initially
        Color[] clearColors = new Color[sheetWidth * sheetHeight];
        for (int i = 0; i < clearColors.Length; i++)
        {
            clearColors[i] = new Color(0, 0, 0, 0);
        }
        spriteSheet.SetPixels(clearColors);
        spriteSheet.Apply();
        
        // Create a temporary texture to read pixels into
        Texture2D frameTexture = new Texture2D(frameWidth, frameHeight, TextureFormat.RGBA32, false);
        
        // High resolution texture if enabled
        Texture2D highResTexture = null;
        if (enableHighResCapture)
        {
            highResTexture = new Texture2D(
                frameWidth * resolutionMultiplier, 
                frameHeight * resolutionMultiplier, 
                TextureFormat.RGBA32, false);
        }
        
        // Ensure we have the previous camera config
        CameraClearFlags previousClearFlags = captureCamera.clearFlags;
        Color previousBackgroundColor = captureCamera.backgroundColor;
        RenderTexture previousTargetTexture = captureCamera.targetTexture;
        
        // Set up the camera for capturing
        captureCamera.clearFlags = CameraClearFlags.SolidColor;
        captureCamera.backgroundColor = backgroundColor;
        
        // Set appropriate render texture based on high res setting
        if (enableHighResCapture)
        {
            captureCamera.targetTexture = highResRenderTexture;
        }
        else
        {
            captureCamera.targetTexture = renderTexture;
        }
        
        // Calculate time increment per frame
        float timeIncrement = 1.0f / captureFrameRate;
        
        // Track frame positions
        List<CaptureFramePosition> framePositions = new List<CaptureFramePosition>();
        
        // Capture each frame
        for (int i = 0; i < frameCount; i++)
        {
            // Set animation time - temporarily enable animation speed
            targetAnimator.speed = 1;
            
            // Set the normalized time for this frame
            float normalizedTime = (i * timeIncrement) / animationLength;
            
            // Use the state name for playback
            targetAnimator.Play(stateName, 0, normalizedTime);
            
            // Critical: Force the animator to evaluate the animation at this time
            targetAnimator.Update(0);
            targetAnimator.speed = 0;
            
            // Wait for animation to fully update in the rendering pipeline
            yield return new WaitForEndOfFrame();
            
            // Render the scene to our target texture
            captureCamera.Render();
            
            if (enableHighResCapture)
            {
                // For high-res capture, we first capture at high resolution then downsample
                RenderTexture.active = highResRenderTexture;
                
                // Read pixels from the high-res render texture
                highResTexture.ReadPixels(new Rect(0, 0, frameWidth * resolutionMultiplier, frameHeight * resolutionMultiplier), 0, 0);
                highResTexture.Apply();
                
                // Downsample to the target frame size (this gives better quality than direct low-res rendering)
                Graphics.ConvertTexture(highResTexture, renderTexture);
                
                // Now read from the downsampled render texture
                RenderTexture.active = renderTexture;
                frameTexture.ReadPixels(new Rect(0, 0, frameWidth, frameHeight), 0, 0);
                frameTexture.Apply();
            }
            else
            {
                // Standard resolution capture
                RenderTexture.active = renderTexture;
                
                // Read pixels from the render texture
                frameTexture.ReadPixels(new Rect(0, 0, frameWidth, frameHeight), 0, 0);
                frameTexture.Apply();
            }
            
            // Reset active render texture
            RenderTexture.active = null;
            
            // Calculate position in sprite sheet
            int row = i / maxFramesPerRow;
            int col = i % maxFramesPerRow;
            int x = col * frameWidth;
            int y = sheetHeight - ((row + 1) * frameHeight); // Y is flipped in texture coordinates
            
            // Copy frame pixels to sprite sheet
            spriteSheet.SetPixels(x, y, frameWidth, frameHeight, frameTexture.GetPixels());
            
            // Write additional metadata to help with correct slicing
            // Store frame position in the filename
            framePositions.Add(new CaptureFramePosition { 
                index = i, 
                x = x, 
                y = y, 
                width = frameWidth, 
                height = frameHeight 
            });
            
            // Report progress
            if (i % 5 == 0) // Only log every 5 frames to reduce console spam
            {
                Debug.Log($"Captured frame {i+1}/{frameCount} ({Mathf.Round(normalizedTime * 100)}%)");
            }
            
            // Small delay to ensure animation state is completely updated
            yield return null;
        }
        
        // Apply all changes to the sprite sheet
        spriteSheet.Apply();
        
        // Save the sprite sheet
        string extension = imageFormat == CaptureFormat.PNG ? ".png" : ".jpg";
        string directionSuffix = direction.HasValue ? "_" + direction.Value.ToString().ToLower() : "";
        string fileName = $"{outputFileBaseName}{clip.name}{directionSuffix}{extension}";
        string filePath = Path.Combine(animFolder, fileName);
        
        byte[] bytes;
        if (imageFormat == CaptureFormat.PNG)
        {
            bytes = spriteSheet.EncodeToPNG();
        }
        else
        {
            bytes = spriteSheet.EncodeToJPG();
        }
        
        File.WriteAllBytes(filePath, bytes);
        
        // Restore camera settings
        captureCamera.clearFlags = previousClearFlags;
        captureCamera.backgroundColor = previousBackgroundColor;
        captureCamera.targetTexture = previousTargetTexture;
        
        // Resume animation playback
        targetAnimator.speed = 1;
        
        // Clean up
        Destroy(frameTexture);
        if (highResTexture != null)
        {
            Destroy(highResTexture);
        }
        Destroy(spriteSheet);
        
        Debug.Log($"Finished capturing sprite sheet for {clip.name}. Saved to: {filePath}");
        
        // Create metadata JSON file with sprite sheet info
        WriteMetadataFile(clip.name, animFolder, direction, frameCount, frameWidth, frameHeight, cols, rows);
        
        // Return the sprite sheet path for further processing
        string assetPath = "Assets" + filePath.Substring(Application.dataPath.Length).Replace('\\', '/');
        
        yield return assetPath;
    }
    
    private void WriteMetadataFile(string animName, string folderPath, Direction? direction, int frameCount, int frameWidth, int frameHeight, int columns, int rows)
    {
        string directionSuffix = direction.HasValue ? "_" + direction.Value.ToString().ToLower() : "";
        string metadataPath = Path.Combine(folderPath, $"{outputFileBaseName}{animName}{directionSuffix}_metadata.json");
        
        // Create metadata JSON
        string json = "{\n";
        json += $"  \"animationName\": \"{animName}\",\n";
        if (direction.HasValue)
        {
            json += $"  \"direction\": \"{direction.Value}\",\n";
        }
        json += $"  \"frameCount\": {frameCount},\n";
        json += $"  \"frameWidth\": {frameWidth},\n";
        json += $"  \"frameHeight\": {frameHeight},\n";
        json += $"  \"columns\": {columns},\n";
        json += $"  \"rows\": {rows},\n";
        json += $"  \"framesPerSecond\": {captureFrameRate}\n";
        json += "}";
        
        File.WriteAllText(metadataPath, json);
        Debug.Log($"Created metadata file: {metadataPath}");
    }
    
#if UNITY_EDITOR
    // Process sprite sheet to create animations (Editor only)
    private AnimationClip ProcessSpriteSheet(string spriteSheetPath, string animationName, string direction)
    {
        // Create relative path for AssetDatabase if needed
        if (!spriteSheetPath.StartsWith("Assets"))
        {
            spriteSheetPath = "Assets" + spriteSheetPath.Substring(6).Replace('\\', '/');
        }

        Debug.Log($"Processing sprite sheet: {spriteSheetPath}");
        
        // Force AssetDatabase refresh to ensure the file is imported
        AssetDatabase.Refresh();
        
        // Get the actual file path
        string fullPath = Application.dataPath + spriteSheetPath.Substring(6);
        
        // Check if file exists
        if (!File.Exists(fullPath))
        {
            Debug.LogError($"Sprite sheet file not found at {fullPath}");
            return null;
        }
        
        // Load the sprite sheet directly from the file
        byte[] fileData = File.ReadAllBytes(fullPath);
        Texture2D spriteSheet = new Texture2D(2, 2);
        spriteSheet.LoadImage(fileData);
        
        // Get dimensions
        int textureWidth = spriteSheet.width;
        int textureHeight = spriteSheet.height;
        
        // Calculate grid
        int columns = textureWidth / frameWidth;
        int rows = textureHeight / frameHeight;
        int totalFrames = columns * rows;
        
        Debug.Log($"Loaded sprite sheet directly: {textureWidth}x{textureHeight}, Grid: {columns}x{rows}, Cell: {frameWidth}x{frameHeight}");
        
        // Create directory for individual frames
        string frameDir = Path.Combine(Application.dataPath, outputBaseDirectory, "SpriteFrames", animationName, direction);
        if (!Directory.Exists(frameDir))
        {
            Directory.CreateDirectory(frameDir);
        }
        
        // List to hold sprite assets
        List<Sprite> sprites = new List<Sprite>();
        List<string> framePaths = new List<string>();
        
        // Extract individual frames as separate files
        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < columns; x++)
            {
                int frameIndex = y * columns + x;
                
                // Calculate frame position (y is flipped in texture coordinates)
                int posX = x * frameWidth;
                int posY = textureHeight - ((y + 1) * frameHeight);
                
                // Skip frames that would go outside the texture
                if (posX >= textureWidth || posY < 0 || posX + frameWidth > textureWidth || posY + frameHeight > textureHeight)
                {
                    Debug.LogWarning($"Frame {frameIndex} would exceed texture bounds at ({posX},{posY}). Skipping.");
                    continue;
                }
                
                // Create a new texture for this frame
                Texture2D frameTexture = new Texture2D(frameWidth, frameHeight, TextureFormat.RGBA32, false);
                
                // Copy pixels from the sprite sheet
                Color[] pixels = spriteSheet.GetPixels(posX, posY, frameWidth, frameHeight);
                frameTexture.SetPixels(pixels);
                frameTexture.Apply();
                
                // Save the frame as a PNG
                string frameName = $"frame_{frameIndex:D3}.png";
                string framePath = Path.Combine(frameDir, frameName);
                File.WriteAllBytes(framePath, frameTexture.EncodeToPNG());
                
                // Record the asset path for later import
                string assetPath = "Assets" + framePath.Substring(Application.dataPath.Length).Replace('\\', '/');
                framePaths.Add(assetPath);
                
                Debug.Log($"Extracted frame {frameIndex} to {assetPath}");
                
                // Clean up
                Destroy(frameTexture);
            }
        }
        
        // Clean up the sprite sheet texture
        Destroy(spriteSheet);
        
        // Force a refresh to import all the new frame images
        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
        
        // Wait for import to complete
        System.Threading.Thread.Sleep(1000);
        
        // Configure all the frame textures as sprites
        foreach (string framePath in framePaths)
        {
            TextureImporter importer = AssetImporter.GetAtPath(framePath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 100;
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Bilinear;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }
        }
        
        // One more refresh to ensure imports are complete
        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
        System.Threading.Thread.Sleep(500);
        
        // Load all the sprite frames
        foreach (string framePath in framePaths)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(framePath);
            if (sprite != null)
            {
                sprites.Add(sprite);
                Debug.Log($"Loaded sprite from {framePath}");
            }
            else
            {
                Debug.LogWarning($"Failed to load sprite from {framePath}");
            }
        }
        
        // Check if we have sprites
        if (sprites.Count == 0)
        {
            Debug.LogError($"No sprites were extracted from {spriteSheetPath}");
            return null;
        }
        
        Debug.Log($"Successfully extracted {sprites.Count} sprite frames");
        
        // Create animation clip name with proper direction suffix
        string clipName = $"{outputFileBaseName}{animationName}_{direction}";
        
        // Generate animation clip path with proper organization
        string animClipPath = Path.Combine("Assets", outputBaseDirectory, "Animations", $"{clipName}.anim");
        animClipPath = animClipPath.Replace('\\', '/');
        
        // Create or get existing animation clip
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(animClipPath);
        if (clip == null)
        {
            clip = new AnimationClip();
            clip.name = clipName;
            AssetDatabase.CreateAsset(clip, animClipPath);
        }
        
        // Set up the animation clip
        EditorCurveBinding spriteBinding = new EditorCurveBinding();
        spriteBinding.type = typeof(SpriteRenderer);
        spriteBinding.path = "";
        spriteBinding.propertyName = "m_Sprite";
        
        // Create the keyframes
        ObjectReferenceKeyframe[] spriteKeyFrames = new ObjectReferenceKeyframe[sprites.Count];
        
        // Calculate time between frames
        float frameDuration = 1.0f / captureFrameRate;
        
        // Apply the keyframes
        for (int i = 0; i < sprites.Count; i++)
        {
            spriteKeyFrames[i] = new ObjectReferenceKeyframe();
            spriteKeyFrames[i].time = i * frameDuration;
            spriteKeyFrames[i].value = sprites[i];
            Debug.Log($"Set keyframe {i}: time={i * frameDuration}, sprite={sprites[i].name}");
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
        
        Debug.Log($"Created animation clip: {animClipPath} with {sprites.Count} frames");
        
        return clip;
    }
    
    // Create an animator controller with all animations
    private void CreateAnimatorController(Dictionary<string, Dictionary<string, AnimationClip>> allAnimations, string outputPath)
    {
        // Create animator controller path
        string assetPath = "Assets" + outputPath.Substring(Application.dataPath.Length).Replace('\\', '/');
        string controllerPath = Path.Combine(assetPath, "AnimationController.controller");
        controllerPath = controllerPath.Replace('\\', '/');
        
        // Create animator controller
        AnimatorController animatorController = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        
        // Get the root state machine
        AnimatorStateMachine rootStateMachine = animatorController.layers[0].stateMachine;
        
        // Start position for states
        Vector3 statePosition = new Vector3(300, 0, 0);
        float yOffset = 70;
        
        // Create parameters for animation and direction
        animatorController.AddParameter("Animation", AnimatorControllerParameterType.Int);
        animatorController.AddParameter("Direction", AnimatorControllerParameterType.Int);
        
        // Create sub-state machines for each animation type
        int animIndex = 0;
        foreach (var animPair in allAnimations)
        {
            string animationName = animPair.Key;
            Dictionary<string, AnimationClip> directionClips = animPair.Value;
            
            // Create a sub-state machine for this animation
            AnimatorStateMachine subStateMachine = rootStateMachine.AddStateMachine(animationName, statePosition);
            statePosition.y += yOffset * 2;
            
            // Create a transition from Any State to this sub-state machine's entry state based on Animation parameter
            var transition = rootStateMachine.AddAnyStateTransition(subStateMachine);
            transition.AddCondition(AnimatorConditionMode.Equals, animIndex, "Animation");
            transition.hasExitTime = false;
            transition.duration = 0.1f;
            
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
            
            // Create states for each direction
            AnimatorState defaultState = null;
            
            foreach (var directionPair in directionClips)
            {
                string direction = directionPair.Key.ToLower();
                AnimationClip clip = directionPair.Value;
                
                AnimatorState state = subStateMachine.AddState(direction, subStatePosition);
                state.motion = clip;
                subStatePosition.y += yOffset;
                
                // Save down or default as the default state
                if (defaultState == null || direction == "down" || direction == "default")
                {
                    defaultState = state;
                }
                
                // Add transitions based on Direction parameter
                if (directionValues.ContainsKey(direction))
                {
                    int dirValue = directionValues[direction];
                    
                    // Create transitions from any state within this sub-state machine
                    var dirTransition = subStateMachine.AddAnyStateTransition(state);
                    dirTransition.AddCondition(AnimatorConditionMode.Equals, dirValue, "Direction");
                    dirTransition.hasExitTime = false;
                    dirTransition.duration = 0.1f;
                }
            }
            
            // Set default state
            if (defaultState != null)
            {
                subStateMachine.defaultState = defaultState;
            }
            
            animIndex++;
        }
        
        // Save changes
        EditorUtility.SetDirty(animatorController);
        AssetDatabase.SaveAssets();
        
        Debug.Log($"Created animator controller: {controllerPath}");
    }
#endif
    
    // UI button to start capturing
    void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10, 10, 320, 150));
        
        if (!isCapturing)
        {
            if (GUILayout.Button("Capture & Create 2D Animation Pipeline", GUILayout.Height(30)))
            {
                StartCapture();
            }
            
            if (GUILayout.Button("Clear Processed Animations", GUILayout.Height(30)))
            {
                processedAnimations.Clear();
                Debug.Log("Cleared processed animations list");
            }
            
            if (GUILayout.Button("Open Output Folder", GUILayout.Height(30)))
            {
                if (Directory.Exists(fullOutputPath))
                {
                    System.Diagnostics.Process.Start("explorer.exe", fullOutputPath.Replace('/', '\\'));
                }
                else
                {
                    Debug.LogWarning("Output folder does not exist yet");
                }
            }
        }
        else
        {
            GUILayout.Label("Capturing and creating animations... Please wait.");
            GUILayout.Label("This process may take several minutes.");
            
            // Show a simple progress indicator
            Rect progressRect = GUILayoutUtility.GetRect(300, 20);
            GUI.Box(progressRect, "");
            GUI.color = Color.green;
            GUI.Box(new Rect(progressRect.x + 2, progressRect.y + 2, (progressRect.width - 4) * Random.Range(0.1f, 0.9f), progressRect.height - 4), "");
            GUI.color = Color.white;
        }
        
        GUILayout.EndArea();
    }
}

// Renamed to avoid naming conflict with SpriteSheetCapture.cs
public class CaptureFramePosition
{
    public int index;
    public int x;
    public int y;
    public int width;
    public int height;
}