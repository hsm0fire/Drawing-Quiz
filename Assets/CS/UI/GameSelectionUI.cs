
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class GameSelectionUI : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject gamesPanel;
    [SerializeField] private GameObject confirmationPanel;
    [SerializeField] private GameObject waitingPanel;

    [Header("Game Buttons")]
    [SerializeField] private Button drawAndGuessButton;
    [SerializeField] private Button QuestionChallengeButton;
    [SerializeField] private Button reactionTestButton;
    [SerializeField] private Button memoryMatchButton;

    [Header("Confirmation UI")]
    [SerializeField] private TMP_Text selectedGameNameText;
    [SerializeField] private TMP_Text confirmationMessageText;
    [SerializeField] private Button acceptButton;
    [SerializeField] private Button rejectButton;

    [Header("Waiting UI")]
    [SerializeField] private TMP_Text waitingText;

    [Header("Status UI")]
    [SerializeField] private TMP_Text statusText;

    [Header("Player Names")]
    [SerializeField] private TMP_Text hostNameText;
    [SerializeField] private TMP_Text guestNameText;

    [Header("Popup Animation")]
    [SerializeField] private float animationDuration = 0.25f;
    [SerializeField] private float startingScale = 0.80f;

    private GameSelectionManager selectionManager;
    private GameObject currentPanel;
    private Coroutine initializationCoroutine;
    private Coroutine panelAnimation;

    private void Start()
    {
        // الألعاب التي لم يتم تطويرها بعد
        if (QuestionChallengeButton != null)
{
    QuestionChallengeButton.interactable = true;
    QuestionChallengeButton.onClick.AddListener(OnQuestionChallengeClicked);
}

        if (reactionTestButton != null)
            reactionTestButton.interactable = false;

        if (memoryMatchButton != null)
            memoryMatchButton.interactable = false;

        // ربط الأزرار
        if (drawAndGuessButton != null)
            drawAndGuessButton.onClick.AddListener(OnDrawAndGuessClicked);

        if (acceptButton != null)
            acceptButton.onClick.AddListener(OnAcceptClicked);

        if (rejectButton != null)
            rejectButton.onClick.AddListener(OnRejectClicked);

        initializationCoroutine = StartCoroutine(InitializeUI());
    }

    private IEnumerator InitializeUI()
    {
        while (selectionManager == null || !selectionManager.IsSpawned)
        {
            selectionManager = FindFirstObjectByType<GameSelectionManager>();
            yield return null;
        }

        selectionManager.SelectedGameIdVariable.OnValueChanged += OnSelectionChanged;
        selectionManager.SelectionStateVariable.OnValueChanged += OnStateChanged;
        selectionManager.HostNameVariable.OnValueChanged += OnHostNameChanged;
selectionManager.GuestNameVariable.OnValueChanged += OnGuestNameChanged;

UpdatePlayerNames();

        RefreshUI();
    }

    private void UpdatePlayerNames()
{
    if (selectionManager == null)
        return;

    if (hostNameText != null)
        hostNameText.text = selectionManager.HostName;

    if (guestNameText != null)
        guestNameText.text = selectionManager.GuestName;
}

private void OnHostNameChanged(
    Unity.Collections.FixedString64Bytes oldValue,
    Unity.Collections.FixedString64Bytes newValue)
{
    if (hostNameText != null)
        hostNameText.text = newValue.ToString();
}

private void OnGuestNameChanged(
    Unity.Collections.FixedString64Bytes oldValue,
    Unity.Collections.FixedString64Bytes newValue)
{
    if (guestNameText != null)
        guestNameText.text = newValue.ToString();
}

    private void OnDestroy()
    {
        if (panelAnimation != null)
            StopCoroutine(panelAnimation);

        if (selectionManager != null && selectionManager.IsSpawned)
        {
            selectionManager.SelectedGameIdVariable.OnValueChanged -= OnSelectionChanged;
            selectionManager.SelectionStateVariable.OnValueChanged -= OnStateChanged;
        }

        if (drawAndGuessButton != null)
            drawAndGuessButton.onClick.RemoveListener(OnDrawAndGuessClicked);

        if (acceptButton != null)
            acceptButton.onClick.RemoveListener(OnAcceptClicked);

        if (rejectButton != null)
            rejectButton.onClick.RemoveListener(OnRejectClicked);

            if (selectionManager != null && selectionManager.IsSpawned)
{
    selectionManager.HostNameVariable.OnValueChanged -= OnHostNameChanged;
    selectionManager.GuestNameVariable.OnValueChanged -= OnGuestNameChanged;
}

if (QuestionChallengeButton != null)
    QuestionChallengeButton.onClick.RemoveListener(OnQuestionChallengeClicked);
    }

    private void OnSelectionChanged(int oldValue, int newValue)
    {
        RefreshUI();
    }

    private void OnStateChanged(int oldValue, int newValue)
    {
        RefreshUI();
    }

    private void RefreshUI()
    {
        if (selectionManager == null || !selectionManager.IsSpawned)
            return;

        bool isHost = selectionManager.IsServer;

        int selectedGame = selectionManager.SelectedGameId;
        int state = selectionManager.SelectionState;

        // الحالة الافتراضية
        if (state == GameSelectionManager.StateIdle)
        {
            if (isHost)
            {
                ShowPanel(gamesPanel);
                SetStatus("اختر اللعبة التي تريد لعبها.");
            }
            else
            {
                ShowPanel(waitingPanel);
                SetWaitingText("بانتظار المضيف لاختيار لعبة...");
                SetStatus("بانتظار اختيار المضيف.");
            }

            return;
        }

        // انتظار موافقة الضيف
        if (state == GameSelectionManager.StateWaitingForGuest)
        {
            if (isHost)
            {
                ShowPanel(waitingPanel);
                SetWaitingText("تم إرسال اللعبة للضيف. بانتظار موافقته...");
                SetStatus("بانتظار موافقة اللاعب الآخر.");
            }
            else
            {
                ShowPanel(confirmationPanel);

                string gameName = GetGameName(selectedGame);

                if (selectedGameNameText != null)
                    selectedGameNameText.text = gameName;

                if (confirmationMessageText != null)
                    confirmationMessageText.text =
                        "المضيف يريد لعب " + gameName + ". هل توافق؟";

                SetStatus("يرجى تأكيد اختيار اللعبة.");
            }

            return;
        }

        // رفض الضيف
        if (state == GameSelectionManager.StateRejected)
        {
            if (isHost)
            {
                ShowPanel(gamesPanel);
                SetStatus("الضيف رفض اللعبة. اختر لعبة أخرى.");
            }
            else
            {
                ShowPanel(waitingPanel);
                SetWaitingText("تم رفض اللعبة. بانتظار اختيار المضيف لعبة أخرى...");
                SetStatus("بانتظار اختيار جديد.");
            }

            return;
        }

        // تحميل اللعبة
        if (state == GameSelectionManager.StateLoadingGame)
        {
            ShowPanel(waitingPanel);
            SetWaitingText("تمت الموافقة! جاري تحميل اللعبة...");
            SetStatus("استعد للعب!");
        }
    }

    // إظهار لوحة مع حركة Pop-up
    private void ShowPanel(GameObject targetPanel)
    {
        if (targetPanel == null)
            return;

        // إذا كانت اللوحة نفسها ظاهرة فلا نعيد الحركة
        if (currentPanel == targetPanel && targetPanel.activeSelf)
            return;

        if (panelAnimation != null)
            StopCoroutine(panelAnimation);

        HideAllPanels();

        currentPanel = targetPanel;
        targetPanel.SetActive(true);

        panelAnimation = StartCoroutine(AnimatePanel(targetPanel));
    }

    private void HideAllPanels()
    {
        GameObject[] panels =
        {
            gamesPanel,
            confirmationPanel,
            waitingPanel
        };

        foreach (GameObject panel in panels)
        {
            if (panel == null)
                continue;

            panel.SetActive(false);

            CanvasGroup group = panel.GetComponent<CanvasGroup>();

            if (group != null)
                group.alpha = 1f;

            panel.transform.localScale = Vector3.one;
        }
    }

    private IEnumerator AnimatePanel(GameObject panel)
    {
        CanvasGroup canvasGroup = panel.GetComponent<CanvasGroup>();

        // إنشاء CanvasGroup تلقائياً إذا لم يكن موجوداً
        if (canvasGroup == null)
            canvasGroup = panel.AddComponent<CanvasGroup>();

        float elapsed = 0f;

        Vector3 targetScale = Vector3.one;
        Vector3 initialScale = Vector3.one * startingScale;

        panel.transform.localScale = initialScale;
        canvasGroup.alpha = 0f;

        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        while (elapsed < animationDuration)
        {
            elapsed += Time.unscaledDeltaTime;

            float progress = Mathf.Clamp01(elapsed / animationDuration);

            // حركة Ease Out
            float easedProgress = 1f - Mathf.Pow(1f - progress, 3f);

            panel.transform.localScale =
                Vector3.Lerp(initialScale, targetScale, easedProgress);

            canvasGroup.alpha = easedProgress;

            yield return null;
        }

        panel.transform.localScale = targetScale;
        canvasGroup.alpha = 1f;

        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;

        panelAnimation = null;
    }

    private void OnDrawAndGuessClicked()
    {
        if (selectionManager == null || !selectionManager.IsSpawned)
            return;

        if (!selectionManager.IsServer)
            return;

        PlayClickSound();

        selectionManager.RequestGameSelectionServerRpc(
            GameSelectionManager.DrawAndGuessGameId
        );
    }

    private void OnQuestionChallengeClicked()
{
    if (selectionManager == null || !selectionManager.IsSpawned)
        return;

    if (!selectionManager.IsServer)
        return;

    PlayClickSound();

    selectionManager.RequestGameSelectionServerRpc(
        GameSelectionManager.QuestionChallengeGameId
    );
}

    private void OnAcceptClicked()
    {
        if (selectionManager == null || !selectionManager.IsSpawned)
            return;

        if (selectionManager.IsServer)
            return;

        PlayClickSound();

        selectionManager.RespondToGameSelectionServerRpc(true);
    }

    private void OnRejectClicked()
    {
        if (selectionManager == null || !selectionManager.IsSpawned)
            return;

        if (selectionManager.IsServer)
            return;

        PlayClickSound();

        selectionManager.RespondToGameSelectionServerRpc(false);
    }

    private string GetGameName(int gameId)
    {
        switch (gameId)
        {
            case GameSelectionManager.DrawAndGuessGameId:
                return "Draw and Guess";

            case GameSelectionManager.QuestionChallengeGameId:
                return "Question Challenge";

            case GameSelectionManager.ReactionTestGameId:
                return "Reaction Test";

            case GameSelectionManager.MemoryMatchGameId:
                return "Memory Match";

            default:
                return "لعبة غير معروفة";
        }
    }

    private void SetWaitingText(string message)
    {
        if (waitingText != null)
            waitingText.text = message;
    }

    private void SetStatus(string message)
    {
        if (statusText != null)
            statusText.text = message;
    }

    private void PlayClickSound()
    {
        if (SoundEffectsManager.Instance != null)
            SoundEffectsManager.Instance.PlayButtonClick();
    }
}
