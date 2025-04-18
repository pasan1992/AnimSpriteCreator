using UnityEngine;

[ExecuteInEditMode]
public class IsometricCameraSetup : MonoBehaviour
{
    [Header("Camera Settings")]
    [Tooltip("Reference to the camera to set up")]
    public Camera targetCamera;
    
    [Tooltip("Whether to use true isometric (30° angle) or classic game isometric (26.57° angle)")]
    public IsometricType isometricType = IsometricType.ClassicGame;
    
    [Tooltip("Distance from the center point")]
    public float distance = 10f;
    
    [Tooltip("Target to look at (if null, will use Vector3.zero)")]
    public Transform lookTarget;
    
    [Tooltip("Use orthographic projection for true retro feel")]
    public bool useOrthographic = true;
    
    [Tooltip("Size of the orthographic view (when using orthographic)")]
    public float orthographicSize = 2f;
    
    [Tooltip("Field of view (when using perspective)")]
    [Range(1f, 179f)]
    public float fieldOfView = 60f;
    
    [Tooltip("Apply pixel perfect rendering")]
    public bool pixelPerfect = true;
    
    [Tooltip("Pixel size/scale for pixel perfect rendering")]
    public int pixelScale = 1;
    
    [Tooltip("Maximum orthographic size for pixel perfect mode")]
    public float maxOrthographicSize = 5f;
    
    [Header("Grid Settings")]
    [Tooltip("Show debug grid to help with alignment")]
    public bool showDebugGrid = true;
    
    [Tooltip("Size of the debug grid")]
    public int gridSize = 10;
    
    [Tooltip("Color of the debug grid")]
    public Color gridColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
    
    [Header("Character Setup")]
    [Tooltip("The character to position (should be the same as what's referenced in AnimationCapture)")]
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
    
    // Character rotation enum
    public enum FacingDirection
    {
        Down = 0,
        Left = 90,
        Up = 180,
        Right = 270
    }
    
    [Header("Character Rotation Settings")]
    [Tooltip("Current facing direction (for manual testing)")]
    public FacingDirection facingDirection = FacingDirection.Down;
    
    public enum IsometricType
    {
        TrueIsometric,    // 30° angle for perfect isometric
        ClassicGame       // 26.57° (2:1 pixel ratio) for classic game isometric
    }
    
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
        
        // Calculate the correct angles for isometric view
        float rotationX, rotationY;
        
        if (isometricType == IsometricType.TrueIsometric)
        {
            // True isometric - 30° from horizontal, rotated 45° on Y axis
            rotationX = 30f;
            rotationY = 45f;
        }
        else
        {
            // Classic game isometric (2:1 pixel ratio) - approximate 26.57° from horizontal (arctan(0.5))
            rotationX = Mathf.Rad2Deg * Mathf.Atan(0.5f);
            rotationY = 45f;
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
        
        // Calculate the camera position in spherical coordinates
        float rotX = rotationX * Mathf.Deg2Rad;
        float rotY = rotationY * Mathf.Deg2Rad;
        
        float x = distance * Mathf.Sin(rotX) * Mathf.Sin(rotY);
        float y = distance * Mathf.Cos(rotX);
        float z = distance * Mathf.Sin(rotX) * Mathf.Cos(rotY);
        
        targetCamera.transform.position = new Vector3(x, y, z) + targetPosition;
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
        float baseSize = 2f;
        
        // Scale based on pixelScale (higher pixel scale = smaller orthographic size)
        float calculatedSize = baseSize * (1f / Mathf.Max(1, pixelScale));
        
        // Apply the size with an upper limit
        targetCamera.orthographicSize = Mathf.Min(calculatedSize, maxOrthographicSize);
        
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
        
        // Draw vertical reference
        Gizmos.DrawLine(center, center + Vector3.up * gridSize);
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
        
        // Update camera
        UpdateCameraSettings();
        
        Debug.Log("Character and camera aligned for isometric view");
    }
    
    public void UpdateCharacterRotation()
    {
        if (character == null)
            return;
            
        // Rotate character based on facing direction
        float yRotation = (int)facingDirection;
        character.transform.rotation = Quaternion.Euler(0, yRotation, 0);
        
        // Update camera position after rotating character
        UpdateCameraSettings();
    }
    
    // UI for easy adjustment in editor
    private void OnGUI()
    {
        if (!Application.isEditor || Application.isPlaying)
            return;
            
        GUILayout.BeginArea(new Rect(10, 10, 200, 180));
        
        if (GUILayout.Button("Align Character & Camera", GUILayout.Height(30)))
        {
            AlignCharacterAndCamera();
        }
        
        GUILayout.Space(5);
        
        if (character != null)
        {
            GUILayout.Label("Character Direction:", GUI.skin.label);
            
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
[UnityEditor.CustomEditor(typeof(IsometricCameraSetup))]
public class IsometricCameraSetupEditor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        
        IsometricCameraSetup script = (IsometricCameraSetup)target;
        
        UnityEditor.EditorGUILayout.Space();
        if (GUILayout.Button("Align Character & Camera", GUILayout.Height(30)))
        {
            script.AlignCharacterAndCamera();
        }
    }
}
#endif