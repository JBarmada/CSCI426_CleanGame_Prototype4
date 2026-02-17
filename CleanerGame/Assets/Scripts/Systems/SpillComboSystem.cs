using System;
using UnityEngine;

public class SpillComboSystem : MonoBehaviour
{
    public static SpillComboSystem Instance { get; private set; }

    [Header("Combo")]
    [SerializeField] private float comboTimeoutSeconds = 3f;
    [SerializeField] private float multiplierStepPerCombo = 0.25f;
    [SerializeField] private float maxCoinMultiplier = 3f;

    [Header("Break Conditions")]
    [SerializeField] private RestaurantManager restaurantManager;
    [SerializeField] private RestaurantManager.DirtinessLevel breakAtOrAboveDirtiness = RestaurantManager.DirtinessLevel.VeryDirty;

    [Header("Wiring")]
    [SerializeField] private RestaurantDayCycle dayCycle;
    [SerializeField] private bool resetOnNewDay = true;

    [Header("Debug (Read Only)")]
    [SerializeField] private int debugComboCount;
    [SerializeField] private float debugCurrentMultiplier;
    [SerializeField] private float debugSecondsBeforeTimeout;

    public event Action OnComboChanged;

    public int ComboCount => comboCount;
    public float CurrentCoinMultiplier => GetMultiplierForCombo(comboCount);
    public float RemainingComboSeconds => comboCount <= 0
        ? 0f
        : Mathf.Max(0f, comboTimeoutSeconds - timeSinceLastClean);

    private int comboCount;
    private float timeSinceLastClean;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        comboCount = 0;
        timeSinceLastClean = 0f;
        UpdateDebug();
    }

    private void Start()
    {
        if (restaurantManager == null)
            restaurantManager = RestaurantManager.Instance;

        if (dayCycle == null)
            dayCycle = FindFirstObjectByType<RestaurantDayCycle>();

        if (dayCycle != null)
            dayCycle.DayStarted += HandleDayStarted;
    }

    private void OnDestroy()
    {
        if (dayCycle != null)
            dayCycle.DayStarted -= HandleDayStarted;
    }

    private void Update()
    {
        if (comboCount <= 0)
        {
            UpdateDebug();
            return;
        }

        timeSinceLastClean += Time.deltaTime;

        if (timeSinceLastClean > comboTimeoutSeconds)
        {
            ResetCombo();
            return;
        }

        if (restaurantManager == null)
            restaurantManager = RestaurantManager.Instance;

        if (restaurantManager != null)
        {
            var dirtiness = restaurantManager.GetDirtinessLevel();
            if (dirtiness >= breakAtOrAboveDirtiness)
            {
                ResetCombo();
                return;
            }
        }

        UpdateDebug();
    }

    public int RegisterSpillCleanAndGetCoins(int baseCoins)
    {
        if (baseCoins <= 0)
            return 0;

        bool continuesCombo = comboCount > 0 && timeSinceLastClean <= comboTimeoutSeconds;
        comboCount = continuesCombo ? comboCount + 1 : 1;
        timeSinceLastClean = 0f;

        float multiplier = GetMultiplierForCombo(comboCount);
        int finalCoins = Mathf.Max(1, Mathf.RoundToInt(baseCoins * multiplier));

        UpdateDebug();
        OnComboChanged?.Invoke();
        return finalCoins;
    }

    public void ResetCombo()
    {
        if (comboCount <= 0)
            return;

        comboCount = 0;
        timeSinceLastClean = 0f;
        UpdateDebug();
        OnComboChanged?.Invoke();
    }

    private void HandleDayStarted(int dayNumber)
    {
        if (!resetOnNewDay)
            return;

        ResetCombo();
    }

    private float GetMultiplierForCombo(int combo)
    {
        if (combo <= 1)
            return 1f;

        float multiplier = 1f + (combo - 1) * multiplierStepPerCombo;
        return Mathf.Clamp(multiplier, 1f, Mathf.Max(1f, maxCoinMultiplier));
    }

    private void UpdateDebug()
    {
        debugComboCount = comboCount;
        debugCurrentMultiplier = CurrentCoinMultiplier;
        debugSecondsBeforeTimeout = RemainingComboSeconds;
    }
}