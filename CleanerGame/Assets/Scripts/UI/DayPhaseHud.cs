using System.Collections;
using TMPro;
using UnityEngine;

public class DayPhaseHud : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TMP_Text phaseText;
    [SerializeField] private RestaurantDayCycle dayCycle;

    [Header("Text")]
    [SerializeField] private string morningText = "Morning";
    [SerializeField] private string rushText = "Rush Hour";
    [SerializeField] private string afternoonText = "Afternoon Slow Down";
    [SerializeField] private string closingText = "Closing Time";

    [Header("Colors")]
    [SerializeField] private Color morningColor = new Color(0.85f, 0.9f, 0.6f);
    [SerializeField] private Color rushColor = new Color(0.9f, 0.65f, 0.2f);
    [SerializeField] private Color afternoonColor = new Color(0.75f, 0.6f, 0.4f);
    [SerializeField] private Color closingColor = new Color(0.9f, 0.35f, 0.3f);

    [Header("Fade")]
    [SerializeField] private float fadeInSeconds = 0.5f;
    [SerializeField] private float holdSeconds = 1.5f;
    [SerializeField] private float fadeOutSeconds = 0.75f;

    private Coroutine fadeRoutine;

    private void Awake()
    {
        if (phaseText == null)
            phaseText = GetComponent<TMP_Text>();

        if (dayCycle == null)
            dayCycle = FindFirstObjectByType<RestaurantDayCycle>();
    }

    private void OnEnable()
    {
        if (dayCycle != null)
            dayCycle.PhaseChanged += HandlePhaseChanged;

        ShowCurrentPhase();
    }

    private void OnDisable()
    {
        if (dayCycle != null)
            dayCycle.PhaseChanged -= HandlePhaseChanged;
    }

    private void HandlePhaseChanged(RestaurantDayCycle.DayPhase phase)
    {
        StartFade(phase);
    }

    private void ShowCurrentPhase()
    {
        if (dayCycle == null) return;
        StartFade(dayCycle.GetPhase());
    }

    private void StartFade(RestaurantDayCycle.DayPhase phase)
    {
        if (phaseText == null) return;

        if (fadeRoutine != null)
            StopCoroutine(fadeRoutine);

        SetTextAndColor(phase);
        fadeRoutine = StartCoroutine(FadeSequence());
    }

    private void SetTextAndColor(RestaurantDayCycle.DayPhase phase)
    {
        switch (phase)
        {
            case RestaurantDayCycle.DayPhase.Morning:
                phaseText.text = morningText;
                phaseText.color = WithAlpha(morningColor, 0f);
                break;
            case RestaurantDayCycle.DayPhase.RushHour:
                phaseText.text = rushText;
                phaseText.color = WithAlpha(rushColor, 0f);
                break;
            case RestaurantDayCycle.DayPhase.AfternoonSlowdown:
                phaseText.text = afternoonText;
                phaseText.color = WithAlpha(afternoonColor, 0f);
                break;
            default:
                phaseText.text = closingText;
                phaseText.color = WithAlpha(closingColor, 0f);
                break;
        }
    }

    private IEnumerator FadeSequence()
    {
        yield return Fade(0f, 1f, fadeInSeconds);
        if (holdSeconds > 0f)
            yield return new WaitForSeconds(holdSeconds);
        yield return Fade(1f, 0f, fadeOutSeconds);
    }

    private IEnumerator Fade(float from, float to, float duration)
    {
        if (duration <= 0f)
        {
            SetAlpha(to);
            yield break;
        }

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t / duration));
            SetAlpha(alpha);
            yield return null;
        }

        SetAlpha(to);
    }

    private void SetAlpha(float alpha)
    {
        if (phaseText == null) return;
        Color c = phaseText.color;
        c.a = alpha;
        phaseText.color = c;
    }

    private Color WithAlpha(Color color, float alpha)
    {
        color.a = alpha;
        return color;
    }
}
