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

    private void Awake()
    {
        if (wallet == null) wallet = CoinWallet.Instance != null ? CoinWallet.Instance : FindFirstObjectByType<CoinWallet>();
        if (broomSystem == null) broomSystem = BroomPowerupSystem.Instance != null ? BroomPowerupSystem.Instance : FindFirstObjectByType<BroomPowerupSystem>();
        if (button == null) button = GetComponent<Button>();
        if (icon == null) icon = GetComponent<Image>();

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
        if (wallet == null || button == null || icon == null || broomSystem == null) return;

        bool canAfford = wallet.Coins >= broomCost;
        bool hasUses = broomSystem.CanUseToday();

        bool canBuy = canAfford && hasUses;
        button.interactable = canBuy;

        var c = icon.color;
        c.a = canBuy ? enabledAlpha : disabledAlpha;
        icon.color = c;
    }

    private void OnClickBuy()
    {
        if (wallet == null || broomSystem == null) return;

        // must have remaining uses
        if (!broomSystem.CanUseToday())
        {
            Refresh();
            return;
        }

        // must be able to pay
        if (!wallet.TrySpend(broomCost))
        {
            Refresh();
            return;
        }

        // consume one daily use (activates +15% speed effect)
        if (!broomSystem.TryConsumeUse())
        {
            // edge case: day rollover or race; refund if you want
            wallet.AddCoins(broomCost);
            Refresh();
            return;
        }

        Debug.Log($"Broom powerup used! Uses today: {broomSystem.UsesToday}. Multiplier now: {broomSystem.CurrentMultiplier:F2}x");
        Refresh();
    }
}
