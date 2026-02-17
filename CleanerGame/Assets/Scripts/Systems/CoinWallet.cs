using System;
using UnityEngine;

public class CoinWallet : MonoBehaviour
{
    [Header("Coins")]
    [SerializeField] private int startingCoins = 0;
    [SerializeField] private bool dontDestroyOnLoad = false;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip coinClip;
    [Range(0f, 1f)]
    [SerializeField] private float coinVolume = 1f;

    public static CoinWallet Instance { get; private set; }

    public int Coins => coins;

    // ✅ UI + systems can subscribe to this
    public event Action<int> CoinsChanged;

    private int coins;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        coins = Mathf.Max(0, startingCoins);

        if (dontDestroyOnLoad)
            DontDestroyOnLoad(gameObject);

        if (audioSource == null && coinClip != null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
                audioSource = gameObject.AddComponent<AudioSource>();

            audioSource.playOnAwake = false;
        }
    }

    private void Start()
    {
        CoinsChanged?.Invoke(coins);
    }

    public void AddCoins(int amount)
    {
        if (amount <= 0) return;

        coins += amount;
        CoinsChanged?.Invoke(coins);

        if (coinClip != null && audioSource != null)
            audioSource.PlayOneShot(coinClip, coinVolume);
    }

    // ============================
    // ✅ NEW: spending support
    // ============================

    public bool CanAfford(int amount)
    {
        return amount <= 0 || coins >= amount;
    }

    public bool TrySpend(int amount)
    {
        if (amount <= 0) return true;
        if (coins < amount) return false;

        coins -= amount;
        CoinsChanged?.Invoke(coins);
        return true;
    }
}
