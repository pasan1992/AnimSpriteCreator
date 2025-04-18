using UnityEngine;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class AnimationCaptureSpriteSheet : MonoBehaviour
{
    [Header("Animation Settings")]
    [Tooltip("Reference to the Animator component")]
    public Animator targetAnimator;
    
    [Tooltip("Frames to capture per second")]
    public int captureFrameRate = 24;
    
    [Tooltip("Camera to use for capturing frames")]
    public Camera captureCamera;
    
    [Header("Direction Settings")]
    [Tooltip("Whether to capture animation from multiple directions")]
    public bool captureMultipleDirections = true;
    
    [Tooltip("Game object to rotate for different directions")]
    public GameObject characterRoot;
    
    [Tooltip("Use camera setup component for directions")]
    public bool useCameraSetupComponent = true;
    
    [Tooltip("Reference to a camera setup component (IsometricCameraSetup, TopDownCameraSetup, or AngledTopDownCameraSetup)")]
    public MonoBehaviour cameraSetupComponent;
    
    [Header("Capture Settings")]
    [Tooltip("Output folder path relative to Application.dataPath")]
    public string outputFolderName = "AnimationFrames";
    
    [Tooltip("Base name for the captured frames")]
    public string outputFileBaseName = "frame_";
    
    [Tooltip("Image format for the captured frames")]
    public CaptureFormat imageFormat = CaptureFormat.PNG;
    
    [Header("Character Size Settings")]
    [Tooltip("Width of the captured frames")]
    public int captureWidth = 256;
    
    [Tooltip("Height of the captured frames")]
    public int captureHeight = 256;
    
    [Tooltip("Scale multiplier for the character (increases apparent size)")]
    [Range(0.1f, 10f)]
    public float characterSizeMultiplier = 1.0f;
    
    [Tooltip("Position the camera closer to make character appear larger")]
    [Range(0.1f, 10f)]
    public float cameraDistanceMultiplier = 1.0f;
    
    [Tooltip("Background color for the captured frames")]
    public Color backgroundColor = Color.clear;
    
    [Header("Sprite Sheet Settings")]
    [Tooltip("Maximum number of frames in a row for the sprite sheet")]
    public int maxFramesPerRow = 8;
    
    [Tooltip("Padding between frames in the sprite sheet")]
    public int framePadding = 2;
    
    [Tooltip("Whether to save individual frames as well as the sprite sheet")]
    public bool saveIndividualFrames = false;
    
    // Private variables
    private RenderTexture renderTexture;
    private bool isCapturing = false;
    private string fullOutputPath;
    private HashSet<string> processedAnimations = new HashSet<string>();
    
    // Direction-related variables
    private enum Direction { Down, Left, Up, Right }
    private readonly Dictionary<Direction, float> directionAngles = new Dictionary<Direction, float>
    {
        { Direction.Down, 0f },
        { Direction.Left, -90f },
        { Direction.Up, -180f },
        { Direction.Right, -270f }
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
        
        // Create render texture
        renderTexture = new RenderTexture(captureWidth, captureHeight, 24);
        
        // Create output directory
        fullOutputPath = Path.Combine(Application.dataPath, outputFolderName);
        
        // If using a camera setup component, validate it's one of the expected types
        ValidateCameraSetupComponent();
    }
    
    private void ValidateCameraSetupComponent()
    {
        if (useCameraSetupComponent && cameraSetupComponent != null)
        {
            string componentType = cameraSetupComponent.GetType().Name;
            if (componentType != "IsometricCameraSetup" && 
                componentType != "TopDownCameraSetup" && 
                componentType != "AngledTopDownCameraSetup")
            {
                Debug.LogWarning("The provided camera setup component is not one of the expected types: IsometricCameraSetup, TopDownCameraSetup, or AngledTopDownCameraSetup");
                useCameraSetupComponent = false;
            }
            else
            {
                Debug.Log($"Using {componentType} for camera setup during capture");
            }
        }
        else if (useCameraSetupComponent && cameraSetupComponent == null)
        {
            Debug.LogWarning("Camera setup component is null but useCameraSetupComponent is true. Disabling useCameraSetupComponent.");
            useCameraSetupComponent = false;
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
        
        // Keep track of the original values to restore later
        Quaternion originalRotation = characterRoot != null ? characterRoot.transform.rotation : Quaternion.identity;
        Vector3 originalScale = characterRoot != null ? characterRoot.transform.localScale : Vector3.one;
        float originalDistance = 0f;
        float originalOrthographicSize = 0f;
        
        // Store original camera settings if using a camera setup
        if (useCameraSetupComponent && cameraSetupComponent != null)
        {
            // Get original distance and size based on camera setup type
            string componentType = cameraSetupComponent.GetType().Name;
            
            if (componentType == "IsometricCameraSetup")
            {
                var setup = cameraSetupComponent as IsometricCameraSetup;
                originalDistance = setup.distance;
                originalOrthographicSize = setup.orthographicSize;
                
                // Apply size multipliers
                setup.distance = originalDistance / cameraDistanceMultiplier;
                setup.orthographicSize = originalOrthographicSize / characterSizeMultiplier;
            }
            else if (componentType == "TopDownCameraSetup")
            {
                var setup = cameraSetupComponent as TopDownCameraSetup;
                originalDistance = setup.cameraHeight;
                originalOrthographicSize = setup.orthographicSize;
                
                // Apply size multipliers
                setup.cameraHeight = originalDistance / cameraDistanceMultiplier;
                setup.orthographicSize = originalOrthographicSize / characterSizeMultiplier;
            }
            else if (componentType == "AngledTopDownCameraSetup")
            {
                var setup = cameraSetupComponent as AngledTopDownCameraSetup;
                originalDistance = setup.cameraDistance;
                originalOrthographicSize = setup.orthographicSize;
                
                // Apply size multipliers
                setup.cameraDistance = originalDistance / cameraDistanceMultiplier;
                setup.orthographicSize = originalOrthographicSize / characterSizeMultiplier;
            }
        }
        
        // Apply scale multiplier directly to character if needed
        if (characterRoot != null && !useCameraSetupComponent)
        {
            characterRoot.transform.localScale = originalScale * characterSizeMultiplier;
        }
        
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
            
            if (captureMultipleDirections && characterRoot != null)
            {
                // Capture the animation from multiple directions
                foreach (var directionPair in directionAngles)
                {
                    Direction direction = directionPair.Key;
                    float yRotation = directionPair.Value;
                    
                    // Set the character rotation - either using camera setup or direct rotation
                    if (useCameraSetupComponent && cameraSetupComponent != null)
                    {
                        RotateCharacterUsingCameraSetup(direction);
                    }
                    else
                    {
                        // Set the character rotation for this direction directly
                        characterRoot.transform.rotation = Quaternion.Euler(0, yRotation, 0);
                    }
                    
                    // Create subfolder for this animation and direction
                    string directionSubfolder = Path.Combine(fullOutputPath, animName, direction.ToString());
                    if (!Directory.Exists(directionSubfolder))
                    {
                        Directory.CreateDirectory(directionSubfolder);
                    }
                    else if (saveIndividualFrames)
                    {
                        // Clean previous frames only if we're saving individual frames
                        string[] existingFiles = Directory.GetFiles(directionSubfolder);
                        foreach (string file in existingFiles)
                        {
                            if (file.EndsWith(".png") || file.EndsWith(".jpg"))
                            {
                                File.Delete(file);
                            }
                        }
                    }
                    
                    // Capture all frames for this animation and direction
                    yield return StartCoroutine(CaptureAnimationFramesAsSpriteSheet(clip, directionSubfolder));
                    
                    // Give time for the UI to update and showing progress
                    yield return new WaitForSeconds(0.1f);
                }
            }
            else
            {
                // Create subfolder for this animation only
                string animSubfolder = Path.Combine(fullOutputPath, animName);
                if (!Directory.Exists(animSubfolder))
                {
                    Directory.CreateDirectory(animSubfolder);
                }
                else if (saveIndividualFrames)
                {
                    // Clean previous frames only if we're saving individual frames
                    string[] existingFiles = Directory.GetFiles(animSubfolder);
                    foreach (string file in existingFiles)
                    {
                        if (file.EndsWith(".png") || file.EndsWith(".jpg"))
                        {
                            File.Delete(file);
                        }
                    }
                }
                
                // Capture all frames for this animation
                yield return StartCoroutine(CaptureAnimationFramesAsSpriteSheet(clip, animSubfolder));
                
                // Give time for the UI to update
                yield return new WaitForSeconds(0.1f);
            }
        }
        
        // Restore original values
        if (characterRoot != null)
        {
            characterRoot.transform.rotation = originalRotation;
            
            // Only restore scale if not using camera setup
            if (!useCameraSetupComponent)
            {
                characterRoot.transform.localScale = originalScale;
            }
        }
        
        // Restore camera setup values
        if (useCameraSetupComponent && cameraSetupComponent != null)
        {
            string componentType = cameraSetupComponent.GetType().Name;
            
            if (componentType == "IsometricCameraSetup")
            {
                var setup = cameraSetupComponent as IsometricCameraSetup;
                setup.distance = originalDistance;
                setup.orthographicSize = originalOrthographicSize;
                setup.UpdateCameraSettings();
            }
            else if (componentType == "TopDownCameraSetup")
            {
                var setup = cameraSetupComponent as TopDownCameraSetup;
                setup.cameraHeight = originalDistance;
                setup.orthographicSize = originalOrthographicSize;
                setup.UpdateCameraSettings();
            }
            else if (componentType == "AngledTopDownCameraSetup")
            {
                var setup = cameraSetupComponent as AngledTopDownCameraSetup;
                setup.cameraDistance = originalDistance;
                setup.orthographicSize = originalOrthographicSize;
                setup.UpdateCameraSettings();
            }
        }
        
        isCapturing = false;
        Debug.Log("Animation capture process complete!");
    }
    
    private void RotateCharacterUsingCameraSetup(Direction direction)
    {
        if (cameraSetupComponent == null || characterRoot == null)
            return;
            
        string componentType = cameraSetupComponent.GetType().Name;
        
        // Handle each camera setup type
        if (componentType == "AngledTopDownCameraSetup")
        {
            var cameraSetup = cameraSetupComponent as AngledTopDownCameraSetup;
            
            // Set the direction on the AngledTopDownCameraSetup component
            switch (direction)
            {
                case Direction.Down:
                    cameraSetup.facingDirection = AngledTopDownCameraSetup.FacingDirection.Down;
                    break;
                case Direction.Left:
                    cameraSetup.facingDirection = AngledTopDownCameraSetup.FacingDirection.Left;
                    break;
                case Direction.Up:
                    cameraSetup.facingDirection = AngledTopDownCameraSetup.FacingDirection.Up;
                    break;
                case Direction.Right:
                    cameraSetup.facingDirection = AngledTopDownCameraSetup.FacingDirection.Right;
                    break;
            }
            
            // Update character rotation through the camera setup component
            cameraSetup.UpdateCharacterRotation();
        }
        else if (componentType == "TopDownCameraSetup")
        {
            var cameraSetup = cameraSetupComponent as TopDownCameraSetup;
            
            // Set the direction on the TopDownCameraSetup component
            switch (direction)
            {
                case Direction.Down:
                    cameraSetup.facingDirection = TopDownCameraSetup.FacingDirection.Down;
                    break;
                case Direction.Left:
                    cameraSetup.facingDirection = TopDownCameraSetup.FacingDirection.Left;
                    break;
                case Direction.Up:
                    cameraSetup.facingDirection = TopDownCameraSetup.FacingDirection.Up;
                    break;
                case Direction.Right:
                    cameraSetup.facingDirection = TopDownCameraSetup.FacingDirection.Right;
                    break;
            }
            
            // Update character rotation through the camera setup component
            cameraSetup.UpdateCharacterRotation();
        }
        else if (componentType == "IsometricCameraSetup")
        {
            // For isometric, we need to do direct rotation since IsometricCameraSetup doesn't have FacingDirection enum
            float yRotation = directionAngles[direction];
            characterRoot.transform.rotation = Quaternion.Euler(0, yRotation, 0);
        }
    }
    
    private IEnumerator CaptureAnimationFramesAsSpriteSheet(AnimationClip clip, string outputPath)
    {
        // Prepare the animator
        targetAnimator.Play(clip.name, 0, 0f);
        targetAnimator.speed = 0; // Pause animation
        
        float animationLength = clip.length;
        int frameCount = Mathf.CeilToInt(animationLength * captureFrameRate);
        
        Debug.Log($"Capturing {clip.name}: {frameCount} frames, {animationLength} seconds");
        
        // Ensure we have the previous camera config
        CameraClearFlags previousClearFlags = captureCamera.clearFlags;
        Color previousBackgroundColor = captureCamera.backgroundColor;
        RenderTexture previousTargetTexture = captureCamera.targetTexture;
        
        // Set up the camera for capturing
        captureCamera.clearFlags = CameraClearFlags.SolidColor;
        captureCamera.backgroundColor = backgroundColor;
        captureCamera.targetTexture = renderTexture;
        
        // Create a temporary texture to read pixels into
        Texture2D frameTexture = new Texture2D(captureWidth, captureHeight, TextureFormat.RGBA32, false);
        
        // Calculate time increment per frame
        float timeIncrement = 1.0f / captureFrameRate;
        
        // Prepare list to store all frames for the sprite sheet
        List<Texture2D> frames = new List<Texture2D>(frameCount);
        
        // Capture each frame
        for (int i = 0; i < frameCount; i++)
        {
            // Set animation time
            float normalizedTime = (i * timeIncrement) / animationLength;
            targetAnimator.Play(clip.name, 0, normalizedTime);
            
            // Wait for animation to update
            yield return new WaitForEndOfFrame();
            
            // Capture the frame
            Texture2D capturedFrame = CaptureFrame(frameTexture, i, outputPath);
            frames.Add(capturedFrame);
            
            // Report progress
            if (i % 5 == 0) // Only log every 5 frames to reduce console spam
            {
                Debug.Log($"Captured frame {i+1}/{frameCount} ({Mathf.Round(normalizedTime * 100)}%)");
            }
            
            // Small delay to ensure animation is properly updated
            yield return null;
        }
        
        // Create the sprite sheet from captured frames
        yield return StartCoroutine(CreateSpriteSheet(frames, clip.name, outputPath));
        
        // Restore camera settings
        captureCamera.clearFlags = previousClearFlags;
        captureCamera.backgroundColor = previousBackgroundColor;
        captureCamera.targetTexture = previousTargetTexture;
        
        // Resume animation playback
        targetAnimator.speed = 1;
        
        Debug.Log($"Finished capturing {clip.name}. Frames saved to: {outputPath}");
    }
    
    private Texture2D CaptureFrame(Texture2D frameTexture, int frameIndex, string outputPath)
    {
        // Render the scene to our target texture
        captureCamera.Render();
        
        // Active render texture must be set so we can read from it
        RenderTexture.active = renderTexture;
        
        // Read pixels from the render texture
        frameTexture.ReadPixels(new Rect(0, 0, captureWidth, captureHeight), 0, 0);
        frameTexture.Apply();
        
        // Reset active render texture
        RenderTexture.active = null;
        
        // Create a copy of the captured frame
        Texture2D frameCopy = new Texture2D(captureWidth, captureHeight, TextureFormat.RGBA32, false);
        frameCopy.SetPixels(frameTexture.GetPixels());
        frameCopy.Apply();
        
        // Save individual frame if needed
        if (saveIndividualFrames)
        {
            // Convert to bytes based on format
            byte[] bytes;
            string extension;
            
            if (imageFormat == CaptureFormat.PNG)
            {
                bytes = frameCopy.EncodeToPNG();
                extension = ".png";
            }
            else
            {
                bytes = frameCopy.EncodeToJPG();
                extension = ".jpg";
            }
            
            // Save to file
            string fileName = $"{outputFileBaseName}{frameIndex:D4}{extension}";
            string filePath = Path.Combine(outputPath, fileName);
            File.WriteAllBytes(filePath, bytes);
        }
        
        return frameCopy;
    }
    
    private IEnumerator CreateSpriteSheet(List<Texture2D> frames, string animationName, string outputPath)
    {
        int frameCount = frames.Count;
        if (frameCount == 0)
        {
            Debug.LogWarning("No frames to create sprite sheet from!");
            yield break;
        }
        
        // Calculate the layout of the sprite sheet
        int framesPerRow = Mathf.Min(maxFramesPerRow, frameCount);
        int rows = Mathf.CeilToInt((float)frameCount / framesPerRow);
        
        // Calculate the dimensions of the sprite sheet
        int spriteSheetWidth = framesPerRow * (captureWidth + framePadding) - framePadding;
        int spriteSheetHeight = rows * (captureHeight + framePadding) - framePadding;
        
        // Create the sprite sheet texture
        Texture2D spriteSheet = new Texture2D(spriteSheetWidth, spriteSheetHeight, TextureFormat.RGBA32, false);
        
        // Fill the sprite sheet with transparent pixels (for padding areas)
        Color[] clearColors = new Color[spriteSheetWidth * spriteSheetHeight];
        for (int i = 0; i < clearColors.Length; i++)
        {
            clearColors[i] = new Color(0, 0, 0, 0);
        }
        spriteSheet.SetPixels(clearColors);
        
        // Arrange frames in the sprite sheet
        for (int i = 0; i < frameCount; i++)
        {
            // Calculate the position in the sprite sheet
            int row = i / framesPerRow;
            int col = i % framesPerRow;
            
            int xPos = col * (captureWidth + framePadding);
            int yPos = spriteSheetHeight - ((row + 1) * captureHeight) - (row * framePadding);
            
            // Set the pixels for this frame
            spriteSheet.SetPixels(xPos, yPos, captureWidth, captureHeight, frames[i].GetPixels());
            
            // Allow the editor to breathe on large sprite sheets
            if (i % 10 == 0)
            {
                yield return null;
            }
        }
        
        // Apply changes
        spriteSheet.Apply();
        
        // Save the sprite sheet
        byte[] bytes;
        string extension;
        
        if (imageFormat == CaptureFormat.PNG)
        {
            bytes = spriteSheet.EncodeToPNG();
            extension = ".png";
        }
        else
        {
            bytes = spriteSheet.EncodeToJPG();
            extension = ".jpg";
        }
        
        string fileName = $"{animationName}_SpriteSheet{extension}";
        string filePath = Path.Combine(outputPath, fileName);
        File.WriteAllBytes(filePath, bytes);
        
        Debug.Log($"Sprite sheet created: {filePath}");
        
        // Clean up individual frame textures
        foreach (Texture2D frame in frames)
        {
            Destroy(frame);
        }
        
        Destroy(spriteSheet);
    }
    
    // UI button to start capturing
    void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10, 10, 300, 220));
        
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
            
            GUILayout.Space(10);
            
            // Character size controls
            GUILayout.Label("Character Size Settings:", GUI.skin.label);
            
            GUILayout.BeginHorizontal();
            GUILayout.Label("Size Multiplier:", GUILayout.Width(100));
            characterSizeMultiplier = GUILayout.HorizontalSlider(characterSizeMultiplier, 0.1f, 5f, GUILayout.Width(150));
            GUILayout.Label(characterSizeMultiplier.ToString("F1"), GUILayout.Width(30));
            GUILayout.EndHorizontal();
            
            GUILayout.BeginHorizontal();
            GUILayout.Label("Camera Distance:", GUILayout.Width(100));
            cameraDistanceMultiplier = GUILayout.HorizontalSlider(cameraDistanceMultiplier, 0.1f, 5f, GUILayout.Width(150));
            GUILayout.Label(cameraDistanceMultiplier.ToString("F1"), GUILayout.Width(30));
            GUILayout.EndHorizontal();
            
            GUILayout.Space(10);
            
            GUILayout.BeginHorizontal();
            GUILayout.Label("Save Individual Frames:", GUILayout.Width(150));
            saveIndividualFrames = GUILayout.Toggle(saveIndividualFrames, "");
            GUILayout.EndHorizontal();
        }
        else
        {
            GUILayout.Label("Capturing animations... Please wait.");
        }
        
        GUILayout.EndArea();
    }
}