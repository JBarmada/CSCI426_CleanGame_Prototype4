using UnityEngine;

public class SpillSpawner : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject spillPrefab;              // your Spill prefab
    [SerializeField] private Transform[] tables;                  // drag table transforms here
    [SerializeField] private CustomerManager customerManager;     // drag your CustomerManager here

    [Header("Spill Tuning")]
    [SerializeField] private int minCustomersToSpawn = 3;         // only start spawning after this many customers
    [SerializeField] private float baseSecondsPerSpill = 10f;     // slow rate
    [SerializeField] private float maxSpillsPerSecond = 0.25f;    // cap (0.25 = 1 spill every 4s)
    [SerializeField] private int maxActiveSpills = 10;            // cap total spills in scene

    [Header("Spawn Placement Around Tables")]
    [SerializeField] private float innerRing = 1.2f;              // min distance from table center
    [SerializeField] private float outerRing = 2.4f;              // max distance from table center

    [Header("Grounding + Overlap Checks")]
    [SerializeField] private LayerMask floorMask;                 // set to Floor layer
    [SerializeField] private LayerMask blockMask;                 // Tables/Chairs/Walls layers
    [SerializeField] private float raycastHeight = 6f;            // start raycast from above
    [SerializeField] private float yOffset = 0.01f;               // tiny lift to avoid z-fighting
    [SerializeField] private float spillHalfExtents = 0.35f;      // approx half-size of spill footprint
    [SerializeField] private int attemptsPerSpawn = 20;

    private float spawnTimer;

    private void Update()
    {
        if (spillPrefab == null || tables == null || tables.Length == 0 || customerManager == null)
            return;

        int customers = customerManager.CustomerCount;

        // Not busy enough -> no spills
        if (customers < minCustomersToSpawn) return;

        // Rate increases with customers
        float spillsPerSecond = CustomersToSpillsPerSecond(customers);
        float secondsPerSpill = 1f / spillsPerSecond;

        spawnTimer += Time.deltaTime;
        if (spawnTimer >= secondsPerSpill)
        {
            spawnTimer = 0f;
            TrySpawnSpill();
        }
    }

    private float CustomersToSpillsPerSecond(int customers)
    {
        int effective = Mathf.Max(0, customers - minCustomersToSpawn + 1);
        float baseRate = 1f / Mathf.Max(0.1f, baseSecondsPerSpill); // spills/sec at threshold
        float ramp = baseRate * effective;                           // linear ramp
        return Mathf.Min(ramp, maxSpillsPerSecond);
    }

    private void TrySpawnSpill()
    {
        // ✅ Cap how many spills exist (no tags required)
        int activeSpills = FindObjectsByType<SpillManager>(FindObjectsSortMode.None).Length;
        if (activeSpills >= maxActiveSpills) return;

        Transform table = tables[Random.Range(0, tables.Length)];

        if (!TryFindSpawnPointAroundTable(table.position, out Vector3 pos))
            return;

        Instantiate(spillPrefab, pos, Quaternion.Euler(90f, 0f, 0f));
    }

    private bool TryFindSpawnPointAroundTable(Vector3 tablePos, out Vector3 spawnPos)
    {
        for (int i = 0; i < attemptsPerSpawn; i++)
        {
            // 1) Pick a point in a RING around the table (prevents spawning directly on top)
            Vector2 dir = Random.insideUnitCircle.normalized;
            float dist = Random.Range(innerRing, outerRing);

            Vector3 rayStart = new Vector3(
                tablePos.x + dir.x * dist,
                tablePos.y + raycastHeight,
                tablePos.z + dir.y * dist
            );

            // 2) Raycast DOWN to find the floor mesh
            if (!Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, raycastHeight * 2f, floorMask, QueryTriggerInteraction.Ignore))
                continue;

            Vector3 candidate = hit.point + Vector3.up * yOffset;

            // 3) Reject if it overlaps tables/chairs/walls/etc.
            Vector3 halfExtents = new Vector3(spillHalfExtents, 0.2f, spillHalfExtents);

            bool blocked = Physics.CheckBox(
                candidate,
                halfExtents,
                Quaternion.identity,
                blockMask,
                QueryTriggerInteraction.Ignore
            );

            if (!blocked)
            {
                spawnPos = candidate;
                return true;
            }
        }

        spawnPos = default;
        return false;
    }

    private void OnDrawGizmosSelected()
    {
        // Debug: shows the approximate spill footprint at this spawner's position
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(transform.position, new Vector3(spillHalfExtents * 2f, 0.4f, spillHalfExtents * 2f));
    }
}
