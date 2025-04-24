using UnityEngine;

[ExecuteInEditMode]
[RequireComponent(typeof(Camera))]
public class PixelArtEffect : MonoBehaviour
{
    [Header("Pixel Art Settings")]
    [Range(1, 1000)]
    public int pixelSize = 10;
    
    [Header("Outline Settings")]
    public Color outlineColor = Color.black;
    [Range(0, 10)]
    public float outlineThickness = 1f;
    [Range(0, 1)]
    public float outlineThreshold = 0.1f;
    
    private Material pixelArtMaterial;
    private Shader pixelArtShader;
    
    private void Awake()
    {
        // Find the shader
        pixelArtShader = Shader.Find("Custom/PixelArtShader");
        
        // Create a material with the shader
        if (pixelArtShader != null && pixelArtMaterial == null)
        {
            pixelArtMaterial = new Material(pixelArtShader);
            pixelArtMaterial.hideFlags = HideFlags.HideAndDontSave;
        }
    }
    
    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if (pixelArtMaterial == null)
        {
            Graphics.Blit(source, destination);
            return;
        }
        
        // Set the properties
        pixelArtMaterial.SetFloat("_PixelSize", pixelSize);
        pixelArtMaterial.SetColor("_OutlineColor", outlineColor);
        pixelArtMaterial.SetFloat("_OutlineThickness", outlineThickness);
        pixelArtMaterial.SetFloat("_OutlineThreshold", outlineThreshold);
        
        // Apply the material
        Graphics.Blit(source, destination, pixelArtMaterial);
    }
    
    private void OnDisable()
    {
        if (pixelArtMaterial != null)
        {
            DestroyImmediate(pixelArtMaterial);
            pixelArtMaterial = null;
        }
    }
}