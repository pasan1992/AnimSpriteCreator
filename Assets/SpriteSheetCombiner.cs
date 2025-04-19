 using UnityEngine;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;

public class SpriteSheetCombiner : MonoBehaviour
{
    [Header("Input Settings")]
    [Tooltip("Root folder containing sprite sheets to combine (relative to Application.dataPath)")]
    public string inputFolderPath = "AnimationFrames";
    
    [Tooltip("Filter for specific animation name (leave empty for all)")]
    public string animationNameFilter = "";
    
    [Tooltip("Whether to scan subfolders for sprite sheets")]
    public bool includeSubfolders = true;

    [Header("Output Settings")]
    [Tooltip("Output folder path for combined sprite sheets (relative to Application.dataPath)")]
    public string outputFolderPath = "GeneratedAnimations/Combined";
    
    [Tooltip("Base name for the combined sprite sheets")]
    public string outputFileBaseName = "combined_";
    
    [Tooltip("Image format for the combined sprite sheets")]
    public SpriteSheetCapture.CaptureFormat imageFormat = SpriteSheetCapture.CaptureFormat.PNG;

    [Header("Combination Settings")]
    [Tooltip("How to arrange multiple sprite sheets")]
    public CombinationMode combinationMode = CombinationMode.DirectionsInRows;
    
    [Tooltip("Combine all animation directions into one sheet")]
    public bool combineDirections = true;
    
    [Tooltip("Maximum number of animation frames per row when combining directions")]
    public int maxFramesPerRow = 8;
    
    [Tooltip("Add a border between different sprite sheets in the combined sheet")]
    public bool addBorder = false;
    
    [Tooltip("Border thickness in pixels")]
    public int borderThickness = 1;
    
    [Tooltip("Border color")]
    public Color borderColor = Color.black;

    // Combination modes
    public enum CombinationMode
    {
        DirectionsInRows,      // Each direction gets its own row
        DirectionsInColumns,   // Each direction gets its own column
        DirectionsInGrid,      // Arrange directions in a grid
        Custom                 // Custom arrangement (future implementation)
    }

    // Metadata class to store information about a sprite sheet
    [System.Serializable]
    public class SpriteSheetMetadata
    {
        public string animationName;
        public string direction;
        public int frameCount;
        public int frameWidth;
        public int frameHeight;
        public int columns;
        public int rows;
        public int framesPerSecond;
        public string filePath;
        
        public static SpriteSheetMetadata FromJson(string json, string filePath)
        {
            SpriteSheetMetadata metadata = JsonUtility.FromJson<SpriteSheetMetadata>(json);
            metadata.filePath = filePath;
            return metadata;
        }
    }

    // List of sprite sheets to combine
    private List<SpriteSheetMetadata> spriteSheets = new List<SpriteSheetMetadata>();
    
    // UI button to start combining
    void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10, 10, 300, 150));
        
        if (GUILayout.Button("Scan for Sprite Sheets", GUILayout.Height(30)))
        {
            ScanForSpriteSheets();
        }
        
        if (spriteSheets.Count > 0)
        {
            GUILayout.Label($"Found {spriteSheets.Count} sprite sheets.");
            
            if (GUILayout.Button("Combine Sprite Sheets", GUILayout.Height(30)))
            {
                StartCoroutine(CombineSpriteSheets());
            }
        }
        
        GUILayout.EndArea();
    }
    
    // Scan for sprite sheets in the input folder
    public void ScanForSpriteSheets()
    {
        spriteSheets.Clear();
        
        string rootPath = Path.Combine(Application.dataPath, inputFolderPath);
        
        if (!Directory.Exists(rootPath))
        {
            Debug.LogError($"Input folder not found: {rootPath}");
            return;
        }
        
        // Search options
        SearchOption searchOption = includeSubfolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        
        // Find all metadata files
        string[] metadataFiles = Directory.GetFiles(rootPath, "*_metadata.json", searchOption);
        
        foreach (string metadataFile in metadataFiles)
        {
            try
            {
                string json = File.ReadAllText(metadataFile);
                
                // Extract potential sprite sheet path from metadata filename
                string directory = Path.GetDirectoryName(metadataFile);
                string metadataFileName = Path.GetFileNameWithoutExtension(metadataFile);
                string baseName = metadataFileName.Replace("_metadata", "");
                
                // Skip if animation name filter is set and doesn't match
                if (!string.IsNullOrEmpty(animationNameFilter))
                {
                    // Extract animation name from metadata
                    SpriteSheetMetadata tempMetadata = JsonUtility.FromJson<SpriteSheetMetadata>(json);
                    if (tempMetadata != null && !tempMetadata.animationName.Contains(animationNameFilter))
                    {
                        continue;
                    }
                }
                
                // Look for matching image file
                string pngPath = Path.Combine(directory, $"animation_{baseName}.png");
                string jpgPath = Path.Combine(directory, $"animation_{baseName}.jpg");
                
                string spriteSheetPath = File.Exists(pngPath) ? pngPath : 
                                        (File.Exists(jpgPath) ? jpgPath : null);
                
                if (spriteSheetPath != null)
                {
                    SpriteSheetMetadata metadata = SpriteSheetMetadata.FromJson(json, spriteSheetPath);
                    spriteSheets.Add(metadata);
                    Debug.Log($"Found sprite sheet: {spriteSheetPath}");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error processing metadata file {metadataFile}: {e.Message}");
            }
        }
        
        // Group sprite sheets by animation
        Dictionary<string, List<SpriteSheetMetadata>> animationGroups = 
            spriteSheets.GroupBy(s => s.animationName)
                       .ToDictionary(g => g.Key, g => g.ToList());
        
        Debug.Log($"Found {spriteSheets.Count} sprite sheets in {animationGroups.Count} animation groups.");
        
        foreach (var group in animationGroups)
        {
            Debug.Log($"Animation: {group.Key} - {group.Value.Count} directions/variants");
        }
    }
    
    // Combine sprite sheets into a single sheet
    private IEnumerator CombineSpriteSheets()
    {
        if (spriteSheets.Count == 0)
        {
            Debug.LogWarning("No sprite sheets found to combine.");
            yield break;
        }
        
        // Group sprite sheets by animation
        Dictionary<string, List<SpriteSheetMetadata>> animationGroups = 
            spriteSheets.GroupBy(s => s.animationName)
                       .ToDictionary(g => g.Key, g => g.ToList());
        
        // Create output directory if it doesn't exist
        string outputPath = Path.Combine(Application.dataPath, outputFolderPath);
        if (!Directory.Exists(outputPath))
        {
            Directory.CreateDirectory(outputPath);
        }
        
        // Process each animation group
        foreach (var group in animationGroups)
        {
            string animationName = group.Key;
            List<SpriteSheetMetadata> sheets = group.Value;
            
            Debug.Log($"Combining {sheets.Count} sprite sheets for animation '{animationName}'");
            
            // If we're not combining directions, handle each sheet separately
            if (!combineDirections)
            {
                foreach (var sheet in sheets)
                {
                    yield return StartCoroutine(CombineSingleSpriteSheet(sheet));
                }
                continue;
            }
            
            // If we are combining directions, we need to process them all together
            yield return StartCoroutine(CombineMultipleSpriteSheets(animationName, sheets));
        }
        
        Debug.Log("Sprite sheet combination complete!");
        
        // Refresh asset database if in the editor
        #if UNITY_EDITOR
        AssetDatabase.Refresh();
        #endif
    }
    
    // Combine a single sprite sheet (just copies it with formatting)
    private IEnumerator CombineSingleSpriteSheet(SpriteSheetMetadata metadata)
    {
        // Load the original sprite sheet
        Texture2D originalSheet = LoadTexture(metadata.filePath);
        if (originalSheet == null)
        {
            Debug.LogError($"Failed to load texture: {metadata.filePath}");
            yield break;
        }
        
        // Create output filename
        string outputFilename = $"{outputFileBaseName}{metadata.animationName}";
        if (!string.IsNullOrEmpty(metadata.direction))
        {
            outputFilename += $"_{metadata.direction.ToLower()}";
        }
        string extension = imageFormat == SpriteSheetCapture.CaptureFormat.PNG ? ".png" : ".jpg";
        string outputFile = Path.Combine(Application.dataPath, outputFolderPath, outputFilename + extension);
        
        // Save the texture
        byte[] bytes;
        if (imageFormat == SpriteSheetCapture.CaptureFormat.PNG)
        {
            bytes = originalSheet.EncodeToPNG();
        }
        else
        {
            bytes = originalSheet.EncodeToJPG();
        }
        
        File.WriteAllBytes(outputFile, bytes);
        
        // Create metadata JSON file
        WriteMetadataFile(metadata, outputFile.Replace(extension, "_metadata.json"));
        
        Debug.Log($"Saved combined sheet to: {outputFile}");
        
        // Clean up
        Destroy(originalSheet);
        yield return null;
    }
    
    // Combine multiple sprite sheets from the same animation (different directions)
    private IEnumerator CombineMultipleSpriteSheets(string animationName, List<SpriteSheetMetadata> sheets)
    {
        if (sheets.Count == 0)
        {
            yield break;
        }
        
        // Make sure all sheets have the same frame size
        int frameWidth = sheets[0].frameWidth;
        int frameHeight = sheets[0].frameHeight;
        
        bool sizeMismatch = sheets.Any(s => s.frameWidth != frameWidth || s.frameHeight != frameHeight);
        if (sizeMismatch)
        {
            Debug.LogError($"Cannot combine sheets with different frame sizes for {animationName}");
            yield break;
        }
        
        // Calculate the total number of frames across all directions
        int totalFrameCount = sheets.Sum(s => s.frameCount);
        
        // Calculate the combined sheet dimensions based on the combination mode
        int combinedWidth = 0;
        int combinedHeight = 0;
        int rows = 0;
        int columns = 0;
        
        switch (combinationMode)
        {
            case CombinationMode.DirectionsInRows:
                // Each direction gets its own row
                rows = sheets.Count;
                columns = sheets.Max(s => s.frameCount);
                combinedWidth = columns * frameWidth;
                combinedHeight = rows * frameHeight;
                if (addBorder)
                {
                    combinedWidth += (columns - 1) * borderThickness;
                    combinedHeight += (rows - 1) * borderThickness;
                }
                break;
                
            case CombinationMode.DirectionsInColumns:
                // Each direction gets its own column
                columns = sheets.Count;
                rows = sheets.Max(s => s.frameCount);
                combinedWidth = columns * frameWidth;
                combinedHeight = rows * frameHeight;
                if (addBorder)
                {
                    combinedWidth += (columns - 1) * borderThickness;
                    combinedHeight += (rows - 1) * borderThickness;
                }
                break;
                
            case CombinationMode.DirectionsInGrid:
                // Arrange all frames in a grid, respecting maxFramesPerRow
                columns = Mathf.Min(totalFrameCount, maxFramesPerRow);
                rows = Mathf.CeilToInt((float)totalFrameCount / maxFramesPerRow);
                combinedWidth = columns * frameWidth;
                combinedHeight = rows * frameHeight;
                if (addBorder)
                {
                    combinedWidth += (columns - 1) * borderThickness;
                    combinedHeight += (rows - 1) * borderThickness;
                }
                break;
                
            case CombinationMode.Custom:
                // For now, fall back to DirectionsInRows
                rows = sheets.Count;
                columns = sheets.Max(s => s.frameCount);
                combinedWidth = columns * frameWidth;
                combinedHeight = rows * frameHeight;
                if (addBorder)
                {
                    combinedWidth += (columns - 1) * borderThickness;
                    combinedHeight += (rows - 1) * borderThickness;
                }
                break;
        }
        
        // Create the combined texture
        Texture2D combinedTexture = new Texture2D(combinedWidth, combinedHeight, TextureFormat.RGBA32, false);
        
        // Fill with transparent pixels initially
        Color[] clearColors = new Color[combinedWidth * combinedHeight];
        for (int i = 0; i < clearColors.Length; i++)
        {
            clearColors[i] = new Color(0, 0, 0, 0);
        }
        combinedTexture.SetPixels(clearColors);
        
        // Keep track of frame positions for metadata
        List<FramePosition> framePositions = new List<FramePosition>();
        
        // Now copy the frames from each sheet based on the combination mode
        switch (combinationMode)
        {
            case CombinationMode.DirectionsInRows:
                yield return StartCoroutine(CombineDirectionsInRows(sheets, combinedTexture, frameWidth, frameHeight, framePositions));
                break;
                
            case CombinationMode.DirectionsInColumns:
                yield return StartCoroutine(CombineDirectionsInColumns(sheets, combinedTexture, frameWidth, frameHeight, framePositions));
                break;
                
            case CombinationMode.DirectionsInGrid:
                yield return StartCoroutine(CombineDirectionsInGrid(sheets, combinedTexture, frameWidth, frameHeight, framePositions));
                break;
                
            case CombinationMode.Custom:
                // Fall back to DirectionsInRows for now
                yield return StartCoroutine(CombineDirectionsInRows(sheets, combinedTexture, frameWidth, frameHeight, framePositions));
                break;
        }
        
        // Apply changes to the combined texture
        combinedTexture.Apply();
        
        // Create output filename
        string outputFilename = $"{outputFileBaseName}{animationName}_combined";
        string extension = imageFormat == SpriteSheetCapture.CaptureFormat.PNG ? ".png" : ".jpg";
        string outputFile = Path.Combine(Application.dataPath, outputFolderPath, outputFilename + extension);
        
        // Save the texture
        byte[] bytes;
        if (imageFormat == SpriteSheetCapture.CaptureFormat.PNG)
        {
            bytes = combinedTexture.EncodeToPNG();
        }
        else
        {
            bytes = combinedTexture.EncodeToJPG();
        }
        
        File.WriteAllBytes(outputFile, bytes);
        
        // Create a combined metadata object
        SpriteSheetMetadata combinedMetadata = new SpriteSheetMetadata
        {
            animationName = animationName,
            direction = "combined",
            frameCount = totalFrameCount,
            frameWidth = frameWidth,
            frameHeight = frameHeight,
            columns = columns,
            rows = rows,
            framesPerSecond = sheets[0].framesPerSecond, // Use the FPS from the first sheet
            filePath = outputFile
        };
        
        // Write combined metadata file
        WriteMetadataFile(combinedMetadata, outputFile.Replace(extension, "_metadata.json"));
        
        // Write detailed mapping metadata (which direction each frame belongs to)
        WriteDirectionMappingFile(sheets, framePositions, outputFile.Replace(extension, "_mapping.json"));
        
        Debug.Log($"Saved combined sheet to: {outputFile}");
        
        // Clean up
        Destroy(combinedTexture);
        yield return null;
    }
    
    // Combine sprite sheets with each direction in its own row
    private IEnumerator CombineDirectionsInRows(List<SpriteSheetMetadata> sheets, Texture2D combinedTexture, 
                                               int frameWidth, int frameHeight, List<FramePosition> framePositions)
    {
        int frameIndex = 0;
        
        // Sort the sheets by direction for consistent ordering
        var sortedSheets = SortSheetsByDirection(sheets);
        
        // Process each direction (row)
        for (int row = 0; row < sortedSheets.Count; row++)
        {
            var metadata = sortedSheets[row];
            Texture2D originalSheet = LoadTexture(metadata.filePath);
            
            if (originalSheet == null)
            {
                Debug.LogError($"Failed to load texture: {metadata.filePath}");
                continue;
            }
            
            // Process each frame in this direction
            for (int col = 0; col < metadata.frameCount; col++)
            {
                // Calculate the source and destination positions
                int srcX = (col % metadata.columns) * frameWidth;
                int srcY = (col / metadata.columns) * frameHeight;
                srcY = originalSheet.height - srcY - frameHeight; // Adjust for inverted Y in textures
                
                int destX = col * frameWidth;
                if (addBorder) destX += col * borderThickness;
                
                int destY = row * frameHeight;
                if (addBorder) destY += row * borderThickness;
                destY = combinedTexture.height - destY - frameHeight; // Adjust for inverted Y in textures
                
                // Copy the frame pixels
                Color[] framePixels = originalSheet.GetPixels(srcX, srcY, frameWidth, frameHeight);
                combinedTexture.SetPixels(destX, destY, frameWidth, frameHeight, framePixels);
                
                // Add frame position info
                framePositions.Add(new FramePosition
                {
                    index = frameIndex++,
                    x = destX,
                    y = combinedTexture.height - destY - frameHeight, // Convert back to "normal" Y for metadata
                    width = frameWidth,
                    height = frameHeight,
                    direction = metadata.direction,
                    originalFrameIndex = col
                });
                
                // Add borders if needed
                if (addBorder && col < metadata.frameCount - 1)
                {
                    // Add vertical border after this frame
                    Color[] borderPixels = new Color[frameHeight * borderThickness];
                    for (int i = 0; i < borderPixels.Length; i++)
                    {
                        borderPixels[i] = borderColor;
                    }
                    combinedTexture.SetPixels(destX + frameWidth, destY, borderThickness, frameHeight, borderPixels);
                }
            }
            
            // Add horizontal border if needed
            if (addBorder && row < sortedSheets.Count - 1)
            {
                int borderY = row * frameHeight + frameHeight;
                if (addBorder) borderY += row * borderThickness;
                borderY = combinedTexture.height - borderY - borderThickness; // Adjust for inverted Y
                
                Color[] borderPixels = new Color[combinedTexture.width * borderThickness];
                for (int i = 0; i < borderPixels.Length; i++)
                {
                    borderPixels[i] = borderColor;
                }
                combinedTexture.SetPixels(0, borderY, combinedTexture.width, borderThickness, borderPixels);
            }
            
            // Clean up
            Destroy(originalSheet);
            
            // Yield to prevent freezing
            if (row % 2 == 0) yield return null;
        }
    }
    
    // Combine sprite sheets with each direction in its own column
    private IEnumerator CombineDirectionsInColumns(List<SpriteSheetMetadata> sheets, Texture2D combinedTexture, 
                                                  int frameWidth, int frameHeight, List<FramePosition> framePositions)
    {
        int frameIndex = 0;
        
        // Sort the sheets by direction for consistent ordering
        var sortedSheets = SortSheetsByDirection(sheets);
        
        // Process each direction (column)
        for (int col = 0; col < sortedSheets.Count; col++)
        {
            var metadata = sortedSheets[col];
            Texture2D originalSheet = LoadTexture(metadata.filePath);
            
            if (originalSheet == null)
            {
                Debug.LogError($"Failed to load texture: {metadata.filePath}");
                continue;
            }
            
            // Process each frame in this direction
            for (int row = 0; row < metadata.frameCount; row++)
            {
                // Calculate the source and destination positions
                int srcX = (row % metadata.columns) * frameWidth;
                int srcY = (row / metadata.columns) * frameHeight;
                srcY = originalSheet.height - srcY - frameHeight; // Adjust for inverted Y in textures
                
                int destX = col * frameWidth;
                if (addBorder) destX += col * borderThickness;
                
                int destY = row * frameHeight;
                if (addBorder) destY += row * borderThickness;
                destY = combinedTexture.height - destY - frameHeight; // Adjust for inverted Y in textures
                
                // Copy the frame pixels
                Color[] framePixels = originalSheet.GetPixels(srcX, srcY, frameWidth, frameHeight);
                combinedTexture.SetPixels(destX, destY, frameWidth, frameHeight, framePixels);
                
                // Add frame position info
                framePositions.Add(new FramePosition
                {
                    index = frameIndex++,
                    x = destX,
                    y = combinedTexture.height - destY - frameHeight, // Convert back to "normal" Y for metadata
                    width = frameWidth,
                    height = frameHeight,
                    direction = metadata.direction,
                    originalFrameIndex = row
                });
                
                // Add borders if needed
                if (addBorder && row < metadata.frameCount - 1)
                {
                    // Add horizontal border after this frame
                    int borderY = row * frameHeight + frameHeight;
                    if (addBorder) borderY += row * borderThickness;
                    borderY = combinedTexture.height - borderY - borderThickness; // Adjust for inverted Y
                    
                    Color[] borderPixels = new Color[frameWidth * borderThickness];
                    for (int i = 0; i < borderPixels.Length; i++)
                    {
                        borderPixels[i] = borderColor;
                    }
                    combinedTexture.SetPixels(destX, borderY, frameWidth, borderThickness, borderPixels);
                }
            }
            
            // Add vertical border if needed
            if (addBorder && col < sortedSheets.Count - 1)
            {
                int borderX = col * frameWidth + frameWidth;
                if (addBorder) borderX += col * borderThickness;
                
                Color[] borderPixels = new Color[combinedTexture.height * borderThickness];
                for (int i = 0; i < borderPixels.Length; i++)
                {
                    borderPixels[i] = borderColor;
                }
                
                // Textures have inverted Y, so we need to set pixels row by row
                for (int y = 0; y < combinedTexture.height; y++)
                {
                    Color[] rowBorderPixels = new Color[borderThickness];
                    for (int x = 0; x < borderThickness; x++)
                    {
                        rowBorderPixels[x] = borderColor;
                    }
                    combinedTexture.SetPixels(borderX, y, borderThickness, 1, rowBorderPixels);
                }
            }
            
            // Clean up
            Destroy(originalSheet);
            
            // Yield to prevent freezing
            if (col % 2 == 0) yield return null;
        }
    }
    
    // Combine sprite sheets into a grid arrangement
    private IEnumerator CombineDirectionsInGrid(List<SpriteSheetMetadata> sheets, Texture2D combinedTexture, 
                                               int frameWidth, int frameHeight, List<FramePosition> framePositions)
    {
        int frameIndex = 0;
        int maxFramesPerRow = this.maxFramesPerRow;
        
        // Sort the sheets by direction for consistent ordering
        var sortedSheets = SortSheetsByDirection(sheets);
        
        // Process each direction
        foreach (var metadata in sortedSheets)
        {
            Texture2D originalSheet = LoadTexture(metadata.filePath);
            
            if (originalSheet == null)
            {
                Debug.LogError($"Failed to load texture: {metadata.filePath}");
                continue;
            }
            
            // Process each frame in this direction
            for (int frame = 0; frame < metadata.frameCount; frame++)
            {
                // Calculate the source position
                int srcX = (frame % metadata.columns) * frameWidth;
                int srcY = (frame / metadata.columns) * frameHeight;
                srcY = originalSheet.height - srcY - frameHeight; // Adjust for inverted Y in textures
                
                // Calculate the destination position in the grid
                int destCol = frameIndex % maxFramesPerRow;
                int destRow = frameIndex / maxFramesPerRow;
                
                int destX = destCol * frameWidth;
                if (addBorder) destX += destCol * borderThickness;
                
                int destY = destRow * frameHeight;
                if (addBorder) destY += destRow * borderThickness;
                destY = combinedTexture.height - destY - frameHeight; // Adjust for inverted Y in textures
                
                // Copy the frame pixels
                Color[] framePixels = originalSheet.GetPixels(srcX, srcY, frameWidth, frameHeight);
                combinedTexture.SetPixels(destX, destY, frameWidth, frameHeight, framePixels);
                
                // Add frame position info
                framePositions.Add(new FramePosition
                {
                    index = frameIndex,
                    x = destX,
                    y = combinedTexture.height - destY - frameHeight, // Convert back to "normal" Y for metadata
                    width = frameWidth,
                    height = frameHeight,
                    direction = metadata.direction,
                    originalFrameIndex = frame
                });
                
                // Add borders if needed
                if (addBorder)
                {
                    // Add right border if not at the end of a row
                    if (destCol < maxFramesPerRow - 1)
                    {
                        Color[] borderPixels = new Color[frameHeight * borderThickness];
                        for (int i = 0; i < borderPixels.Length; i++)
                        {
                            borderPixels[i] = borderColor;
                        }
                        combinedTexture.SetPixels(destX + frameWidth, destY, borderThickness, frameHeight, borderPixels);
                    }
                    
                    // Add bottom border if not in the last row
                    if (destRow < (frameIndex + 1) / maxFramesPerRow)
                    {
                        int borderY = destRow * frameHeight + frameHeight;
                        if (addBorder) borderY += destRow * borderThickness;
                        borderY = combinedTexture.height - borderY - borderThickness; // Adjust for inverted Y
                        
                        Color[] borderPixels = new Color[frameWidth * borderThickness];
                        for (int i = 0; i < borderPixels.Length; i++)
                        {
                            borderPixels[i] = borderColor;
                        }
                        combinedTexture.SetPixels(destX, borderY, frameWidth, borderThickness, borderPixels);
                    }
                }
                
                frameIndex++;
            }
            
            // Clean up
            Destroy(originalSheet);
            
            // Yield to prevent freezing
            yield return null;
        }
    }
    
    // Sort sheets by direction in a standard order (Down, DownRight, Right, UpRight, Up, UpLeft, Left, DownLeft)
    private List<SpriteSheetMetadata> SortSheetsByDirection(List<SpriteSheetMetadata> sheets)
    {
        // Define a standard direction order
        Dictionary<string, int> directionOrder = new Dictionary<string, int>
        {
            { "Down", 0 },
            { "DownRight", 1 },
            { "Right", 2 },
            { "UpRight", 3 },
            { "Up", 4 },
            { "UpLeft", 5 },
            { "Left", 6 },
            { "DownLeft", 7 }
        };
        
        return sheets.OrderBy(s => 
        {
            if (string.IsNullOrEmpty(s.direction) || !directionOrder.ContainsKey(s.direction))
                return 999; // Put unknown directions at the end
            return directionOrder[s.direction];
        }).ToList();
    }
    
    // Write metadata for a combined sprite sheet
    private void WriteMetadataFile(SpriteSheetMetadata metadata, string filePath)
    {
        string json = JsonUtility.ToJson(metadata, true);
        File.WriteAllText(filePath, json);
        Debug.Log($"Created metadata file: {filePath}");
    }
    
    // Write detailed mapping information
    private void WriteDirectionMappingFile(List<SpriteSheetMetadata> originalSheets, List<FramePosition> framePositions, string filePath)
    {
        // Create a mapping object
        var mapping = new
        {
            totalFrames = framePositions.Count,
            directions = originalSheets.Select(s => s.direction).ToArray(),
            frameMapping = framePositions.Select(fp => new 
            {
                index = fp.index,
                x = fp.x,
                y = fp.y,
                width = fp.width,
                height = fp.height,
                direction = fp.direction,
                originalFrameIndex = fp.originalFrameIndex
            }).ToArray()
        };
        
        // Convert to JSON
        string json = JsonUtility.ToJson(mapping, true);
        File.WriteAllText(filePath, json);
        Debug.Log($"Created mapping file: {filePath}");
    }
    
    // Load a texture from file path
    private Texture2D LoadTexture(string filePath)
    {
        if (!File.Exists(filePath))
        {
            Debug.LogError($"File not found: {filePath}");
            return null;
        }
        
        byte[] fileData = File.ReadAllBytes(filePath);
        Texture2D texture = new Texture2D(2, 2);
        
        if (texture.LoadImage(fileData))
        {
            return texture;
        }
        else
        {
            Debug.LogError($"Failed to load image: {filePath}");
            return null;
        }
    }
    
    // Extended FramePosition class with direction information
    private class FramePosition
    {
        public int index;
        public int x;
        public int y;
        public int width;
        public int height;
        public string direction;
        public int originalFrameIndex;
    }
}

#if UNITY_EDITOR
// Editor helper to create a menu item
public class SpriteSheetCombinerMenu 
{
    [MenuItem("Tools/Animation Creator/Create SpriteSheet Combiner")]
    private static void CreateSpriteSheetCombiner()
    {
        // Check if an instance already exists
        SpriteSheetCombiner existingCombiner = Object.FindObjectOfType<SpriteSheetCombiner>();
        if (existingCombiner != null)
        {
            Debug.Log("SpriteSheet Combiner already exists in the scene.");
            Selection.activeGameObject = existingCombiner.gameObject;
            return;
        }
        
        // Create a new GameObject with the component
        GameObject combinerObject = new GameObject("SpriteSheet Combiner");
        combinerObject.AddComponent<SpriteSheetCombiner>();
        
        // Set it as the active selection
        Selection.activeGameObject = combinerObject;
        
        Debug.Log("SpriteSheet Combiner created successfully.");
    }
}
#endif