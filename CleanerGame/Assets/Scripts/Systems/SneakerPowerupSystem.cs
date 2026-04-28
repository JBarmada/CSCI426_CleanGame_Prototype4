using System;
using UnityEngine;

public class SneakerPowerupSystem : MonoBehaviour
{
    public static SneakerPowerupSystem Instance { get; private set; }

    [Header("Limits (per restaurant day)")]
    [SerializeField] private int maxUsesPerDay = 2;

    [Header("Effect")]
    [Tooltip("Each purchase multiplies movement speed by this amount (stacks like the mop power-up).")]
    [SerializeField] private float movementMultiplierPerUse = 1.5f;
    [SerializeField] private bool stackUses = true;

    [Header("Wiring")]
    [SerializeField] private RestaurantDayCycle dayCycle;

    [Header("Debug (Read Only)")]
    [SerializeField] private int debugUsesToday;
    [SerializeField] private int debugUsesLeft;
    [SerializeField] private float debugCurrentMultiplier;
    [SerializeField] private int debugTotalUses;

    public event Action OnChanged;

    private int usesToday;
    private int totalUses;

    public int UsesToday => usesToday;
    public int UsesLeftToday => Mathf.Max(0, maxUsesPerDay - usesToday);
    public int MaxUsesPerDay => maxUsesPerDay;
    public int TotalUses => totalUses;
    public bool IsOwned => totalUses > 0;

    public float CurrentMultiplier
    {
        get
        {
            if (totalUses <= 0) return 1f;
            if (!stackUses) return movementMultiplierPerUse;
            return Mathf.Pow(movementMultiplierPerUse, totalUses);
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        if (dayCycle == null)
            dayCycle = FindFirstObjectByType<RestaurantDayCycle>();

        if (dayCycle != null)
            dayCycle.DayStarted += HandleDayStarted;

        usesToday = 0;
        totalUses = 0;
        UpdateDebug();
        OnChanged?.Invoke();
    }

    private void OnDestroy()
    {
        if (dayCycle != null)
            dayCycle.DayStarted -= HandleDayStarted;

        if (Instance == this)
            Instance = null;
    }

    private void HandleDayStarted(int newDayCount)
    {
        usesToday = 0;
        UpdateDebug();
        OnChanged?.Invoke();
        Debug.Log($"[SneakerPowerup] New day {newDayCount} -> uses reset.");
    }

    public bool CanUseToday()
    {
        return usesToday < maxUsesPerDay;
    }

    public bool TryConsumeUse()
    {
        if (!CanUseToday()) return false;

        usesToday++;
        totalUses++;
        UpdateDebug();
        OnChanged?.Invoke();
        Debug.Log($"[SneakerPowerup] Used {usesToday}/{maxUsesPerDay} today, total {totalUses}. Mult={CurrentMultiplier:F2}x");
        return true;
    }

    private void UpdateDebug()
    {
        debugUsesToday = usesToday;
        debugUsesLeft = UsesLeftToday;
        debugCurrentMultiplier = CurrentMultiplier;
        debugTotalUses = totalUses;
    }
}
