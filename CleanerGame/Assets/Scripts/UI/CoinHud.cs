using TMPro;
using UnityEngine;

public class CoinHud : MonoBehaviour
{
    [SerializeField] private TMP_Text coinText;
    [SerializeField] private string prefix = "Coins: ";
    [SerializeField] private CoinWallet wallet;

    private void Awake()
    {
        if (coinText == null)
            coinText = GetComponent<TMP_Text>();
    }

    private void OnEnable()
    {
        ResolveWallet();
        if (wallet != null)
            wallet.CoinsChanged += HandleCoinsChanged;

        Refresh();
    }

    private void OnDisable()
    {
        if (wallet != null)
            wallet.CoinsChanged -= HandleCoinsChanged;
    }

    private void HandleCoinsChanged(int newAmount)
    {
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
        int amount = wallet == null ? 0 : wallet.Coins;
        coinText.text = prefix + amount;
    }
}
