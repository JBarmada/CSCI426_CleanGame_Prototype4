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
    [SerializeField] private AudioClip activateClip;
    [Range(0f, 1f)]
    [SerializeField] private float activateVolume = 1f;

    private void Awake()
    {
        // Components on same object
        if (button == null) button = GetComponent<Button>();
        if (icon == null) icon = GetComponent<Image>();

        // Prefer singletons, fallback to find
        if (wallet == null) wallet = CoinWallet.Instance != null ? CoinWallet.Instance : FindFirstObjectByType<CoinWallet>();
        if (broomSystem == null) broomSystem = BroomPowerupSystem.Instance != null ? BroomPowerupSystem.Instance : FindFirstObjectByType<BroomPowerupSystem>();

        // AudioSource convenience
        if (audioSource == null && activateClip != null)
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

    private void HandleCoinsChanged(int _) => Refresh();

    private void Refresh()
    {
        // If anything critical is missing, make it clearly disabled (not “fake active”)
        if (button == null || icon == null)
            return;

        if (wallet == null || broomSystem == null)
        {
            button.interactable = false;
            SetIconAlpha(disabledAlpha);
            return;
        }

        bool canAfford = wallet.Coins >= broomCost;
        bool hasUses = broomSystem.CanUseToday();

        bool canBuy = canAfford && hasUses;
        button.interactable = canBuy;

        SetIconAlpha(canBuy ? enabledAlpha : disabledAlpha);
    }

    private void SetIconAlpha(float a)
    {
        if (icon == null) return;
        var c = icon.color;
        c.a = a;
        icon.color = c;
    }

    private void OnClickBuy()
    {
        // Re-acquire in case scene load order changed
        if (wallet == null) wallet = CoinWallet.Instance != null ? CoinWallet.Instance : FindFirstObjectByType<CoinWallet>();
        if (broomSystem == null) broomSystem = BroomPowerupSystem.Instance != null ? BroomPowerupSystem.Instance : FindFirstObjectByType<BroomPowerupSystem>();

        if (wallet == null || broomSystem == null)
        {
            Debug.LogWarning("[PowerupButton] Missing CoinWallet or BroomPowerupSystem.");
            Refresh();
            return;
        }

        if (!broomSystem.CanUseToday())
        {
            Debug.Log("[PowerupButton] No powerup uses left today.");
            Refresh();
            return;
        }

        if (!wallet.TrySpend(broomCost))
        {
            Debug.Log("[PowerupButton] Not enough coins.");
            Refresh();
            return;
        }

        if (!broomSystem.TryConsumeUse())
        {
            // Safety refund
            wallet.AddCoins(broomCost);
            Debug.LogWarning("[PowerupButton] Failed to consume use. Refunded coins.");
            Refresh();
            return;
        }

        // ✅ Play activation SFX ONLY on success
        if (activateClip != null)
        {
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
                if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
            }

            audioSource.PlayOneShot(activateClip, activateVolume);
        }

        Debug.Log($"[PowerupButton] Broom powerup activated! UsesToday={broomSystem.UsesToday}, Mult={broomSystem.CurrentMultiplier:F2}x");
        Refresh();
    }
}
