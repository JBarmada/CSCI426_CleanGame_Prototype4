using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Short guided tutorial: blocking popups plus an in-world hint while the player plays.
/// Expects <see cref="TutorialMode"/> to be active (set from the title screen).
/// </summary>
public class InteractiveTutorial : MonoBehaviour
{
    private enum Stage
    {
        Welcome,
        WaitFirstClean,
        AfterCoins,
        Strikes,
        WinGoal,
        Powerup
    }

    [SerializeField] private GameFlowManager gameFlow;
    [SerializeField] private RestaurantDayCycle dayCycle;
    [SerializeField] private RestaurantSpillTracker spillTracker;
    [SerializeField] private CoinWallet coinWallet;
    [SerializeField] private RestaurantManager restaurantManager;

    [Header("Powerup step")]
    [SerializeField] private int coinsToDemonstratePowerup = 8;

    private CanvasGroup rootGroup;
    private GameObject dimmer;
    private GameObject panel;
    private TextMeshProUGUI titleTmp;
    private TextMeshProUGUI bodyTmp;
    private Button continueButton;
    private TextMeshProUGUI continueTmp;
    private GameObject hintBanner;
    private TextMeshProUGUI hintTmp;

    private Stage stage = Stage.Welcome;
    private int cleansSeen;
    private bool builtUi;

    private void Awake()
    {
        if (!TutorialMode.IsActive)
        {
            gameObject.SetActive(false);
            return;
        }

        EnsureReferences();
        dayCycle?.ApplyTutorialRelaxation();
        BuildUi();
        builtUi = true;
    }

    private void Start()
    {
        if (!TutorialMode.IsActive || !builtUi)
            return;

        if (gameFlow == null)
            gameFlow = GameFlowManager.Instance;

        gameFlow?.PauseGame();
        ShowBlocking(
            "Welcome",
            "You are the janitor. Move with WASD (relative to the camera), crouch with Left Ctrl, and zoom the view with the mouse wheel.\n\n" +
            "Hold Space near a spill to sweep with your broom.",
            "Got it");
    }

    private void Update()
    {
        if (!TutorialMode.IsActive || !builtUi)
            return;

        if (stage == Stage.WaitFirstClean && spillTracker != null)
        {
            if (spillTracker.SpillsCleaned > cleansSeen)
            {
                cleansSeen = spillTracker.SpillsCleaned;
                stage = Stage.AfterCoins;
                if (gameFlow == null)
                    gameFlow = GameFlowManager.Instance;
                gameFlow?.PauseGame();
                ShowBlocking(
                    "Coins",
                    "Cleaning spills pays coins. Your balance appears in the HUD.\n\n" +
                    "Later you can spend coins on the broom power-up to sweep faster for a while.",
                    "Next");
            }
        }
    }

    private void EnsureReferences()
    {
        if (gameFlow == null)
            gameFlow = GameFlowManager.Instance;
        if (dayCycle == null)
            dayCycle = FindFirstObjectByType<RestaurantDayCycle>();
        if (spillTracker == null)
            spillTracker = FindFirstObjectByType<RestaurantSpillTracker>();
        if (coinWallet == null)
            coinWallet = CoinWallet.Instance != null ? CoinWallet.Instance : FindFirstObjectByType<CoinWallet>();
        if (restaurantManager == null)
            restaurantManager = RestaurantManager.Instance;
    }

    private void BuildUi()
    {
        var rect = GetComponent<RectTransform>();
        if (rect == null)
            rect = gameObject.AddComponent<RectTransform>();

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        rootGroup = GetComponent<CanvasGroup>();
        if (rootGroup == null)
            rootGroup = gameObject.AddComponent<CanvasGroup>();
        rootGroup.blocksRaycasts = true;
        rootGroup.interactable = true;

        dimmer = CreateUiObject("TutorialDimmer", rect);
        var dimRect = dimmer.GetComponent<RectTransform>();
        StretchFull(dimRect);
        var dimImg = dimmer.AddComponent<Image>();
        dimImg.color = new Color(0.05f, 0.05f, 0.12f, 0.82f);
        dimImg.raycastTarget = true;

        panel = CreateUiObject("TutorialPanel", dimRect);
        var panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(720f, 420f);

        var panelImg = panel.AddComponent<Image>();
        panelImg.color = new Color(0.12f, 0.12f, 0.2f, 0.98f);
        panelImg.raycastTarget = true;

        titleTmp = CreateText("Title", panel.transform, 28, FontStyles.Bold, TextAlignmentOptions.Top);
        var titleRt = titleTmp.GetComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0f, 1f);
        titleRt.anchorMax = new Vector2(1f, 1f);
        titleRt.pivot = new Vector2(0.5f, 1f);
        titleRt.anchoredPosition = new Vector2(0f, -24f);
        titleRt.sizeDelta = new Vector2(-48f, 44f);

        bodyTmp = CreateText("Body", panel.transform, 22, FontStyles.Normal, TextAlignmentOptions.TopLeft);
        var bodyRt = bodyTmp.GetComponent<RectTransform>();
        bodyRt.anchorMin = new Vector2(0f, 0f);
        bodyRt.anchorMax = new Vector2(1f, 1f);
        bodyRt.pivot = new Vector2(0.5f, 1f);
        bodyRt.offsetMin = new Vector2(28f, 88f);
        bodyRt.offsetMax = new Vector2(-28f, -72f);

        continueButton = CreateUiObject("ContinueButton", panel.transform).AddComponent<Button>();
        var btnRt = continueButton.GetComponent<RectTransform>();
        btnRt.anchorMin = new Vector2(0.5f, 0f);
        btnRt.anchorMax = new Vector2(0.5f, 0f);
        btnRt.pivot = new Vector2(0.5f, 0f);
        btnRt.anchoredPosition = new Vector2(0f, 24f);
        btnRt.sizeDelta = new Vector2(220f, 44f);
        var btnImg = continueButton.gameObject.AddComponent<Image>();
        btnImg.color = new Color(0.25f, 0.55f, 0.95f, 1f);
        continueButton.targetGraphic = btnImg;
        continueButton.onClick.AddListener(OnContinueClicked);

        continueTmp = CreateText("ContinueLabel", continueButton.transform, 20, FontStyles.Bold, TextAlignmentOptions.Center);
        var clRt = continueTmp.GetComponent<RectTransform>();
        StretchFull(clRt);
        continueTmp.raycastTarget = false;

        hintBanner = CreateUiObject("TutorialHint", rect);
        var hintRt = hintBanner.GetComponent<RectTransform>();
        hintRt.anchorMin = new Vector2(0.5f, 0f);
        hintRt.anchorMax = new Vector2(0.5f, 0f);
        hintRt.pivot = new Vector2(0.5f, 0f);
        hintRt.anchoredPosition = new Vector2(0f, 32f);
        hintRt.sizeDelta = new Vector2(780f, 96f);
        var hintBg = hintBanner.AddComponent<Image>();
        hintBg.color = new Color(0f, 0f, 0f, 0.65f);
        hintTmp = CreateText("HintText", hintBanner.transform, 20, FontStyles.Normal, TextAlignmentOptions.Center);
        var htRt = hintTmp.GetComponent<RectTransform>();
        StretchFull(htRt);
        htRt.offsetMin = new Vector2(16f, 12f);
        htRt.offsetMax = new Vector2(-16f, -12f);

        GameObject endRoot = CreateUiObject("EndTutorialButton", rect);
        var endRt = endRoot.GetComponent<RectTransform>();
        endRt.anchorMin = new Vector2(1f, 1f);
        endRt.anchorMax = new Vector2(1f, 1f);
        endRt.pivot = new Vector2(1f, 1f);
        endRt.anchoredPosition = new Vector2(-18f, -18f);
        endRt.sizeDelta = new Vector2(168f, 40f);
        var endImg = endRoot.AddComponent<Image>();
        endImg.color = new Color(0.22f, 0.22f, 0.28f, 0.95f);
        endImg.raycastTarget = true;
        Button endBtn = endRoot.AddComponent<Button>();
        endBtn.targetGraphic = endImg;
        endBtn.onClick.AddListener(OnEndTutorialClicked);
        TextMeshProUGUI endLabel = CreateText("EndLabel", endRoot.transform, 17, FontStyles.Bold, TextAlignmentOptions.Center);
        StretchFull(endLabel.GetComponent<RectTransform>());
        endLabel.text = "End tutorial";
        endLabel.raycastTarget = false;

        HideAll();
        endRoot.SetActive(true);
    }

    private static GameObject CreateUiObject(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    private static TextMeshProUGUI CreateText(string name, Transform parent, float size, FontStyles style, TextAlignmentOptions align)
    {
        var go = CreateUiObject(name, parent);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = size;
        tmp.fontStyle = style;
        tmp.alignment = align;
        tmp.color = Color.white;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.overflowMode = TextOverflowModes.Overflow;
        return tmp;
    }

    private static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private void HideAll()
    {
        dimmer.SetActive(false);
        hintBanner.SetActive(false);
        rootGroup.alpha = 1f;
    }

    private void ShowBlocking(string title, string body, string continueLabel)
    {
        hintBanner.SetActive(false);
        dimmer.SetActive(true);
        titleTmp.text = title;
        bodyTmp.text = body;
        continueTmp.text = continueLabel;
        continueButton.gameObject.SetActive(true);
    }

    private void ShowHint(string message)
    {
        dimmer.SetActive(false);
        hintBanner.SetActive(true);
        hintTmp.text = message;
    }

    private void OnContinueClicked()
    {
        switch (stage)
        {
            case Stage.Welcome:
                stage = Stage.WaitFirstClean;
                gameFlow?.ResumeGame();
                ShowHint("Customers sit, eat, and leave. When they stand up they can leave a spill — walk over it and hold Space to sweep.");
                cleansSeen = spillTracker != null ? spillTracker.SpillsCleaned : 0;
                break;

            case Stage.AfterCoins:
                stage = Stage.Strikes;
                gameFlow?.PauseGame();
                ShowBlocking(
                    "Filth & strikes",
                    "Dirtiness comes from spills on the floor. If the restaurant stays in the worst filth tier too long, you earn a strike (red marks in the HUD).\n\n" +
                    $"Too many strikes ({(restaurantManager != null ? restaurantManager.GetFilthyLimit() : 3)} by default) and you are fired — game over.\n\n" +
                    "Keep the floor clean to avoid strikes and keep more customers coming.",
                    "Next");
                break;

            case Stage.Strikes:
                stage = Stage.WinGoal;
                ShowBlocking(
                    "How you win (full game)",
                    "In the full campaign, days are limited. Between days you get a summary: keep filth time low for bonus coins and reputation stars.\n\n" +
                    "On the last day you can try to get promoted — you need enough reputation and coins. Survive filth, earn money, and plan for that final choice.",
                    "Next");
                break;

            case Stage.WinGoal:
                stage = Stage.Powerup;
                if (coinWallet != null)
                    coinWallet.DebugSetCoins(Mathf.Max(coinsToDemonstratePowerup, coinWallet.Coins));
                gameFlow?.ResumeGame();
                ShowHint(
                    "You have extra coins for this lesson. When the broom button lights up, click it to spend coins and sweep faster for a while.\n\n" +
                    "Each purchase uses one of your daily broom uses; they reset each restaurant day. Press End tutorial in the corner when you are done.");
                break;
        }
    }

    private void OnEndTutorialClicked()
    {
        TutorialMode.End();
        if (gameFlow == null)
            gameFlow = GameFlowManager.Instance;
        if (gameFlow != null)
            gameFlow.RestartGame();
    }
}
