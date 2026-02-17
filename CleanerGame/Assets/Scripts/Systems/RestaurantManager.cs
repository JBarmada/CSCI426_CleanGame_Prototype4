using UnityEngine;
using System;

public class RestaurantManager : MonoBehaviour
{
    public static RestaurantManager Instance;

    public enum DirtinessLevel
    {
        Clean,
        Dirty,
        VeryDirty,
        Filthy
    }


    public int Dirtiness { get; private set; }
    public int Popularity { get; private set; }

    [Header("Dirtiness Tracking")]
    [SerializeField] private float dirtinessRefreshSeconds = 1f;
    [SerializeField] private RestaurantDayCycle dayCycle;
    [SerializeField] private int filthyLimit = 3;
    [SerializeField] private bool useBossCutscene = true;

    [Header("Customer Caps")]
    [SerializeField] private int cleanMaxCustomers = 12;
    [SerializeField] private int someDirtinessMinSpills = 1;
    [SerializeField] private int mediumDirtinessMinSpills = 4;
    [SerializeField] private int veryDirtyMinSpills = 6;
    [SerializeField] private int tooMuchDirtinessMinSpills = 7;
    [SerializeField] private int tooMuchDirtinessSpan = 5;
    [SerializeField] private Vector2Int someDirtinessCustomers = new Vector2Int(8, 10);
    [SerializeField] private Vector2Int mediumDirtinessCustomers = new Vector2Int(4, 6);
    [SerializeField] private Vector2Int tooMuchDirtinessCustomers = new Vector2Int(1, 2);

    private float dirtinessTimer;
    private int cachedMaxCustomers;
    private DirtinessLevel currentDirtinessLevel;
    private DirtinessLevel previousDirtinessLevel;
    private float veryDirtySeconds;
    private float filthySeconds;
    private int filthyCount;
    private bool gameOverTriggered;
    private bool pendingFilthyStrike;

    public event Action<int> FilthyCountChanged;
    public event Action GameOverByFilth;
    public event Action FilthyStrikeTriggered;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);

        if (dayCycle == null)
            dayCycle = GetComponent<RestaurantDayCycle>();
        if (dayCycle != null)
            dayCycle.DayStarted += HandleDayStarted;

        RefreshDirtiness();
    }

    private void Update()
    {
        float deltaTime = Time.deltaTime;
        AccumulateDirtinessTime(deltaTime);

        dirtinessTimer += deltaTime;
        if (dirtinessTimer < dirtinessRefreshSeconds) return;

        dirtinessTimer = 0f;
        RefreshDirtiness();
    }

    private void OnDestroy()
    {
        if (dayCycle != null)
            dayCycle.DayStarted -= HandleDayStarted;
    }

    public void AddDirt(int amount)
    {
        Dirtiness += amount;
        Dirtiness = Mathf.Max(Dirtiness, 0);

        if (Dirtiness > 50)
            DecreasePopularity();
    }

    public void CleanDirt(int amount)
    {
        Dirtiness -= amount;
        Dirtiness = Mathf.Max(Dirtiness, 0);
    }

    void DecreasePopularity()
    {
        Popularity--;
    }

    public int GetMaxCustomersForDirtiness()
    {
        return cachedMaxCustomers;
    }

    public DirtinessLevel GetDirtinessLevel()
    {
        return currentDirtinessLevel;
    }

    public int GetFilthyCount()
    {
        return filthyCount;
    }

    public bool IsFilthyStrikePending()
    {
        return pendingFilthyStrike;
    }

    public int GetFilthyLimit()
    {
        return Mathf.Max(1, filthyLimit);
    }

    public float GetVeryDirtySeconds()
    {
        return veryDirtySeconds;
    }

    public float GetFilthySeconds()
    {
        return filthySeconds;
    }

    public int GetDirtinessLevelIndex()
    {
        return (int)currentDirtinessLevel;
    }

    public float GetFilthTimeSeconds()
    {
        return veryDirtySeconds + filthySeconds;
    }

    public float GetDirtinessCapMultiplier()
    {
        if (cleanMaxCustomers <= 0) return 0f;
        float cap = CalculateMaxCustomers(Dirtiness);
        return Mathf.Clamp01(cap / cleanMaxCustomers);
    }

    private void RefreshDirtiness()
    {
        Dirtiness = FindObjectsByType<SpillManager>(FindObjectsSortMode.None).Length;
        Dirtiness = Mathf.Max(Dirtiness, 0);
        cachedMaxCustomers = CalculateMaxCustomers(Dirtiness);
        previousDirtinessLevel = currentDirtinessLevel;
        currentDirtinessLevel = CalculateDirtinessLevel(Dirtiness);
        CheckFilthyTransition();
    }

    private void CheckFilthyTransition()
    {
        if (currentDirtinessLevel != DirtinessLevel.Filthy) return;
        if (previousDirtinessLevel == DirtinessLevel.Filthy) return;

        if (pendingFilthyStrike) return;
        pendingFilthyStrike = true;

        if (!useBossCutscene || FilthyStrikeTriggered == null)
        {
            ConfirmFilthyStrike();
            return;
        }

        FilthyStrikeTriggered?.Invoke();
    }

    public void ConfirmFilthyStrike()
    {
        if (!pendingFilthyStrike) return;
        pendingFilthyStrike = false;

        filthyCount++;
        FilthyCountChanged?.Invoke(filthyCount);

        if (!gameOverTriggered && filthyCount >= Mathf.Max(1, filthyLimit))
        {
            gameOverTriggered = true;
            GameOverByFilth?.Invoke();
        }
    }

    private DirtinessLevel CalculateDirtinessLevel(int dirtiness)
    {
        if (dirtiness <= 0) return DirtinessLevel.Clean;

        int veryDirtyMin = Mathf.Clamp(veryDirtyMinSpills, 1, Mathf.Max(1, tooMuchDirtinessMinSpills));
        if (dirtiness < veryDirtyMin) return DirtinessLevel.Dirty;
        if (dirtiness < tooMuchDirtinessMinSpills) return DirtinessLevel.VeryDirty;

        return DirtinessLevel.Filthy;
    }

    private void AccumulateDirtinessTime(float deltaTime)
    {
        if (deltaTime <= 0f) return;

        if (currentDirtinessLevel == DirtinessLevel.VeryDirty)
            veryDirtySeconds += deltaTime;
        else if (currentDirtinessLevel == DirtinessLevel.Filthy)
            filthySeconds += deltaTime;
    }

    private void HandleDayStarted(int dayNumber)
    {
        ResetFilthTimers();
    }

    public void ResetFilthTimers()
    {
        veryDirtySeconds = 0f;
        filthySeconds = 0f;
    }

    private int CalculateMaxCustomers(int dirtiness)
    {
        if (dirtiness <= 0) return cleanMaxCustomers;

        if (dirtiness < mediumDirtinessMinSpills)
            return EvaluateRange(someDirtinessCustomers, dirtiness, someDirtinessMinSpills, mediumDirtinessMinSpills - 1);

        if (dirtiness < tooMuchDirtinessMinSpills)
            return EvaluateRange(mediumDirtinessCustomers, dirtiness, mediumDirtinessMinSpills, tooMuchDirtinessMinSpills - 1);

        int tooMuchMax = tooMuchDirtinessMinSpills + Mathf.Max(1, tooMuchDirtinessSpan);
        return EvaluateRange(tooMuchDirtinessCustomers, dirtiness, tooMuchDirtinessMinSpills, tooMuchMax);
    }

    private int EvaluateRange(Vector2Int range, int value, int min, int max)
    {
        int minValue = Mathf.Min(range.x, range.y);
        int maxValue = Mathf.Max(range.x, range.y);

        if (max <= min)
            return maxValue;

        float t = Mathf.InverseLerp(min, max, value);
        return Mathf.RoundToInt(Mathf.Lerp(minValue, maxValue, t));
    }
}
