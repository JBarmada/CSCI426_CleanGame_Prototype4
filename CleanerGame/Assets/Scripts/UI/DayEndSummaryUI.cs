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
    [SerializeField] private GameObject promotionResultImage;
    [SerializeField] private GameObject firedResultImage;
    [SerializeField] private float resultImageRevealDelaySeconds = 3f;
    [SerializeField] private float resultImageImpactSeconds = 0.2f;
    [SerializeField] private float resultImageImpactStartScale = 1.8f;
    [SerializeField] private float resultImageImpactEndScale = 1f;
    [SerializeField] private float resultImageImpactShakeStrength = 16f;

    [Header("Promotion Rules")]
    [SerializeField] private int promotionDay = 3;
    [SerializeField] private int reputationRequiredToPromote = 3;
    [SerializeField] private int promotionCostCoins = 25;
    [SerializeField] private int reputationStarCostCoins = 17;

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

    [Header("Promotion Decision Animation")]
    [SerializeField] private float decisionSlideSeconds = 0.45f;
    [SerializeField] private float decisionSlideStartRightOffset = 650f;
    [SerializeField] private float decisionStarsSlideDelaySeconds = 0f;
    [SerializeField] private float decisionCoinsSlideDelaySeconds = 0.12f;

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

    [Header("End Screen Audio")]
    [SerializeField] private AudioSource endScreenAudioSource;
    [SerializeField] private AudioClip promotionScreenClip;
    [SerializeField] private AudioClip loseScreenClip;
    [Range(0f, 1f)]
    [SerializeField] private float endScreenVolume = 1f;

    [Header("Debug")]
    [SerializeField] private bool debugEnable;
    [SerializeField] private bool debugLog;
    [SerializeField] private bool debugWaitForGameStart = true;
    [SerializeField] private bool debugApplyOnce = true;
    [SerializeField] private bool debugForceShowSummary;
    [SerializeField] private int debugSummaryDay = 3;
    [SerializeField] private bool debugSummaryIsFinalDay = true;
    [SerializeField] private bool debugOverrideReputation;
    [SerializeField] private int debugReputationValue;
    [SerializeField] private bool debugOverrideCoins;
    [SerializeField] private int debugCoinsValue;
    [SerializeField] private int debugGiveCoinsAmount = 5;
    [SerializeField] private KeyCode debugGiveCoinsKey = KeyCode.F5;
    [SerializeField] private KeyCode debugGiveRepKey = KeyCode.F6;
    [SerializeField] private KeyCode debugApplyOverridesKey = KeyCode.F7;
    [SerializeField] private KeyCode debugNextDayKey = KeyCode.F8;

    private Coroutine starRoutine;
    private Coroutine promotionStarPopRoutine;
    private Coroutine promotionDecisionRoutine;
    private Coroutine decisionSlideRoutine;
    private Coroutine resultImageRevealRoutine;
    private float previousTimeScale = 1f;
    private int lastSummaryDay = -1;
    private bool debugApplied;

    private bool waitingForPromotionDecision = false;
    private bool legacyReputationStarVisibleState = true;

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


        EnsureEndScreenAudioSource();

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
        HideResultImages();


        if (debugEnable)
            StartCoroutine(ApplyDebugWhenReady());

        if (debugEnable && debugLog)
            Debug.Log("[DayEndSummaryUI][Debug] Enabled.", this);
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
        HideResultImages();

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
        UpdateLegacyReputationStarVisibility();
        RefreshPromotionStars();
        RefreshPromotionCoins();
        ShowPromotionStars(waitingForPromotionDecision);
        ShowPromotionCoins(waitingForPromotionDecision);

        if (waitingForPromotionDecision)
            StartPromotionDecisionSlideIn();

        if (waitingForPromotionDecision)
        {
            int rep = reputation == null ? 0 : reputation.Reputation;
            int coins = coinWallet == null ? 0 : coinWallet.Coins;
            Debug.Log($"[DayEndSummaryUI] Try-to-promote UI spawned on day {dayNumber}. Rep={rep}, Coins={coins}");
        }

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
        bool hasRequiredReputation = rep >= reputationRequiredToPromote;

        if (continueButton != null)
            continueButton.interactable = false;

        RefreshPromotionCoins();
        if (!hasRequiredReputation)
        {
            bool insufficientCoins;
            bool purchasedStar = TryPurchaseReputationStar(out insufficientCoins);
            if (purchasedStar)
            {
                if (continueButton != null)
                    continueButton.interactable = true;
                promotionDecisionRoutine = null;
                yield break;
            }

            AnimatePromotionStars(false);
            if (insufficientCoins)
            {
                yield return ShowFiredAfterDelay();
                promotionDecisionRoutine = null;
                yield break;
            }

            if (continueButton != null)
                continueButton.interactable = true;
            promotionDecisionRoutine = null;
            yield break;
        }

        if (coinWallet == null || !coinWallet.TrySpend(promotionCostCoins))
        {
            AnimatePromotionStars(false);
            yield return ShowFiredAfterDelay();
            promotionDecisionRoutine = null;
            yield break;
        }

        RefreshPromotionCoins();
        AnimatePromotionStars(true);

        float waitSeconds = Mathf.Max(0f, promotionStarShakeSeconds + promotionDecisionDelaySeconds);
        if (waitSeconds > 0f)
            yield return new WaitForSecondsRealtime(waitSeconds);

        HideRoot();
        Time.timeScale = 0f;

        ShowPanel(promotionScreen, promotionCanvasGroup);
        PlayEndScreenAudio(promotionScreenClip);
        StartResultImageReveal(promotionResultImage);

        Debug.Log($"[DayEndSummaryUI] PROMOTED ✅ (Rep={rep}/{reputationRequiredToPromote})");

        if (continueButton != null)
            continueButton.interactable = true;
        promotionDecisionRoutine = null;
    }

    private bool TryPurchaseReputationStar(out bool insufficientCoins)
    {
        insufficientCoins = false;
        if (reputation == null) return false;
        if (reputation.Reputation >= reputationRequiredToPromote) return false;
        if (coinWallet == null || !coinWallet.TrySpend(reputationStarCostCoins))
        {
            insufficientCoins = true;
            return false;
        }

        if (!reputation.TryIncreaseReputation())
        {
            coinWallet.AddCoins(reputationStarCostCoins);
            return false;
        }

        RefreshPromotionCoins();
        RefreshPromotionStars();
        PlayStarResultAudio(true);
        PlayPromotionStarPop(reputation.Reputation - 1);
        return true;
    }

    private IEnumerator ShowFiredAfterDelay()
    {
        float waitSeconds = Mathf.Max(0f, promotionStarShakeSeconds + promotionDecisionDelaySeconds);
        if (waitSeconds > 0f)
            yield return new WaitForSecondsRealtime(waitSeconds);

        HideRoot();
        Time.timeScale = 0f;

        ShowPanel(firedScreen, firedCanvasGroup);
        PlayEndScreenAudio(loseScreenClip);
        StartResultImageReveal(firedResultImage);
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

    private void PlayPromotionStarPop(int index)
    {
        if (promotionStars == null || index < 0 || index >= promotionStars.Length) return;
        if (promotionStars[index] == null) return;

        if (promotionStarPopRoutine != null)
            StopCoroutine(promotionStarPopRoutine);

        promotionStarPopRoutine = StartCoroutine(PromotionStarPopRoutine(promotionStars[index]));
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

    private void EnsureEndScreenAudioSource()
    {
        if (endScreenAudioSource == null)
            endScreenAudioSource = GetComponent<AudioSource>();

        if (endScreenAudioSource == null)
            endScreenAudioSource = gameObject.AddComponent<AudioSource>();

        if (endScreenAudioSource != null)
            endScreenAudioSource.playOnAwake = false;
    }

    private void PlayEndScreenAudio(AudioClip clip)
    {
        if (clip == null)
            return;

        EnsureEndScreenAudioSource();
        if (endScreenAudioSource == null)
            return;

        endScreenAudioSource.PlayOneShot(clip, endScreenVolume);
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

    private IEnumerator PromotionStarPopRoutine(Image star)
    {
        if (star == null)
            yield break;

        Transform starTransform = star.transform;
        Vector3 baseScale = starTransform.localScale;
        Vector3 targetScale = baseScale * starPopScale;

        float t = 0f;
        while (t < starPopSeconds)
        {
            t += Time.unscaledDeltaTime;
            float lerp = Mathf.Clamp01(t / starPopSeconds);
            starTransform.localScale = Vector3.Lerp(baseScale, targetScale, lerp);
            yield return null;
        }

        t = 0f;
        while (t < starPopSeconds)
        {
            t += Time.unscaledDeltaTime;
            float lerp = Mathf.Clamp01(t / starPopSeconds);
            starTransform.localScale = Vector3.Lerp(targetScale, baseScale, lerp);
            yield return null;
        }

        starTransform.localScale = baseScale;
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
        UpdateLegacyReputationStarVisibility();
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

        if (!shouldShow)
            RestorePromotionStarsLayout();

        if (promotionStarsRoot != null && isInternalObject)
            promotionStarsRoot.SetActive(shouldShow);
    }

    private void ShowPromotionCoins(bool shouldShow)
    {
        bool isInternalObject = IsPromotionObjectUnderSummaryRoot(promotionCoinsRoot);

        if (!shouldShow)
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

    private void StartPromotionDecisionSlideIn()
    {
        if (decisionSlideRoutine != null)
            StopCoroutine(decisionSlideRoutine);

        decisionSlideRoutine = StartCoroutine(PromotionDecisionSlideRoutine());
    }

    private IEnumerator PromotionDecisionSlideRoutine()
    {
        RectTransform starsRect = promotionStarsRoot == null ? null : promotionStarsRoot.GetComponent<RectTransform>();
        RectTransform coinsRect = promotionCoinsRoot == null ? null : promotionCoinsRoot.GetComponent<RectTransform>();

        Vector2 starsTarget = GetAnchoredPositionForMiddleAnchor(starsRect, promotionStarsCenterOffset);
        Vector2 coinsTarget = GetAnchoredPositionForMiddleAnchor(coinsRect, promotionCoinsOffset);

        Vector2 starsStart = starsTarget + new Vector2(Mathf.Max(0f, decisionSlideStartRightOffset), 0f);
        Vector2 coinsStart = coinsTarget + new Vector2(Mathf.Max(0f, decisionSlideStartRightOffset), 0f);

        if (starsRect != null)
        {
            CaptureLayoutIfNeeded(starsRect, ref promotionStarsLayoutState, ref promotionStarsLayoutCaptured);
            starsRect.anchorMin = new Vector2(0.5f, 0.5f);
            starsRect.anchorMax = new Vector2(0.5f, 0.5f);
            starsRect.pivot = new Vector2(0.5f, 0.5f);
            starsRect.anchoredPosition = starsStart;
        }

        if (coinsRect != null)
        {
            CaptureLayoutIfNeeded(coinsRect, ref promotionCoinsLayoutState, ref promotionCoinsLayoutCaptured);
            coinsRect.anchorMin = new Vector2(0.5f, 0.5f);
            coinsRect.anchorMax = new Vector2(0.5f, 0.5f);
            coinsRect.pivot = new Vector2(0.5f, 0.5f);
            coinsRect.anchoredPosition = coinsStart;
        }

        float duration = Mathf.Max(0f, decisionSlideSeconds);
        float starsDelay = Mathf.Max(0f, decisionStarsSlideDelaySeconds);
        float coinsDelay = Mathf.Max(0f, decisionCoinsSlideDelaySeconds);
        float totalDuration = Mathf.Max(starsDelay + duration, coinsDelay + duration);

        if (duration <= 0f)
        {
            if (starsRect != null) starsRect.anchoredPosition = starsTarget;
            if (coinsRect != null) coinsRect.anchoredPosition = coinsTarget;
            decisionSlideRoutine = null;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < totalDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float starsT = Mathf.Clamp01((elapsed - starsDelay) / duration);
            float coinsT = Mathf.Clamp01((elapsed - coinsDelay) / duration);
            starsT = 1f - Mathf.Pow(1f - starsT, 3f);
            coinsT = 1f - Mathf.Pow(1f - coinsT, 3f);

            if (starsRect != null)
                starsRect.anchoredPosition = Vector2.LerpUnclamped(starsStart, starsTarget, starsT);
            if (coinsRect != null)
                coinsRect.anchoredPosition = Vector2.LerpUnclamped(coinsStart, coinsTarget, coinsT);

            yield return null;
        }

        if (starsRect != null) starsRect.anchoredPosition = starsTarget;
        if (coinsRect != null) coinsRect.anchoredPosition = coinsTarget;

        decisionSlideRoutine = null;
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

        CaptureLayoutIfNeeded(targetRect, ref layoutState, ref layoutCaptured);

        targetRect.anchorMin = new Vector2(0.5f, 0.5f);
        targetRect.anchorMax = new Vector2(0.5f, 0.5f);
        targetRect.pivot = new Vector2(0.5f, 0.5f);

        if (promotionMiddleAnchor == null)
        {
            targetRect.anchoredPosition = offset;
            return;
        }

        targetRect.anchoredPosition = GetAnchoredPositionForMiddleAnchor(targetRect, offset);
    }

    private Vector2 GetAnchoredPositionForMiddleAnchor(RectTransform targetRect, Vector2 offset)
    {
        if (targetRect == null)
            return offset;

        if (promotionMiddleAnchor == null)
            return offset;

        RectTransform parentRect = targetRect.parent as RectTransform;
        if (parentRect == null)
            return offset;

        Camera uiCamera = null;
        Canvas canvas = targetRect.GetComponentInParent<Canvas>();
        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            uiCamera = canvas.worldCamera;

        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(uiCamera, promotionMiddleAnchor.position);
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screenPoint, uiCamera, out Vector2 localPoint))
            return localPoint + offset;

        return offset;
    }

    private void CaptureLayoutIfNeeded(RectTransform rect, ref RectLayoutState state, ref bool captured)
    {
        if (rect == null || captured) return;
        state = CaptureLayoutState(rect);
        captured = true;
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
        if (decisionSlideRoutine != null)
        {
            StopCoroutine(decisionSlideRoutine);
            decisionSlideRoutine = null;
        }

        if (!promotionStarsLayoutCaptured || promotionStarsRoot == null) return;

        RectTransform rect = promotionStarsRoot.GetComponent<RectTransform>();
        if (rect == null) return;

        ApplyLayoutState(rect, promotionStarsLayoutState);
    }

    private void RestorePromotionCoinsLayout()
    {
        if (decisionSlideRoutine != null)
        {
            StopCoroutine(decisionSlideRoutine);
            decisionSlideRoutine = null;
        }

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

    private void HideResultImages()
    {
        if (resultImageRevealRoutine != null)
        {
            StopCoroutine(resultImageRevealRoutine);
            resultImageRevealRoutine = null;
        }

        if (promotionResultImage != null)
            promotionResultImage.SetActive(false);

        if (firedResultImage != null)
            firedResultImage.SetActive(false);
    }

    private void StartResultImageReveal(GameObject targetImage)
    {
        HideResultImages();

        if (targetImage == null)
            return;

        resultImageRevealRoutine = StartCoroutine(DelayedShowResultImageRoutine(targetImage));
    }

    private IEnumerator DelayedShowResultImageRoutine(GameObject targetImage)
    {
        float delay = Mathf.Max(0f, resultImageRevealDelaySeconds);
        if (delay > 0f)
            yield return new WaitForSecondsRealtime(delay);

        if (targetImage != null)
        {
            targetImage.SetActive(true);
            yield return StartCoroutine(PlayResultImageImpactRoutine(targetImage));
        }

        resultImageRevealRoutine = null;
    }

    private IEnumerator PlayResultImageImpactRoutine(GameObject targetImage)
    {
        if (targetImage == null)
            yield break;

        Transform imageTransform = targetImage.transform;
        Vector3 originalScale = imageTransform.localScale;
        Vector3 impactStart = originalScale * Mathf.Max(0.1f, resultImageImpactStartScale);
        Vector3 impactEnd = originalScale * Mathf.Max(0.1f, resultImageImpactEndScale);
        Vector3 originalPosition = imageTransform.localPosition;

        float duration = Mathf.Max(0f, resultImageImpactSeconds);
        if (duration <= 0f)
        {
            imageTransform.localScale = impactEnd;
            imageTransform.localPosition = originalPosition;
            yield break;
        }

        imageTransform.localScale = impactStart;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - Mathf.Pow(1f - t, 4f);

            float shakeAmount = (1f - eased) * Mathf.Max(0f, resultImageImpactShakeStrength);
            float shakeX = Mathf.Sin(elapsed * 90f) * shakeAmount;
            float shakeY = Mathf.Cos(elapsed * 70f) * shakeAmount * 0.35f;

            imageTransform.localScale = Vector3.LerpUnclamped(impactStart, impactEnd, eased);
            imageTransform.localPosition = originalPosition + new Vector3(shakeX, shakeY, 0f);

            yield return null;
        }

        imageTransform.localScale = impactEnd;
        imageTransform.localPosition = originalPosition;
    }

    private void UpdateLegacyReputationStarVisibility()
    {
        if (reputationStar == null) return;

        if (waitingForPromotionDecision)
        {
            legacyReputationStarVisibleState = reputationStar.gameObject.activeSelf;
            reputationStar.gameObject.SetActive(false);
            return;
        }

        reputationStar.gameObject.SetActive(legacyReputationStarVisibleState);
    }

    private IEnumerator ApplyDebugWhenReady()
    {
        if (debugApplyOnce && debugApplied)
            yield break;

        if (debugWaitForGameStart)
        {
            while (!HasGameStarted())
                yield return null;
        }

        ApplyDebugOverrides();

        if (debugLog)
            Debug.Log("[DayEndSummaryUI][Debug] Overrides applied.", this);
    }

    private void Update()
    {
        if (!debugEnable) return;
        if (!HasGameStarted())
        {
            if (debugLog)
                Debug.Log("[DayEndSummaryUI][Debug] Waiting for game start.", this);
            return;
        }

        if (debugGiveCoinsKey != KeyCode.None && Input.GetKeyDown(debugGiveCoinsKey))
            DebugGiveCoins();
        if (debugGiveRepKey != KeyCode.None && Input.GetKeyDown(debugGiveRepKey))
            DebugGiveReputation();
        if (debugApplyOverridesKey != KeyCode.None && Input.GetKeyDown(debugApplyOverridesKey))
            ApplyDebugOverrides();
        if (debugNextDayKey != KeyCode.None && Input.GetKeyDown(debugNextDayKey))
            DebugAdvanceDay();
    }

    private bool HasGameStarted()
    {
        if (GameFlowManager.Instance != null)
            return !GameFlowManager.Instance.IsPaused;

        return Time.timeScale > 0f;
    }

    private void ApplyDebugOverrides()
    {
        if (debugApplyOnce && debugApplied)
            return;

        if (debugOverrideReputation && reputation != null)
            reputation.DebugSetReputation(debugReputationValue);

        if (debugOverrideCoins && coinWallet != null)
            coinWallet.DebugSetCoins(debugCoinsValue);

        RefreshPromotionStars();
        RefreshPromotionCoins();

        if (debugForceShowSummary)
            ShowSummary(debugSummaryDay, debugSummaryIsFinalDay);

        debugApplied = true;
    }

    private void DebugGiveCoins()
    {
        if (!debugEnable || !HasGameStarted()) return;
        if (coinWallet == null) return;

        int amount = Mathf.Max(0, debugGiveCoinsAmount);
        if (amount <= 0) return;

        coinWallet.AddCoins(amount);
        RefreshPromotionCoins();

        if (debugLog)
            Debug.Log($"[DayEndSummaryUI][Debug] Added {amount} coins.", this);
    }


    private void DebugGiveReputation()
    {
        if (!debugEnable || !HasGameStarted()) return;
        if (reputation == null) return;

        if (reputation.TryIncreaseReputation())
        {
            RefreshPromotionStars();
            PlayPromotionStarPop(reputation.Reputation - 1);

            if (debugLog)
                Debug.Log("[DayEndSummaryUI][Debug] Added 1 reputation.", this);
        }
        else
        {
            if (debugLog)
                Debug.Log("[DayEndSummaryUI][Debug] Reputation already at max.", this);
        }
    }

    private void DebugAdvanceDay()
    {
        if (!debugEnable || !HasGameStarted()) return;
        if (dayCycle == null) return;

        if (waitingForPromotionDecision)
        {
            dayCycle.DebugAdvanceDay();
            Hide();
            if (debugLog)
                Debug.Log("[DayEndSummaryUI][Debug] Advance day from promotion summary.", this);
            return;
        }

        dayCycle.DebugAdvanceDay();

        if (debugLog)
            Debug.Log("[DayEndSummaryUI][Debug] Advance day hotkey used.", this);
    }

}
