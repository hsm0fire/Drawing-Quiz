
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LobbyManager : MonoBehaviour
{
    [Header("Configuration")]
    [SerializeField] private string gameSceneName = "GameSelection";
    [SerializeField] private int requiredPlayers = 2;

    private bool isLoadingScene = false;

    private void Start()
    {
        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("[LobbyManager] NetworkManager not found!");
            return;
        }

        NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientDisconnected;

        // فحص العدد عند بدء السكربت
        CheckPlayerCount();
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= HandleClientDisconnected;
        }
    }

    private void HandleClientConnected(ulong clientId)
    {
        if (!NetworkManager.Singleton.IsServer)
            return;

        Debug.Log(
            $"[LobbyManager] Player connected: {clientId}. " +
            $"Total players: {NetworkManager.Singleton.ConnectedClientsIds.Count}"
        );

        CheckPlayerCount();
    }

    private void HandleClientDisconnected(ulong clientId)
    {
        if (!NetworkManager.Singleton.IsServer)
            return;

        Debug.Log($"[LobbyManager] Player disconnected: {clientId}");

        if (NetworkManager.Singleton.ConnectedClientsIds.Count < requiredPlayers)
        {
            isLoadingScene = false;
        }
    }

    private void CheckPlayerCount()
    {
        if (NetworkManager.Singleton == null ||
            !NetworkManager.Singleton.IsServer ||
            isLoadingScene)
        {
            return;
        }

        int playerCount = NetworkManager.Singleton.ConnectedClientsIds.Count;

        if (playerCount >= requiredPlayers)
        {
            isLoadingScene = true;

            Debug.Log("[LobbyManager] Two players connected. Loading GameSelection...");

            NetworkManager.Singleton.SceneManager.LoadScene(
                gameSceneName,
                LoadSceneMode.Single
            );
        }
    }
}
