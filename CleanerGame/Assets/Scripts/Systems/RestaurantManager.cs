using System;
using UnityEngine;
using UnityEngine.Serialization;

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
    [FormerlySerializedAs("dirtinessRefreshSeconds")]
    [SerializeField] private float dirtinessRefreshIntervalSeconds = 1f;
    [FormerlySerializedAs("dayCycle")]
    [SerializeField] private RestaurantDayCycle dayCycleSystem;
    [FormerlySerializedAs("filthyLimit")]
    [SerializeField] private int filthyStrikeLimit = 3;
    [FormerlySerializedAs("useBossCutscene")]
    [SerializeField] private bool useBossStrikeCutscene = true;

    [Header("Customer Caps")]
    [FormerlySerializedAs("cleanMaxCustomers")]
    [SerializeField] private int cleanMaxCustomersCap = 12;
    [FormerlySerializedAs("someDirtinessMinSpills")]
    [SerializeField] private int lightDirtinessMinSpillCount = 1;
    [FormerlySerializedAs("mediumDirtinessMinSpills")]
    [SerializeField] private int mediumDirtinessMinSpillCount = 4;
    [FormerlySerializedAs("veryDirtyMinSpills")]
    [SerializeField] private int veryDirtyMinSpillCount = 6;
    [FormerlySerializedAs("tooMuchDirtinessMinSpills")]
    [SerializeField] private int filthyMinSpillCount = 7;
    [FormerlySerializedAs("tooMuchDirtinessSpan")]
    [SerializeField] private int filthySpillCountSpan = 5;
    [FormerlySerializedAs("someDirtinessCustomers")]
    [SerializeField] private Vector2Int lightDirtinessCustomerRange = new Vector2Int(8, 10);
    [FormerlySerializedAs("mediumDirtinessCustomers")]
    [SerializeField] private Vector2Int mediumDirtinessCustomerRange = new Vector2Int(4, 6);
    [FormerlySerializedAs("tooMuchDirtinessCustomers")]
    [SerializeField] private Vector2Int filthyDirtinessCustomerRange = new Vector2Int(1, 2);

    private float dirtinessRefreshTimer;
    private int cachedDirtinessMaxCustomers;
    private DirtinessLevel currentDirtinessTier;
    private DirtinessLevel previousDirtinessTier;
    private float veryDirtyDurationSeconds;
    private float filthyDurationSeconds;
    private int filthyStrikeCount;
    private bool gameOverByFilthTriggered;
    private bool pendingFilthyStrikeCutscene;

    public event Action<int> FilthyCountChanged;
    public event Action GameOverByFilth;
    public event Action FilthyStrikeTriggered;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);

        if (dayCycleSystem == null)
            dayCycleSystem = GetComponent<RestaurantDayCycle>();
        if (dayCycleSystem != null)
            dayCycleSystem.DayStarted += HandleDayStarted;

        if (spawnTuning == null)
            spawnTuning = FindFirstObjectByType<CustomerSpawnTuning>();

        RefreshDirtiness();
    }

    private void Update()
    {
        float deltaTime = Time.deltaTime;
        AccumulateDirtinessTime(deltaTime);

        dirtinessRefreshTimer += deltaTime;
        if (dirtinessRefreshTimer < dirtinessRefreshIntervalSeconds) return;

        dirtinessRefreshTimer = 0f;
        RefreshDirtiness();
    }

    private void OnDestroy()
    {
        if (dayCycleSystem != null)
            dayCycleSystem.DayStarted -= HandleDayStarted;
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
        return cachedDirtinessMaxCustomers;
    }

    public DirtinessLevel GetDirtinessLevel()
    {
        return currentDirtinessTier;
    }

    public int GetFilthyCount()
    {
        return filthyStrikeCount;
    }

    public bool IsFilthyStrikePending()
    {
        return pendingFilthyStrikeCutscene;
    }

    public int GetFilthyLimit()
    {
        return Mathf.Max(1, filthyStrikeLimit);
    }

    public float GetVeryDirtySeconds()
    {
        return veryDirtyDurationSeconds;
    }

    public float GetFilthySeconds()
    {
        return filthyDurationSeconds;
    }

    public int GetDirtinessLevelIndex()
    {
        return (int)currentDirtinessTier;
    }

    public float GetFilthTimeSeconds()
    {
        return veryDirtyDurationSeconds + filthyDurationSeconds;
    }

    public float GetDirtinessCapMultiplier()
    {
        int cleanCap = GetCleanMaxCustomersCap();
        if (cleanCap <= 0) return 0f;
        float cap = CalculateMaxCustomers(Dirtiness);
        return Mathf.Clamp01(cap / cleanCap);
    }

    private void RefreshDirtiness()
    {
        Dirtiness = FindObjectsByType<SpillManager>(FindObjectsSortMode.None).Length;
        Dirtiness = Mathf.Max(Dirtiness, 0);
        cachedDirtinessMaxCustomers = CalculateMaxCustomers(Dirtiness);
        previousDirtinessTier = currentDirtinessTier;
        currentDirtinessTier = CalculateDirtinessLevel(Dirtiness);
        CheckFilthyTransition();
    }

    private void CheckFilthyTransition()
    {
        if (currentDirtinessTier != DirtinessLevel.Filthy) return;
        if (previousDirtinessTier == DirtinessLevel.Filthy) return;

        if (pendingFilthyStrikeCutscene) return;
        pendingFilthyStrikeCutscene = true;

        if (!useBossStrikeCutscene || FilthyStrikeTriggered == null)
        {
            ConfirmFilthyStrike();
            return;
        }

        FilthyStrikeTriggered?.Invoke();
    }

    public void ConfirmFilthyStrike()
    {
        if (!pendingFilthyStrikeCutscene) return;
        pendingFilthyStrikeCutscene = false;

        filthyStrikeCount++;
        FilthyCountChanged?.Invoke(filthyStrikeCount);

        if (!gameOverByFilthTriggered && filthyStrikeCount >= Mathf.Max(1, filthyStrikeLimit))
        {
            gameOverByFilthTriggered = true;
            GameOverByFilth?.Invoke();
        }
    }

    private DirtinessLevel CalculateDirtinessLevel(int dirtiness)
    {
        if (dirtiness <= 0) return DirtinessLevel.Clean;

        int veryDirtyMin = Mathf.Clamp(GetVeryDirtyMinSpillCount(), 1, Mathf.Max(1, GetFilthyMinSpillCount()));
        if (dirtiness < veryDirtyMin) return DirtinessLevel.Dirty;
        if (dirtiness < GetFilthyMinSpillCount()) return DirtinessLevel.VeryDirty;

        return DirtinessLevel.Filthy;
    }

    private void AccumulateDirtinessTime(float deltaTime)
    {
        if (deltaTime <= 0f) return;

        if (currentDirtinessTier == DirtinessLevel.VeryDirty)
            veryDirtyDurationSeconds += deltaTime;
        else if (currentDirtinessTier == DirtinessLevel.Filthy)
            filthyDurationSeconds += deltaTime;
    }

    private void HandleDayStarted(int dayNumber)
    {
        ResetFilthTimers();
    }

    public void ResetFilthTimers()
    {
        veryDirtyDurationSeconds = 0f;
        filthyDurationSeconds = 0f;
    }

    private int CalculateMaxCustomers(int dirtiness)
    {
        if (dirtiness <= 0) return GetCleanMaxCustomersCap();

        int mediumMin = GetMediumDirtinessMinSpillCount();
        int filthyMin = GetFilthyMinSpillCount();
        int filthyMax = filthyMin + Mathf.Max(1, GetFilthySpillCountSpan());

        if (dirtiness < mediumMin)
            return EvaluateRange(GetLightDirtinessCustomerRange(), dirtiness, GetLightDirtinessMinSpillCount(), mediumMin - 1);

        if (dirtiness < filthyMin)
            return EvaluateRange(GetMediumDirtinessCustomerRange(), dirtiness, mediumMin, filthyMin - 1);

        return EvaluateRange(GetFilthyDirtinessCustomerRange(), dirtiness, filthyMin, filthyMax);
    }

    private int GetCleanMaxCustomersCap()
    {
        return spawnTuning == null ? 12 : spawnTuning.CleanMaxCustomersCap;
    }

    private int GetLightDirtinessMinSpillCount()
    {
        return spawnTuning == null ? 1 : spawnTuning.LightDirtinessMinSpillCount;
    }

    private int GetMediumDirtinessMinSpillCount()
    {
        return spawnTuning == null ? 4 : spawnTuning.MediumDirtinessMinSpillCount;
    }

    private int GetVeryDirtyMinSpillCount()
    {
        return spawnTuning == null ? 6 : spawnTuning.VeryDirtyMinSpillCount;
    }

    private int GetFilthyMinSpillCount()
    {
        return spawnTuning == null ? 7 : spawnTuning.FilthyMinSpillCount;
    }

    private int GetFilthySpillCountSpan()
    {
        return spawnTuning == null ? 5 : spawnTuning.FilthySpillCountSpan;
    }

    private Vector2Int GetLightDirtinessCustomerRange()
    {
        return spawnTuning == null ? new Vector2Int(8, 10) : spawnTuning.LightDirtinessCustomerRange;
    }

    private Vector2Int GetMediumDirtinessCustomerRange()
    {
        return spawnTuning == null ? new Vector2Int(4, 6) : spawnTuning.MediumDirtinessCustomerRange;
    }

    private Vector2Int GetFilthyDirtinessCustomerRange()
    {
        return spawnTuning == null ? new Vector2Int(1, 2) : spawnTuning.FilthyDirtinessCustomerRange;
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
