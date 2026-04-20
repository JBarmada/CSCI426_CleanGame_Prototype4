using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CoinHud : MonoBehaviour
{
    [SerializeField] private TMP_Text coinText;
    [SerializeField] private CoinWallet wallet;
    [SerializeField] private RestaurantDayCycle dayCycle;
    [SerializeField] private Image coinIcon;
    [SerializeField] private float coinPulseScale = 1.2f;
    [SerializeField] private float coinPulseDuration = 0.12f;
    [SerializeField] private float textPulseScale = 1.15f;

    [Header("Layering")]
    [SerializeField] private int sortingOrder = 300;

    private int lastWalletCoins;
    private Coroutine coinPulseRoutine;
    private Vector3 coinIconBaseScale;
    private Vector3 coinTextBaseScale;

    private void Awake()
    {
        if (coinText == null)
            coinText = GetComponent<TMP_Text>();

        if (coinIcon == null)
            coinIcon = GetComponentInChildren<Image>(true);

        if (coinIcon != null)
            coinIconBaseScale = coinIcon.transform.localScale;

        if (coinText != null)
            coinTextBaseScale = coinText.transform.localScale;

        if (dayCycle == null)
            dayCycle = FindFirstObjectByType<RestaurantDayCycle>();

        UISortingUtility.EnsureSorting(gameObject, sortingOrder);
    }

    private void OnEnable()
    {
        ResolveWallet();
        ResolveDayCycle();
        if (wallet != null)
        {
            wallet.CoinsChanged += HandleCoinsChanged;
            lastWalletCoins = wallet.Coins;
        }

        if (dayCycle != null)
            dayCycle.DayStarted += HandleDayStarted;

        Refresh();
    }

    private void OnDisable()
    {
        if (wallet != null)
            wallet.CoinsChanged -= HandleCoinsChanged;

        if (dayCycle != null)
            dayCycle.DayStarted -= HandleDayStarted;

        if (coinPulseRoutine != null)
        {
            StopCoroutine(coinPulseRoutine);
            coinPulseRoutine = null;
        }

        if (coinIcon != null)
            coinIcon.transform.localScale = coinIconBaseScale;

        if (coinText != null)
            coinText.transform.localScale = coinTextBaseScale;
    }

    private void HandleCoinsChanged(int newAmount)
    {
        if (newAmount > lastWalletCoins)
        {
            PlayCoinPulse();
        }

        lastWalletCoins = newAmount;
        Refresh();
    }

    private void HandleDayStarted(int _)
    {
        Refresh();
    }

    private void ResolveWallet()
    {
        if (wallet != null) return;
        wallet = CoinWallet.Instance != null ? CoinWallet.Instance : FindFirstObjectByType<CoinWallet>();
    }

    private void ResolveDayCycle()
    {
        if (dayCycle != null) return;
        dayCycle = FindFirstObjectByType<RestaurantDayCycle>();
    }

    private void Refresh()
    {
        if (coinText == null) return;
        coinText.text = wallet == null ? "0" : wallet.Coins.ToString();
    }

    private void PlayCoinPulse()
    {
        if (coinIcon == null)
            return;

        if (coinPulseRoutine != null)
            StopCoroutine(coinPulseRoutine);

        coinPulseRoutine = StartCoroutine(CoinPulseRoutine());
    }

    private IEnumerator CoinPulseRoutine()
    {
        Vector3 startScale = coinIconBaseScale;
        Vector3 peakScale = startScale * coinPulseScale;
        float halfDuration = coinPulseDuration * 0.5f;
        Vector3 textPeakScale = coinTextBaseScale * Mathf.Max(1f, textPulseScale);

        for (float time = 0f; time < halfDuration; time += Time.unscaledDeltaTime)
        {
            float t = time / halfDuration;
            coinIcon.transform.localScale = Vector3.Lerp(startScale, peakScale, t);
            if (coinText != null)
                coinText.transform.localScale = Vector3.Lerp(coinTextBaseScale, textPeakScale, t);
            yield return null;
        }

        for (float time = 0f; time < halfDuration; time += Time.unscaledDeltaTime)
        {
            float t = time / halfDuration;
            coinIcon.transform.localScale = Vector3.Lerp(peakScale, startScale, t);
            if (coinText != null)
                coinText.transform.localScale = Vector3.Lerp(textPeakScale, coinTextBaseScale, t);
            yield return null;
        }

        coinIcon.transform.localScale = startScale;
        if (coinText != null)
            coinText.transform.localScale = coinTextBaseScale;
        coinPulseRoutine = null;
    }
}
