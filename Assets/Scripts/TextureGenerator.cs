using UnityEngine;

/// <summary>
/// Generates textures for the watch mode visualization shaders.
/// </summary>
public class TextureGenerator : MonoBehaviour
{
    [Header("Texture Settings")]
    [SerializeField] private int textureSize = 256;
    [SerializeField] private bool generateOnStart = true;
    
    // Texture references
    private Texture2D noiseTexture;
    private Texture2D gradientTexture;
    private Texture2D gridTexture;
    
    private void Start()
    {
        if (generateOnStart)
        {
            GenerateTextures();
        }
    }
    
    public void GenerateTextures()
    {
        // Generate noise texture
        noiseTexture = GenerateNoiseTexture();
        SaveTextureToAssets(noiseTexture, "NoiseTexture");
        
        // Generate gradient texture
        gradientTexture = GenerateGradientTexture();
        SaveTextureToAssets(gradientTexture, "GradientTexture");
        
        // Generate grid texture
        gridTexture = GenerateGridTexture();
        SaveTextureToAssets(gridTexture, "GridTexture");
        
        Debug.Log("[TextureGenerator] Textures generated successfully.");
    }
    
    private Texture2D GenerateNoiseTexture()
    {
        Texture2D texture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);
        
        for (int y = 0; y < textureSize; y++)
        {
            for (int x = 0; x < textureSize; x++)
            {
                float noise = Mathf.PerlinNoise(x / (float)textureSize * 4f, y / (float)textureSize * 4f);
                Color color = new Color(noise, noise, noise, 1f);
                texture.SetPixel(x, y, color);
            }
        }
        
        texture.Apply();
        texture.wrapMode = TextureWrapMode.Repeat;
        texture.filterMode = FilterMode.Bilinear;
        
        return texture;
    }
    
    private Texture2D GenerateGradientTexture()
    {
        Texture2D texture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);
        
        for (int y = 0; y < textureSize; y++)
        {
            for (int x = 0; x < textureSize; x++)
            {
                float gradientX = x / (float)textureSize;
                float gradientY = y / (float)textureSize;
                Color color = new Color(gradientX, gradientY, 0f, 1f);
                texture.SetPixel(x, y, color);
            }
        }
        
        texture.Apply();
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        
        return texture;
    }
    
    private Texture2D GenerateGridTexture()
    {
        Texture2D texture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);
        
        int gridSize = textureSize / 8;
        int lineWidth = Mathf.Max(1, textureSize / 64);
        
        for (int y = 0; y < textureSize; y++)
        {
            for (int x = 0; x < textureSize; x++)
            {
                bool isGridLine = x % gridSize < lineWidth || y % gridSize < lineWidth;
                Color color = isGridLine ? Color.white : new Color(0f, 0f, 0f, 0f);
                texture.SetPixel(x, y, color);
            }
        }
        
        texture.Apply();
        texture.wrapMode = TextureWrapMode.Repeat;
        texture.filterMode = FilterMode.Bilinear;
        
        return texture;
    }
    
    private void SaveTextureToAssets(Texture2D texture, string name)
    {
#if UNITY_EDITOR
        // In the editor, we can save the texture as an asset
        string path = $"Assets/Textures/{name}.asset";
        UnityEditor.AssetDatabase.CreateAsset(texture, path);
        UnityEditor.AssetDatabase.SaveAssets();
        Debug.Log($"[TextureGenerator] Saved texture to {path}");
#endif
    }
}
