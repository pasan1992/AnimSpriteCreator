using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteInEditMode]
public class AngledTopDownCameraSetup : MonoBehaviour
{
    [Header("Camera Settings")]
    [Tooltip("Reference to the camera to set up")]
    public Camera targetCamera;
    
    [Tooltip("Distance from the character")]
    public float cameraDistance = 12f;
    
    [Tooltip("Target to look at (if null, will use Vector3.zero)")]
    public Transform lookTarget;
    
    [Tooltip("Use orthographic projection for classic 2D feel")]
    public bool useOrthographic = true;
    
    [Tooltip("Size of the orthographic view (when using orthographic)")]
    public float orthographicSize = 3f;
    
    [Tooltip("Field of view (when using perspective)")]
    [Range(1f, 179f)]
    public float fieldOfView = 60f;
    
    [Header("Angle Settings")]
    [Tooltip("Angle from the ground (45-60° for classic 3/4 view)")]
    [Range(30f, 75f)]
    public float viewAngle = 45f;
    
    [Tooltip("Horizontal angle offset (standard: -45° for Zelda-like games)")]
    [Range(-90f, 90f)]
    public float horizontalAngle = -45f;
    
    [Tooltip("Apply pixel perfect rendering")]
    public bool pixelPerfect = true;
    
    [Tooltip("Pixel size/scale for pixel perfect rendering")]
    public int pixelScale = 1;
    
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
    
    [Header("Visual Reference")]
    [Tooltip("Show a preview sphere representing the player")]
    public bool showCharacterReference = true;
    
    [Tooltip("Size of the reference sphere")]
    public float referenceSphereSize = 1f;
    
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
        
        // Calculate camera position using spherical coordinates
        // Convert angles to radians
        float verticalAngleRad = viewAngle * Mathf.Deg2Rad;
        float horizontalAngleRad = horizontalAngle * Mathf.Deg2Rad;
        
        // Calculate camera position
        float y = cameraDistance * Mathf.Sin(verticalAngleRad);
        float horizontalDistance = cameraDistance * Mathf.Cos(verticalAngleRad);
        float x = horizontalDistance * Mathf.Sin(horizontalAngleRad);
        float z = horizontalDistance * Mathf.Cos(horizontalAngleRad);
        
        // Set camera position and look target
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
        float baseSize = 3f;
        
        // Scale based on pixelScale (higher values = smaller orthographic size)
        float calculatedSize = baseSize * (1f / Mathf.Max(1, pixelScale));
        
        // Apply the size
        targetCamera.orthographicSize = calculatedSize;
    }
    
    private void OnDrawGizmos()
    {
        Vector3 center = Vector3.zero;
        if (character != null)
        {
            center = character.transform.position;
        }
        else if (lookTarget != null)
        {
            center = lookTarget.position;
        }
        
        // Draw reference grid
        if (showDebugGrid && gridSize > 0)
        {
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
        }
        
        // Draw direction indicators
        if (showCharacterReference)
        {
            // Draw reference sphere
            Gizmos.color = Color.white;
            Gizmos.DrawWireSphere(center, referenceSphereSize);
            
            // Draw forward direction (blue Z-axis)
            Gizmos.color = Color.blue;
            Vector3 characterForward = Quaternion.Euler(0, (int)facingDirection, 0) * Vector3.forward;
            Gizmos.DrawLine(center, center + characterForward * referenceSphereSize * 1.5f);
            
            // Draw right direction (red X-axis)
            Gizmos.color = Color.red;
            Vector3 characterRight = Quaternion.Euler(0, (int)facingDirection, 0) * Vector3.right;
            Gizmos.DrawLine(center, center + characterRight * referenceSphereSize * 1.2f);
            
            // Draw up direction (green Y-axis)
            Gizmos.color = Color.green;
            Gizmos.DrawLine(center, center + Vector3.up * referenceSphereSize * 1.2f);
        }
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
        
        Debug.Log("Character and camera aligned for 3/4 view (angled top-down)");
    }
    
    // Presets for common angles
    public void ApplyClassicZeldaPreset()
    {
        viewAngle = 45f;
        horizontalAngle = -45f;
        orthographicSize = 2.5f;
        UpdateCameraSettings();
        Debug.Log("Applied classic Zelda-like (A Link to the Past) preset");
    }
    
    public void ApplyStardewValleyPreset()
    {
        viewAngle = 60f;
        horizontalAngle = -45f;
        orthographicSize = 3f;
        UpdateCameraSettings();
        Debug.Log("Applied Stardew Valley-like preset");
    }
    
    public void ApplyEnterTheGungeonPreset()
    {
        viewAngle = 50f;
        horizontalAngle = -45f;
        orthographicSize = 4f;
        UpdateCameraSettings();
        Debug.Log("Applied Enter the Gungeon-like preset");
    }
    
    // UI for easy adjustment in editor
    private void OnGUI()
    {
        if (!Application.isEditor || Application.isPlaying)
            return;
            
        GUILayout.BeginArea(new Rect(10, 10, 200, 230));
        
        if (GUILayout.Button("Align Character & Camera", GUILayout.Height(30)))
        {
            AlignCharacterAndCamera();
        }
        
        GUILayout.Space(5);
        
        GUILayout.Label("Presets:", GUI.skin.label);
        
        if (GUILayout.Button("Zelda: A Link to the Past", GUILayout.Height(25)))
        {
            ApplyClassicZeldaPreset();
        }
        
        if (GUILayout.Button("Stardew Valley", GUILayout.Height(25)))
        {
            ApplyStardewValleyPreset();
        }
        
        if (GUILayout.Button("Enter the Gungeon", GUILayout.Height(25)))
        {
            ApplyEnterTheGungeonPreset();
        }
        
        GUILayout.Space(5);
        
        GUILayout.Label("Character Direction:", GUI.skin.label);
        
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
[CustomEditor(typeof(AngledTopDownCameraSetup))]
public class AngledTopDownCameraSetupEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        
        AngledTopDownCameraSetup script = (AngledTopDownCameraSetup)target;
        
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Quick Settings", EditorStyles.boldLabel);
        
        if (GUILayout.Button("Align Character & Camera", GUILayout.Height(30)))
        {
            script.AlignCharacterAndCamera();
        }
        
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Game Presets", EditorStyles.boldLabel);
        
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Zelda: ALTTP"))
        {
            script.ApplyClassicZeldaPreset();
        }
        if (GUILayout.Button("Stardew Valley"))
        {
            script.ApplyStardewValleyPreset();
        }
        if (GUILayout.Button("Enter the Gungeon"))
        {
            script.ApplyEnterTheGungeonPreset();
        }
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Character Rotation", EditorStyles.boldLabel);
        
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Down"))
        {
            script.facingDirection = AngledTopDownCameraSetup.FacingDirection.Down;
            script.UpdateCharacterRotation();
        }
        if (GUILayout.Button("Left"))
        {
            script.facingDirection = AngledTopDownCameraSetup.FacingDirection.Left;
            script.UpdateCharacterRotation();
        }
        if (GUILayout.Button("Up"))
        {
            script.facingDirection = AngledTopDownCameraSetup.FacingDirection.Up;
            script.UpdateCharacterRotation();
        }
        if (GUILayout.Button("Right"))
        {
            script.facingDirection = AngledTopDownCameraSetup.FacingDirection.Right;
            script.UpdateCharacterRotation();
        }
        EditorGUILayout.EndHorizontal();
    }
}
#endif