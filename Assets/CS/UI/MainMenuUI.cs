using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MainMenuUI : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private TMP_InputField playerNameInput;
    [SerializeField] private Button createRoomBtn;
    [SerializeField] private Button joinRoomBtn;
    [SerializeField] private TMP_InputField joinCodeInput;
    [SerializeField] private TMP_Text roomCodeDisplay;
    [SerializeField] private Button copyRoomCodeBtn; // زر نسخ الكود (يمكن وضعه على نفس كائن النص)
    [SerializeField] private TMP_Text statusText;

    private const string PLAYER_NAME_KEY = "SavedPlayerName";
    private string generatedRoomCode = "";
    private Coroutine copyNoticeCoroutine;

    private void Start()
    {
        // استرجاع الاسم المحفوظ مسبقاً
        string savedName = PlayerPrefs.GetString(PLAYER_NAME_KEY, "لاعب " + Random.Range(100, 999));
        if (playerNameInput != null)
        {
            playerNameInput.text = savedName;
        }

        createRoomBtn.onClick.AddListener(OnCreateRoomClicked);
        joinRoomBtn.onClick.AddListener(OnJoinRoomClicked);

        // تفعيل زر النسخ
        if (copyRoomCodeBtn != null)
        {
            copyRoomCodeBtn.onClick.AddListener(OnCopyRoomCodeClicked);
            copyRoomCodeBtn.interactable = false; // معطل حتى يتم إنشاء كود
        }
    }

    private void SaveCurrentPlayerName()
    {
        string chosenName = playerNameInput != null && !string.IsNullOrWhiteSpace(playerNameInput.text)
            ? playerNameInput.text.Trim()
            : "لاعب " + Random.Range(100, 999);

        PlayerPrefs.SetString(PLAYER_NAME_KEY, chosenName);
        PlayerPrefs.Save();
        
        // حفظ الاسم في كلاس وسيط ثابت لقراءته في المشاهد التالية
        PlayerDataHolder.LocalPlayerName = chosenName;
    }

    private async void OnCreateRoomClicked()
    {
        SaveCurrentPlayerName();
        statusText.text = "جاري إنشاء الغرفة...";
        
        string code = await RelayManager.Instance.CreateRelayRoom();
        if (!string.IsNullOrEmpty(code))
        {
            generatedRoomCode = code;
            roomCodeDisplay.text = code;

            // الانتقال المباشر لحالة الانتظار التقليدية بدون نصوص طويلة أو أقواس
            statusText.text = "بانتظار انضمام اللاعب الثاني...";

            if (copyRoomCodeBtn != null)
            {
                copyRoomCodeBtn.interactable = true;
            }
        }
        else
        {
            statusText.text = "فشل إنشاء الغرفة، حاول ثانية.";
        }
    }

    private async void OnJoinRoomClicked()
    {
        SaveCurrentPlayerName();
        statusText.text = "جاري الانضمام...";
        bool success = await RelayManager.Instance.JoinRelayRoom(joinCodeInput.text.Trim());
        if (!success)
        {
            statusText.text = "فشل الانضمام! تأكد من الكود.";
        }
    }

    /// <summary>
    /// عند الضغط على الكود: نسخه وإظهار تأكيد مؤقت ثم العودة لنص الانتظار
    /// </summary>
    private void OnCopyRoomCodeClicked()
    {
        if (string.IsNullOrEmpty(generatedRoomCode)) return;

        GUIUtility.systemCopyBuffer = generatedRoomCode;

        if (SoundEffectsManager.Instance != null)
        {
            SoundEffectsManager.Instance.PlayButtonClick();
        }

        if (copyNoticeCoroutine != null) StopCoroutine(copyNoticeCoroutine);
        copyNoticeCoroutine = StartCoroutine(ShowCopiedFeedbackRoutine());
    }

    private IEnumerator ShowCopiedFeedbackRoutine()
    {
        // عرض رسالة النسخ بلون أخضر
        statusText.text = "تم نسخ الكود بنجاح!";
        statusText.color = Color.green;

        yield return new WaitForSeconds(1.5f);

        // العودة تلقائياً للنص الأصلي وحالة الانتظار
        statusText.text = "بانتظار انضمام اللاعب الثاني...";
        statusText.color = Color.white; // أو لون الخط الافتراضي لديك
    }
}