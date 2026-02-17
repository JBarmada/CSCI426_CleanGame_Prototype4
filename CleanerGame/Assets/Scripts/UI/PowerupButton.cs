using UnityEngine;
using UnityEngine.UI;

public class PowerupButton : MonoBehaviour
{
    [Header("Wiring")]
    [SerializeField] private CoinWallet wallet;
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
        if (button == null) button = GetComponent<Button>();
        if (icon == null) icon = GetComponent<Image>();

        if (button != null)
            button.onClick.AddListener(OnClickBuy);

        if (wallet != null)
            wallet.CoinsChanged += HandleCoinsChanged;

        Refresh();
    }

    private void OnDestroy()
    {
        if (wallet != null)
            wallet.CoinsChanged -= HandleCoinsChanged;
    }

    private void HandleCoinsChanged(int _)
    {
        Refresh();
    }

    private void Refresh()
    {
        if (wallet == null || button == null || icon == null) return;

        bool canAfford = wallet.Coins >= broomCost;
        button.interactable = canAfford;

        var c = icon.color;
        c.a = canAfford ? enabledAlpha : disabledAlpha;
        icon.color = c;
    }

    private void OnClickBuy()
    {
        if (wallet == null) return;

        if (!wallet.TrySpend(broomCost))
            return;

        // Optional: immediately dim if you can’t afford again
        Refresh();

        // ✅ Trigger your actual broom powerup here
        Debug.Log("Broom powerup purchased!");
    }
}
