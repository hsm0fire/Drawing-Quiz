using RTLTMPro;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class LobbyUI : NetworkBehaviour
{
    public static LobbyUI Instance { get; private set; }

    [Header("UI References - Cards & Status")]
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private TMP_Text player1NameText;
    [SerializeField] private TMP_Text player2NameText;
    [SerializeField] private RTLTextMeshPro matchScoreText;

    [Header("UI References - Avatars & Badges (Optional)")]
    [SerializeField] private GameObject player2QuestionMark; // علامة الاستفهام عند غياب اللاعب الثاني
    [SerializeField] private GameObject player2AvatarIcon;   // أيقونة الشخصية عند انضمامه

    [Header("UI References - Action Buttons")]
    [SerializeField] private Button startGameBtn;
    [SerializeField] private Button leaveRoomBtn;

    [Header("Settings")]
    [SerializeField] private string gameSceneName = "DrawAndGuess";
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    // أسماء اللاعبين المتزامنة في الردهة
    private NetworkVariable<FixedString64Bytes> hostName = new NetworkVariable<FixedString64Bytes>(
        "في الانتظار...", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private NetworkVariable<FixedString64Bytes> clientName = new NetworkVariable<FixedString64Bytes>(
        "في الانتظار...", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // عداد الانتصارات الإجمالي المتزامن عبر الغرفة (Session Match Wins)
    private NetworkVariable<int> hostTotalWins = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private NetworkVariable<int> clientTotalWins = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private void Awake()
    {
        Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        // 1. مراقبة تغير الأسماء والنتائج لمزامنتها لدى الجميع
        hostName.OnValueChanged += (oldVal, newVal) => UpdateLobbyUI();
        clientName.OnValueChanged += (oldVal, newVal) => UpdateLobbyUI();
        hostTotalWins.OnValueChanged += (oldVal, newVal) => UpdateLobbyUI();
        clientTotalWins.OnValueChanged += (oldVal, newVal) => UpdateLobbyUI();

        // 2. زر بدء اللعب (للمستضيف فقط ويُفعل عند اكتمال العدد)
        if (startGameBtn != null)
        {
            startGameBtn.gameObject.SetActive(IsServer);
            startGameBtn.interactable = false;
            startGameBtn.onClick.AddListener(OnStartGameClicked);
        }

        // 3. زر مغادرة الغرفة (متاح للطرفين)
        if (leaveRoomBtn != null)
        {
            leaveRoomBtn.onClick.AddListener(OnLeaveRoomClicked);
        }

        // 4. إرسال اسم اللاعب المحلي فور دخوله
        SubmitPlayerNameServerRpc(PlayerDataHolder.LocalPlayerName);

        // 5. إدارة الأحداث الشبكية على الخادم
        if (IsServer)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
            CheckPlayerCount();
        }

        // استرجاع نتائج الجلسة عند العودة إلى الردهة
        if (IsServer)
        {
            hostTotalWins.Value = SessionScoreManager.HostWins;
            clientTotalWins.Value = SessionScoreManager.ClientWins;

            Debug.Log($"[LobbyUI] Restored Session Score: " +
                $"{hostTotalWins.Value} - {clientTotalWins.Value}");
        }

        UpdateLobbyUI();
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer && NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }

        UpdateLobbyUI();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void SubmitPlayerNameServerRpc(string pName, RpcParams rpcParams = default)
    {
        ulong senderId = rpcParams.Receive.SenderClientId;
        ulong hostId = NetworkManager.Singleton.ConnectedClientsIds[0];

        if (senderId == hostId)
        {
            hostName.Value = pName;
        }
        else
        {
            clientName.Value = pName;
        }

        CheckPlayerCount();
    }

    private void OnClientConnected(ulong clientId)
    {
        CheckPlayerCount();
    }

private void OnClientDisconnected(ulong clientId)
{
    if (!IsServer) return;

    // تصفير نتائج الجلسة عند مغادرة أحد اللاعبين
    SessionScoreManager.ResetSession();

    hostTotalWins.Value = 0;
    clientTotalWins.Value = 0;

    clientName.Value = "في الانتظار...";

    CheckPlayerCount();

    UpdateLobbyUI();
}

    private void CheckPlayerCount()
    {
        if (!IsServer) return;

        int count = NetworkManager.Singleton.ConnectedClientsIds.Count;

        if (count >= 2)
        {
            if (startGameBtn != null) startGameBtn.interactable = true;
        }
        else
        {
            if (startGameBtn != null) startGameBtn.interactable = false;
            clientName.Value = "في الانتظار...";
        }
    }

    private void UpdateLobbyUI()
    {
        // تحديث كروت الأسماء
        if (player1NameText != null)
            player1NameText.text = $"{hostName.Value}";

        bool hasGuest = !clientName.Value.ToString().Contains("في الانتظار");

        if (player2NameText != null)
            player2NameText.text = hasGuest ? $"{clientName.Value}" : "بانتظار لاعب...";

        // تبديل أيقونة وعلامة الاستفهام الخاصة باللاعب الثاني
        if (player2QuestionMark != null) player2QuestionMark.SetActive(!hasGuest);
        if (player2AvatarIcon != null) player2AvatarIcon.SetActive(hasGuest);

        if (matchScoreText != null)
        {
            string guestDisplay = hasGuest ? clientName.Value.ToString() : "الخصم";
            // \u200E تمنع انعكاس الأرقام والشرطة تماماً
            matchScoreText.text =
            $"{hostName.Value} ({hostTotalWins.Value} - {clientTotalWins.Value}) {guestDisplay}";
        }

        // نص الحالة العام
        if (statusText != null)
        {
            statusText.text = hasGuest ? "اكتمل اللاعبون! جاهزون للانطلاق." : "بانتظار انضمام الخصم... (1/2)";
            statusText.color = hasGuest ? new Color(0.2f, 1f, 0.2f) : new Color(1f, 0.85f, 0.2f);
        }
    }

    private void OnStartGameClicked()
    {
        if (!IsServer) return;

        // تشغيل صوت النقر
        if (SoundEffectsManager.Instance != null) SoundEffectsManager.Instance.PlayButtonClick();

        NetworkManager.Singleton.SceneManager.LoadScene(gameSceneName, UnityEngine.SceneManagement.LoadSceneMode.Single);
    }

    /// <summary>
    /// مغادرة الغرفة وإيقاف الاتصال والعودة للقائمة الرئيسية
    /// </summary>
    private void OnLeaveRoomClicked()
    {
        if (SoundEffectsManager.Instance != null) SoundEffectsManager.Instance.PlayButtonClick();

        // تصفير نتائج الجلسة محلياً قبل الخروج
        SessionScoreManager.ResetSession();

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.Shutdown();
        }

        UnityEngine.SceneManagement.SceneManager.LoadScene(mainMenuSceneName);
    }

    /// <summary>
    /// دالة استدعاء لتسجيل فوز جديد لأحد الطرفين (تُستدعى عند انتهاء لعبة الرسم أو الأسئلة)
    /// </summary>
    public void AddWinToHost()
    {
        if (IsServer) hostTotalWins.Value++;
    }

    public void AddWinToClient()
    {
        if (IsServer) clientTotalWins.Value++;
    }
}