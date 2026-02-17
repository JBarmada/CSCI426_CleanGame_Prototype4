using UnityEngine;
using System;

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
    [SerializeField] private int maxDays = 3;

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

    public event Action<DayPhase> PhaseChanged;

    private void Awake()
    {
        NormalizeDaySegments();
        InitializeDay();
    }

    private void Update()
    {
        UpdateDayCycle(Time.deltaTime);
    }

    public float GetSpawnMultiplier()
    {
        switch (currentPhase)
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

    public bool IsClosed
    {
        get { return currentPhase == DayPhase.Closing || gameOver; }
    }

    public DayPhase GetPhase()
    {
        return currentPhase;
    }

    private void InitializeDay()
    {
        dayTimer = 0f;
        dayCount = 1;
        currentPhase = DayPhase.Morning;
        AnnouncePhase(currentPhase);
        PhaseChanged?.Invoke(currentPhase);
    }

    private void UpdateDayCycle(float deltaTime)
    {
        if (gameOver) return;

        dayTimer += deltaTime;
        DayPhase phase = GetPhaseForTime(dayTimer);
        if (phase != currentPhase)
            SetPhase(phase);

        if (dayTimer >= dayLengthSeconds)
        {
            dayTimer = 0f;
            dayCount++;

            if (!infiniteDays && dayCount > Mathf.Max(1, maxDays))
            {
                gameOver = true;
                SetPhase(DayPhase.Closing);
                Debug.Log("[DayCycle] Game over - no more days.");
                return;
            }

            SetPhase(DayPhase.Morning);
        }
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
}
