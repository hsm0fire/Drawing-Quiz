using System;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using RelayService = Unity.Services.Relay.RelayService;
using RelayServiceException = Unity.Services.Relay.RelayServiceException;
using Allocation = Unity.Services.Relay.Models.Allocation;
using JoinAllocation = Unity.Services.Relay.Models.JoinAllocation;
using UnityEngine;

public class RelayManager : MonoBehaviour
{
    public static RelayManager Instance { get; private set; }

    // حدث اختياري لتنبيه الواجهات عند جاهزية تسجيل الدخول
    public event Action OnAuthenticationComplete;
    public bool IsAuthenticated { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private async void Start()
    {
        await InitializeServicesAsync();
    }

    private async Task InitializeServicesAsync()
    {
        try
        {
            // تهيئة خدمات يونيتي السحابية
            await UnityServices.InitializeAsync();

            // تسجيل دخول مجهول لتوليد PlayerId فريد لكل جهاز
            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
                Debug.Log($"[RelayManager] Signed in as: {AuthenticationService.Instance.PlayerId}");
            }

            IsAuthenticated = true;
            OnAuthenticationComplete?.Invoke();
        }
        catch (Exception e)
        {
            Debug.LogError($"[RelayManager] Initialization failed: {e.Message}");
        }
    }

    /// <summary>
    /// ينشئ غرفة عبر Relay ويعيد كود الانضمام (Join Code) المكون من 6 خانات.
    /// </summary>
    public async Task<string> CreateRelayRoom(int maxPlayers = 2)
    {
        if (!IsAuthenticated)
        {
            Debug.LogError("[RelayManager] Cannot create room: Services not initialized.");
            return null;
        }

        try
        {
            // حجز مكان للاعبين الإضافيين (maxPlayers - 1 لأن الـ Host محلي)
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(maxPlayers - 1);

            // استخراج كود الانضمام
            string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            // تمرير البيانات المباشرة للـ UnityTransport عبر RelayServerData المعتمد
            NetworkManager.Singleton.GetComponent<UnityTransport>().SetHostRelayData(
    allocation.RelayServer.IpV4,
    (ushort)allocation.RelayServer.Port,
    allocation.AllocationIdBytes,
    allocation.Key,
    allocation.ConnectionData
);

            // تشغيل السيرفر كمضيف (Host)
            NetworkManager.Singleton.StartHost();

            Debug.Log($"[RelayManager] Room created successfully! Join Code: {joinCode}");
            return joinCode;
        }
        catch (RelayServiceException e)
        {
            Debug.LogError($"[RelayManager] Error creating Relay room: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// ينضم لغرفة موجودة باستخدام كود الانضمام.
    /// </summary>
    public async Task<bool> JoinRelayRoom(string joinCode)
    {
        if (!IsAuthenticated)
        {
            Debug.LogError("[RelayManager] Cannot join room: Services not initialized.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(joinCode))
        {
            Debug.LogError("[RelayManager] Join code is empty.");
            return false;
        }

        try
        {
            // طلب معلومات الاتصال الخاصة بالكود المدخل
            JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode.Trim().ToUpper());

            // تمرير إعدادات الاتصال إلى Transport للعميل (Client)
            NetworkManager.Singleton.GetComponent<UnityTransport>().SetClientRelayData(
    joinAllocation.RelayServer.IpV4,
    (ushort)joinAllocation.RelayServer.Port,
    joinAllocation.AllocationIdBytes,
    joinAllocation.Key,
    joinAllocation.ConnectionData,
    joinAllocation.HostConnectionData
);

            // بدء اتصال العميل
            bool success = NetworkManager.Singleton.StartClient();
            Debug.Log($"[RelayManager] Joining room with code {joinCode} -> Result: {success}");
            return success;
        }
        catch (RelayServiceException e)
        {
            Debug.LogError($"[RelayManager] Error joining Relay room: {e.Message}");
            return false;
        }
    }
}