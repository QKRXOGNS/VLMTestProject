using UnityEngine;

using UnityEngine.Networking;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Unity Sentis 2.0을 사용한 VLM(Vision-Language Model) 추론 관리자
/// LiquidAI/LFM2.5-VL-450M 모델의 출력을 처리합니다.
/// 
/// 사용법:
/// 1. Sentis 2.x 패키지 설치 (Window > Package Manager > Sentis)
/// 2. 이 스크립트를 GameObject에 추가
/// 3. modelAsset에 ONNX 파일 할당 (또는 API 서버 모드 사용)
/// </summary>
public class VLMInference : MonoBehaviour
{
    [Header("Model Settings")]
    [SerializeField] private Unity.InferenceEngine.ModelAsset modelAsset;
    [SerializeField] private TextAsset metadataAsset;
    
    [Header("Inference Settings")]
    [SerializeField] private int maxTokens = 200;
    [SerializeField] private float temperature = 0.7f;
    [SerializeField] private int imageSize = 448;
    
    [Header("Server Mode (Alternative)")]
    [SerializeField] private bool useServerMode = true;
    [SerializeField] private string serverUrl = "http://localhost:8000";
    
    private Unity.InferenceEngine.Worker worker;
    private ModelMetadata metadata;
    private bool isInitialized = false;
    
    // === Public Properties ===
    
    public int MaxTokens
    {
        get => maxTokens;
        set => maxTokens = Mathf.Clamp(value, 1, 4096);
    }
    
    public float Temperature
    {
        get => temperature;
        set => temperature = Mathf.Clamp(value, 0f, 2f);
    }
    
    public bool UseServerMode
    {
        get => useServerMode;
        set => useServerMode = value;
    }
    
    public string ServerUrl
    {
        get => serverUrl;
        set => serverUrl = value;
    }
    
    public bool IsInitialized => isInitialized;
    
    // === Unity Lifecycle ===
    
    void Start()
    {
        if (useServerMode)
        {
            Debug.Log("[VLM] Server mode enabled - no local model needed");
            isInitialized = true;
        }
        else
        {
            InitializeLocalModel();
        }
    }
    
    void OnDestroy()
    {
        worker?.Dispose();
    }
    
    // === Local Model Mode ===
    
    void InitializeLocalModel()
    {
        if (modelAsset == null)
        {
            Debug.LogWarning("[VLM] Model asset not assigned. Use server mode.");
            useServerMode = true;
            return;
        }
        
        try
        {
            // Sentis 2.0: ModelLoader.Load 사용
            var model = Unity.InferenceEngine.ModelLoader.Load(modelAsset);
            
            // Sentis 2.0: new Worker(model, backendType)
            worker = new Unity.InferenceEngine.Worker(model, Unity.InferenceEngine.BackendType.CPU);
            
            if (metadataAsset != null)
            {
                metadata = JsonUtility.FromJson<ModelMetadata>(metadataAsset.text);
                imageSize = metadata.image_size;
            }
            
            isInitialized = true;
            Debug.Log("[VLM] Local model loaded successfully");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[VLM] Failed to load model: {e.Message}");
            Debug.Log("[VLM] Falling back to server mode");
            useServerMode = true;
        }
    }
    
    // === Main Inference Methods ===
    
    /// <summary>
    /// 게임 화면(Texture2D)과 텍스트 쿼리를 입력받아 추론 수행
    /// </summary>
    public void Infer(Texture2D gameImage, string query, System.Action<string> onComplete)
    {
        if (useServerMode)
        {
            StartCoroutine(InferServer(gameImage, query, onComplete));
        }
        else
        {
            StartCoroutine(InferLocal(gameImage, query, onComplete));
        }
    }
    
    /// <summary>
    /// 서버 모드 추론
    /// </summary>
    System.Collections.IEnumerator InferServer(Texture2D gameImage, string query, System.Action<string> onComplete)
    {
        // 이미지를 Base64로 인코딩
        byte[] imageBytes = gameImage.EncodeToPNG();
        string base64Image = System.Convert.ToBase64String(imageBytes);
        
        // JSON 요청 생성
        var request = new VLMRequest
        {
            image = base64Image,
            query = query,
            max_tokens = maxTokens,
            temperature = temperature
        };
        
        string jsonBody = JsonUtility.ToJson(request);
        
        using (UnityWebRequest www = new UnityWebRequest(
            serverUrl + "/infer",
            "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonBody);
            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");
            www.timeout = 60;  // 60초 타임아웃
            
            yield return www.SendWebRequest();
            
            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[VLM] Server error: {www.error}");
                onComplete?.Invoke(null);
            }
            else
            {
                string responseText = www.downloadHandler.text;
                var response = JsonUtility.FromJson<VLMResponse>(responseText);
                
                if (response != null && response.success)
                {
                    onComplete?.Invoke(response.response);
                }
                else
                {
                    Debug.LogError($"[VLM] Inference failed: {response?.error}");
                    onComplete?.Invoke(null);
                }
            }
        }
    }
    
    /// <summary>
    /// 로컬 모델 추론 (실험적 - VLM은 복잡해서 서버 모드 권장)
    /// </summary>
    System.Collections.IEnumerator InferLocal(Texture2D gameImage, string query, System.Action<string> onComplete)
    {
        if (!isInitialized || worker == null)
        {
            Debug.LogError("[VLM] Model not initialized");
            onComplete?.Invoke(null);
            yield break;
        }
        
        Debug.LogWarning("[VLM] Local VLM inference is complex. Consider using server mode.");
        onComplete?.Invoke(null);
    }
    
    // === Helper Methods ===
    
    /// <summary>
    /// Unity API 호출용 JSON 파싱
    /// </summary>
    public GameAnalysisResult ParseAPIResponse(string jsonResponse)
    {
        if (string.IsNullOrEmpty(jsonResponse))
            return null;
        
        try
        {
            return JsonUtility.FromJson<GameAnalysisResult>(jsonResponse);
        }
        catch
        {
            Debug.LogWarning("[VLM] Failed to parse response as JSON");
            return null;
        }
    }
    
    /// <summary>
    /// 이미지를 모델 입력 형식으로 전처리
    /// Sentis 2.0: Tensor<float> 사용
    /// </summary>
    Unity.InferenceEngine.Tensor<float> PreprocessImage(Texture2D image)
    {
        // 리사이즈
        Texture2D resized = ResizeTexture(image, imageSize, imageSize);
        
        // 정규화 (ImageNet 통계)
        float[] pixels = new float[3 * imageSize * imageSize];
        Color32[] colors32 = resized.GetPixels32();
        
        for (int i = 0; i < colors32.Length; i++)
        {
            int x = i % imageSize;
            int y = i / imageSize;
            int idx = (y * imageSize + x);
            
            pixels[idx] = (colors32[i].r / 255f - 0.481f) / 0.269f;
            pixels[idx + imageSize * imageSize] = (colors32[i].g / 255f - 0.458f) / 0.261f;
            pixels[idx + 2 * imageSize * imageSize] = (colors32[i].b / 255f - 0.408f) / 0.276f;
        }
        
        Destroy(resized);
        
        // Sentis 2.0: new Tensor<float>(shape, data)
        return new Unity.InferenceEngine.Tensor<float>(new Unity.InferenceEngine.TensorShape(1, 3, imageSize, imageSize), pixels);
    }
    
    Texture2D ResizeTexture(Texture2D source, int width, int height)
    {
        RenderTexture rt = RenderTexture.GetTemporary(width, height, 24);
        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = rt;
        
        Graphics.Blit(source, rt);
        
        Texture2D result = new Texture2D(width, height, TextureFormat.RGB24, false);
        result.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        result.Apply();
        
        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);
        
        return result;
    }
    
    [ContextMenu("Test Inference")]
    void TestInference()
    {
        Texture2D testImage = new Texture2D(256, 256);
        string testQuery = "이 이미지에 무엇이 있나요?";
        
        Infer(testImage, testQuery, (response) =>
        {
            Debug.Log($"[VLM] Response: {response}");
        });
        
        Destroy(testImage);
    }
}

// === Data Classes ===

[System.Serializable]
public class VLMRequest
{
    public string image;
    public string query;
    public int max_tokens;
    public float temperature;
}

[System.Serializable]
public class VLMResponse
{
    public string response;
    public bool success;
    public string error;
}

[System.Serializable]
public class ModelMetadata
{
    public string model_type;
    public string base_model;
    public int max_seq_length;
    public int image_size;
    public InputSchema input_schema;
    public OutputSchema output_schema;
    public Preprocessing preprocessing;
}

[System.Serializable]
public class InputSchema
{
    public TensorInfo images;
    public TensorInfo input_ids;
    public TensorInfo attention_mask;
}

[System.Serializable]
public class OutputSchema
{
    public TensorInfo logits;
}

[System.Serializable]
public class TensorInfo
{
    public int[] shape;
    public string dtype;
}

[System.Serializable]
public class Preprocessing
{
    public ImagePreprocess image;
}

[System.Serializable]
public class ImagePreprocess
{
    public int[] resize;
    public Normalize normalize;
}

[System.Serializable]
public class Normalize
{
    public float[] mean;
    public float[] std;
}

// === Game-Specific Response Classes ===

[System.Serializable]
public class GameAnalysisResult
{
    public string[] detected_objects;
    public PlayerStatus player_status;
    public string recommended_action;
    public List<APIFunction> api_calls;
}

[System.Serializable]
public class PlayerStatus
{
    public int health;
    public int inventory_slots;
    public int level;
    public float position_x;
    public float position_y;
}

[System.Serializable]
public class APIFunction
{
    public string function;
    [System.NonSerialized] public Dictionary<string, object> @params;
}
