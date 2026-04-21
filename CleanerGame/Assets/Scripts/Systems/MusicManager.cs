using UnityEngine;

public class MusicManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CustomerManager customerManager;
    [SerializeField] private RestaurantDayCycle dayCycle;
    [SerializeField] private AudioSource songSource;
    [SerializeField] private AudioSource ambientSource;
    [SerializeField] private AudioSource day3RainSource;
    [SerializeField] private AudioClip day3RainClip;
    [Range(0f, 1f)]
    [SerializeField] private float day3RainVolume = 1f;
    [Range(0f, 1f)]
    [SerializeField] private float day3SongVolumeMultiplier = 0.35f;
    [Range(0f, 1f)]
    [SerializeField] private float day3AmbientVolumeMultiplier = 0.55f;

    [Header("Ambient Intensity")]
    [SerializeField] private int maxCustomersForMaxIntensity = 12;
    [SerializeField] private float minAmbientVolume = 0.2f;
    [SerializeField] private float maxAmbientVolume = 0.9f;
    [SerializeField] private AnimationCurve intensityCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private float ambientSmoothingSeconds = 0.25f;

    private bool loggedMissingRainClip;
    private float baseSongVolume = 1f;

    private void Awake()
    {
        ResolveReferences();
        CaptureBaseVolumes();
        ConfigureDay3RainSource();
    }

    private void OnEnable()
    {
        ResolveReferences();

        if (dayCycle != null)
            dayCycle.DayStarted += HandleDayStarted;
    }

    private void OnDisable()
    {
        if (dayCycle != null)
            dayCycle.DayStarted -= HandleDayStarted;
    }

    void Start()
    {
        ResolveReferences();
        CaptureBaseVolumes();

        if (songSource != null && !songSource.isPlaying)
        {
            songSource.loop = true;
            songSource.Play();
        }

        if (ambientSource != null && !ambientSource.isPlaying)
        {
            ambientSource.loop = true;
            ambientSource.Play();
        }

        UpdateDay3Rain(dayCycle != null && dayCycle.DayCount == 3);
    }

    void Update()
    {
        ResolveReferences();
        bool isDay3 = dayCycle != null && dayCycle.DayCount == 3;
        UpdateDay3Rain(isDay3);
        UpdateSongVolume(isDay3);

        if (ambientSource == null) return;

        int customerCount = customerManager == null ? 0 : customerManager.ActiveCustomerCount;
        float t = maxCustomersForMaxIntensity <= 0
            ? 1f
            : Mathf.InverseLerp(0f, maxCustomersForMaxIntensity, customerCount);

        float curved = intensityCurve == null ? t : intensityCurve.Evaluate(t);
        float targetVolume = Mathf.Lerp(minAmbientVolume, maxAmbientVolume, curved);
        if (isDay3)
            targetVolume *= day3AmbientVolumeMultiplier;

        if (ambientSmoothingSeconds <= 0f)
        {
            ambientSource.volume = Mathf.Clamp01(targetVolume);
            return;
        }

        float smoothing = 1f - Mathf.Exp(-Time.deltaTime / ambientSmoothingSeconds);
        ambientSource.volume = Mathf.Lerp(ambientSource.volume, Mathf.Clamp01(targetVolume), smoothing);
    }

    private void HandleDayStarted(int dayNumber)
    {
        UpdateDay3Rain(dayNumber == 3);
    }

    private void ResolveReferences()
    {
        if (customerManager == null)
            customerManager = FindFirstObjectByType<CustomerManager>();

        if (dayCycle == null)
            dayCycle = FindFirstObjectByType<RestaurantDayCycle>();
    }

    private void CaptureBaseVolumes()
    {
        if (songSource != null)
            baseSongVolume = Mathf.Max(0f, songSource.volume);
    }

    private void UpdateSongVolume(bool isDay3)
    {
        if (songSource == null)
            return;

        float targetVolume = isDay3 ? baseSongVolume * day3SongVolumeMultiplier : baseSongVolume;
        if (ambientSmoothingSeconds <= 0f)
        {
            songSource.volume = Mathf.Clamp01(targetVolume);
            return;
        }

        float smoothing = 1f - Mathf.Exp(-Time.deltaTime / ambientSmoothingSeconds);
        songSource.volume = Mathf.Lerp(songSource.volume, Mathf.Clamp01(targetVolume), smoothing);
    }

    private void ConfigureDay3RainSource()
    {
        if (day3RainClip == null && day3RainSource != null)
            day3RainClip = day3RainSource.clip;

        if (day3RainClip == null)
            return;

        if (day3RainSource == null)
            day3RainSource = gameObject.AddComponent<AudioSource>();

        day3RainSource.clip = day3RainClip;
        day3RainSource.enabled = true;
        day3RainSource.loop = true;
        day3RainSource.playOnAwake = false;
        day3RainSource.volume = day3RainVolume;
        day3RainSource.spatialBlend = 0f;
        day3RainSource.priority = 64;
        day3RainSource.ignoreListenerPause = true;
    }

    private void UpdateDay3Rain(bool enable)
    {
        ConfigureDay3RainSource();

        if (day3RainSource == null || day3RainClip == null)
        {
            if (enable && !loggedMissingRainClip)
            {
                Debug.LogWarning("[MusicManager] Day 3 rain audio could not play because no rain clip is assigned.", this);
                loggedMissingRainClip = true;
            }

            return;
        }

        day3RainSource.volume = day3RainVolume;
        day3RainSource.mute = false;

        if (enable)
        {
            if (!day3RainSource.isPlaying)
            {
                day3RainSource.Play();
                Debug.Log("[MusicManager] Day 3 rain audio started.", this);
            }
        }
        else if (day3RainSource.isPlaying)
        {
            day3RainSource.Stop();
        }
    }
}
