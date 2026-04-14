using UnityEngine;
using UnityEngine.UI;

public class SneakerPowerupButton : MonoBehaviour
{
    [Header("Wiring")]
    [SerializeField] private CoinWallet wallet;
    [SerializeField] private SneakerPowerupSystem sneakerSystem;
    [SerializeField] private Button button;
    [SerializeField] private Image icon;

    [Header("Cost")]
    [SerializeField] private int sneakerCost = 9;

    [Header("Visuals")]
    [SerializeField] private float disabledAlpha = 0.25f;
    [SerializeField] private float enabledAlpha = 1f;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip availableClip;
    [Range(0f, 1f)]
    [SerializeField] private float availableVolume = 1f;
    [SerializeField] private AudioClip purchasedClip;
    [Range(0f, 1f)]
    [SerializeField] private float purchasedVolume = 1f;

    private bool wasAvailableLastFrame = false;

    private void Awake()
    {
        if (button == null) button = GetComponent<Button>();
        if (icon == null) icon = GetComponent<Image>();

        if (wallet == null)
            wallet = CoinWallet.Instance != null ? CoinWallet.Instance : FindFirstObjectByType<CoinWallet>();

        if (sneakerSystem == null)
            sneakerSystem = SneakerPowerupSystem.Instance != null ? SneakerPowerupSystem.Instance : FindFirstObjectByType<SneakerPowerupSystem>();

        if (audioSource == null && availableClip != null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
                audioSource = gameObject.AddComponent<AudioSource>();

            audioSource.playOnAwake = false;
        }

        if (button != null)
            button.onClick.AddListener(OnClickBuy);

        if (wallet != null)
            wallet.CoinsChanged += HandleCoinsChanged;

        if (sneakerSystem != null)
            sneakerSystem.OnChanged += Refresh;

        Refresh();
    }

    private void OnDestroy()
    {
        if (wallet != null) wallet.CoinsChanged -= HandleCoinsChanged;
        if (sneakerSystem != null) sneakerSystem.OnChanged -= Refresh;
    }

    private void HandleCoinsChanged(int _)
    {
        Refresh();
    }

    private void Refresh()
    {
        if (button == null || icon == null || wallet == null || sneakerSystem == null)
            return;

        bool canAfford = wallet.Coins >= sneakerCost;
        bool hasUses = sneakerSystem.CanUseToday();
        bool isAvailableNow = canAfford && hasUses;

        if (isAvailableNow && !wasAvailableLastFrame)
            PlayAvailableSound();

        wasAvailableLastFrame = isAvailableNow;

        button.interactable = isAvailableNow;
        SetIconAlpha(isAvailableNow ? enabledAlpha : disabledAlpha);
    }

    private void PlayAvailableSound()
    {
        if (availableClip == null) return;

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
                audioSource = gameObject.AddComponent<AudioSource>();

            audioSource.playOnAwake = false;
        }

        audioSource.PlayOneShot(availableClip, availableVolume);
    }

    private void SetIconAlpha(float a)
    {
        var c = icon.color;
        c.a = a;
        icon.color = c;
    }

    private void OnClickBuy()
    {
        if (wallet == null || sneakerSystem == null) return;

        if (!sneakerSystem.CanUseToday()) return;
        if (!wallet.TrySpend(sneakerCost)) return;
        if (!sneakerSystem.TryConsumeUse())
        {
            wallet.AddCoins(sneakerCost);
            return;
        }

        PlayPurchasedSound();
        Debug.Log($"[SneakerPowerupButton] Purchased. Uses={sneakerSystem.UsesToday}, Mult={sneakerSystem.CurrentMultiplier:F2}x");
        Refresh();
    }

    private void PlayPurchasedSound()
    {
        if (purchasedClip == null) return;

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
                audioSource = gameObject.AddComponent<AudioSource>();

            audioSource.playOnAwake = false;
        }

        audioSource.PlayOneShot(purchasedClip, purchasedVolume);
    }
}
