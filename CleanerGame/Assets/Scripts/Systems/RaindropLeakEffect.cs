using UnityEngine;
using System.Collections;

/// <summary>
/// Spawns animated raindrop sprites that fall from above to simulate a ceiling leak.
/// Works in conjunction with WaterSpillManager for visual effect during leak animation.
/// </summary>
public class RaindropLeakEffect : MonoBehaviour
{
    [SerializeField] private Sprite raindropSprite;
    [SerializeField] private Color raindropColor = Color.white;
    [SerializeField] private GameObject targetPuddle; // The puddle GameObject to spawn raindrops above
    [SerializeField] private float spawnHeight = 2f; // How far above to spawn

    [Header("Raindrop Properties")]
    [SerializeField] private float raindropSize = 0.2f;
    [SerializeField] private float fallDuration = 1.5f; // How long to fall
    [SerializeField] private float spawnInterval = 0.1f; // Time between spawns (smaller = more frequent)
    [SerializeField] private float randomXOffset = 0.3f; // Spread them out horizontally
    [SerializeField] private float randomZOffset = 0.3f; // Spread them out in depth

    [Header("Animation")]
    [SerializeField] private AnimationCurve fallCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [SerializeField] private bool fadeOutAsItFalls = true;

    private Coroutine spawnCoroutine;

    /// <summary>
    /// Start spawning raindrops for the given duration
    /// </summary>
    public void StartRaindropLeak(float leakDuration)
    {
        if (spawnCoroutine != null)
            StopCoroutine(spawnCoroutine);

        if (targetPuddle == null)
        {
            Debug.LogWarning("[RaindropLeakEffect] Target puddle not assigned!");
            return;
        }

        spawnCoroutine = StartCoroutine(SpawnRaindropsForDuration(leakDuration));
    }

    /// <summary>
    /// Stop spawning raindrops immediately
    /// </summary>
    public void StopRaindropLeak()
    {
        if (spawnCoroutine != null)
        {
            StopCoroutine(spawnCoroutine);
            spawnCoroutine = null;
        }
    }

    private IEnumerator SpawnRaindropsForDuration(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            SpawnRaindrop();
            elapsed += spawnInterval;
            yield return new WaitForSeconds(spawnInterval);
        }
    }

    private void SpawnRaindrop()
    {
        if (raindropSprite == null)
        {
            Debug.LogWarning("[RaindropLeakEffect] Raindrop sprite not assigned!");
            return;
        }

        if (targetPuddle == null)
            return;

        // Create raindrop GameObject
        GameObject raindrop = new GameObject("Raindrop");
        raindrop.transform.SetParent(transform);

        // Use puddle position as target
        Vector3 puddlePos = targetPuddle.transform.position;

        // Calculate spawn position (above the puddle, with random offsets)
        Vector3 spawnPos = puddlePos + Vector3.up * spawnHeight;
        spawnPos.x += Random.Range(-randomXOffset, randomXOffset);
        spawnPos.z += Random.Range(-randomZOffset, randomZOffset);
        raindrop.transform.position = spawnPos;

        // Add SpriteRenderer
        SpriteRenderer spriteRenderer = raindrop.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = raindropSprite;
        spriteRenderer.color = raindropColor;
        spriteRenderer.sortingOrder = 0; // Adjust if needed

        // Set size via scale
        raindrop.transform.localScale = Vector3.one * raindropSize;

        // Animate the fall
        StartCoroutine(AnimateRaindropFall(raindrop, puddlePos));
    }

    private IEnumerator AnimateRaindropFall(GameObject raindrop, Vector3 targetPos)
    {
        Vector3 startPos = raindrop.transform.position;
        SpriteRenderer spriteRenderer = raindrop.GetComponent<SpriteRenderer>();
        Color startColor = spriteRenderer.color;

        for (float elapsed = 0f; elapsed < fallDuration; elapsed += Time.deltaTime)
        {
            if (raindrop == null) yield break; // Safety check

            float t = Mathf.Clamp01(elapsed / fallDuration);
            float curveT = fallCurve.Evaluate(t);

            // Position: fall from start to target
            raindrop.transform.position = Vector3.Lerp(startPos, targetPos, curveT);

            // Fade out as it falls
            if (fadeOutAsItFalls && spriteRenderer != null)
            {
                Color color = startColor;
                color.a = Mathf.Lerp(startColor.a, 0f, t);
                spriteRenderer.color = color;
            }

            yield return null;
        }

        // Destroy when done
        if (raindrop != null)
            Destroy(raindrop);
    }
}
