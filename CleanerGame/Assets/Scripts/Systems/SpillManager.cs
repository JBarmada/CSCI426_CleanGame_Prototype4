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

        if (sweepProgress >= sweepsToClean)
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
        float t = Mathf.Clamp01(sweepProgress / sweepsToClean);
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
            coinWallet.AddCoins(coinsPerClean);
    }
}
