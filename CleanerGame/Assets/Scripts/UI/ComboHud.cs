using TMPro;
using UnityEngine;

public class ComboHud : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TMP_Text comboText;
    [SerializeField] private SpillComboSystem comboSystem;

    [Header("Text")]
    [SerializeField] private string idleText = string.Empty;
    [SerializeField] private string comboFormat = "Combo x{0}";

    [Header("Refresh")]
    [SerializeField] private float refreshSeconds = 0.1f;

    [Header("Feedback")]
    [SerializeField] private float pulseScale = 1.18f;
    [SerializeField] private float pulseSeconds = 0.16f;

    private float refreshTimer;
    private Coroutine pulseRoutine;
    private Vector3 baseScale;

    private void Awake()
    {
        if (comboText == null)
            comboText = GetComponent<TMP_Text>();

        baseScale = comboText != null ? comboText.transform.localScale : transform.localScale;
    }

    private void OnEnable()
    {
        ResolveComboSystem();

        if (comboSystem != null)
            comboSystem.OnComboChanged += HandleComboChanged;

        Refresh();
    }

    private void OnDisable()
    {
        if (comboSystem != null)
            comboSystem.OnComboChanged -= HandleComboChanged;

        if (pulseRoutine != null)
        {
            StopCoroutine(pulseRoutine);
            pulseRoutine = null;
        }

        if (comboText != null)
            comboText.transform.localScale = baseScale;
    }

    private void Update()
    {
        refreshTimer += Time.deltaTime;
        if (refreshTimer < refreshSeconds) return;

        refreshTimer = 0f;
        Refresh();
    }

    private void HandleComboChanged()
    {
        PlayPulse();
        Refresh();
    }

    private void ResolveComboSystem()
    {
        if (comboSystem != null) return;
        comboSystem = SpillComboSystem.Instance ?? FindFirstObjectByType<SpillComboSystem>();
    }

    private void Refresh()
    {
        if (comboText == null) return;

        if (comboSystem == null)
            ResolveComboSystem();

        if (comboSystem == null)
        {
            comboText.text = idleText;
            return;
        }

        if (comboSystem.ComboCount <= 0)
        {
            comboText.text = idleText;
            return;
        }

        string text = string.Format(comboFormat, comboSystem.ComboCount);
        comboText.text = text;
    }

    private void PlayPulse()
    {
        if (comboText == null || comboSystem == null || comboSystem.ComboCount <= 0)
            return;

        if (pulseRoutine != null)
            StopCoroutine(pulseRoutine);

        pulseRoutine = StartCoroutine(PulseRoutine());
    }

    private System.Collections.IEnumerator PulseRoutine()
    {
        float duration = Mathf.Max(0.01f, pulseSeconds);
        float halfDuration = duration * 0.5f;
        Vector3 peakScale = baseScale * Mathf.Max(1f, pulseScale);

        for (float time = 0f; time < halfDuration; time += Time.unscaledDeltaTime)
        {
            float t = time / halfDuration;
            comboText.transform.localScale = Vector3.Lerp(baseScale, peakScale, t);
            yield return null;
        }

        for (float time = 0f; time < halfDuration; time += Time.unscaledDeltaTime)
        {
            float t = time / halfDuration;
            comboText.transform.localScale = Vector3.Lerp(peakScale, baseScale, t);
            yield return null;
        }

        comboText.transform.localScale = baseScale;
        pulseRoutine = null;
    }
}
