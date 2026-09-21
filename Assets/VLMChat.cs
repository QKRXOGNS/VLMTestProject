using UnityEngine;
using Unity.InferenceEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// LFM2.5-VL-450M VLM 모델을 사용한 채팅 시스템
/// Unity Sentis로 로컬 추론 수행
/// </summary>
public class VLMChat : MonoBehaviour
{
    [Header("Model Files (.sentis)")]
    [SerializeField] private ModelAsset embedTokensAsset;
    [SerializeField] private ModelAsset visionEncoderAsset;  
    [SerializeField] private ModelAsset decoderAsset;
    
    [Header("Tokenizer")]
    [SerializeField] private TextAsset tokenizerJson;
    
    [Header("Settings")]
    [SerializeField] private int maxNewTokens = 128;
    [SerializeField] private float temperature = 0.7f;
    [SerializeField] private int imageSize = 384;
    
    [Header("Backend")]
    [SerializeField] private BackendType backendType = BackendType.GPUCompute;
    
    private Worker embedWorker;
    private Worker visionWorker;
    private Worker decoderWorker;
    
    private VLTokenizer tokenizer;
    private bool isInitialized = false;
    
    // 채팅 기록
    private List<string> conversationHistory = new List<string>();
    
    void Start()
    {
        InitializeVLM();
    }
    
    void OnDestroy()
    {
        embedWorker?.Dispose();
        visionWorker?.Dispose();
        decoderWorker?.Dispose();
    }
    
    /// <summary>
    /// VLM 모델 초기화
    /// </summary>
    void InitializeVLM()
    {
        if (embedTokensAsset == null || visionEncoderAsset == null || decoderAsset == null)
        {
            Debug.LogError("[VLMChat] 모든 모델 파일(.sentis)을 할당해주세요!");
            return;
        }
        
        if (tokenizerJson == null)
        {
            Debug.LogError("[VLMChat] Tokenizer JSON 파일을 할당해주세요!");
            return;
        }
        
        try
        {
            // 모델 로드
            var embedModel = ModelLoader.Load(embedTokensAsset);
            var visionModel = ModelLoader.Load(visionEncoderAsset);
            var decoderModel = ModelLoader.Load(decoderAsset);
            
            // Workers 생성
            embedWorker = new Worker(embedModel, backendType);
            visionWorker = new Worker(visionModel, backendType);
            decoderWorker = new Worker(decoderModel, backendType);
            
            // 토크나이저 초기화
            tokenizer = new VLTokenizer(tokenizerJson.text);
            
            isInitialized = true;
            Debug.Log("[VLMChat] VLM 초기화 완료!");
            
            // 샘플 응답
            Debug.Log("[VLMChat] 채팅 준비 완료! Chat() 메서드로 대화를 시작하세요.");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[VLMChat] 초기화 실패: {e.Message}");
            Debug.LogError(e.StackTrace);
        }
    }
    
    /// <summary>
    /// 채팅 메시지 전송 및 응답 받기
    /// </summary>
    public void Chat(string userMessage, Texture2D image, System.Action<string> onResponse)
    {
        if (!isInitialized)
        {
            onResponse?.Invoke("VLM이 초기화되지 않았습니다.");
            return;
        }
        
        StartCoroutine(GenerateResponse(userMessage, image, onResponse));
    }
    
    /// <summary>
    /// 텍스트만으로 채팅 (이미지 없이)
    /// </summary>
    public void ChatText(string userMessage, System.Action<string> onResponse)
    {
        Chat(userMessage, null, onResponse);
    }
    
    /// <summary>
    /// 응답 생성 코루틴
    /// </summary>
    System.Collections.IEnumerator GenerateResponse(string userMessage, Texture2D image, System.Action<string> onResponse)
    {
        // 대화 기록에 사용자 메시지 추가
        conversationHistory.Add($"User: {userMessage}");
        
        // 토큰화
        int[] inputIds = tokenizer.Encode(userMessage);
        
        // ======= 이미지 인코딩 (이미지가 있는 경우) =======
        Tensor<float> imageFeatures = null;
        if (image != null)
        {
            yield return StartCoroutine(EncodeImage(image, (features) => imageFeatures = features));
        }
        
        // ======= 토큰 임베딩 =======
        Tensor<int> inputTensor = new Tensor<int>(new TensorShape(1, inputIds.Length), inputIds);
        embedWorker.SetInput("input_ids", inputTensor);
        embedWorker.Schedule();
        
        // ======= 디코딩 =======
        var outputIds = Decode(decoderWorker, imageFeatures, inputIds);
        
        // 토큰에서 텍스트로 변환
        string response = tokenizer.Decode(outputIds);
        
        conversationHistory.Add($"Assistant: {response}");
        
        // 대화 기록 제한 (최근 10개)
        if (conversationHistory.Count > 20)
        {
            conversationHistory.RemoveRange(0, conversationHistory.Count - 20);
        }
        
        onResponse?.Invoke(response);
    }
    
    /// <summary>
    /// 이미지 인코딩
    /// </summary>
    System.Collections.IEnumerator EncodeImage(Texture2D image, System.Action<Tensor<float>> onComplete)
    {
        // 이미지 전처리
        Tensor<float> inputTensor = PreprocessImage(image);
        
        // 비전 인코더 실행
        visionWorker.SetInput("pixel_values", inputTensor);
        visionWorker.Schedule();
        
        // 출력 가져오기 (PeekOutput은 이미 읽기 가능한 텐서 반환)
        Tensor<float> output = visionWorker.PeekOutput() as Tensor<float>;
        
        onComplete?.Invoke(output);
        
        yield return null;
    }
    
    /// <summary>
    /// 디코딩 (자동 회귀 생성)
    /// </summary>
    List<int> Decode(Worker worker, Tensor<float> imageFeatures, int[] inputIds)
    {
        var outputIds = new List<int>();
        int[] currentIds = inputIds.ToArray();
        
        for (int step = 0; step < maxNewTokens; step++)
        {
            // 입력 텐서 생성
            Tensor<int> inputTensor = new Tensor<int>(
                new TensorShape(1, currentIds.Length), 
                currentIds
            );
            
            worker.SetInput("input_ids", inputTensor);
            
            // 이미지 피처가 있으면 설정
            if (imageFeatures != null)
            {
                worker.SetInput("image_features", imageFeatures);
            }
            
            worker.Schedule();
            
            // 출력에서 다음 토큰 예측
            Tensor<float> logits = worker.PeekOutput() as Tensor<float>;
            
            // 마지막 위치의 로짓 가져오기
            int vocabSize = (int)logits.shape[logits.shape.rank - 1];
            float[] logitsData = logits.DownloadToArray();
            
            int lastIdx = logitsData.Length - vocabSize;
            float[] lastLogits = new float[vocabSize];
            System.Array.Copy(logitsData, lastIdx, lastLogits, 0, vocabSize);
            
            // Temperature sampling
            int nextToken = SampleTopP(lastLogits, temperature);
            
            if (nextToken == tokenizer.EOSToken || outputIds.Count >= maxNewTokens)
            {
                break;
            }
            
            outputIds.Add(nextToken);
            
            // 다음 입력 준비
            var newIds = new List<int>(currentIds);
            newIds.Add(nextToken);
            currentIds = newIds.ToArray();
        }
        
        return outputIds;
    }
    
    /// <summary>
    /// Top-p (nucleus) sampling
    /// </summary>
    int SampleTopP(float[] logits, float p)
    {
        // Softmax
        float maxLogit = logits.Max();
        float sum = 0;
        float[] probs = new float[logits.Length];
        
        for (int i = 0; i < logits.Length; i++)
        {
            probs[i] = Mathf.Exp(logits[i] - maxLogit);
            sum += probs[i];
        }
        
        for (int i = 0; i < probs.Length; i++)
        {
            probs[i] /= sum;
        }
        
        // Top-p sampling
        float cumulative = 0;
        float threshold = p;
        
        for (int i = 0; i < probs.Length; i++)
        {
            int sortIdx = ArgSort(probs, i);
            cumulative += probs[sortIdx];
            
            if (cumulative >= threshold)
            {
                return sortIdx;
            }
        }
        
        return ArgMax(logits);
    }
    
    int ArgMax(float[] arr)
    {
        int maxIdx = 0;
        float maxVal = arr[0];
        for (int i = 1; i < arr.Length; i++)
        {
            if (arr[i] > maxVal)
            {
                maxVal = arr[i];
                maxIdx = i;
            }
        }
        return maxIdx;
    }
    
    int ArgSort(float[] arr, int n)
    {
        // 간단한 partial sort
        float target = arr[n];
        int idx = n;
        for (int i = n + 1; i < arr.Length; i++)
        {
            if (arr[i] > target)
            {
                target = arr[i];
                idx = i;
            }
        }
        return idx;
    }
    
    /// <summary>
    /// 이미지 전처리
    /// </summary>
    Tensor<float> PreprocessImage(Texture2D image)
    {
        // 리사이즈
        Texture2D resized = ResizeTexture(image, imageSize, imageSize);
        
        float[] pixels = new float[3 * imageSize * imageSize];
        Color32[] colors = resized.GetPixels32();
        
        // ImageNet 정규화 ( UINT8 양자화에 맞게 조정 )
        for (int i = 0; i < colors.Length; i++)
        {
            int x = i % imageSize;
            int y = i / imageSize;
            int idx = y * imageSize + x;
            
            // 정규화 (0-255 -> -1 to 1 범위)
            pixels[idx] = (colors[i].r / 255f) * 2f - 1f;
            pixels[idx + imageSize * imageSize] = (colors[i].g / 255f) * 2f - 1f;
            pixels[idx + 2 * imageSize * imageSize] = (colors[i].b / 255f) * 2f - 1f;
        }
        
        Destroy(resized);
        
        return new Tensor<float>(new TensorShape(1, 3, imageSize, imageSize), pixels);
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
    
    /// <summary>
    /// 대화 기록 가져오기
    /// </summary>
    public string GetConversationHistory()
    {
        return string.Join("\n", conversationHistory);
    }
    
    /// <summary>
    /// 대화 기록 지우기
    /// </summary>
    public void ClearHistory()
    {
        conversationHistory.Clear();
    }
}
