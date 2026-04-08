using UnityEngine;

[RequireComponent(typeof(Collider))]
public class SpillManager : MonoBehaviour
{
    [Header("Cleaning (Hold to Sweep)")]
    [SerializeField] private KeyCode sweepKey = KeyCode.Space;
    [SerializeField] private float sweepsPerSecond = 3f;   // 3 sweep motions / sec
    [SerializeField] private int sweepsToClean = 3;        // total motions needed (3 @ 3/sec = 1s)

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

    private bool playerInRange;
    private float sweepProgress; // counts "motions" continuously
    private Collider col;
    private bool cleaned;
    private bool particlesStopped;

    private void Awake()
    {
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

        sweepProgress = 0f;
        UpdateVisual();
    }

    private void Update()
    {
        // Always update glow pulse even when player isn't cleaning
        UpdateGlowPulse();

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

                Debug.Log($"[Spill] Cleaned with multiplier {mult:F2}x");

                if (destroyRoot && transform.parent != null)
                    Destroy(transform.parent.gameObject);
                else
                    Destroy(gameObject);
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
            playerInRange = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
            playerInRange = false;
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

        // Full alpha at 0 progress, fades out as progress approaches sweepsToClean
        float t = Mathf.Clamp01(sweepProgress / GetEffectiveSweepsToClean());
        float alpha = 1f - t;

        var c = spriteRenderer.color;
        c.a = alpha;
        spriteRenderer.color = c;

        // Shrink the spill toward 50% as it gets cleaned
        float scale = Mathf.Lerp(1f, 0.5f, t);
        transform.localScale = Vector3.one * scale;
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
