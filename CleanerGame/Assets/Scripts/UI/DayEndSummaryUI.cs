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
    private float previousTimeScale = 1f;
    private int lastSummaryDay = -1;

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
            coinWallet = FindFirstObjectByType<CoinWallet>();

        if (continueButton != null)
            continueButton.onClick.AddListener(OnContinuePressed);
    }

    private void OnEnable()
    {
        if (dayCycle != null)
            dayCycle.DayEnded += HandleDayEnded;

        Hide();
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

        float filthTime = restaurantManager == null ? 0f : restaurantManager.GetFilthTimeSeconds();
        float filthyTime = restaurantManager == null ? 0f : restaurantManager.GetFilthySeconds();
        int spillsCleaned = spillTracker == null ? 0 : spillTracker.SpillsCleaned;

        int salaryBonus = CalculateSalaryBonus(filthTime);
        if (coinWallet != null && salaryBonus > 0)
            coinWallet.AddCoins(salaryBonus);

        bool earnedStar = filthyTime < 3f;
        if (earnedStar && reputation != null)
            reputation.TryIncreaseReputation();

        UpdateUI(spillsCleaned, filthTime, salaryBonus, earnedStar);

        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        ShowRoot();
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

        if (starPopClip != null)
            PlayStarAudio(starPopClip);
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

    private void OnContinuePressed()
    {
        Hide();

        if (dayCycle != null)
            dayCycle.ContinueToNextDay();
    }

    private void Hide()
    {
        HideRoot();

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
}
