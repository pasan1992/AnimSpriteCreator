using UnityEngine;
using System.IO;
using System.Collections;

public class StaticObjectCapture : MonoBehaviour
{
    [Header("Object Settings")]
    [Tooltip("The static object to capture")]
    public GameObject targetObject;
    
    [Tooltip("Camera to use for capturing")]
    public Camera captureCamera;
    
    [Header("Capture Settings")]
    [Tooltip("Output folder path relative to Application.dataPath")]
    public string outputFolderName = "ObjectCaptures";
    
    [Tooltip("Name for the captured image")]
    public string outputFileName = "object_capture";
    
    [Tooltip("Image format for the captured frame")]
    public CaptureFormat imageFormat = CaptureFormat.PNG;
    
    [Header("Object Size Settings")]
    [Tooltip("Width of the captured image")]
    public int captureWidth = 512;
    
    [Tooltip("Height of the captured image")]
    public int captureHeight = 512;
    
    [Tooltip("Scale multiplier for the object (increases apparent size)")]
    [Range(0.1f, 10f)]
    public float objectSizeMultiplier = 1.0f;
    
    [Tooltip("Position the camera closer to make object appear larger")]
    [Range(0.1f, 10f)]
    public float cameraDistanceMultiplier = 1.0f;
    
    [Tooltip("Background color for the captured frame")]
    public Color backgroundColor = Color.clear;
    
    // Private variables
    private RenderTexture renderTexture;
    private string fullOutputPath;
    private Vector3 originalScale;
    private float originalDistance;
    
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
        
        // Create render texture
        renderTexture = new RenderTexture(captureWidth, captureHeight, 24);
        
        // Create output directory
        fullOutputPath = Path.Combine(Application.dataPath, outputFolderName);
        
        if (!Directory.Exists(fullOutputPath))
        {
            Directory.CreateDirectory(fullOutputPath);
        }
    }
    
    public void CaptureObject()
    {
        if (targetObject == null)
        {
            Debug.LogError("No target object assigned!");
            return;
        }
        
        // Store original values to restore later
        originalScale = targetObject.transform.localScale;
        
        // Store original camera distance
        if (captureCamera.orthographic)
        {
            originalDistance = captureCamera.orthographicSize;
            captureCamera.orthographicSize = originalDistance / cameraDistanceMultiplier;
        }
        else
        {
            Vector3 cameraToObject = targetObject.transform.position - captureCamera.transform.position;
            originalDistance = cameraToObject.magnitude;
            
            // Adjust camera position to change distance
            Vector3 direction = cameraToObject.normalized;
            captureCamera.transform.position = targetObject.transform.position - (direction * (originalDistance / cameraDistanceMultiplier));
        }
        
        // Apply scale multiplier
        targetObject.transform.localScale = originalScale * objectSizeMultiplier;
        
        // Capture the object
        StartCoroutine(CaptureObjectCoroutine());
    }
    
    private IEnumerator CaptureObjectCoroutine()
    {
        // Wait for end of frame to ensure all rendering is complete
        yield return new WaitForEndOfFrame();
        
        // Ensure we have the previous camera config
        CameraClearFlags previousClearFlags = captureCamera.clearFlags;
        Color previousBackgroundColor = captureCamera.backgroundColor;
        RenderTexture previousTargetTexture = captureCamera.targetTexture;
        
        // Set up the camera for capturing
        captureCamera.clearFlags = CameraClearFlags.SolidColor;
        captureCamera.backgroundColor = backgroundColor;
        captureCamera.targetTexture = renderTexture;
        
        // Create a temporary texture to read pixels into
        Texture2D captureTexture = new Texture2D(captureWidth, captureHeight, TextureFormat.RGBA32, false);
        
        // Render the scene to our target texture
        captureCamera.Render();
        
        // Active render texture must be set so we can read from it
        RenderTexture.active = renderTexture;
        
        // Read pixels from the render texture
        captureTexture.ReadPixels(new Rect(0, 0, captureWidth, captureHeight), 0, 0);
        captureTexture.Apply();
        
        // Reset active render texture
        RenderTexture.active = null;
        
        // Convert to bytes based on format
        byte[] bytes;
        string extension;
        
        if (imageFormat == CaptureFormat.PNG)
        {
            bytes = captureTexture.EncodeToPNG();
            extension = ".png";
        }
        else
        {
            bytes = captureTexture.EncodeToJPG();
            extension = ".jpg";
        }
        
        // Save to file
        string filePath = Path.Combine(fullOutputPath, $"{outputFileName}{extension}");
        File.WriteAllBytes(filePath, bytes);
        
        Debug.Log($"Object captured and saved to: {filePath}");
        
        // Restore camera settings
        captureCamera.clearFlags = previousClearFlags;
        captureCamera.backgroundColor = previousBackgroundColor;
        captureCamera.targetTexture = previousTargetTexture;
        
        // Restore original values
        targetObject.transform.localScale = originalScale;
        
        // Restore camera distance
        if (captureCamera.orthographic)
        {
            captureCamera.orthographicSize = originalDistance;
        }
        else
        {
            Vector3 direction = (targetObject.transform.position - captureCamera.transform.position).normalized;
            captureCamera.transform.position = targetObject.transform.position - (direction * originalDistance);
        }
        
        // Clean up
        Destroy(captureTexture);
    }
    
    // UI button to capture
    void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10, 10, 300, 180));
        
        if (GUILayout.Button("Capture Object", GUILayout.Height(30)))
        {
            CaptureObject();
        }
        
        GUILayout.Space(10);
        
        // Object size controls
        GUILayout.Label("Object Size Settings:", GUI.skin.label);
        
        GUILayout.BeginHorizontal();
        GUILayout.Label("Size Multiplier:", GUILayout.Width(100));
        objectSizeMultiplier = GUILayout.HorizontalSlider(objectSizeMultiplier, 0.1f, 5f, GUILayout.Width(150));
        GUILayout.Label(objectSizeMultiplier.ToString("F1"), GUILayout.Width(30));
        GUILayout.EndHorizontal();
        
        GUILayout.BeginHorizontal();
        GUILayout.Label("Camera Distance:", GUILayout.Width(100));
        cameraDistanceMultiplier = GUILayout.HorizontalSlider(cameraDistanceMultiplier, 0.1f, 5f, GUILayout.Width(150));
        GUILayout.Label(cameraDistanceMultiplier.ToString("F1"), GUILayout.Width(30));
        GUILayout.EndHorizontal();
        
        GUILayout.EndArea();
    }
}