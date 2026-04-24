using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class SpillManager : MonoBehaviour
{
    // ── Static registry so ThirdPersonController can cheaply query all live spills ──
    private static readonly List<SpillManager> s_activeSpills = new List<SpillManager>();
    /// <summary>All SpillManager instances currently alive in the scene.</summary>
    public static IReadOnlyList<SpillManager> ActiveSpills => s_activeSpills;

    /// <summary>True while the player is in range, holding the sweep key, and the spill isn't cleaned.</summary>
    public bool IsBeingCleaned => playerInRange && !cleaned && Input.GetKey(sweepKey);

    public bool ContainsSlipPoint(Vector3 worldPosition)
    {
        if (cleaned || spriteRenderer == null)
            return false;

        Vector3 localPoint = spriteRenderer.transform.InverseTransformPoint(worldPosition);
        Vector2 halfSize = spriteRenderer.size * 0.5f * Mathf.Max(0.1f, slipVisualFootprintScale);
        if (halfSize.x <= 0f || halfSize.y <= 0f)
            return false;

        float normalizedX = localPoint.x / halfSize.x;
        float normalizedY = localPoint.y / halfSize.y;
        return (normalizedX * normalizedX) + (normalizedY * normalizedY) <= 1f;
    }

    [Header("Cleaning (Hold to Sweep)")]
    [SerializeField] private KeyCode sweepKey = KeyCode.Space;
    [SerializeField] private float sweepsPerSecond = 3f;   // 3 sweep motions / sec
    [SerializeField] private int sweepsToClean = 3;        // total motions needed (3 @ 3/sec = 1s)
    [SerializeField] private float cleaningRadius = 1.15f;

    [Header("Slip Trigger")]
    [Tooltip("How much of the visible sprite can trigger slipping. 1 = full sprite, lower values require the player to be more centered on the spill.")]
    [Range(0.1f, 1.25f)]
    [SerializeField] private float slipVisualFootprintScale = 0.8f;

    [Header("Coins")]
    [SerializeField] private int coinsPerClean = 1;
    [SerializeField] private CoinWallet coinWallet;
    [SerializeField] private RestaurantSpillTracker spillTracker;
    [SerializeField] private SpillComboSystem comboSystem;

    [Header("Day 2 Tuning")]
    [SerializeField] private bool useDay2Tuning = true;
    [Range(0.1f, 1f)]
    [SerializeField] private float day2SweepsToCleanMultiplier = 0.75f;
    [SerializeField] private RestaurantDayCycle dayCycle;

    [Header("Visuals")]
    [SerializeField] private SpriteRenderer spriteRenderer; // assign, or auto-find
    [SerializeField] private SpriteRenderer glowRenderer;   // child named "glow"
    [SerializeField] private bool destroyRoot = false;      // true if this script is on a child trigger

    [Header("Glow Pulse")]
    [SerializeField] private float pulseSpeed = 2f;
    [SerializeField] private float pulseMinAlpha = 0.3f;

    [Header("Particles")]
    [SerializeField] private ParticleSystem spillParticles;

    [Header("Completion Feedback")]
    [SerializeField] private GameObject cleanBurstPrefab;
    [SerializeField] private float cleanBurstLifetime = 1f;
    [SerializeField] private AudioClip cleanCompleteClip;
    [Range(0f, 1f)]
    [SerializeField] private float cleanCompleteVolume = 0.8f;
    [SerializeField] private float cleanPunchScale = 1.35f;
    [SerializeField] private float cleanFinishSeconds = 0.12f;

    private bool playerInRange;
    private float sweepProgress; // counts "motions" continuously
    private Collider col;
    private bool cleaned;
    private bool particlesStopped;
    private Vector3 initialScale;
    private Coroutine cleanRoutine;
    private Transform playerTransform;
    private Transform cleaningAnchor;

    private void Awake()
    {
        s_activeSpills.Add(this);
        initialScale = transform.localScale;
        col = GetComponent<Collider>();
        col.isTrigger = true;

        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        if (glowRenderer == null)
        {
            var glowTransform = transform.Find("glow");
            if (glowTransform != null)
                glowRenderer = glowTransform.GetComponent<SpriteRenderer>();
        }

        if (spillParticles == null)
            spillParticles = GetComponentInChildren<ParticleSystem>();

        if (dayCycle == null)
            dayCycle = FindFirstObjectByType<RestaurantDayCycle>();

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerTransform = player.transform;
            ResolveCleaningAnchor();
        }

        sweepProgress = 0f;
        UpdateVisual();
    }

    private void Update()
    {
        // Always update glow pulse even when player isn't cleaning
        UpdateGlowPulse();

        UpdatePlayerCleaningRange();

        if (!playerInRange || cleaned) return;

        if (Input.GetKey(sweepKey))
        {
            float mult = BroomPowerupSystem.Instance != null ? BroomPowerupSystem.Instance.CurrentMultiplier : 1f;
            sweepProgress += (sweepsPerSecond * mult) * Time.deltaTime;
            UpdateVisual();

            // Stop particles as soon as cleaning begins
            if (!particlesStopped && spillParticles != null)
            {
                spillParticles.Stop();
                particlesStopped = true;
            }

            if (sweepProgress >= GetEffectiveSweepsToClean())
            {
                cleaned = true;
                AwardCoins();
                playerInRange = false;

                if (col != null)
                    col.enabled = false;

                Debug.Log($"[Spill] Cleaned with multiplier {mult:F2}x");
                cleanRoutine = StartCoroutine(PlayCleanCompletion());
            }
        }
    }

    private void UpdateGlowPulse()
    {
        if (glowRenderer == null || cleaned) return;

        float t = Mathf.Clamp01(sweepProgress / GetEffectiveSweepsToClean());

        if (t <= 0f)
        {
            // Idle: pulse alpha between pulseMinAlpha and 1
            float pulse = Mathf.PingPong(Time.time * pulseSpeed, 1f);
            float alpha = Mathf.Lerp(pulseMinAlpha, 1f, pulse);
            var c = glowRenderer.color;
            c.a = alpha;
            glowRenderer.color = c;
        }
        else
        {
            // Cleaning in progress: fade glow out with main sprite
            var c = glowRenderer.color;
            c.a = 1f - t;
            glowRenderer.color = c;
        }
    }

    private void UpdateVisual()
    {
        if (spriteRenderer == null) return;

        float t = Mathf.Clamp01(sweepProgress / GetEffectiveSweepsToClean());

        // Shrink the spill toward 50% of its original size as it gets cleaned
        transform.localScale = initialScale * Mathf.Lerp(1f, 0.5f, t);
    }

    private void UpdatePlayerCleaningRange()
    {
        if (playerTransform == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                playerTransform = player.transform;
                ResolveCleaningAnchor();
            }
        }

        if (playerTransform == null)
        {
            playerInRange = false;
            return;
        }

        if (cleaningAnchor == null)
            ResolveCleaningAnchor();

        Transform rangeSource = cleaningAnchor != null ? cleaningAnchor : playerTransform;
        Vector3 playerPlanar = new Vector3(rangeSource.position.x, 0f, rangeSource.position.z);
        Vector3 spillPlanar = new Vector3(transform.position.x, 0f, transform.position.z);
        playerInRange = Vector3.Distance(playerPlanar, spillPlanar) <= cleaningRadius;
    }

    private void ResolveCleaningAnchor()
    {
        if (ThirdPersonController.Instance != null && ThirdPersonController.Instance.broom != null)
        {
            cleaningAnchor = ThirdPersonController.Instance.broom;
            return;
        }

        cleaningAnchor = playerTransform;
    }

    private System.Collections.IEnumerator PlayCleanCompletion()
    {
        if (cleanBurstPrefab != null)
        {
            GameObject fx = Instantiate(cleanBurstPrefab, transform.position, transform.rotation);
            if (cleanBurstLifetime > 0f)
                Destroy(fx, cleanBurstLifetime);
        }

        if (cleanCompleteClip != null)
            AudioSource.PlayClipAtPoint(cleanCompleteClip, transform.position, cleanCompleteVolume);

        float duration = Mathf.Max(0.01f, cleanFinishSeconds);
        Vector3 startScale = transform.localScale;
        Vector3 peakScale = startScale * Mathf.Max(1f, cleanPunchScale);

        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            float t = Mathf.Clamp01(elapsed / duration);
            float scaleT = Mathf.Sin(t * Mathf.PI);
            transform.localScale = Vector3.Lerp(startScale, peakScale, scaleT);

            yield return null;
        }

        DestroySelf();
    }

    private void OnDestroy()
    {
        s_activeSpills.Remove(this);
    }

    private void DestroySelf()
    {
        if (destroyRoot && transform.parent != null)
            Destroy(transform.parent.gameObject);
        else
            Destroy(gameObject);
    }

    private void AwardCoins()
    {
        if (coinsPerClean <= 0) return;

        if (coinWallet == null)
            coinWallet = FindFirstObjectByType<CoinWallet>();

        if (coinWallet != null)
        {
            if (comboSystem == null)
                comboSystem = SpillComboSystem.Instance ?? FindFirstObjectByType<SpillComboSystem>();

            int awardedCoins = comboSystem != null
                ? comboSystem.RegisterSpillCleanAndGetCoins(coinsPerClean)
                : coinsPerClean;

            coinWallet.AddCoins(awardedCoins);
        }

        if (spillTracker == null)
            spillTracker = FindFirstObjectByType<RestaurantSpillTracker>();

        if (spillTracker != null)
            spillTracker.AddSpillCleaned();
    }

    private float GetEffectiveSweepsToClean()
    {
        float target = Mathf.Max(1f, sweepsToClean);
        if (useDay2Tuning && dayCycle != null && dayCycle.DayCount == 2)
            target = Mathf.Max(1f, target * day2SweepsToCleanMultiplier);

        return target;
    }
}
