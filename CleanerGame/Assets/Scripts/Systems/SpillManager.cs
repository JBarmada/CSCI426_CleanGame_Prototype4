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

    [Header("Fade")]
    [SerializeField] private SpriteRenderer spriteRenderer; // assign, or auto-find
    [SerializeField] private bool destroyRoot = false;      // true if this script is on a child trigger

    private bool playerInRange;
    private float sweepProgress; // counts "motions" continuously
    private Collider col;
    private bool cleaned;

    private void Awake()
    {
        col = GetComponent<Collider>();
        col.isTrigger = true;

        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        if (dayCycle == null)
            dayCycle = FindFirstObjectByType<RestaurantDayCycle>();

        sweepProgress = 0f;
        UpdateVisual();
    }

    private void Update()
    {
        if (!playerInRange || cleaned) return;

        if (Input.GetKey(sweepKey))
        {
            float mult = BroomPowerupSystem.Instance != null ? BroomPowerupSystem.Instance.CurrentMultiplier : 1f;
            sweepProgress += (sweepsPerSecond * mult) * Time.deltaTime;
            UpdateVisual();

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

    private void UpdateVisual()
    {
        if (spriteRenderer == null) return;

        // Full alpha at 0 progress, fades out as progress approaches sweepsToClean
        float t = Mathf.Clamp01(sweepProgress / GetEffectiveSweepsToClean());
        float alpha = 1f - t;

        var c = spriteRenderer.color;
        c.a = alpha;
        spriteRenderer.color = c;
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
