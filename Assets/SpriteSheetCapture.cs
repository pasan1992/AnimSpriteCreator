using UnityEngine;
using System.IO;
using System.Collections;
using System.Collections.Generic;

public class SpriteSheetCapture : MonoBehaviour
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
    [Tooltip("Output folder path relative to Application.dataPath")]
    public string outputFolderName = "AnimationFrames";
    
    [Tooltip("Base name for the captured sprite sheets")]
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
    
    // Private variables
    private RenderTexture renderTexture;
    private RenderTexture highResRenderTexture;
    private bool isCapturing = false;
    private string fullOutputPath;
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
        { Direction.Up, -180f },
        { Direction.UpRight, -225f },
        { Direction.Right, 90f },
        { Direction.DownRight, 45f }
    };
    
    public enum CaptureFormat
    {
        PNG,
        JPG
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
        
        // Create output directory
        fullOutputPath = Path.Combine(Application.dataPath, outputFolderName);
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
        
        // Create output directory if it doesn't exist
        if (!Directory.Exists(fullOutputPath))
        {
            Directory.CreateDirectory(fullOutputPath);
        }
        
        // Start the automatic capture process
        StartCoroutine(CaptureAllAnimations());
    }
    
    private IEnumerator CaptureAllAnimations()
    {
        isCapturing = true;
        
        // Get all animation clips from the controller
        AnimationClip[] clips = targetAnimator.runtimeAnimatorController.animationClips;
        Debug.Log($"Found {clips.Length} animation clips");
        
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
            
            // Create animation folder if it doesn't exist
            string animFolder = Path.Combine(fullOutputPath, animName);
            if (!Directory.Exists(animFolder))
            {
                Directory.CreateDirectory(animFolder);
            }
            
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
                        AdjustCameraDistance();
                    }
                    
                    // Give time for the camera to update
                    yield return new WaitForEndOfFrame();
                    
                    // Capture all frames for this animation and direction into a single sprite sheet
                    yield return StartCoroutine(CaptureSpriteSheet(clip, stateName, animFolder, direction));
                    
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
                    AdjustCameraDistance();
                }
                
                // Give time for the camera to update
                yield return new WaitForEndOfFrame();
                
                // Capture all frames for this animation into a single sprite sheet
                yield return StartCoroutine(CaptureSpriteSheet(clip, stateName, animFolder, null));
                
                // Give time for the UI to update
                yield return new WaitForSeconds(0.1f);
            }
        }
        
        // Restore original rotation
        if (characterRoot != null)
        {
            characterRoot.transform.rotation = originalRotation;
        }
        
        // Restore original camera settings
        RestoreCameraSettings();
        
        isCapturing = false;
        Debug.Log("Animation capture process complete!");
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
        
        yield return null;
    }
    
    private void WriteMetadataFile(string animName, string folderPath, Direction? direction, int frameCount, int frameWidth, int frameHeight, int columns, int rows)
    {
        string directionSuffix = direction.HasValue ? "_" + direction.Value.ToString().ToLower() : "";
        string metadataPath = Path.Combine(folderPath, $"{animName}{directionSuffix}_metadata.json");
        
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
    
    // UI button to start capturing
    void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10, 10, 300, 100));
        
        if (!isCapturing)
        {
            if (GUILayout.Button("Capture All Animations as Sprite Sheets", GUILayout.Height(30)))
            {
                StartCapture();
            }
            
            if (GUILayout.Button("Clear Processed Animations", GUILayout.Height(30)))
            {
                processedAnimations.Clear();
                Debug.Log("Cleared processed animations list");
            }
        }
        else
        {
            GUILayout.Label("Capturing sprite sheets... Please wait.");
        }
        
        GUILayout.EndArea();
    }
}