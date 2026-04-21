using UnityEngine;

/// <summary>
/// Manages the water spill specific to Level 3.
/// Handles visibility, color customization, and leak animation for the water spill.
/// </summary>
public class WaterSpillManager : MonoBehaviour
{
    [SerializeField] private RestaurantDayCycle dayCycle;
    [SerializeField] private GameObject waterSpillRoot;
    [SerializeField] private SpriteRenderer waterSpillRenderer;
    [SerializeField] private Color waterSpillColor = new Color(0.2f, 0.6f, 0.9f, 0.7f); // Light blue with transparency

    [Header("Leak Animation")]
    [SerializeField] private bool useLeakAnimation = true;
    [SerializeField] private float leakDuration = 3f; // How long the leak animation takes
    [SerializeField] private float leakStartHeight = 4.5f; // How far above the puddle the drops start
    [SerializeField] private RaindropLeakEffect raindropLeakEffect; // Raindrop animation effect

    private Vector3 finalPosition;
    private bool isLeaking;
    private Coroutine leakCoroutine;

    private void OnEnable()
    {
        if (dayCycle == null)
            dayCycle = FindFirstObjectByType<RestaurantDayCycle>();

        if (waterSpillRoot == null)
            waterSpillRoot = gameObject;

        if (waterSpillRenderer == null)
            waterSpillRenderer = GetComponent<SpriteRenderer>();

        if (dayCycle != null)
            dayCycle.DayStarted += HandleDayStarted;

        // Store the final position for leak animation
        finalPosition = waterSpillRoot.transform.position;

        bool isLevel3 = dayCycle != null && dayCycle.DayCount == 3;
        ApplyState(isLevel3);
        ApplyColor();

        if (isLevel3 && useLeakAnimation && !isLeaking)
            leakCoroutine = StartCoroutine(PlayLeakAnimation());
    }

    private void OnDisable()
    {
        if (dayCycle != null)
            dayCycle.DayStarted -= HandleDayStarted;

        if (leakCoroutine != null)
        {
            StopCoroutine(leakCoroutine);
            leakCoroutine = null;
        }

        if (raindropLeakEffect != null)
            raindropLeakEffect.StopRaindropLeak();

        isLeaking = false;
    }

    private void HandleDayStarted(int dayNumber)
    {
        if (dayNumber == 3)
        {
            ApplyState(true);
            if (useLeakAnimation && !isLeaking)
                leakCoroutine = StartCoroutine(PlayLeakAnimation());
        }
        else
        {
            ApplyState(false);
        }
    }

    private void ApplyState(bool isLevel3)
    {
        if (waterSpillRoot != null)
            waterSpillRoot.SetActive(isLevel3);
    }

    private void ApplyColor()
    {
        if (waterSpillRenderer != null)
            waterSpillRenderer.color = waterSpillColor;
    }

    private System.Collections.IEnumerator PlayLeakAnimation()
    {
        isLeaking = true;

        // Make sprite start invisible
        Color startColor = waterSpillColor;
        startColor.a = 0f;
        if (waterSpillRenderer != null)
            waterSpillRenderer.color = startColor;

        if (raindropLeakEffect != null)
        {
            raindropLeakEffect.SetSpawnHeight(leakStartHeight);
            raindropLeakEffect.StartRaindropLeak(leakDuration);
        }

        // Animate the leak: raindrops fall from above while the puddle appears on the floor.
        for (float elapsed = 0f; elapsed < leakDuration; elapsed += Time.deltaTime)
        {
            float t = Mathf.Clamp01(elapsed / leakDuration);

            // Fade in the sprite
            Color currentColor = waterSpillColor;
            currentColor.a = Mathf.Lerp(0f, waterSpillColor.a, t);
            if (waterSpillRenderer != null)
                waterSpillRenderer.color = currentColor;

            yield return null;
        }

        // Ensure final state is correct
        waterSpillRoot.transform.position = finalPosition;
        if (waterSpillRenderer != null)
            waterSpillRenderer.color = waterSpillColor;

        // Stop raindrop leak effect after animation is complete
        if (raindropLeakEffect != null)
            raindropLeakEffect.StopRaindropLeak();

        isLeaking = false;
        leakCoroutine = null;
    }
}
