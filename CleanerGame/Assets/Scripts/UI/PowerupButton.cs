using UnityEngine;
using UnityEngine.UI;

public class PowerupButton : MonoBehaviour
{
    [Header("Wiring")]
    [SerializeField] private CoinWallet wallet;
    [SerializeField] private BroomPowerupSystem broomSystem;
    [SerializeField] private Button button;
    [SerializeField] private Image icon;

    [Header("Cost")]
    [SerializeField] private int broomCost = 5;

    [Header("Visuals")]
    [SerializeField] private float disabledAlpha = 0.25f;
    [SerializeField] private float enabledAlpha = 1f;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip availableClip;   // 🔔 plays when button lights up
    [Range(0f, 1f)]
    [SerializeField] private float availableVolume = 1f;

    private bool wasAvailableLastFrame = false;

    private void Awake()
    {
        // Components
        if (button == null) button = GetComponent<Button>();
        if (icon == null) icon = GetComponent<Image>();

        // Systems
        if (wallet == null)
            wallet = CoinWallet.Instance != null ? CoinWallet.Instance : FindFirstObjectByType<CoinWallet>();

        if (broomSystem == null)
            broomSystem = BroomPowerupSystem.Instance != null ? BroomPowerupSystem.Instance : FindFirstObjectByType<BroomPowerupSystem>();

        // Audio setup
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

        if (broomSystem != null)
            broomSystem.OnChanged += Refresh;

        Refresh();
    }

    private void OnDestroy()
    {
        if (wallet != null) wallet.CoinsChanged -= HandleCoinsChanged;
        if (broomSystem != null) broomSystem.OnChanged -= Refresh;
    }

    private void HandleCoinsChanged(int _)
    {
        Refresh();
    }

    private void Refresh()
    {
        if (button == null || icon == null || wallet == null || broomSystem == null)
            return;

        bool canAfford = wallet.Coins >= broomCost;
        bool hasUses = broomSystem.CanUseToday();
        bool isAvailableNow = canAfford && hasUses;

        // 🔔 Play sound ONLY when becoming available
        if (isAvailableNow && !wasAvailableLastFrame)
        {
            PlayAvailableSound();
        }

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
        if (wallet == null || broomSystem == null) return;

        if (!broomSystem.CanUseToday()) return;
        if (!wallet.TrySpend(broomCost)) return;
        if (!broomSystem.TryConsumeUse())
        {
            wallet.AddCoins(broomCost); // refund safety
            return;
        }

        Debug.Log($"[PowerupButton] Broom used. Uses={broomSystem.UsesToday}, Mult={broomSystem.CurrentMultiplier:F2}x");
        Refresh();
    }
}
