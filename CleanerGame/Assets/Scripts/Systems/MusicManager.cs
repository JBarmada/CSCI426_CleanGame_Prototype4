using UnityEngine;

public class MusicManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CustomerManager customerManager;
    [SerializeField] private AudioSource songSource;
    [SerializeField] private AudioSource ambientSource;

    [Header("Ambient Intensity")]
    [SerializeField] private int maxCustomersForMaxIntensity = 12;
    [SerializeField] private float minAmbientVolume = 0.2f;
    [SerializeField] private float maxAmbientVolume = 0.9f;
    [SerializeField] private AnimationCurve intensityCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private float ambientSmoothingSeconds = 0.25f;

    void Start()
    {
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
    }

    void Update()
    {
        if (ambientSource == null) return;

        int customerCount = customerManager == null ? 0 : customerManager.ActiveCustomerCount;
        float t = maxCustomersForMaxIntensity <= 0
            ? 1f
            : Mathf.InverseLerp(0f, maxCustomersForMaxIntensity, customerCount);

        float curved = intensityCurve == null ? t : intensityCurve.Evaluate(t);
        float targetVolume = Mathf.Lerp(minAmbientVolume, maxAmbientVolume, curved);

        if (ambientSmoothingSeconds <= 0f)
        {
            ambientSource.volume = Mathf.Clamp01(targetVolume);
            return;
        }

        float smoothing = 1f - Mathf.Exp(-Time.deltaTime / ambientSmoothingSeconds);
        ambientSource.volume = Mathf.Lerp(ambientSource.volume, Mathf.Clamp01(targetVolume), smoothing);
    }
}
