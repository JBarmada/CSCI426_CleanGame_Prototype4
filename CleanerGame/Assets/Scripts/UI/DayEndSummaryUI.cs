using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DayEndSummaryUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject root;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private RestaurantDayCycle dayCycle;
    [SerializeField] private RestaurantManager restaurantManager;
    [SerializeField] private RestaurantReputation reputation;
    [SerializeField] private RestaurantSpillTracker spillTracker;
    [SerializeField] private CoinWallet coinWallet;

    [Header("UI")]
    [SerializeField] private TMP_Text spillsCleanedText;
    [SerializeField] private TMP_Text filthTimeText;
    [SerializeField] private TMP_Text salaryBonusText;
    [SerializeField] private Image reputationStar;
    [SerializeField] private Button continueButton;

    [Header("Promotion End Screens")]
    [SerializeField] private GameObject promotionScreen;   // assign Promotion Panel (inactive by default)
    [SerializeField] private GameObject firedScreen;       // assign Fired Panel (inactive by default)
    [SerializeField] private CanvasGroup promotionCanvasGroup;
    [SerializeField] private CanvasGroup firedCanvasGroup;

    [Header("Promotion Rules")]
    [SerializeField] private int promotionDay = 3;
    [SerializeField] private int reputationRequiredToPromote = 3;

    [Header("Promotion Decision Stars")]
    [SerializeField] private RectTransform promotionMiddleAnchor;
    [SerializeField] private GameObject promotionStarsRoot;
    [SerializeField] private Image[] promotionStars;
    [SerializeField] private bool centerPromotionStarsOnShow = true;
    [SerializeField] private Vector2 promotionStarsCenterOffset = Vector2.zero;
    [SerializeField] private Color promotionStarNeutralColor = Color.white;
    [SerializeField] private Color promotionStarSuccessColor = new Color(0.2f, 0.85f, 0.25f);
    [SerializeField] private Color promotionStarFailColor = new Color(0.95f, 0.2f, 0.2f);
    [SerializeField] private float promotionStarShakeSeconds = 0.4f;
    [SerializeField] private float promotionStarShakeStrength = 12f;
    [SerializeField] private float promotionDecisionDelaySeconds = 0.35f;

    [Header("Promotion Decision Coins")]
    [SerializeField] private GameObject promotionCoinsRoot;
    [SerializeField] private TMP_Text promotionCoinsText;
    [SerializeField] private string promotionCoinsFormat = "Coins: {0}";
    [SerializeField] private Vector2 promotionCoinsOffset = new Vector2(0f, -70f);

    [Header("Star")]
    [SerializeField] private Sprite starEmptySprite;
    [SerializeField] private Sprite starFullSprite;
    [SerializeField] private Color starInactiveColor = Color.black;
    [SerializeField] private Color starActiveColor = new Color(1f, 0.85f, 0.2f);
    [SerializeField] private AudioSource starAudioSource;
    [SerializeField] private AudioClip starPopClip;
    [SerializeField] private AudioClip starFailClip;
    [Range(0f, 1f)]
    [SerializeField] private float starPopVolume = 1f;
    [SerializeField] private float starPopScale = 1.2f;
    [SerializeField] private float starPopSeconds = 0.2f;

    private Coroutine starRoutine;
    private Coroutine promotionDecisionRoutine;
    private float previousTimeScale = 1f;
    private int lastSummaryDay = -1;

    private bool waitingForPromotionDecision = false;

    private RectLayoutState promotionStarsLayoutState;
    private bool promotionStarsLayoutCaptured;
    private RectLayoutState promotionCoinsLayoutState;
    private bool promotionCoinsLayoutCaptured;

    private struct RectLayoutState
    {
        public Vector2 anchorMin;
        public Vector2 anchorMax;
        public Vector2 pivot;
        public Vector2 anchoredPosition;
    }

    private void Awake()
    {
        if (root == null)
            root = gameObject;

        if (canvasGroup == null && root != null)
            canvasGroup = root.GetComponent<CanvasGroup>();

        if (dayCycle == null)
            dayCycle = FindFirstObjectByType<RestaurantDayCycle>();

        if (restaurantManager == null)
            restaurantManager = RestaurantManager.Instance;

        if (reputation == null)
            reputation = FindFirstObjectByType<RestaurantReputation>();

        if (spillTracker == null)
            spillTracker = FindFirstObjectByType<RestaurantSpillTracker>();

        if (coinWallet == null)
            coinWallet = CoinWallet.Instance != null ? CoinWallet.Instance : FindFirstObjectByType<CoinWallet>();

        if (continueButton != null)
            continueButton.onClick.AddListener(OnContinuePressed);

        TryAutoBindPromotionStars();
        ShowPromotionStars(false);
        ShowPromotionCoins(false);
    }

    private void OnEnable()
    {
        if (dayCycle != null)
            dayCycle.DayEnded += HandleDayEnded;

        TryAutoBindPromotionStars();

        // Just hide visuals (don't touch timeScale here)
        HideRoot();
        HidePanel(promotionScreen, promotionCanvasGroup);
        HidePanel(firedScreen, firedCanvasGroup);
        ShowPromotionCoins(false);
    }

    private void OnDisable()
    {
        if (dayCycle != null)
            dayCycle.DayEnded -= HandleDayEnded;
    }

    private void HandleDayEnded(int dayNumber, bool isFinalDay)
    {
        if (dayCycle == null || dayCycle.InfiniteDays) return;
        if (dayNumber == lastSummaryDay) return;

        lastSummaryDay = dayNumber;
        ShowSummary(dayNumber, isFinalDay);
    }

    private void ShowSummary(int dayNumber, bool isFinalDay)
    {
        if (root == null) return;

        // Ensure end screens are hidden when summary shows
        HidePanel(promotionScreen, promotionCanvasGroup);
        HidePanel(firedScreen, firedCanvasGroup);

        float filthTime = restaurantManager == null ? 0f : restaurantManager.GetFilthTimeSeconds();
        int spillsCleaned = spillTracker == null ? 0 : spillTracker.SpillsCleaned;

        int salaryBonus = CalculateSalaryBonus(filthTime);
        if (coinWallet != null && salaryBonus > 0)
            coinWallet.AddCoins(salaryBonus);

        bool earnedStar = filthTime < 3f;
        if (earnedStar && reputation != null)
            reputation.TryIncreaseReputation();

        UpdateUI(spillsCleaned, filthTime, salaryBonus, earnedStar);

        UpdateContinueButtonForDay(dayNumber, isFinalDay);

        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;

        ShowRoot();
    }

    private void UpdateContinueButtonForDay(int dayNumber, bool isFinalDay)
    {
        waitingForPromotionDecision = isFinalDay && dayNumber == promotionDay;
        RefreshPromotionStars();
        RefreshPromotionCoins();
        ShowPromotionStars(waitingForPromotionDecision);
        ShowPromotionCoins(waitingForPromotionDecision);

        if (continueButton == null) return;

        TMP_Text buttonText = continueButton.GetComponentInChildren<TMP_Text>();
        if (buttonText == null) return;

        buttonText.text = waitingForPromotionDecision ? "try to promote" : "CONTINUE";
    }

    private void OnContinuePressed()
    {
        // ✅ Final day: DO NOT Hide() first (or you'd hide the end screen too)
        if (waitingForPromotionDecision)
        {
            if (promotionDecisionRoutine != null)
                return;

            promotionDecisionRoutine = StartCoroutine(ResolvePromotionOrFiredRoutine());
            return;
        }

        // Normal days: hide + continue
        Hide();

        if (dayCycle != null)
            dayCycle.ContinueToNextDay();
    }

    private IEnumerator ResolvePromotionOrFiredRoutine()
    {
        int rep = reputation == null ? 0 : reputation.Reputation;
        bool promoted = rep >= reputationRequiredToPromote;

        if (continueButton != null)
            continueButton.interactable = false;

        RefreshPromotionCoins();
        AnimatePromotionStars(promoted);

        float waitSeconds = Mathf.Max(0f, promotionStarShakeSeconds + promotionDecisionDelaySeconds);
        if (waitSeconds > 0f)
            yield return new WaitForSecondsRealtime(waitSeconds);

        // Hide ONLY the summary panel if you want (optional)
        // If you want the summary background to disappear, keep this:
        HideRoot();

        // Keep time paused for end screens (optional)
        Time.timeScale = 0f;

        if (promoted)
        {
            ShowPanel(promotionScreen, promotionCanvasGroup);

            Debug.Log($"[DayEndSummaryUI] PROMOTED ✅ (Rep={rep}/{reputationRequiredToPromote})");
        }
        else
        {
            ShowPanel(firedScreen, firedCanvasGroup);

            Debug.Log($"[DayEndSummaryUI] FIRED ❌ (Rep={rep}/{reputationRequiredToPromote})");
        }

        if (continueButton != null)
            continueButton.interactable = true;
        promotionDecisionRoutine = null;
    }

    private void UpdateUI(int spillsCleaned, float filthTime, int salaryBonus, bool earnedStar)
    {
        if (spillsCleanedText != null)
            spillsCleanedText.text = "Spills Cleaned: " + spillsCleaned;

        if (filthTimeText != null)
            filthTimeText.text = "Filth Time: " + filthTime.ToString("0.0") + "s";

        if (salaryBonusText != null)
            salaryBonusText.text = "Salary Bonus: +" + salaryBonus + " coins";

        if (reputationStar != null)
        {
            reputationStar.sprite = earnedStar ? starFullSprite : starEmptySprite;
            reputationStar.color = earnedStar ? starActiveColor : starInactiveColor;
            PlayStarResultAudio(earnedStar);
            if (earnedStar)
                PlayStarPop();
        }
    }

    private void PlayStarPop()
    {
        if (reputationStar == null) return;

        if (starRoutine != null)
            StopCoroutine(starRoutine);

        starRoutine = StartCoroutine(StarPopRoutine());
    }

    private void PlayStarResultAudio(bool earnedStar)
    {
        if (earnedStar)
        {
            if (starPopClip != null)
                PlayStarAudio(starPopClip);
            return;
        }

        if (starFailClip != null)
            PlayStarAudio(starFailClip);
    }

    private void PlayStarAudio(AudioClip clip)
    {
        if (clip == null) return;

        if (starAudioSource == null)
            starAudioSource = GetComponent<AudioSource>();
        if (starAudioSource == null)
            starAudioSource = gameObject.AddComponent<AudioSource>();

        starAudioSource.playOnAwake = false;
        starAudioSource.PlayOneShot(clip, starPopVolume);
    }

    private IEnumerator StarPopRoutine()
    {
        Vector3 baseScale = reputationStar.transform.localScale;
        Vector3 targetScale = baseScale * starPopScale;

        float t = 0f;
        while (t < starPopSeconds)
        {
            t += Time.unscaledDeltaTime;
            float lerp = Mathf.Clamp01(t / starPopSeconds);
            reputationStar.transform.localScale = Vector3.Lerp(baseScale, targetScale, lerp);
            yield return null;
        }

        t = 0f;
        while (t < starPopSeconds)
        {
            t += Time.unscaledDeltaTime;
            float lerp = Mathf.Clamp01(t / starPopSeconds);
            reputationStar.transform.localScale = Vector3.Lerp(targetScale, baseScale, lerp);
            yield return null;
        }

        reputationStar.transform.localScale = baseScale;
    }

    private int CalculateSalaryBonus(float filthTime)
    {
        if (filthTime <= 5f) return 5;
        float penalty = Mathf.Max(0f, filthTime - 5f);
        int deduction = Mathf.FloorToInt(penalty);
        return Mathf.Clamp(5 - deduction, 0, 5);
    }

    private void Hide()
    {
        HideRoot();
        ShowPromotionStars(false);
        ShowPromotionCoins(false);
        Time.timeScale = previousTimeScale;
    }

    private void ShowRoot()
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
            return;
        }

        if (root != null && root != gameObject)
            root.SetActive(true);
    }

    private void HideRoot()
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
            return;
        }

        if (root != null && root != gameObject)
            root.SetActive(false);
    }

    private void ShowPanel(GameObject panel, CanvasGroup group)
    {
        if (group != null)
        {
            group.alpha = 1f;
            group.interactable = true;
            group.blocksRaycasts = true;
            return;
        }

        if (panel != null)
            panel.SetActive(true);
    }

    private void HidePanel(GameObject panel, CanvasGroup group)
    {
        if (group != null)
        {
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
            return;
        }

        if (panel != null)
            panel.SetActive(false);
    }

    private void RefreshPromotionStars()
    {
        TryAutoBindPromotionStars();
        if (promotionStars == null || promotionStars.Length == 0) return;

        int rep = reputation == null ? 0 : Mathf.Max(0, reputation.Reputation);
        for (int i = 0; i < promotionStars.Length; i++)
        {
            if (promotionStars[i] == null) continue;

            bool filled = i < rep;
            if (starFullSprite != null || starEmptySprite != null)
                promotionStars[i].sprite = filled ? starFullSprite : starEmptySprite;

            promotionStars[i].color = promotionStarNeutralColor;
            promotionStars[i].transform.localRotation = Quaternion.identity;
        }
    }

    private void ShowPromotionStars(bool shouldShow)
    {
        bool isInternalObject = IsPromotionObjectUnderSummaryRoot(promotionStarsRoot);

        if (shouldShow)
            PositionPromotionDecisionUI();
        else
            RestorePromotionStarsLayout();

        if (promotionStarsRoot != null && isInternalObject)
            promotionStarsRoot.SetActive(shouldShow);
    }

    private void ShowPromotionCoins(bool shouldShow)
    {
        bool isInternalObject = IsPromotionObjectUnderSummaryRoot(promotionCoinsRoot);

        if (shouldShow)
            PositionPromotionDecisionUI();
        else
            RestorePromotionCoinsLayout();

        if (promotionCoinsRoot != null && isInternalObject)
            promotionCoinsRoot.SetActive(shouldShow);
    }

    private void TryAutoBindPromotionStars()
    {
        if (promotionStarsRoot == null) return;
        if (promotionStars != null && promotionStars.Length > 0) return;

        promotionStars = promotionStarsRoot.GetComponentsInChildren<Image>(true);
    }

    private void PositionPromotionDecisionUI()
    {
        CenterPromotionStars();
        PositionPromotionCoins();
    }

    private void CenterPromotionStars()
    {
        if (!centerPromotionStarsOnShow) return;
        if (promotionStarsRoot == null) return;

        RectTransform rect = promotionStarsRoot.GetComponent<RectTransform>();
        if (rect == null) return;

        SetRectToMiddleAnchor(rect, promotionStarsCenterOffset, ref promotionStarsLayoutState, ref promotionStarsLayoutCaptured);
    }

    private void PositionPromotionCoins()
    {
        if (promotionCoinsRoot == null) return;

        RectTransform rect = promotionCoinsRoot.GetComponent<RectTransform>();
        if (rect == null) return;

        SetRectToMiddleAnchor(rect, promotionCoinsOffset, ref promotionCoinsLayoutState, ref promotionCoinsLayoutCaptured);
    }

    private void RefreshPromotionCoins()
    {
        if (promotionCoinsText == null) return;

        int coins = coinWallet == null ? 0 : coinWallet.Coins;
        promotionCoinsText.text = string.Format(promotionCoinsFormat, coins);
    }

    private void SetRectToMiddleAnchor(RectTransform targetRect, Vector2 offset, ref RectLayoutState layoutState, ref bool layoutCaptured)
    {
        if (targetRect == null) return;

        if (!layoutCaptured)
        {
            layoutState = CaptureLayoutState(targetRect);
            layoutCaptured = true;
        }

        targetRect.anchorMin = new Vector2(0.5f, 0.5f);
        targetRect.anchorMax = new Vector2(0.5f, 0.5f);
        targetRect.pivot = new Vector2(0.5f, 0.5f);

        if (promotionMiddleAnchor == null)
        {
            targetRect.anchoredPosition = offset;
            return;
        }

        RectTransform parentRect = targetRect.parent as RectTransform;
        if (parentRect == null)
        {
            targetRect.anchoredPosition = offset;
            return;
        }

        Camera uiCamera = null;
        Canvas canvas = targetRect.GetComponentInParent<Canvas>();
        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            uiCamera = canvas.worldCamera;

        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(uiCamera, promotionMiddleAnchor.position);
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screenPoint, uiCamera, out Vector2 localPoint))
        {
            targetRect.anchoredPosition = localPoint + offset;
            return;
        }

        targetRect.anchoredPosition = offset;
    }

    private bool IsPromotionObjectUnderSummaryRoot(GameObject obj)
    {
        if (obj == null) return false;
        if (root == null) return true;

        Transform rootTransform = root.transform;
        Transform objTransform = obj.transform;
        return objTransform == rootTransform || objTransform.IsChildOf(rootTransform);
    }

    private RectLayoutState CaptureLayoutState(RectTransform rect)
    {
        RectLayoutState state = new RectLayoutState
        {
            anchorMin = rect.anchorMin,
            anchorMax = rect.anchorMax,
            pivot = rect.pivot,
            anchoredPosition = rect.anchoredPosition
        };

        return state;
    }

    private void RestorePromotionStarsLayout()
    {
        if (!promotionStarsLayoutCaptured || promotionStarsRoot == null) return;

        RectTransform rect = promotionStarsRoot.GetComponent<RectTransform>();
        if (rect == null) return;

        ApplyLayoutState(rect, promotionStarsLayoutState);
    }

    private void RestorePromotionCoinsLayout()
    {
        if (!promotionCoinsLayoutCaptured || promotionCoinsRoot == null) return;

        RectTransform rect = promotionCoinsRoot.GetComponent<RectTransform>();
        if (rect == null) return;

        ApplyLayoutState(rect, promotionCoinsLayoutState);
    }

    private void ApplyLayoutState(RectTransform rect, RectLayoutState state)
    {
        rect.anchorMin = state.anchorMin;
        rect.anchorMax = state.anchorMax;
        rect.pivot = state.pivot;
        rect.anchoredPosition = state.anchoredPosition;
    }

    private void AnimatePromotionStars(bool promoted)
    {
        if (promotionStars == null || promotionStars.Length == 0) return;

        if (starRoutine != null)
            StopCoroutine(starRoutine);

        starRoutine = StartCoroutine(PromotionStarResultRoutine(promoted));
    }

    private IEnumerator PromotionStarResultRoutine(bool promoted)
    {
        Color resultColor = promoted ? promotionStarSuccessColor : promotionStarFailColor;
        float duration = Mathf.Max(0f, promotionStarShakeSeconds);
        float strength = Mathf.Max(0f, promotionStarShakeStrength);

        Vector3[] basePositions = new Vector3[promotionStars.Length];
        for (int i = 0; i < promotionStars.Length; i++)
        {
            if (promotionStars[i] == null) continue;
            basePositions[i] = promotionStars[i].transform.localPosition;
            promotionStars[i].color = resultColor;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            for (int i = 0; i < promotionStars.Length; i++)
            {
                if (promotionStars[i] == null) continue;

                float x = Mathf.Sin((elapsed * 65f) + i) * strength;
                promotionStars[i].transform.localPosition = basePositions[i] + new Vector3(x, 0f, 0f);
            }

            yield return null;
        }

        for (int i = 0; i < promotionStars.Length; i++)
        {
            if (promotionStars[i] == null) continue;
            promotionStars[i].transform.localPosition = basePositions[i];
        }
    }
}
