using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CoinHud : MonoBehaviour
{
    [SerializeField] private TMP_Text coinText;
    [SerializeField] private CoinWallet wallet;
    [SerializeField] private Image coinIcon;
    [SerializeField] private float coinPulseScale = 1.2f;
    [SerializeField] private float coinPulseDuration = 0.12f;

    private int currentCoins;
    private int lastWalletCoins;
    private Coroutine coinPulseRoutine;
    private Vector3 coinIconBaseScale;

    private void Awake()
    {
        if (coinText == null)
            coinText = GetComponent<TMP_Text>();

        if (coinIcon == null)
            coinIcon = GetComponentInChildren<Image>(true);

        if (coinIcon != null)
            coinIconBaseScale = coinIcon.transform.localScale;
    }

    private void OnEnable()
    {
        ResolveWallet();
        if (wallet != null)
        {
            wallet.CoinsChanged += HandleCoinsChanged;
            lastWalletCoins = wallet.Coins;
            currentCoins = 0;
        }
        else
        {
            lastWalletCoins = 0;
            currentCoins = 0;
        }

        Refresh();
    }

    private void OnDisable()
    {
        if (wallet != null)
            wallet.CoinsChanged -= HandleCoinsChanged;

        if (coinPulseRoutine != null)
        {
            StopCoroutine(coinPulseRoutine);
            coinPulseRoutine = null;
        }

        if (coinIcon != null)
            coinIcon.transform.localScale = coinIconBaseScale;
    }

    private void HandleCoinsChanged(int newAmount)
    {
        int delta = newAmount - lastWalletCoins;
        if (delta > 0)
        {
            PlayCoinPulse();
            currentCoins += delta;
        }

        lastWalletCoins = newAmount;
        Refresh();
    }

    private void ResolveWallet()
    {
        if (wallet != null) return;
        wallet = FindFirstObjectByType<CoinWallet>();
    }

    private void Refresh()
    {
        if (coinText == null) return;
        coinText.text = currentCoins.ToString();
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

        for (float time = 0f; time < halfDuration; time += Time.unscaledDeltaTime)
        {
            float t = time / halfDuration;
            coinIcon.transform.localScale = Vector3.Lerp(startScale, peakScale, t);
            yield return null;
        }

        for (float time = 0f; time < halfDuration; time += Time.unscaledDeltaTime)
        {
            float t = time / halfDuration;
            coinIcon.transform.localScale = Vector3.Lerp(peakScale, startScale, t);
            yield return null;
        }

        coinIcon.transform.localScale = startScale;
        coinPulseRoutine = null;
    }
}
