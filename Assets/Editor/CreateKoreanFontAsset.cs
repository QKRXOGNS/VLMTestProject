using UnityEngine;
using UnityEditor;
using TMPro;
using UnityEngine.TextCore.LowLevel;

public class CreateKoreanFontAsset
{
    [MenuItem("Tools/Create Korean TMP Font Asset")]
    public static void CreateFontAsset()
    {
        // Use the NanumGothic font file
        string fontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/Korean/NanumGothic.ttf";
        
        Font font = AssetDatabase.LoadAssetAtPath<Font>(fontPath);
        if (font == null)
        {
            Debug.LogError("Font not found at: " + fontPath);
            return;
        }
        
        Debug.Log("Found font: " + font.name);
        
        // Create TMP Font Asset with standard SDF settings
        TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(
            font,      // Source font
            90,        // Point size
            11,        // Atlas padding
            GlyphRenderMode.SDFAA,  // Render mode
            1024,      // Atlas width
            1024,      // Atlas height
            AtlasPopulationMode.Dynamic  // Population mode
        );
        
        if (fontAsset == null)
        {
            Debug.LogError("Failed to create TMP Font Asset - font may not be compatible with FreeType");
            return;
        }
        
        Debug.Log("Created font asset in memory");
        
        string assetPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/Korean/NanumGothic SDF.asset";
        AssetDatabase.CreateAsset(fontAsset, assetPath);
        AssetDatabase.SaveAssets();
        
        Debug.Log("Created TMP Font Asset: " + assetPath);
        
        // Reload
        AssetDatabase.Refresh();
        fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
        
        if (fontAsset == null)
        {
            Debug.LogError("Failed to reload font asset");
            return;
        }
        
        // Now add Korean characters to populate the atlas
        string koreanChars = BuildKoreanCharacterString();
        Debug.Log("Adding " + koreanChars.Length + " Korean characters...");
        
        string missingChars;
        bool success = fontAsset.TryAddCharacters(koreanChars, out missingChars);
        
        if (success)
        {
            Debug.Log("Successfully added all Korean characters!");
        }
        else
        {
            Debug.LogWarning("Some characters could not be added. Missing count: " + (missingChars?.Length ?? 0));
        }
        
        EditorUtility.SetDirty(fontAsset);
        AssetDatabase.SaveAssets();
        
        // Set as fallback for LiberationSans SDF
        string liberationPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
        TMP_FontAsset liberationFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(liberationPath);
        
        if (liberationFont != null)
        {
            var so = new SerializedObject(liberationFont);
            var fallbackProp = so.FindProperty("fallbackFontAssets");
            
            if (fallbackProp != null && fallbackProp.isArray)
            {
                fallbackProp.arraySize = 1;
                fallbackProp.GetArrayElementAtIndex(0).objectReferenceValue = fontAsset;
                so.ApplyModifiedProperties();
                
                EditorUtility.SetDirty(liberationFont);
                AssetDatabase.SaveAssets();
                Debug.Log("Set NanumGothic as fallback for LiberationSans SDF");
            }
        }
        
        Debug.Log("Korean font setup complete!");
    }
    
    private static string BuildKoreanCharacterString()
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        
        // Basic ASCII
        for (int i = 32; i <= 126; i++)
        {
            sb.Append((char)i);
        }
        
        // Common Korean punctuation
        sb.Append("·…‥");
        
        // All Hangul syllables (가-힣: 0xAC00 - 0xD7A3)
        for (int i = 0xAC00; i <= 0xD7A3; i++)
        {
            sb.Append(char.ConvertFromUtf32(i));
        }
        
        return sb.ToString();
    }
}
