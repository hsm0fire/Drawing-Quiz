using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LobbyManager : MonoBehaviour
{
    [Header("Configuration")]
    [SerializeField] private string gameSceneName = "GameLobby";
    [SerializeField] private int requiredPlayers = 2;

    private void Start()
    {
        // الاشتراك في أحداث دخول وخروج اللاعبين عبر الشبكة
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;
        }
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected;
        }
    }

    private void HandleClientConnected(ulong clientId)
    {
        // فقط المضيف (Host/Server) هو المخول بإصدار أمر تحميل المشهد للجميع
        if (!NetworkManager.Singleton.IsServer) return;

        Debug.Log($"[LobbyManager] Player connected! Total players: {NetworkManager.Singleton.ConnectedClientsIds.Count}");

        // عند اكتمال عدد اللاعبين المطلوب (2 لاعبين)
        if (NetworkManager.Singleton.ConnectedClientsIds.Count >= requiredPlayers)
        {
            Debug.Log("[LobbyManager] Both players connected! Loading next scene...");
            
            // تحميل المشهد المتزامن لجميع المتصلين تلقائياً
            NetworkManager.Singleton.SceneManager.LoadScene(gameSceneName, LoadSceneMode.Single);
        }
    }
}