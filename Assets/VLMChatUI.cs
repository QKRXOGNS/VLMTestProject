using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// VLM 채팅 UI 관리자
/// 간단한 채팅 인터페이스 제공
/// </summary>
public class VLMChatUI : MonoBehaviour
{
    [Header("VLM Chat Reference")]
    [SerializeField] private VLMChat vlmChat;
    
    [Header("UI Elements")]
    [SerializeField] private TextMeshProUGUI outputText;
    [SerializeField] private TMP_InputField inputField;
    [SerializeField] private Button sendButton;
    [SerializeField] private RawImage displayImage;
    [SerializeField] private Toggle useImageToggle;
    
    [Header("Settings Panel")]
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private Slider temperatureSlider;
    [SerializeField] private TextMeshProUGUI temperatureValue;
    [SerializeField] private Slider maxTokensSlider;
    [SerializeField] private TextMeshProUGUI maxTokensValue;
    [SerializeField] private Dropdown backendDropdown;
    
    private Texture2D currentScreenshot;
    private bool isGenerating = false;
    
    void Start()
    {
        // 버튼 리스너 등록
        sendButton.onClick.AddListener(OnSendMessage);
        inputField.onEndEdit.AddListener(OnSendMessage);
        
        // 설정 이벤트
        temperatureSlider.onValueChanged.AddListener(OnTemperatureChanged);
        maxTokensSlider.onValueChanged.AddListener(OnMaxTokensChanged);
        backendDropdown.onValueChanged.AddListener(OnBackendChanged);
        
        // 초기값 설정
        temperatureValue.text = vlmChat != null ? "0.7" : "N/A";
        maxTokensValue.text = vlmChat != null ? "128" : "N/A";
        
        // 초기 메시지
        outputText.text = "<color=grey>[VLM Chat]</color>\n" +
                         "<color=cyan>안녕하세요! 메시지를 입력하세요.</color>\n" +
                         "<color=grey>설정을 변경하려면 우측 상단 버튼을 클릭하세요.</color>";
    }
    
    void Update()
    {
        // Enter 키로 전송
        if (Input.GetKeyDown(KeyCode.Return) && !string.IsNullOrEmpty(inputField.text))
        {
            OnSendMessage(inputField.text);
        }
        
        // Screenshot 키 (F12)
        if (Input.GetKeyDown(KeyCode.F12))
        {
            CaptureScreen();
        }
    }
    
    /// <summary>
    /// 메시지 전송 (버튼/입력용)
    /// </summary>
    public void OnSendMessage()
    {
        OnSendMessage(inputField.text);
    }
    
    /// <summary>
    /// 메시지 전송
    /// </summary>
    public void OnSendMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message) || isGenerating)
            return;
        
        if (vlmChat == null)
        {
            outputText.text += $"\n<color=red>[오류] VLM Chat이 연결되지 않았습니다.</color>";
            return;
        }
        
        inputField.text = "";
        isGenerating = true;
        
        // 사용자 메시지 표시
        outputText.text += $"\n\n<color=yellow>나:</color> {message}";
        
        // 설정 읽기
        float temp = temperatureSlider.value;
        int maxTokens = (int)maxTokensSlider.value;
        
        // VLM 응답 받기
        Texture2D imageToSend = (useImageToggle != null && useImageToggle.isOn) ? currentScreenshot : null;
        
        vlmChat.Chat(message, imageToSend, (response) =>
        {
            UnityMainThreadDispatcher.Enqueue(() =>
            {
                outputText.text += $"\n<color=cyan>VLM:</color> {response}";
                isGenerating = false;
            });
        });
    }
    
    /// <summary>
    /// 화면 캡처
    /// </summary>
    public void CaptureScreen()
    {
        // 간단한 화면 캡처
        currentScreenshot = ScreenCapture.CaptureScreenshotAsTexture();
        
        if (currentScreenshot != null)
        {
            if (displayImage != null)
            {
                displayImage.texture = currentScreenshot;
                displayImage.gameObject.SetActive(true);
            }
            
            outputText.text += $"\n<color=green>[시스템] 화면 캡처 완료! \"이미지 사용\" 토글을 켜세요.</color>";
        }
        else
        {
            outputText.text += $"\n<color=red>[오류] 화면 캡처 실패</color>";
        }
    }
    
    /// <summary>
    /// 설정 변경: Temperature
    /// </summary>
    void OnTemperatureChanged(float value)
    {
        temperatureValue.text = value.ToString("F2");
    }
    
    /// <summary>
    /// 설정 변경: Max Tokens
    /// </summary>
    void OnMaxTokensChanged(float value)
    {
        maxTokensValue.text = ((int)value).ToString();
    }
    
    /// <summary>
    /// 설정 변경: Backend
    /// </summary>
    void OnBackendChanged(int index)
    {
        if (vlmChat != null)
        {
            outputText.text += $"\n<color=orange>[설정] Backend 변경 시 VLM을 다시 초기화해야 합니다.</color>";
        }
    }
    
    /// <summary>
    /// 대화 기록 지우기
    /// </summary>
    public void ClearHistory()
    {
        outputText.text = "<color=grey>[VLM Chat]</color>\n<color=cyan>대화가 초기화되었습니다.</color>";
        vlmChat?.ClearHistory();
    }
    
    /// <summary>
    /// 현재 스크린샷 가져오기
    /// </summary>
    public Texture2D GetCurrentScreenshot()
    {
        return currentScreenshot;
    }
}

/// <summary>
/// Unity 메인 스레드에서 액션 실행을 위한 유틸리티
/// </summary>
public class UnityMainThreadDispatcher : MonoBehaviour
{
    private static readonly System.Collections.Generic.Queue<System.Action> _queue = new System.Collections.Generic.Queue<System.Action>();
    private static UnityMainThreadDispatcher _instance;
    
    public static UnityMainThreadDispatcher Instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject("UnityMainThreadDispatcher");
                _instance = go.AddComponent<UnityMainThreadDispatcher>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }
    
    public static void Enqueue(System.Action action)
    {
        lock (_queue)
        {
            _queue.Enqueue(action);
        }
    }
    
    void Update()
    {
        Execute();
    }
    
    public static void Execute()
    {
        while (true)
        {
            System.Action action = null;
            lock (_queue)
            {
                if (_queue.Count > 0)
                {
                    action = _queue.Dequeue();
                }
            }
            
            if (action != null)
            {
                action();
            }
            else
            {
                break;
            }
        }
    }
}
