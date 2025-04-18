using UnityEngine;

[ExecuteInEditMode]
public class TopDownCameraSetup : MonoBehaviour
{
    [Header("Camera Settings")]
    [Tooltip("Reference to the camera to set up")]
    public Camera targetCamera;
    
    [Tooltip("Distance from the character")]
    public float cameraHeight = 10f;
    
    [Tooltip("Target to look at (if null, will use Vector3.zero)")]
    public Transform lookTarget;
    
    [Tooltip("Use orthographic projection for 2D top-down feel")]
    public bool useOrthographic = true;
    
    [Tooltip("Size of the orthographic view (when using orthographic)")]
    public float orthographicSize = 3f;
    
    [Tooltip("Field of view (when using perspective)")]
    [Range(1f, 179f)]
    public float fieldOfView = 60f;
    
    [Tooltip("Apply pixel perfect rendering")]
    public bool pixelPerfect = true;
    
    [Tooltip("Pixel size/scale for pixel perfect rendering")]
    public int pixelScale = 1;
    
    [Tooltip("Slight angle offset from perfect top-down (0 = directly overhead)")]
    [Range(0f, 30f)]
    public float angleOffset = 0f;
    
    [Header("Grid Settings")]
    [Tooltip("Show debug grid to help with alignment")]
    public bool showDebugGrid = true;
    
    [Tooltip("Size of the debug grid")]
    public int gridSize = 10;
    
    [Tooltip("Color of the debug grid")]
    public Color gridColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
    
    [Header("Character Setup")]
    [Tooltip("The character to position")]
    public GameObject character;
    
    [Tooltip("Automatically center the character in view")]
    public bool centerCharacter = true;
    
    [Tooltip("Ground position Y value")]
    public float groundLevel = 0f;
    
    [Tooltip("Character offset from center")]
    public Vector3 characterOffset = Vector3.zero;
    
    [Tooltip("Apply a specific scale to the character")]
    public bool overrideCharacterScale = false;
    
    [Tooltip("Character scale (if override is enabled)")]
    public Vector3 characterScale = Vector3.one;
    

    [Tooltip("Character rotation presets for multi-directional capture")]
    public enum FacingDirection
    {
        Down = 0,
        Left = 90,
        Up = 180,
        Right = 270
    }
    
    [Tooltip("Current facing direction (for manual testing)")]
    public FacingDirection facingDirection = FacingDirection.Down;
    
    private void OnValidate()
    {
        if (targetCamera == null)
        {
            targetCamera = GetComponent<Camera>();
            if (targetCamera == null && Camera.main != null)
            {
                targetCamera = Camera.main;
            }
        }
        
        UpdateCameraSettings();
        
        // Update character rotation when direction changes in the inspector
        if (character != null)
        {
            UpdateCharacterRotation();
        }
    }
    
    private void Start()
    {
        UpdateCameraSettings();
    }
    
    private void Update()
    {
        if (Application.isEditor && !Application.isPlaying)
        {
            UpdateCameraSettings();
        }
    }
    
    public void UpdateCameraSettings()
    {
        if (targetCamera == null)
            return;
        
        // Set camera projection
        targetCamera.orthographic = useOrthographic;
        
        if (useOrthographic)
        {
            targetCamera.orthographicSize = orthographicSize;
        }
        else
        {
            targetCamera.fieldOfView = fieldOfView;
        }
        
        // Position the character if needed
        Vector3 targetPosition = Vector3.zero;
        if (centerCharacter && character != null)
        {
            // Place character at the center point with optional offset
            character.transform.position = new Vector3(characterOffset.x, groundLevel + characterOffset.y, characterOffset.z);
            
            // Override scale if requested
            if (overrideCharacterScale)
            {
                character.transform.localScale = characterScale;
            }
            
            targetPosition = character.transform.position;
        }
        else if (lookTarget != null)
        {
            targetPosition = lookTarget.position;
        }
        
        // Calculate camera position
        Vector3 cameraPosition = targetPosition + new Vector3(0, cameraHeight, 0);
        
        // If we have an angle offset, adjust camera position
        if (angleOffset > 0)
        {
            // Convert angle to radians
            float angleRad = angleOffset * Mathf.Deg2Rad;
            
            // Move camera slightly in negative Z direction based on angle
            float offsetZ = -cameraHeight * Mathf.Tan(angleRad);
            cameraPosition += new Vector3(0, 0, offsetZ);
        }
        
        // Position the camera
        targetCamera.transform.position = cameraPosition;
        
        // Make the camera look at the target
        targetCamera.transform.LookAt(targetPosition);
        
        // Apply pixel perfect settings
        if (pixelPerfect && useOrthographic)
        {
            ApplyPixelPerfectSettings();
        }
    }
    
    private void ApplyPixelPerfectSettings()
    {
        if (targetCamera == null || !targetCamera.orthographic)
            return;
            
        // Use a more reasonable approach for pixel art
        // If pixel perfect is enabled, we'll calculate based on pixelScale,
        // but ensure it stays within reasonable bounds
        
        // Get a reasonable base size
        float baseSize = 3f;
        
        // Scale based on pixelScale (higher pixel scale = smaller orthographic size)
        float calculatedSize = baseSize * (1f / Mathf.Max(1, pixelScale));
        
        // Apply the size with an upper limit
        targetCamera.orthographicSize = calculatedSize;
        
        Debug.Log($"Applied pixel perfect settings: PixelScale={pixelScale}, OrthographicSize={targetCamera.orthographicSize}");
    }
    
    private void OnDrawGizmos()
    {
        if (!showDebugGrid || gridSize <= 0)
            return;
            
        Vector3 center = lookTarget != null ? lookTarget.position : Vector3.zero;
        
        Gizmos.color = gridColor;
        
        // Draw horizontal grid
        for (int x = -gridSize; x <= gridSize; x++)
        {
            Gizmos.DrawLine(
                new Vector3(x, 0, -gridSize) + center, 
                new Vector3(x, 0, gridSize) + center
            );
        }
        
        for (int z = -gridSize; z <= gridSize; z++)
        {
            Gizmos.DrawLine(
                new Vector3(-gridSize, 0, z) + center, 
                new Vector3(gridSize, 0, z) + center
            );
        }
        
        // Draw view direction indicators for each camera angle
        Gizmos.color = Color.blue;
        Gizmos.DrawLine(center, center + Vector3.forward * 2);
        Gizmos.color = Color.red;
        Gizmos.DrawLine(center, center + Vector3.right * 2);
    }
    
    public void UpdateCharacterRotation()
    {
        if (character == null)
            return;
            
        // Rotate character based on facing direction
        float yRotation = (int)facingDirection;
        character.transform.rotation = Quaternion.Euler(0, yRotation, 0);
    }
    
    // Button to align everything with one click
    public void AlignCharacterAndCamera()
    {
        if (character == null)
        {
            Debug.LogWarning("No character assigned to position");
            return;
        }
        
        // Position character at ground level
        character.transform.position = new Vector3(characterOffset.x, groundLevel + characterOffset.y, characterOffset.z);
        
        // Apply scale if needed
        if (overrideCharacterScale)
        {
            character.transform.localScale = characterScale;
        }
        
        // Update character rotation
        UpdateCharacterRotation();
        
        // Update camera
        UpdateCameraSettings();
        
        Debug.Log("Character and camera aligned for top-down view");
    }
    
    // UI for easy adjustment in editor
    private void OnGUI()
    {
        if (!Application.isEditor || Application.isPlaying)
            return;
            
        GUILayout.BeginArea(new Rect(10, 10, 200, 140));
        
        if (GUILayout.Button("Align Character & Camera", GUILayout.Height(30)))
        {
            AlignCharacterAndCamera();
        }
        
        GUILayout.Space(5);
        
        if (character != null)
        {
            if (GUILayout.Button("Rotate Down (0°)", GUILayout.Height(25)))
            {
                facingDirection = FacingDirection.Down;
                UpdateCharacterRotation();
            }
            
            if (GUILayout.Button("Rotate Left (90°)", GUILayout.Height(25)))
            {
                facingDirection = FacingDirection.Left;
                UpdateCharacterRotation();
            }
            
            if (GUILayout.Button("Rotate Up (180°)", GUILayout.Height(25)))
            {
                facingDirection = FacingDirection.Up;
                UpdateCharacterRotation();
            }
            
            if (GUILayout.Button("Rotate Right (270°)", GUILayout.Height(25)))
            {
                facingDirection = FacingDirection.Right;
                UpdateCharacterRotation();
            }
        }
        
        GUILayout.EndArea();
    }
}

// Add custom editor to provide a button in the Inspector
#if UNITY_EDITOR
[UnityEditor.CustomEditor(typeof(TopDownCameraSetup))]
public class TopDownCameraSetupEditor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        
        TopDownCameraSetup script = (TopDownCameraSetup)target;
        
        UnityEditor.EditorGUILayout.Space();
        if (GUILayout.Button("Align Character & Camera", GUILayout.Height(30)))
        {
            script.AlignCharacterAndCamera();
        }
        
        UnityEditor.EditorGUILayout.Space();
        UnityEditor.EditorGUILayout.LabelField("Character Rotation", UnityEditor.EditorStyles.boldLabel);
        
        UnityEditor.EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Down"))
        {
            script.facingDirection = TopDownCameraSetup.FacingDirection.Down;
            script.UpdateCharacterRotation();
        }
        if (GUILayout.Button("Left"))
        {
            script.facingDirection = TopDownCameraSetup.FacingDirection.Left;
            script.UpdateCharacterRotation();
        }
        if (GUILayout.Button("Up"))
        {
            script.facingDirection = TopDownCameraSetup.FacingDirection.Up;
            script.UpdateCharacterRotation();
        }
        if (GUILayout.Button("Right"))
        {
            script.facingDirection = TopDownCameraSetup.FacingDirection.Right;
            script.UpdateCharacterRotation();
        }
        UnityEditor.EditorGUILayout.EndHorizontal();
    }
}
#endif