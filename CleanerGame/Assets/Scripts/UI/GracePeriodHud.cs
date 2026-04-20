using TMPro;
using UnityEngine;

/// <summary>
/// Displays a countdown during the post-closing grace period ("Clean up! X s").
/// Attach to a Canvas GameObject that contains a TMP_Text label.
/// Assign the RestaurantDayCycle reference in the Inspector (or leave null for auto-find).
/// The root GameObject is shown/hidden automatically — start it disabled in the scene.
/// </summary>
public class GracePeriodHud : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RestaurantDayCycle dayCycle;
    [SerializeField] private TMP_Text countdownText;

    [Header("Text")]
    [SerializeField] private string messagePrefix = "Clean up! ";
    [SerializeField] private string messageSuffix = "s";

    [Header("Colors")]
    [SerializeField] private Color urgentColor  = new Color(0.9f, 0.2f, 0.1f);   // < 4 s
    [SerializeField] private Color warningColor = new Color(0.95f, 0.65f, 0.1f); // < 7 s
    [SerializeField] private Color normalColor  = new Color(0.2f, 0.8f, 0.2f);

    [Header("Layering")]
    [SerializeField] private int sortingOrder = 300;

    private void Awake()
    {
        if (dayCycle == null)
            dayCycle = FindFirstObjectByType<RestaurantDayCycle>();

        if (countdownText == null)
            countdownText = GetComponentInChildren<TMP_Text>();

        UISortingUtility.EnsureSorting(gameObject, sortingOrder);
    }

    private void Start()
    {
        // Subscribe here (not OnEnable) so that the SetActive(false) below
        // — which fires OnDisable — does NOT remove our C# event listeners.
        // C# delegates on dayCycle survive SetActive; only OnDisable is called.
        if (dayCycle != null)
        {
            dayCycle.GracePeriodStarted += OnGracePeriodStarted;
            dayCycle.GracePeriodTick    += OnGracePeriodTick;
            dayCycle.GracePeriodEnded   += OnGracePeriodEnded;
        }

        // Hide until grace period begins
        gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (dayCycle != null)
        {
            dayCycle.GracePeriodStarted -= OnGracePeriodStarted;
            dayCycle.GracePeriodTick    -= OnGracePeriodTick;
            dayCycle.GracePeriodEnded   -= OnGracePeriodEnded;
        }
    }

    private void OnGracePeriodStarted(float totalSeconds)
    {
        gameObject.SetActive(true);
        UpdateDisplay(totalSeconds);
    }

    private void OnGracePeriodTick(float remaining)
    {
        UpdateDisplay(remaining);
    }

    private void OnGracePeriodEnded()
    {
        gameObject.SetActive(false);
    }

    private void UpdateDisplay(float remaining)
    {
        if (countdownText == null) return;

        int secs = Mathf.CeilToInt(remaining);
        countdownText.text = $"{messagePrefix}{secs}{messageSuffix}";

        if (remaining < 2f)
            countdownText.color = urgentColor;
        else if (remaining < 5f)
            countdownText.color = warningColor;
        else
            countdownText.color = normalColor;
    }
}
