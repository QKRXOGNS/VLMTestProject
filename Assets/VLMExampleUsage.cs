using UnityEngine;

/// <summary>
/// Unity VLM 사용 예시 스크립트
/// </summary>
public class VLMExampleUsage : MonoBehaviour
{
    [SerializeField] private VLMInference vlm;
    [SerializeField] private Camera gameCamera;
    
    // === 예시 1: 게임 상태 분석 ===
    
    public void AnalyzeGameState()
    {
        Texture2D screenshot = ScreenCapture.CaptureScreenshotAsTexture();
        
        string query = @"이 게임 화면을 분석하고 다음 정보를 JSON으로 알려줘:
{
    ""detected_objects"": [""오브젝트 목록""],
    ""player_status"": {""health"": 0-100, ""inventory_slots"": 0},
    ""recommended_action"": ""권장 행동"",
    ""api_calls"": [{""function"": ""함수명"", ""params"": {}}]
}";
        
        vlm.Infer(screenshot, query, (response) =>
        {
            if (response != null)
            {
                var result = vlm.ParseAPIResponse(response);
                
                if (result != null)
                {
                    ProcessGameAnalysis(result);
                }
                else
                {
                    Debug.Log($"[VLM] Raw response: {response}");
                }
            }
        });
        
        Destroy(screenshot);
    }
    
    void ProcessGameAnalysis(GameAnalysisResult result)
    {
        Debug.Log($"[VLM] 탐지된 오브젝트: {string.Join(", ", result.detected_objects)}");
        
        if (result.player_status != null)
        {
            Debug.Log($"[VLM] 플레이어 체력: {result.player_status.health}");
            Debug.Log($"[VLM] 인벤토리: {result.player_status.inventory_slots}");
        }
        
        Debug.Log($"[VLM] 권장 행동: {result.recommended_action}");
        
        if (result.api_calls != null)
        {
            foreach (var apiCall in result.api_calls)
            {
                ExecuteAPICall(apiCall);
            }
        }
    }
    
    void ExecuteAPICall(APIFunction apiCall)
    {
        Debug.Log($"[VLM] API 호출: {apiCall.function}");
        
        switch (apiCall.function)
        {
            case "AdjustDifficulty":
                break;
            case "SpawnEnemy":
                break;
            case "ShowUI":
                break;
            case "PlaySound":
                break;
            case "CreateQuest":
                break;
            default:
                Debug.LogWarning($"[VLM] Unknown API: {apiCall.function}");
                break;
        }
    }
    
    // === 예시 2: NPC 대화 ===
    
    public void TalkToNPC(string npcId, string playerMessage)
    {
        Texture2D screenshot = ScreenCapture.CaptureScreenshotAsTexture();
        
        string query = $"이 NPC({npcId})와 대화场景입니다. 플레이어: \"{playerMessage}\" 이 NPC의 대답을 자연스럽게 생성해주세요. 한국어로 응답해주세요.";
        
        vlm.Infer(screenshot, query, (response) =>
        {
            if (response != null)
            {
                Debug.Log($"[NPC] {response}");
            }
        });
        
        Destroy(screenshot);
    }
    
    // === 예시 3: 퀘스트 생성 ===
    
    public void GenerateDynamicQuest()
    {
        var playerData = CollectPlayerData();
        
        Texture2D screenshot = ScreenCapture.CaptureScreenshotAsTexture();
        
        string query = $"현재 게임 상황을 분석하고 플레이어에게 적합한 퀘스트를 생성해주세요. 플레이어 데이터: - 레벨: {playerData.level} - 전투력: {playerData.combatPower} - 완료한 퀘스트: {string.Join(", ", playerData.completedQuests)}";
        
        vlm.Infer(screenshot, query, (response) =>
        {
            if (response != null)
            {
                Debug.Log($"[VLM] 생성된 퀘스트: {response}");
            }
        });
        
        Destroy(screenshot);
    }
    
    PlayerData CollectPlayerData()
    {
        return new PlayerData
        {
            level = 5,
            combatPower = 1200,
            completedQuests = new[] { "tutorial", "first_battle" },
            equipment = new[] { "iron_sword", "leather_armor" }
        };
    }
    
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.T))
        {
            AnalyzeGameState();
        }
        
        if (Input.GetKeyDown(KeyCode.Q))
        {
            GenerateDynamicQuest();
        }
    }
    
    class PlayerData
    {
        public int level;
        public int combatPower;
        public string[] completedQuests;
        public string[] equipment;
    }
}
