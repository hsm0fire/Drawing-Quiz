using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameSelectionManager : NetworkBehaviour
{
    // معرفات الألعاب
    public const int DrawAndGuessGameId = 1;
    public const int QuestionChallengeGameId = 2;
    public const int ReactionTestGameId = 3;
    public const int MemoryMatchGameId = 4;

    // حالات الاختيار
    public const int StateIdle = 0;
    public const int StateWaitingForGuest = 1;
    public const int StateRejected = 2;
    public const int StateLoadingGame = 3;

    [Header("Scene Settings")]
    [SerializeField] private string gameLobbySceneName = "GameLobby";


// أسماء اللاعبين المتزامنة بين المضيف والضيف
private NetworkVariable<FixedString64Bytes> hostName =
    new NetworkVariable<FixedString64Bytes>(
        "اسم اللاعب الأول",
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

private NetworkVariable<FixedString64Bytes> guestName =
    new NetworkVariable<FixedString64Bytes>(
        "اسم اللاعب الثاني",
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

// واجهات قراءة الأسماء
public string HostName => hostName.Value.ToString();
public string GuestName => guestName.Value.ToString();

public NetworkVariable<FixedString64Bytes> HostNameVariable => hostName;
public NetworkVariable<FixedString64Bytes> GuestNameVariable => guestName;
    // اللعبة المختارة: -1 يعني لا توجد لعبة مختارة
    private NetworkVariable<int> selectedGameId =
        new NetworkVariable<int>(
            -1,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    // حالة عملية الاختيار
    private NetworkVariable<int> selectionState =
        new NetworkVariable<int>(
            StateIdle,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    // واجهة قراءة القيم من GameSelectionUI
    public int SelectedGameId => selectedGameId.Value;
    public int SelectionState => selectionState.Value;

    public NetworkVariable<int> SelectedGameIdVariable => selectedGameId;
    public NetworkVariable<int> SelectionStateVariable => selectionState;

    public override void OnNetworkSpawn()
{
    if (IsServer)
    {
        NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientDisconnected;

        // تعيين اسم المضيف
        hostName.Value = GetSafePlayerName(PlayerDataHolder.LocalPlayerName);
        guestName.Value = "بانتظار اللاعب الثاني...";
    }
    else
    {
        // إرسال اسم الضيف إلى الخادم
        SubmitPlayerNameServerRpc(PlayerDataHolder.LocalPlayerName);
    }

    Debug.Log("[GameSelectionManager] Network manager spawned.");
}

[Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
private void SubmitPlayerNameServerRpc(
    string playerName,
    RpcParams rpcParams = default)
{
    ulong senderId = rpcParams.Receive.SenderClientId;

    // لا نقبل الاسم إلا من لاعب متصل
    if (!NetworkManager.Singleton.ConnectedClients.ContainsKey(senderId))
        return;

    string safeName = GetSafePlayerName(playerName);

    if (senderId == NetworkManager.ServerClientId)
    {
        hostName.Value = safeName;
    }
    else
    {
        guestName.Value = safeName;
    }

    Debug.Log($"[GameSelectionManager] Player {senderId} name: {safeName}");
}

private string GetSafePlayerName(string playerName)
{
    if (string.IsNullOrWhiteSpace(playerName))
        return "لاعب";

    playerName = playerName.Trim();

    if (playerName.Length > 60)
        playerName = playerName.Substring(0, 60);

    return playerName;
}

    public override void OnNetworkDespawn()
    {
        if (IsServer && NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback -= HandleClientDisconnected;
        }
    }

    // يستدعيها المضيف عندما يختار لعبة من الواجهة
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void RequestGameSelectionServerRpc(
        int gameId,
        RpcParams rpcParams = default)
    {
        ulong senderId = rpcParams.Receive.SenderClientId;

        // التأكد أن الطلب صادر من المضيف
        if (senderId != NetworkManager.ServerClientId)
        {
            Debug.LogWarning("[GameSelectionManager] A non-host tried to select a game.");
            return;
        }

        // لا نسمح بالاختيار إلا عند وجود لاعبين اثنين
        if (NetworkManager.Singleton.ConnectedClientsIds.Count != 2)
        {
            Debug.LogWarning("[GameSelectionManager] Exactly two players are required.");
            return;
        }

        // لا نقبل إلا الألعاب التي تم تجهيز مشاهدها فعلياً
        if (!TryGetSceneName(gameId, out string sceneName))
        {
            Debug.LogWarning($"[GameSelectionManager] Game ID {gameId} is not available.");
            return;
        }

        // لا نسمح بتغيير الاختيار أثناء تحميل لعبة
        if (selectionState.Value == StateLoadingGame)
            return;

        selectedGameId.Value = gameId;
        selectionState.Value = StateWaitingForGuest;

        Debug.Log($"[GameSelectionManager] Host selected game: {sceneName}");
    }

    // يستدعيها الضيف عند الضغط على موافقة أو رفض
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void RespondToGameSelectionServerRpc(
        bool accepted,
        RpcParams rpcParams = default)
    {
        ulong senderId = rpcParams.Receive.SenderClientId;

        // لا يمكن للمضيف إرسال موافقة الضيف
        if (senderId == NetworkManager.ServerClientId)
        {
            Debug.LogWarning("[GameSelectionManager] Host cannot respond as guest.");
            return;
        }

        // التأكد من أن اللاعب المرسل لا يزال متصلاً
        if (!NetworkManager.Singleton.ConnectedClients.ContainsKey(senderId))
            return;

        // يجب أن تكون هناك عملية اختيار تنتظر رد الضيف
        if (selectionState.Value != StateWaitingForGuest)
            return;

        // يجب أن يكون هناك لاعبان بالضبط
        if (NetworkManager.Singleton.ConnectedClientsIds.Count != 2)
            return;

        if (!accepted)
        {
            selectionState.Value = StateRejected;
            Debug.Log("[GameSelectionManager] Guest rejected the selected game.");
            return;
        }

        if (!TryGetSceneName(selectedGameId.Value, out string sceneName))
        {
            Debug.LogError("[GameSelectionManager] Invalid selected game.");
            selectionState.Value = StateIdle;
            selectedGameId.Value = -1;
            return;
        }

        // التأكد من أن المشهد موجود ضمن Build Settings
        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError($"[GameSelectionManager] Scene not found in Build Settings: {sceneName}");
            selectionState.Value = StateIdle;
            selectedGameId.Value = -1;
            return;
        }

        selectionState.Value = StateLoadingGame;

        Debug.Log($"[GameSelectionManager] Guest accepted. Loading: {sceneName}");

        // الخادم وحده يحمل المشهد لجميع اللاعبين
        NetworkManager.Singleton.SceneManager.LoadScene(
            sceneName,
            LoadSceneMode.Single
        );
    }

    // قائمة المشاهد المسموح بتحميلها
    private bool TryGetSceneName(int gameId, out string sceneName)
    {
        switch (gameId)
        {
            case DrawAndGuessGameId:
                sceneName = "DrawAndGuess";
                return true;

            // نضيف المشاهد الأخرى هنا عندما تصبح جاهزة
            // case CodeBreakerGameId:
            //     sceneName = "CodeBreaker";
            //     return true;

            // case ReactionTestGameId:
            //     sceneName = "ReactionTest";
            //     return true;

            // case MemoryMatchGameId:
            //     sceneName = "MemoryMatch";
            //     return true;

            default:
                sceneName = string.Empty;
                return false;
        }
    }

    // إعادة الاختيار إلى الحالة الافتراضية عند مغادرة أحد اللاعبين
    private void HandleClientDisconnected(ulong clientId)
    {
        if (!IsServer)
            return;

        if (NetworkManager.Singleton.ConnectedClientsIds.Count < 2)
        {
            selectedGameId.Value = -1;
            selectionState.Value = StateIdle;
            guestName.Value = "بانتظار اللاعب الثاني...";

            Debug.Log("[GameSelectionManager] A player disconnected. Selection reset.");
        }
    }
}
