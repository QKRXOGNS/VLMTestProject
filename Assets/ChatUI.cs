using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.InputSystem;

/// <summary>
/// VLM 채팅 UI 매니저
/// 명령어 입력, 응답 표시, 설정 관리를 담당합니다.
/// </summary>
public class ChatUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Transform chatContent;
    [SerializeField] private TMP_InputField inputField;
    [SerializeField] private ScrollRect chatScrollRect;
    [SerializeField] private Button sendButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private GameObject settingsPanel;
    
    [Header("Message Prefabs")]
    [SerializeField] private GameObject userMessagePrefab;
    [SerializeField] private GameObject botMessagePrefab;
    [SerializeField] private GameObject systemMessagePrefab;
    
    [Header("Settings")]
    [SerializeField] private Slider temperatureSlider;
    [SerializeField] private TMP_Text temperatureLabel;
    [SerializeField] private Slider maxTokensSlider;
    [SerializeField] private TMP_Text maxTokensLabel;
    [SerializeField] private Toggle serverModeToggle;
    [SerializeField] private TMP_InputField serverUrlInput;
    
    private VLMInference vlmInference;
    private bool isProcessing = false;
    
    void Awake()
    {
        // 자동 레퍼런스 찾기
        AutoFindReferences();
    }
    
    void Start()
    {
        // VLMInference 컴포넌트 찾기
        vlmInference = FindFirstObjectByType<VLMInference>();
        if (vlmInference == null)
        {
            Debug.LogWarning("[ChatUI] VLMInference not found in scene");
        }
        
        // 버튼 이벤트 연결
        sendButton?.onClick.AddListener(OnSendMessage);
        settingsButton?.onClick.AddListener(ToggleSettings);
        inputField?.onSubmit.AddListener(OnInputSubmitted);
        
        // 설정 초기화
        InitializeSettings();
        
        // 시스템 메시지 표시
        AddSystemMessage("VLM 채팅 시스템에 오신 것을 환영합니다!");
        AddSystemMessage("명령어를 입력하거나 질문을 해주세요.");
        AddSystemMessage("설정 버튼을 클릭하여 VLM 파라미터를 조정할 수 있습니다.");
    }
    
    void AutoFindReferences()
    {
        // chatContent 자동 찾기
        if (chatContent == null)
        {
            var content = transform.Find("ChatPanel/ChatScrollView/Viewport/Content");
            if (content != null) chatContent = content;
        }
        
        // inputField 자동 찾기
        if (inputField == null)
        {
            var input = GetComponentInChildren<TMP_InputField>();
            if (input != null) inputField = input;
        }
        
        // chatScrollRect 자동 찾기
        if (chatScrollRect == null)
        {
            var scrollRect = transform.Find("ChatPanel/ChatScrollView")?.GetComponent<ScrollRect>();
            if (scrollRect != null) chatScrollRect = scrollRect;
        }
        
        // sendButton 자동 찾기
        if (sendButton == null)
        {
            var btn = transform.Find("ChatPanel/InputArea/SendButton")?.GetComponent<Button>();
            if (btn != null) sendButton = btn;
        }
        
        // settingsButton 자동 찾기
        if (settingsButton == null)
        {
            var btn = transform.Find("ChatPanel/SettingsButton")?.GetComponent<Button>();
            if (btn != null) settingsButton = btn;
        }
        
        // settingsPanel 자동 찾기
        if (settingsPanel == null)
        {
            var panel = transform.Find("ChatPanel/SettingsPanel")?.gameObject;
            if (panel != null) settingsPanel = panel;
        }
        
        // 프리팹 자동 찾기
        if (userMessagePrefab == null)
        {
            var prefab = GameObject.Find("UserMessagePrefab");
            if (prefab != null) userMessagePrefab = prefab;
        }
        
        if (botMessagePrefab == null)
        {
            var prefab = GameObject.Find("BotMessagePrefab");
            if (prefab != null) botMessagePrefab = prefab;
        }
        
        // 시스템 메시지는 사용자 메시지Prefab 재사용
        systemMessagePrefab = botMessagePrefab;
        
        // 슬라이더/토글 자동 찾기
        if (temperatureSlider == null)
        {
            var slider = transform.Find("ChatPanel/SettingsPanel/TemperatureSlider")?.GetComponent<Slider>();
            if (slider != null) temperatureSlider = slider;
        }
        
        if (maxTokensSlider == null)
        {
            var slider = transform.Find("ChatPanel/SettingsPanel/MaxTokensSlider")?.GetComponent<Slider>();
            if (slider != null) maxTokensSlider = slider;
        }
        
        if (serverModeToggle == null)
        {
            var toggle = transform.Find("ChatPanel/SettingsPanel/ServerModeToggle")?.GetComponent<Toggle>();
            if (toggle != null) serverModeToggle = toggle;
        }
        
        if (serverUrlInput == null)
        {
            var input = transform.Find("ChatPanel/SettingsPanel/ServerUrlInput")?.GetComponent<TMP_InputField>();
            if (input != null) serverUrlInput = input;
        }
        
        // 초기값 설정
        if (temperatureSlider != null)
        {
            temperatureSlider.minValue = 0f;
            temperatureSlider.maxValue = 2f;
            temperatureSlider.value = 0.7f;
        }
        
        if (maxTokensSlider != null)
        {
            maxTokensSlider.minValue = 1f;
            maxTokensSlider.maxValue = 2048f;
            maxTokensSlider.value = 200f;
        }
        
        if (serverModeToggle != null)
        {
            serverModeToggle.isOn = true;
        }
        
        if (serverUrlInput != null)
        {
            serverUrlInput.text = "http://localhost:8000";
        }
        
        Debug.Log("[ChatUI] Auto-find references completed");
    }
    
    void Update()
    {
        // Enter 키로 메시지 전송 (Shift+Enter는 줄바꿈)
        if (Keyboard.current != null && Keyboard.current.enterKey.wasPressedThisFrame && !Keyboard.current.shiftKey.isPressed)
        {
            if (!string.IsNullOrEmpty(inputField?.text) && !isProcessing)
            {
                OnSendMessage();
            }
        }
    }
    
    // === Message Handling ===
    
    void OnInputSubmitted(string text)
    {
        if (!string.IsNullOrEmpty(text) && !isProcessing)
        {
            OnSendMessage();
        }
    }
    
    public void OnSendMessage()
    {
        if (isProcessing) return;
        
        string message = inputField?.text?.Trim();
        if (string.IsNullOrEmpty(message)) return;
        
        // 사용자 메시지 표시
        AddUserMessage(message);
        inputField.text = "";
        
        // 처리 시작
        StartCoroutine(ProcessMessage(message));
    }
    
    System.Collections.IEnumerator ProcessMessage(string message)
    {
        isProcessing = true;
        
        // 로딩 표시
        GameObject loadingMsg = AddBotMessage("...");
        
        // 명령어 처리
        if (message.StartsWith("/"))
        {
            yield return ProcessCommand(message, loadingMsg);
        }
        else
        {
            // 일반 질의는 VLM으로 전송
            yield return ProcessVLMQuery(message, loadingMsg);
        }
        
        isProcessing = false;
    }
    
    // === Command Processing ===
    
    System.Collections.IEnumerator ProcessCommand(string command, GameObject responseObj)
    {
        string[] parts = command.Split(' ', 2);
        string cmd = parts[0].ToLower();
        string args = parts.Length > 1 ? parts[1] : "";
        
        string response = "";
        
        switch (cmd)
        {
            case "/help":
                response = GetHelpText();
                break;
                
            case "/settings":
                ToggleSettings();
                response = "설정 패널을 열었습니다.";
                break;
                
            case "/clear":
                ClearChat();
                response = "채팅을 지웠습니다.";
                break;
                
            case "/temp":
                response = HandleTemperatureCommand(args);
                break;
                
            case "/tokens":
                response = HandleTokensCommand(args);
                break;
                
            case "/server":
                response = HandleServerCommand(args);
                break;
                
            case "/status":
                response = GetStatusText();
                break;
                
            default:
                response = $"알 수 없는 명령어: {cmd}\n /help를 입력하여 사용 가능한 명령어를 확인하세요.";
                break;
        }
        
        // 로딩 메시지를 실제 응답으로 교체
        UpdateBotMessage(responseObj, response);
        ScrollToBottom();
        
        yield return null;
    }
    
    string GetHelpText()
    {
        return @"<b>사용 가능한 명령어:</b>

<b>/help</b> - 이 도움말을 표시합니다
<b>/settings</b> - 설정 패널을 열기/닫기
<b>/clear</b> - 채팅 기록을 지웁니다
<b>/status</b> - 현재 VLM 상태를 표시합니다

<b>설정 명령어:</b>
<b>/temp [0.0-2.0]</b> - Temperature 값 설정
<b>/tokens [숫자]</b> - 최대 토큰 수 설정
<b>/server [URL]</b> - 서버 URL 설정

<b>그 외:</b>
명령어가 아닌 일반 텍스트는 VLM에 질의로 전송됩니다.";
    }
    
    string HandleTemperatureCommand(string args)
    {
        if (string.IsNullOrEmpty(args))
        {
            return $"현재 Temperature: {temperatureSlider?.value ?? 0.7f}";
        }
        
        if (float.TryParse(args, out float temp))
        {
            temp = Mathf.Clamp(temp, 0f, 2f);
            if (temperatureSlider != null)
            {
                temperatureSlider.value = temp;
            }
            UpdateVLMSettings();
            return $"Temperature를 {temp}로 설정했습니다.";
        }
        
        return "잘못된 값입니다. 0.0에서 2.0 사이의 숫자를 입력해주세요.";
    }
    
    string HandleTokensCommand(string args)
    {
        if (string.IsNullOrEmpty(args))
        {
            return $"현재 Max Tokens: {maxTokensSlider?.value ?? 200}";
        }
        
        if (int.TryParse(args, out int tokens))
        {
            tokens = Mathf.Clamp(tokens, 1, 2048);
            if (maxTokensSlider != null)
            {
                maxTokensSlider.value = tokens;
            }
            UpdateVLMSettings();
            return $"Max Tokens를 {tokens}로 설정했습니다.";
        }
        
        return "잘못된 값입니다. 1에서 2048 사이의 숫자를 입력해주세요.";
    }
    
    string HandleServerCommand(string args)
    {
        if (string.IsNullOrEmpty(args))
        {
            return $"현재 Server URL: {serverUrlInput?.text ?? "http://localhost:8000"}";
        }
        
        if (serverUrlInput != null)
        {
            serverUrlInput.text = args;
        }
        UpdateVLMSettings();
        return $"Server URL을 {args}로 설정했습니다.";
    }
    
    string GetStatusText()
    {
        string serverMode = serverModeToggle != null ? 
            (serverModeToggle.isOn ? "ON" : "OFF") : "Unknown";
        string url = serverUrlInput?.text ?? "Not set";
        float temp = temperatureSlider?.value ?? 0.7f;
        int tokens = (int)(maxTokensSlider?.value ?? 200);
        
        return $@"<b>VLM 상태:</b>
• Server Mode: {serverMode}
• Server URL: {url}
• Temperature: {temp}
• Max Tokens: {tokens}
• VLM 연결: {(vlmInference != null ? "✓ 연결됨" : "✗ 없음")}";
    }
    
    // === VLM Query Processing ===
    
    System.Collections.IEnumerator ProcessVLMQuery(string query, GameObject responseObj)
    {
        if (vlmInference == null)
        {
            UpdateBotMessage(responseObj, "오류: VLMInference 컴포넌트를 찾을 수 없습니다.");
            yield break;
        }
        
        string response = null;
        bool completed = false;
        
        // 화면 캡처 (테스트용으로 흰색 텍스처 사용)
        Texture2D testImage = new Texture2D(448, 448);
        Color32[] colors = new Color32[448 * 448];
        for (int i = 0; i < colors.Length; i++) colors[i] = new Color32(128, 128, 128, 255);
        testImage.SetPixels32(colors);
        testImage.Apply();
        
        vlmInference.Infer(testImage, query, (result) =>
        {
            response = result;
            completed = true;
        });
        
        // 타임아웃 대기
        float timeout = 30f;
        while (!completed && timeout > 0)
        {
            yield return new WaitForSeconds(0.1f);
            timeout -= 0.1f;
        }
        
        Destroy(testImage);
        
        if (completed && response != null)
        {
            UpdateBotMessage(responseObj, response);
        }
        else if (timeout <= 0)
        {
            UpdateBotMessage(responseObj, "오류: 응답 시간이 초과되었습니다.");
        }
        else
        {
            UpdateBotMessage(responseObj, "오류: VLM 응답을 받을 수 없습니다.");
        }
        
        ScrollToBottom();
    }
    
    // === UI Methods ===
    
    void ToggleSettings()
    {
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(!settingsPanel.activeSelf);
        }
    }
    
    void InitializeSettings()
    {
        if (temperatureSlider != null)
        {
            temperatureSlider.onValueChanged.AddListener(OnTemperatureChanged);
            OnTemperatureChanged(temperatureSlider.value);
        }
        
        if (maxTokensSlider != null)
        {
            maxTokensSlider.onValueChanged.AddListener(OnMaxTokensChanged);
            OnMaxTokensChanged(maxTokensSlider.value);
        }
        
        if (serverModeToggle != null)
        {
            serverModeToggle.onValueChanged.AddListener(OnServerModeChanged);
        }
        
        if (serverUrlInput != null)
        {
            serverUrlInput.onEndEdit.AddListener(OnServerUrlChanged);
        }
    }
    
    void OnTemperatureChanged(float value)
    {
        if (temperatureLabel != null)
        {
            temperatureLabel.text = $"Temperature: {value:F2}";
        }
        UpdateVLMSettings();
    }
    
    void OnMaxTokensChanged(float value)
    {
        if (maxTokensLabel != null)
        {
            maxTokensLabel.text = $"Max Tokens: {(int)value}";
        }
        UpdateVLMSettings();
    }
    
    void OnServerModeChanged(bool isOn)
    {
        if (serverUrlInput != null)
        {
            serverUrlInput.interactable = isOn;
        }
        UpdateVLMSettings();
    }
    
    void OnServerUrlChanged(string url)
    {
        UpdateVLMSettings();
    }
    
    void UpdateVLMSettings()
    {
        if (vlmInference == null) return;
        
        // VLMInference 설정 업데이트
        if (temperatureSlider != null)
        {
            vlmInference.Temperature = temperatureSlider.value;
        }
        
        if (maxTokensSlider != null)
        {
            vlmInference.MaxTokens = (int)maxTokensSlider.value;
        }
        
        if (serverModeToggle != null)
        {
            vlmInference.UseServerMode = serverModeToggle.isOn;
        }
        
        if (serverUrlInput != null)
        {
            vlmInference.ServerUrl = serverUrlInput.text;
        }
    }
    
    GameObject AddUserMessage(string message)
    {
        if (userMessagePrefab == null || chatContent == null) return null;
        
        GameObject msgObj = Instantiate(userMessagePrefab, chatContent);
        var textComponent = msgObj.GetComponentInChildren<TMP_Text>();
        if (textComponent != null)
        {
            textComponent.text = message;
        }
        
        ScrollToBottom();
        return msgObj;
    }
    
    GameObject AddBotMessage(string message)
    {
        if (botMessagePrefab == null || chatContent == null) return null;
        
        GameObject msgObj = Instantiate(botMessagePrefab, chatContent);
        var textComponent = msgObj.GetComponentInChildren<TMP_Text>();
        if (textComponent != null)
        {
            textComponent.text = message;
        }
        
        ScrollToBottom();
        return msgObj;
    }
    
    void UpdateBotMessage(GameObject msgObj, string newMessage)
    {
        if (msgObj == null) return;
        
        var textComponent = msgObj.GetComponentInChildren<TMP_Text>();
        if (textComponent != null)
        {
            textComponent.text = newMessage;
        }
    }
    
    void AddSystemMessage(string message)
    {
        if (systemMessagePrefab == null || chatContent == null) return;
        
        GameObject msgObj = Instantiate(systemMessagePrefab, chatContent);
        var textComponent = msgObj.GetComponentInChildren<TMP_Text>();
        if (textComponent != null)
        {
            textComponent.text = message;
        }
    }
    
    void ClearChat()
    {
        if (chatContent == null) return;
        
        foreach (Transform child in chatContent)
        {
            Destroy(child.gameObject);
        }
    }
    
    void ScrollToBottom()
    {
        if (chatScrollRect != null)
        {
            Canvas.ForceUpdateCanvases();
            chatScrollRect.verticalNormalizedPosition = 0f;
        }
    }
    
    [ContextMenu("Test UI")]
    void TestUI()
    {
        AddUserMessage("테스트 메시지");
        AddBotMessage("봇 응답 테스트");
    }
}
