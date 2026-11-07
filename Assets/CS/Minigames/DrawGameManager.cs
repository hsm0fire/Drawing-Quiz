using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using TMPro;
using RTLTMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class DrawGameManager : NetworkBehaviour
{
    [Header("UI References - Game")]
    [SerializeField] private TMP_Text timerText;
    [SerializeField] private TMP_Text wordToDrawText;
    [SerializeField] private TMP_InputField guessInputField;
    [SerializeField] private Button submitGuessBtn;
    [SerializeField] private TMP_Text resultNotificationText;
    [SerializeField] private GameObject drawingToolbar;
    [SerializeField] private TMP_Text roundDisplayText;
    [SerializeField] private RTLTextMeshPro scoreDisplayText;

    [Header("UI References - Game Over Panel")]
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private TMP_Text finalWinnerScoreText;
    [SerializeField] private Button backToLobbyBtn;

    [Header("Game References")]
    [SerializeField] private DrawingBoard drawingBoard;

    [Header("Game Rules")]
    [SerializeField] private int maxRounds = 6;
    
    [Header("UI References - Guessing")]
    [SerializeField] private GameObject guessInputGroup;

    [Header("Word Bank")]
    [SerializeField] private List<string> wordsList = new List<string>
    {
        "سيارة", "بيت", "شجرة", "كرة", "طائرة", "تفاحة", "ساعة", "شمس", "قلم", "سمكة"
    };

    // مزامنة الأسماء
    private NetworkVariable<FixedString64Bytes> hostPlayerName = new NetworkVariable<FixedString64Bytes>(
        "المستضيف", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private NetworkVariable<FixedString64Bytes> clientPlayerName = new NetworkVariable<FixedString64Bytes>(
        "الضيف", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // الكلمة والمؤقت والدور
    private NetworkVariable<FixedString64Bytes> currentSecretWord = new NetworkVariable<FixedString64Bytes>(
        "", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private NetworkVariable<float> timeRemaining = new NetworkVariable<float>(
        60f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private NetworkVariable<ulong> currentDrawerClientId = new NetworkVariable<ulong>(
        999, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private NetworkVariable<FixedString128Bytes> resultMessage = new NetworkVariable<FixedString128Bytes>(
        "", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // نظام النقاط
    private NetworkVariable<int> hostScore = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private NetworkVariable<int> clientScore = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private NetworkVariable<int> currentRound = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private bool gameActive = false;
    private Coroutine hideNotificationCoroutine;
    private Coroutine nextRoundCoroutine;

    public override void OnNetworkSpawn()
    {
        if (submitGuessBtn != null) submitGuessBtn.onClick.AddListener(OnSubmitGuess);
        if (guessInputField != null) guessInputField.onSubmit.AddListener((val) => OnSubmitGuess());

        if (backToLobbyBtn != null)
        {
            backToLobbyBtn.onClick.AddListener(OnBackToLobbyBtnPressed);
        }

        if (gameOverPanel != null) gameOverPanel.SetActive(false);

        if (resultNotificationText != null)
        {
            resultNotificationText.text = "";
            resultNotificationText.gameObject.SetActive(false);
        }

        // تسجيل اسم اللاعب المحلي
        RegisterPlayerNameServerRpc(PlayerDataHolder.LocalPlayerName);

        // مراقبة التغيرات
        timeRemaining.OnValueChanged += (o, n) =>
        {
            if (timerText != null) timerText.text = $"التوقيت: ({Mathf.CeilToInt(n)}s)";
        };

        resultMessage.OnValueChanged += (o, n) =>
        {
            if (resultNotificationText != null)
            {
                string msg = n.ToString();
                if (string.IsNullOrEmpty(msg))
                {
                    resultNotificationText.text = "";
                    resultNotificationText.gameObject.SetActive(false);
                }
                else
                {
                    resultNotificationText.text = msg;
                    resultNotificationText.color = msg.Contains("صحيحة") ? Color.green : new Color(1f, 0.5f, 0f);
                    resultNotificationText.gameObject.SetActive(true);
                }
            }
        };

        hostScore.OnValueChanged += (o, n) => UpdateScoreUI();
        clientScore.OnValueChanged += (o, n) => UpdateScoreUI();
        currentRound.OnValueChanged += (o, n) => UpdateScoreUI();
        hostPlayerName.OnValueChanged += (o, n) => UpdateScoreUI();
        clientPlayerName.OnValueChanged += (o, n) => UpdateScoreUI();

        currentDrawerClientId.OnValueChanged += (o, n) => UpdatePlayerRoleUI();
        currentSecretWord.OnValueChanged += (o, n) => UpdatePlayerRoleUI();

        UpdatePlayerRoleUI();
        UpdateScoreUI();

        if (IsServer)
        {
            NetworkManager.Singleton.SceneManager.OnLoadEventCompleted += OnSceneLoadCompleted;
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer && NetworkManager.Singleton != null && NetworkManager.Singleton.SceneManager != null)
        {
            NetworkManager.Singleton.SceneManager.OnLoadEventCompleted -= OnSceneLoadCompleted;
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RegisterPlayerNameServerRpc(string playerName, RpcParams rpcParams = default)
    {
        ulong sender = rpcParams.Receive.SenderClientId;
        if (sender == NetworkManager.Singleton.ConnectedClientsIds[0])
        {
            hostPlayerName.Value = playerName;
        }
        else
        {
            clientPlayerName.Value = playerName;
        }
    }

    private void OnSceneLoadCompleted(string sceneName, UnityEngine.SceneManagement.LoadSceneMode loadSceneMode, List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
    {
        StartNewRound();
    }

    private void Update()
    {
        if (!IsServer || !gameActive) return;

        if (timeRemaining.Value > 0)
        {
            timeRemaining.Value -= Time.deltaTime;
        }
        else
        {
            EndRound(false);
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void SubmitGuessServerRpc(string guess, RpcParams rpcParams = default)
    {
        if (!gameActive) return;

        ulong senderId = rpcParams.Receive.SenderClientId;
        // لا يمكن للرسام التخمين
        if (senderId == currentDrawerClientId.Value) return;

        string cleanGuess = CleanArabicText(guess);
        string targetWord = CleanArabicText(currentSecretWord.Value.ToString());

        if (cleanGuess == targetWord)
        {
            ulong hostId = NetworkManager.Singleton.ConnectedClientsIds[0];

            if (senderId == hostId) hostScore.Value += 1;
            else clientScore.Value += 1;

            resultMessage.Value = $"إجابة صحيحة! الكلمة هي: {currentSecretWord.Value}";
            
            // تشغيل صوت النجاح للجميع
            PlaySuccessSoundClientRpc();

            // إنهاء الجولة مرة واحدة فقط
            EndRound(true);
        }
        else
        {
            NotifyWrongGuessClientRpc(senderId);
        }
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void NotifyWrongGuessClientRpc(ulong targetClientId)
    {
        if (NetworkManager.Singleton.LocalClientId == targetClientId)
        {
            if (SoundEffectsManager.Instance != null)
            {
                SoundEffectsManager.Instance.PlayWrong();
            }
            ShowTemporaryNotification("إجابة خاطئة، حاول مجدداً!", Color.red, 1.5f);
        }
    }

    private void ShowTemporaryNotification(string msg, Color color, float duration)
    {
        if (resultNotificationText == null) return;

        if (hideNotificationCoroutine != null)
            StopCoroutine(hideNotificationCoroutine);

        resultNotificationText.text = msg;
        resultNotificationText.color = color;
        resultNotificationText.gameObject.SetActive(true);

        hideNotificationCoroutine = StartCoroutine(HideNotificationAfterDelay(duration));
    }

    private IEnumerator HideNotificationAfterDelay(float duration)
    {
        yield return new WaitForSeconds(duration);
        if (resultNotificationText != null && gameActive)
        {
            resultNotificationText.gameObject.SetActive(false);
        }
    }

    private void StartNewRound()
    {
        var connectedClients = NetworkManager.Singleton.ConnectedClientsIds;
        if (connectedClients.Count == 0) return;

        currentRound.Value++;

        if (currentRound.Value > maxRounds)
        {
            ShowGameOverScreen();
            return;
        }

        resultMessage.Value = "";
        string chosenWord = wordsList[Random.Range(0, wordsList.Count)];
        currentSecretWord.Value = chosenWord;
        timeRemaining.Value = 60f;
        gameActive = true;

        // تبديل الأدوار بالترتيب الدقيق: جولة 1 (لاعب 0)، جولة 2 (لاعب 1)، جولة 3 (لاعب 0)...
        int drawerIndex = (currentRound.Value - 1) % connectedClients.Count;
        currentDrawerClientId.Value = connectedClients[drawerIndex];
    }

    private void EndRound(bool guessedSuccessfully)
    {
        if (!gameActive) return; // حماية ضد الاستدعاء المزدوج
        gameActive = false;

        if (!guessedSuccessfully)
        {
            PlayRoundEndSoundClientRpc();

            // عند انتهاء الوقت دون تخمين: نقطة للرسام
            ulong hostId = NetworkManager.Singleton.ConnectedClientsIds[0];
            if (currentDrawerClientId.Value == hostId)
            {
                hostScore.Value += 1;
            }
            else
            {
                clientScore.Value += 1;
            }

            resultMessage.Value = $"انتهى الوقت! الكلمة كانت: {currentSecretWord.Value}";
        }

        if (nextRoundCoroutine != null) StopCoroutine(nextRoundCoroutine);
        nextRoundCoroutine = StartCoroutine(WaitAndNextRoundCoroutine(3.5f));
    }

    private IEnumerator WaitAndNextRoundCoroutine(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (IsServer)
        {
            StartNewRound();
        }
    }

    private void ShowGameOverScreen()
    {
        gameActive = false;
        resultMessage.Value = "";

        string summary;
        int p1Score = hostScore.Value;
        int p2Score = clientScore.Value;
        string p1Name = hostPlayerName.Value.ToString();
        string p2Name = clientPlayerName.Value.ToString();

        // تسجيل الفوز للجلسة الحالية
        if (p1Score > p2Score)
        {
            SessionScoreManager.AddHostWin();
        }
        else if (p2Score > p1Score)
        {
            SessionScoreManager.AddClientWin();
        }

        // صياغة تفصل النتائج بوضوح تام لمنع انقلاب الأرقام لغوياً في TMP
        string scoreLine = $"{p1Name} ({p1Score})  -  ({p2Score}) {p2Name}";

        if (p1Score > p2Score)
            summary = $"الفائز: {p1Name}!\n\n{scoreLine}";
        else if (p2Score > p1Score)
            summary = $"الفائز: {p2Name}!\n\n{scoreLine}";
        else
            summary = $"تعادل بين الطرفين!\n\n{scoreLine}";

        ShowGameOverClientRpc(summary);
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void ShowGameOverClientRpc(string summary)
    {
        // 1. مسح خطوط الرسم فوراً وإخفاء اللوحة حتى لا تظهر أمام أو خلف نافذة النهاية
        if (drawingBoard != null)
        {
            drawingBoard.ClearBoardLocal();
            drawingBoard.gameObject.SetActive(false);
        }

        // 2. إخفاء حقل الإدخال وشريط أدوات الرسم
        if (guessInputGroup != null)
        {
            guessInputGroup.SetActive(false);
        }
        else
        {
            if (guessInputField != null) guessInputField.gameObject.SetActive(false);
            if (submitGuessBtn != null) submitGuessBtn.gameObject.SetActive(false);
        }

        if (drawingToolbar != null)
        {
            drawingToolbar.SetActive(false);
        }

        // 3. إظهار نافذة النهاية
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
            if (finalWinnerScoreText != null)
            {
                finalWinnerScoreText.text = summary;
            }
        }
    }

    private void UpdateScoreUI()
    {
        int displayRound = Mathf.Min(currentRound.Value, maxRounds);
        
        if (roundDisplayText != null)
        {
            roundDisplayText.text = $"الجولة: {displayRound}/{maxRounds}";
        }

        if (scoreDisplayText != null)
        {
            // استخدام أقواس حول النقاط لضمان ثبات الترتيب مهما كانت لغة الواجهة
            scoreDisplayText.text =
            $"{hostPlayerName.Value} ({hostScore.Value} - {clientScore.Value}) {clientPlayerName.Value}";
        }
    }

    private void UpdatePlayerRoleUI()
    {
        string word = currentSecretWord.Value.ToString();
        if (string.IsNullOrEmpty(word)) return;

        bool isMeDrawer = (NetworkManager.Singleton.LocalClientId == currentDrawerClientId.Value);

        if (drawingToolbar != null)
        {
            drawingToolbar.SetActive(isMeDrawer);
        }

        if (drawingBoard != null)
        {
            drawingBoard.isDrawer = isMeDrawer;
            // مسح اللوحة تلقائياً عند بداية دور جديد
            drawingBoard.ClearBoardLocal();
        }

        if (guessInputGroup != null)
        {
            guessInputGroup.SetActive(!isMeDrawer);
        }

        if (isMeDrawer)
        {
            wordToDrawText.text = $"كلمتك هي: {word}";
            if (guessInputField != null) guessInputField.gameObject.SetActive(false);
            if (submitGuessBtn != null) submitGuessBtn.gameObject.SetActive(false);
        }
        else
        {
            wordToDrawText.text = "خمّن ماذا يرسم الطرف الآخر...";
            if (guessInputField != null)
            {
                guessInputField.gameObject.SetActive(true);
                guessInputField.text = "";
            }
            if (submitGuessBtn != null) submitGuessBtn.gameObject.SetActive(true);
        }
    }

    private void OnSubmitGuess()
    {
        if (guessInputField != null && !string.IsNullOrWhiteSpace(guessInputField.text))
        {
            SubmitGuessServerRpc(guessInputField.text);
            guessInputField.text = "";
        }
    }

    private void OnBackToLobbyBtnPressed()
    {
        RequestReturnToLobbyServerRpc();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RequestReturnToLobbyServerRpc()
    {
        NetworkManager.Singleton.SceneManager.LoadScene("GameLobby", UnityEngine.SceneManagement.LoadSceneMode.Single);
    }

    private string CleanArabicText(string text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        string cleaned = Regex.Replace(text, @"[\u200B-\u200F\uFEFF]", "");
        return cleaned.Trim().ToLower();
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void PlaySuccessSoundClientRpc()
    {
        if (SoundEffectsManager.Instance != null)
        {
            SoundEffectsManager.Instance.PlaySuccess();
        }
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void PlayRoundEndSoundClientRpc()
    {
        if (SoundEffectsManager.Instance != null)
        {
            SoundEffectsManager.Instance.PlayRoundEnd();
        }
    }
}