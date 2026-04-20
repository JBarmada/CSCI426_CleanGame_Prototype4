using System;
using UnityEngine;

public class RestaurantDayCycle : MonoBehaviour
{
    public enum DayPhase
    {
        Morning,
        RushHour,
        AfternoonSlowdown,
        Closing
    }

    [Header("Day Cycle")]
    [SerializeField] private float dayLengthSeconds = 60f;
    [SerializeField] private float morningSeconds = 15f;
    [SerializeField] private float rushSeconds = 25f;
    [SerializeField] private float afternoonSeconds = 15f;
    [SerializeField] private float closingSeconds = 5f;
    [SerializeField] private bool infiniteDays = true;
    [SerializeField] private int maxDays = 4;
    [SerializeField] private bool pauseBetweenDays = true;
    [SerializeField] private CustomerSpawnTuning spawnTuning;

    [Header("Grace Period")]
    [Tooltip("Seconds after closing time ends where the player can still clean spills before the next day begins.")]
    [SerializeField] private float gracePeriodSeconds = 10f;

    [Header("Day Jingles")]
    [SerializeField] private AudioSource jingleSource;
    [SerializeField] private AudioClip morningJingle;
    [SerializeField] private AudioClip rushJingle;
    [SerializeField] private AudioClip afternoonJingle;
    [SerializeField] private AudioClip closingJingle;
    [Range(0f, 1f)]
    [SerializeField] private float jingleVolume = 1f;

    private float dayTimer;
    private int dayCount;
    private DayPhase currentPhase;
    private bool gameOver;
    private bool waitingForContinue;

    // Grace period state
    private bool  isInGracePeriod;
    private float gracePeriodTimer;

    public int DayCount => dayCount;
    public bool IsClosed => currentPhase == DayPhase.Closing || gameOver;
    public bool IsGameOver => gameOver;
    public bool IsWaitingForContinue => waitingForContinue;
    public bool InfiniteDays => infiniteDays;
    public bool IsInGracePeriod => isInGracePeriod;
    public float GracePeriodTimeRemaining => gracePeriodTimer;

    public event Action<DayPhase> PhaseChanged;
    public event Action<int>      DayStarted;
    public event Action<int, bool> DayEnded;

    // Grace period events — subscribe in UI to show/hide countdown
    public event Action<float> GracePeriodStarted;   // fired once with total seconds
    public event Action<float> GracePeriodTick;      // fired every frame with remaining seconds
    public event Action        GracePeriodEnded;     // fired when grace period is over

    private void Awake()
    {
        if (spawnTuning == null)
            spawnTuning = FindFirstObjectByType<CustomerSpawnTuning>();
        NormalizeDaySegments();
        InitializeDay();
    }

    private void Update()
    {
        UpdateDayCycle(Time.deltaTime);
    }

    public float GetSpawnMultiplier()
    {
        if (spawnTuning == null)
            return DefaultSpawnMultiplier(currentPhase);

        return spawnTuning.GetDayMultiplier(currentPhase);
    }

    private float DefaultSpawnMultiplier(DayPhase phase)
    {
        switch (phase)
        {
            case DayPhase.Morning:
                return 0.25f;
            case DayPhase.RushHour:
                return 1f;
            case DayPhase.AfternoonSlowdown:
                return 0.5f;
            default:
                return 0f;
        }
    }

    public DayPhase GetPhase()
    {
        return currentPhase;
    }

    public bool ContinueToNextDay()
    {
        if (!waitingForContinue || gameOver) return false;

        waitingForContinue = false;
        StartNextDay();
        return true;
    }

    private void InitializeDay()
    {
        dayTimer = 0f;
        dayCount = 1;
        waitingForContinue = false;
        isInGracePeriod    = false;
        gracePeriodTimer   = 0f;
        SetPhase(DayPhase.Morning);
        DayStarted?.Invoke(dayCount);
    }

    private void UpdateDayCycle(float deltaTime)
    {
        if (gameOver || waitingForContinue) return;

        // ── Grace period countdown ──────────────────────────────────────────
        if (isInGracePeriod)
        {
            gracePeriodTimer -= deltaTime;
            GracePeriodTick?.Invoke(Mathf.Max(0f, gracePeriodTimer));

            if (gracePeriodTimer <= 0f)
            {
                isInGracePeriod = false;
                GracePeriodEnded?.Invoke();

                if (infiniteDays)
                    StartNextDay();
                else
                    EndDay(true);
            }
            return;   // don't advance dayTimer while player is cleaning
        }

        // ── Normal day tick ─────────────────────────────────────────────────
        dayTimer += deltaTime;

        DayPhase phase = GetPhaseForTime(dayTimer);
        if (phase != currentPhase)
            SetPhase(phase);

        if (dayTimer >= dayLengthSeconds)
        {
            // Lock into Closing and start the grace period
            SetPhase(DayPhase.Closing);
            isInGracePeriod  = true;
            gracePeriodTimer = gracePeriodSeconds;
            GracePeriodStarted?.Invoke(gracePeriodSeconds);
        }
    }

    private void StartNextDay()
    {
        dayTimer         = 0f;
        isInGracePeriod  = false;
        gracePeriodTimer = 0f;
        dayCount++;
        SetPhase(DayPhase.Morning);
        DayStarted?.Invoke(dayCount);
    }

    private void EndDay(bool allowGameOver)
    {
        dayTimer = dayLengthSeconds;
        bool isFinalDay = dayCount >= Mathf.Max(1, maxDays);
        if (allowGameOver && isFinalDay)
            gameOver = true;

        bool hasDayEndedListeners = DayEnded != null;

        SetPhase(DayPhase.Closing);
        DayEnded?.Invoke(dayCount, isFinalDay);

        if (pauseBetweenDays && hasDayEndedListeners)
            waitingForContinue = true;
        else if (!isFinalDay)
            StartNextDay();

        if (allowGameOver && isFinalDay)
            Debug.Log("[DayCycle] Game over - no more days.");
    }

    public void DebugAdvanceDay()
    {
        if (gameOver) return;

        if (waitingForContinue)
        {
            ContinueToNextDay();
            return;
        }

        EndDay(false);
    }

    private void SetPhase(DayPhase phase)
    {
        currentPhase = phase;
        AnnouncePhase(phase);
        PhaseChanged?.Invoke(phase);
    }

    private DayPhase GetPhaseForTime(float time)
    {
        float t = Mathf.Repeat(time, dayLengthSeconds);

        if (t < morningSeconds) return DayPhase.Morning;
        t -= morningSeconds;

        if (t < rushSeconds) return DayPhase.RushHour;
        t -= rushSeconds;

        if (t < afternoonSeconds) return DayPhase.AfternoonSlowdown;

        return DayPhase.Closing;
    }

    private void AnnouncePhase(DayPhase phase)
    {
        switch (phase)
        {
            case DayPhase.Morning:
                Debug.Log("[DayCycle] Morning");
                PlayJingle(morningJingle);
                break;
            case DayPhase.RushHour:
                Debug.Log("[DayCycle] Rush hour");
                PlayJingle(rushJingle);
                break;
            case DayPhase.AfternoonSlowdown:
                Debug.Log("[DayCycle] Afternoon slow down");
                PlayJingle(afternoonJingle);
                break;
            default:
                Debug.Log("[DayCycle] Closing time");
                PlayJingle(closingJingle);
                break;
        }
    }

    private void PlayJingle(AudioClip clip)
    {
        if (clip == null) return;

        if (jingleSource == null)
            jingleSource = GetComponent<AudioSource>();

        if (jingleSource == null)
            jingleSource = gameObject.AddComponent<AudioSource>();

        jingleSource.playOnAwake = false;
        jingleSource.PlayOneShot(clip, jingleVolume);
    }

    private void NormalizeDaySegments()
    {
        if (dayLengthSeconds <= 0f)
            dayLengthSeconds = 60f;

        float total = Mathf.Max(0f, morningSeconds) + Mathf.Max(0f, rushSeconds)
            + Mathf.Max(0f, afternoonSeconds) + Mathf.Max(0f, closingSeconds);

        if (total <= 0f)
        {
            morningSeconds = 15f;
            rushSeconds = 25f;
            afternoonSeconds = 15f;
            closingSeconds = 5f;
            total = morningSeconds + rushSeconds + afternoonSeconds + closingSeconds;
        }

        if (Mathf.Abs(total - dayLengthSeconds) <= 0.01f) return;

        float scale = dayLengthSeconds / total;
        morningSeconds *= scale;
        rushSeconds *= scale;
        afternoonSeconds *= scale;
        closingSeconds *= scale;
    }

    /// <summary>
    /// Keeps the tutorial session from ending the campaign or showing multi-day promotion flow.
    /// </summary>
    public void ApplyTutorialRelaxation()
    {
        infiniteDays = true;
        pauseBetweenDays = false;
        dayLengthSeconds = Mathf.Max(dayLengthSeconds, 180f);
        NormalizeDaySegments();
    }

    /// <summary>
    /// Skips directly to the specified day number. Safe to call at any time during play.
    /// </summary>
    public void DebugSkipToDay(int targetDay)
    {
        if (gameOver) return;
        targetDay = Mathf.Clamp(targetDay, 1, Mathf.Max(1, maxDays));

        // Reset all in-flight state so the skip feels like a real day transition
        dayCount           = targetDay - 1;
        dayTimer           = 0f;
        isInGracePeriod    = false;
        gracePeriodTimer   = 0f;
        waitingForContinue = false;
        StartNextDay();
        Debug.Log($"[DayCycle Debug] Skipped to Day {targetDay}");
    }

    [ContextMenu("Debug / Skip to Day 1")] public void DebugSkipToDay1() => DebugSkipToDay(1);
    [ContextMenu("Debug / Skip to Day 2")] public void DebugSkipToDay2() => DebugSkipToDay(2);
    [ContextMenu("Debug / Skip to Day 3")] public void DebugSkipToDay3() => DebugSkipToDay(3);
    [ContextMenu("Debug / Skip to Day 4")] public void DebugSkipToDay4() => DebugSkipToDay(4);
}
