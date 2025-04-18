using UnityEngine;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class AnimationCapture : MonoBehaviour
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
    
    [Header("Capture Settings")]
    [Tooltip("Output folder path relative to Application.dataPath")]
    public string outputFolderName = "AnimationFrames";
    
    [Tooltip("Base name for the captured frames")]
    public string outputFileBaseName = "frame_";
    
    [Tooltip("Image format for the captured frames")]
    public CaptureFormat imageFormat = CaptureFormat.PNG;
    
    [Tooltip("Width of the captured frames")]
    public int captureWidth = 256;
    
    [Tooltip("Height of the captured frames")]
    public int captureHeight = 256;
    
    [Tooltip("Background color for the captured frames")]
    public Color backgroundColor = Color.clear;
    
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
        { Direction.Left, -45f },
        { Direction.Up, -180f },
        { Direction.Right, 45f }
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
        
        // Get all animation states from the animator
        // var animatorController = targetAnimator.runtimeAnimatorController as UnityEngine.Animations.AnimatorController;
        // if (animatorController == null)
        // {
        //     Debug.LogError("Could not get animator controller!");
        //     isCapturing = false;
        //     yield break;
        // }
        
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
            
            if (captureMultipleDirections && characterRoot != null)
            {
                // Capture the animation from multiple directions
                foreach (var directionPair in directionAngles)
                {
                    Direction direction = directionPair.Key;
                    float yRotation = directionPair.Value;
                    
                    // Set the character rotation for this direction
                    characterRoot.transform.rotation = Quaternion.Euler(0, yRotation, 0);
                    
                    // Create subfolder for this animation and direction
                    string directionSubfolder = Path.Combine(fullOutputPath, animName, direction.ToString());
                    if (!Directory.Exists(directionSubfolder))
                    {
                        Directory.CreateDirectory(directionSubfolder);
                    }
                    else
                    {
                        // Clean previous frames
                        string[] existingFiles = Directory.GetFiles(directionSubfolder);
                        foreach (string file in existingFiles)
                        {
                            File.Delete(file);
                        }
                    }
                    
                    // Capture all frames for this animation and direction
                    yield return StartCoroutine(CaptureAnimationFrames(clip, directionSubfolder));
                    
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
                else
                {
                    // Clean previous frames
                    string[] existingFiles = Directory.GetFiles(animSubfolder);
                    foreach (string file in existingFiles)
                    {
                        File.Delete(file);
                    }
                }
                
                // Capture all frames for this animation
                yield return StartCoroutine(CaptureAnimationFrames(clip, animSubfolder));
                
                // Give time for the UI to update
                yield return new WaitForSeconds(0.1f);
            }
        }
        
        // Restore original rotation
        if (characterRoot != null)
        {
            characterRoot.transform.rotation = originalRotation;
        }
        
        isCapturing = false;
        Debug.Log("Animation capture process complete!");
    }
    
    private IEnumerator CaptureAnimationFrames(AnimationClip clip, string outputPath)
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
        
        // Capture each frame
        for (int i = 0; i < frameCount; i++)
        {
            // Set animation time
            float normalizedTime = (i * timeIncrement) / animationLength;
            targetAnimator.Play(clip.name, 0, normalizedTime);
            
            // Wait for animation to update
            yield return new WaitForEndOfFrame();
            
            // Capture the frame
            CaptureFrame(frameTexture, i, outputPath);
            
            // Report progress
            if (i % 5 == 0) // Only log every 5 frames to reduce console spam
            {
                Debug.Log($"Captured frame {i+1}/{frameCount} ({Mathf.Round(normalizedTime * 100)}%)");
            }
            
            // Small delay to ensure animation is properly updated
            yield return null;
        }
        
        // Restore camera settings
        captureCamera.clearFlags = previousClearFlags;
        captureCamera.backgroundColor = previousBackgroundColor;
        captureCamera.targetTexture = previousTargetTexture;
        
        // Resume animation playback
        targetAnimator.speed = 1;
        
        // Clean up
        Destroy(frameTexture);
        
        Debug.Log($"Finished capturing {clip.name}. Frames saved to: {outputPath}");
    }
    
    private void CaptureFrame(Texture2D frameTexture, int frameIndex, string outputPath)
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
        
        // Convert to bytes based on format
        byte[] bytes;
        string extension;
        
        if (imageFormat == CaptureFormat.PNG)
        {
            bytes = frameTexture.EncodeToPNG();
            extension = ".png";
        }
        else
        {
            bytes = frameTexture.EncodeToJPG();
            extension = ".jpg";
        }
        
        // Save to file
        string fileName = $"{outputFileBaseName}{frameIndex:D4}{extension}";
        string filePath = Path.Combine(outputPath, fileName);
        File.WriteAllBytes(filePath, bytes);
    }
    
    // UI button to start capturing
    void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10, 10, 300, 100));
        
        if (!isCapturing)
        {
            if (GUILayout.Button("Capture All Animations", GUILayout.Height(30)))
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
            GUILayout.Label("Capturing animations... Please wait.");
        }
        
        GUILayout.EndArea();
    }
}
