
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(NetworkObject))]
public class QuestionChallengeManager : NetworkBehaviour
{
    [Header("Question Database")]
    [SerializeField] private QuestionDatabase questionDatabase;
    [SerializeField] private int totalQuestions = 10;
    [SerializeField] private float questionDuration = 15f;

    [Header("Question UI")]
    [SerializeField] private TMP_Text questionNumberText;
    [SerializeField] private TMP_Text questionText;
    [SerializeField] private TMP_Text timerText;
    [SerializeField] private TMP_Text statusText;

    [Header("Answer Buttons")]
    [SerializeField] private Button[] answerButtons = new Button[4];
    [SerializeField] private TMP_Text[] answerTexts = new TMP_Text[4];

    [Header("Score UI")]
    [SerializeField] private TMP_Text hostScoreText;
    [SerializeField] private TMP_Text guestScoreText;

    [Header("Results UI")]
    [SerializeField] private GameObject resultsPanel;
    [SerializeField] private TMP_Text hostFinalScoreText;
    [SerializeField] private TMP_Text guestFinalScoreText;
    [SerializeField] private TMP_Text winnerText;

    [Header("Scene Settings")]
    [SerializeField] private string gameLobbySceneName = "GameLobby";
    [SerializeField] private float resultsDisplayDuration = 5f;

    // أسماء اللاعبين متزامنة بين جميع الأجهزة ويكتبها الخادم فقط.
    private readonly NetworkVariable<int> questionNumber =
        new NetworkVariable<int>(0);

    private readonly NetworkVariable<FixedString512Bytes> syncedQuestion =
        new NetworkVariable<FixedString512Bytes>();

    private readonly NetworkVariable<FixedString128Bytes> syncedAnswerA =
        new NetworkVariable<FixedString128Bytes>();

    private readonly NetworkVariable<FixedString128Bytes> syncedAnswerB =
        new NetworkVariable<FixedString128Bytes>();

    private readonly NetworkVariable<FixedString128Bytes> syncedAnswerC =
        new NetworkVariable<FixedString128Bytes>();

    private readonly NetworkVariable<FixedString128Bytes> syncedAnswerD =
        new NetworkVariable<FixedString128Bytes>();

    private readonly NetworkVariable<float> remainingTime =
        new NetworkVariable<float>(0f);

    private readonly NetworkVariable<int> hostScore =
        new NetworkVariable<int>(0);

    private readonly NetworkVariable<int> guestScore =
        new NetworkVariable<int>(0);

    private readonly NetworkVariable<bool> hostAnswered =
        new NetworkVariable<bool>(false);

    private readonly NetworkVariable<bool> guestAnswered =
        new NetworkVariable<bool>(false);

    private readonly NetworkVariable<bool> roundActive =
        new NetworkVariable<bool>(false);

    private readonly NetworkVariable<bool> gameFinished =
        new NetworkVariable<bool>(false);

    private readonly NetworkVariable<int> revealedCorrectIndex =
        new NetworkVariable<int>(-1);

    private readonly NetworkVariable<ulong> guestClientId =
        new NetworkVariable<ulong>(ulong.MaxValue);

        private readonly NetworkVariable<FixedString64Bytes> hostPlayerName =
    new NetworkVariable<FixedString64Bytes>(
        new FixedString64Bytes("المضيف"),
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

private readonly NetworkVariable<FixedString64Bytes> guestPlayerName =
    new NetworkVariable<FixedString64Bytes>(
        new FixedString64Bytes("الضيف"),
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // Server-only data
    private List<QuestionData> selectedQuestions;
    private int currentCorrectAnswer;
    private float timerSyncAccumulator;

    // Local UI data
    private bool localHasAnswered;
    private bool listenersAdded;

    public override void OnNetworkSpawn()
    {
        questionNumber.OnValueChanged += OnQuestionNumberChanged;
        syncedQuestion.OnValueChanged += OnQuestionChanged;

        syncedAnswerA.OnValueChanged += OnNetworkValueChanged;
        syncedAnswerB.OnValueChanged += OnNetworkValueChanged;
        syncedAnswerC.OnValueChanged += OnNetworkValueChanged;
        syncedAnswerD.OnValueChanged += OnNetworkValueChanged;

        remainingTime.OnValueChanged += OnTimerChanged;
        hostScore.OnValueChanged += OnNetworkValueChanged;
        guestScore.OnValueChanged += OnNetworkValueChanged;
        hostPlayerName.OnValueChanged += OnNetworkValueChanged;
        guestPlayerName.OnValueChanged += OnNetworkValueChanged;

        hostAnswered.OnValueChanged += OnNetworkValueChanged;
        guestAnswered.OnValueChanged += OnNetworkValueChanged;

        roundActive.OnValueChanged += OnNetworkValueChanged;
        gameFinished.OnValueChanged += OnNetworkValueChanged;
        revealedCorrectIndex.OnValueChanged += OnNetworkValueChanged;

        AddButtonListeners();

        if (resultsPanel != null)
            resultsPanel.SetActive(false);

        RefreshUI();

        if (IsServer)
            InitializeGame();
    }

    public override void OnNetworkDespawn()
    {
        questionNumber.OnValueChanged -= OnQuestionNumberChanged;
        syncedQuestion.OnValueChanged -= OnQuestionChanged;

        syncedAnswerA.OnValueChanged -= OnNetworkValueChanged;
        syncedAnswerB.OnValueChanged -= OnNetworkValueChanged;
        syncedAnswerC.OnValueChanged -= OnNetworkValueChanged;
        syncedAnswerD.OnValueChanged -= OnNetworkValueChanged;

        remainingTime.OnValueChanged -= OnTimerChanged;
        hostScore.OnValueChanged -= OnNetworkValueChanged;
        guestScore.OnValueChanged -= OnNetworkValueChanged;
        hostPlayerName.OnValueChanged -= OnNetworkValueChanged;
        guestPlayerName.OnValueChanged -= OnNetworkValueChanged;

        hostAnswered.OnValueChanged -= OnNetworkValueChanged;
        guestAnswered.OnValueChanged -= OnNetworkValueChanged;

        roundActive.OnValueChanged -= OnNetworkValueChanged;
        gameFinished.OnValueChanged -= OnNetworkValueChanged;
        revealedCorrectIndex.OnValueChanged -= OnNetworkValueChanged;

        RemoveButtonListeners();
    }

    private void Update()
    {
        if (!IsServer || !roundActive.Value || gameFinished.Value)
            return;

        timerSyncAccumulator += Time.deltaTime;

        if (timerSyncAccumulator >= 0.1f)
        {
            timerSyncAccumulator = 0f;
            remainingTime.Value =
                Mathf.Max(0f, remainingTime.Value - 0.1f);
        }

        if (remainingTime.Value <= 0f)
            EndRound();
    }

    private void InitializeGame()
    {
        // قراءة الاسمين اللذين أدخلهما اللاعبان في MainMenu ثم مزامنتهما للجميع.
        string hostName = GameSelectionManager.LastHostName;
        string guestName = GameSelectionManager.LastGuestName;

        if (string.IsNullOrWhiteSpace(hostName))
            hostName = "اللاعب الأول";

        if (string.IsNullOrWhiteSpace(guestName))
            guestName = "اللاعب الثاني";

        hostPlayerName.Value = new FixedString64Bytes(hostName.Trim());
        guestPlayerName.Value = new FixedString64Bytes(guestName.Trim());
        if (questionDatabase == null)
        {
            Debug.LogError("Question Database is not assigned!");
            return;
        }

        if (NetworkManager.ConnectedClientsIds.Count < 2)
        {
            Debug.LogError("QuestionChallenge requires two players.");
            return;
        }

        guestClientId.Value = FindGuestClientId();

        selectedQuestions = questionDatabase.GetAllQuestions();

        if (selectedQuestions == null || selectedQuestions.Count == 0)
        {
            Debug.LogError("No questions found in database!");
            return;
        }

        ShuffleQuestions();

        if (selectedQuestions.Count > totalQuestions)
            selectedQuestions = selectedQuestions.GetRange(0, totalQuestions);

        hostScore.Value = 0;
        guestScore.Value = 0;
        questionNumber.Value = 0;
        gameFinished.Value = false;

        StartRound();
    }

    private ulong FindGuestClientId()
    {
        foreach (ulong clientId in NetworkManager.ConnectedClientsIds)
        {
            if (clientId != NetworkManager.ServerClientId)
                return clientId;
        }

        return ulong.MaxValue;
    }

    private void ShuffleQuestions()
    {
        for (int i = selectedQuestions.Count - 1; i > 0; i--)
        {
            int randomIndex = Random.Range(0, i + 1);

            QuestionData temp = selectedQuestions[i];
            selectedQuestions[i] = selectedQuestions[randomIndex];
            selectedQuestions[randomIndex] = temp;
        }
    }

    private void StartRound()
    {
        if (!IsServer)
            return;

        if (questionNumber.Value >= selectedQuestions.Count)
        {
            FinishGame();
            return;
        }

        QuestionData data = selectedQuestions[questionNumber.Value];

        if (data.answers == null || data.answers.Length != 4)
        {
            Debug.LogError("Every question must have exactly four answers.");
            FinishGame();
            return;
        }

        currentCorrectAnswer = data.correctAnswerIndex;

        syncedQuestion.Value = new FixedString512Bytes(data.questionText);

        syncedAnswerA.Value = new FixedString128Bytes(data.answers[0]);
        syncedAnswerB.Value = new FixedString128Bytes(data.answers[1]);
        syncedAnswerC.Value = new FixedString128Bytes(data.answers[2]);
        syncedAnswerD.Value = new FixedString128Bytes(data.answers[3]);

        hostAnswered.Value = false;
        guestAnswered.Value = false;

        revealedCorrectIndex.Value = -1;

        remainingTime.Value = questionDuration;
        timerSyncAccumulator = 0f;

        localHasAnswered = false;

        questionNumber.Value++;
        roundActive.Value = true;

        RefreshUI();
    }

    private void AddButtonListeners()
    {
        if (listenersAdded || answerButtons == null)
            return;

        for (int i = 0; i < answerButtons.Length; i++)
        {
            if (answerButtons[i] == null)
                continue;

            int answerIndex = i;
            answerButtons[i].onClick.AddListener(
                () => SubmitAnswer(answerIndex));
        }

        listenersAdded = true;
    }

    private void RemoveButtonListeners()
    {
        if (!listenersAdded || answerButtons == null)
            return;

        for (int i = 0; i < answerButtons.Length; i++)
        {
            if (answerButtons[i] != null)
                answerButtons[i].onClick.RemoveAllListeners();
        }

        listenersAdded = false;
    }

    private void SubmitAnswer(int answerIndex)
    {
        if (!IsSpawned || !roundActive.Value ||
            localHasAnswered || answerIndex < 0 || answerIndex > 3)
            return;

        localHasAnswered = true;
        RefreshUI();

        SubmitAnswerServerRpc(answerIndex);
    }

    [ServerRpc(RequireOwnership = false)]
    private void SubmitAnswerServerRpc(
        int answerIndex,
        ServerRpcParams rpcParams = default)
    {
        if (!roundActive.Value || gameFinished.Value)
            return;

        ulong senderId = rpcParams.Receive.SenderClientId;

        if (senderId == NetworkManager.ServerClientId)
        {
            if (hostAnswered.Value)
                return;

            hostAnswered.Value = true;

            if (answerIndex == currentCorrectAnswer)
                hostScore.Value += 100;
        }
        else if (senderId == guestClientId.Value)
        {
            if (guestAnswered.Value)
                return;

            guestAnswered.Value = true;

            if (answerIndex == currentCorrectAnswer)
                guestScore.Value += 100;
        }
        else
        {
            return;
        }

        RefreshUI();

        if (hostAnswered.Value && guestAnswered.Value)
            EndRound();
    }

    private void EndRound()
    {
        if (!IsServer || !roundActive.Value)
            return;

        roundActive.Value = false;
        remainingTime.Value = 0f;

        revealedCorrectIndex.Value = currentCorrectAnswer;

        RefreshUI();

        StartCoroutine(NextRoundAfterDelay());
    }

    private IEnumerator NextRoundAfterDelay()
    {
        yield return new WaitForSeconds(3f);

        if (!IsServer || gameFinished.Value)
            yield break;

        StartRound();
    }

    

private void FinishGame()
{
    if (!IsServer || gameFinished.Value)
        return;

    roundActive.Value = false;
    remainingTime.Value = 0f;
    gameFinished.Value = true;

    // تسجيل فوز اللاعب في نتائج الجلسة
    if (hostScore.Value > guestScore.Value)
    {
        SessionScoreManager.AddHostWin();
    }
    else if (guestScore.Value > hostScore.Value)
    {
        SessionScoreManager.AddClientWin();
    }

    RefreshUI();

    Debug.Log(
        $"Question Challenge Finished. Host: {hostScore.Value}, Guest: {guestScore.Value}"
    );

    StartCoroutine(ReturnToGameLobby());
}


private IEnumerator ReturnToGameLobby()
{
    yield return new WaitForSeconds(resultsDisplayDuration);

    if (!IsServer || NetworkManager.Singleton == null)
        yield break;

    if (NetworkManager.Singleton.ConnectedClientsIds.Count != 2)
    {
        Debug.LogWarning(
            "[QuestionChallenge] Cannot return to GameLobby: two players are required."
        );
        yield break;
    }

    if (!Application.CanStreamedLevelBeLoaded(gameLobbySceneName))
    {
        Debug.LogError(
            $"[QuestionChallenge] Scene not found: {gameLobbySceneName}"
        );
        yield break;
    }

    Debug.Log("[QuestionChallenge] Returning players to GameLobby.");

    NetworkManager.Singleton.SceneManager.LoadScene(
        gameLobbySceneName,
        UnityEngine.SceneManagement.LoadSceneMode.Single
    );
}



    private void OnNetworkValueChanged<T>(T oldValue, T newValue)
    {
        RefreshUI();
    }

    private void OnQuestionChanged(
        FixedString512Bytes oldValue,
        FixedString512Bytes newValue)
    {
        RefreshUI();
    }

    private void OnExplanationChanged(
        FixedString512Bytes oldValue,
        FixedString512Bytes newValue)
    {
        RefreshUI();
    }

    private void OnTimerChanged(float oldValue, float newValue)
    {
        RefreshUI();
    }

    private void RefreshUI()
    {
        if (questionText != null)
            questionText.text = syncedQuestion.Value.ToString();

        if (questionNumberText != null)
            questionNumberText.text =
                $"السؤال {questionNumber.Value} / {totalQuestions}";

        if (timerText != null)
            timerText.text = Mathf.CeilToInt(remainingTime.Value).ToString();

        if (hostScoreText != null)
    hostScoreText.text =
        $"{hostPlayerName.Value} : {hostScore.Value}";

if (guestScoreText != null)
    guestScoreText.text =
        $"{guestPlayerName.Value} : {guestScore.Value}";

        if (answerTexts != null && answerTexts.Length >= 4)
        {
            answerTexts[0].text = syncedAnswerA.Value.ToString();
            answerTexts[1].text = syncedAnswerB.Value.ToString();
            answerTexts[2].text = syncedAnswerC.Value.ToString();
            answerTexts[3].text = syncedAnswerD.Value.ToString();
        }

        bool isHost = IsClient &&
            NetworkManager.LocalClientId == NetworkManager.ServerClientId;

        bool hasAnswered = isHost
            ? hostAnswered.Value
            : guestAnswered.Value;

        bool canAnswer = IsClient &&
            roundActive.Value &&
            !hasAnswered &&
            !localHasAnswered;

        if (answerButtons != null)
        {
            foreach (Button button in answerButtons)
            {
                if (button != null)
                    button.interactable = canAnswer;
            }
        }

        if (statusText != null && !gameFinished.Value)
        {
            if (roundActive.Value)
                statusText.text = hasAnswered || localHasAnswered
                    ? "تم تسجيل إجابتك، انتظر اللاعب الآخر..."
                    : "اختر الإجابة الصحيحة";
            else if (revealedCorrectIndex.Value >= 0)
                statusText.text = "انتهى الوقت أو أجاب اللاعبان!";
        }

        if (resultsPanel != null)
            resultsPanel.SetActive(gameFinished.Value);


if (gameFinished.Value)
{
    if (hostFinalScoreText != null)
        hostFinalScoreText.text =
            $"{hostPlayerName.Value} : {hostScore.Value}";

    if (guestFinalScoreText != null)
        guestFinalScoreText.text =
            $"{guestPlayerName.Value} : {guestScore.Value}";

    if (winnerText != null)
    {
        if (hostScore.Value > guestScore.Value)
            winnerText.text =
                $"الفائز: {hostPlayerName.Value}";

        else if (guestScore.Value > hostScore.Value)
            winnerText.text =
                $"الفائز: {guestPlayerName.Value}";

        else
            winnerText.text = "تعادل!";
    }
}

    }

    private string GetAnswerText(int index)
    {
        switch (index)
        {
            case 0: return syncedAnswerA.Value.ToString();
            case 1: return syncedAnswerB.Value.ToString();
            case 2: return syncedAnswerC.Value.ToString();
            case 3: return syncedAnswerD.Value.ToString();
            default: return "";
        }
    }

    private void OnQuestionNumberChanged(int oldValue, int newValue)
    {
        localHasAnswered = false;
        RefreshUI();
    }

}
