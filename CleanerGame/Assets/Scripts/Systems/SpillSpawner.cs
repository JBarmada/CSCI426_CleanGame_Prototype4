using UnityEngine;

public class SpillSpawner : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject spillPrefab;          // your Spill prefab

    [Header("Spill Limits")]
    [SerializeField] private int maxActiveSpills = 10;        // cap total spills in scene

    public int MaxActiveSpills => maxActiveSpills;

    public int GetActiveSpillCount()
    {
        return FindObjectsByType<SpillManager>(FindObjectsSortMode.None).Length;
    }

    [Header("Spawn Placement Around Chair (Floor Only)")]
    [SerializeField] private float innerRing = 0.4f;          // min distance from chair center
    [SerializeField] private float outerRing = 1.0f;          // max distance from chair center

    [Header("Grounding + Overlap Checks")]
    [SerializeField] private LayerMask floorMask;             // set to Floor layer
    [SerializeField] private LayerMask blockMask;             // Tables/Chairs/Walls layers
    [SerializeField] private float raycastHeight = 6f;        // start raycast from above
    [SerializeField] private float yOffset = 0.01f;           // tiny lift to avoid z-fighting
    [SerializeField] private float spillHalfExtents = 0.35f;  // approx half-size of spill footprint
    [SerializeField] private int attemptsPerSpawn = 20;

    /// <summary>
    /// Call this when a customer leaves a chair.
    /// Spawns a spill on the FLOOR near the chair position (not on chair/table).
    /// </summary>
    public bool TrySpawnSpillNearChair(Vector3 chairWorldPos)
    {
        if (spillPrefab == null) return false;

        // ✅ Cap how many spills exist (no tags required)
        int activeSpills = FindObjectsByType<SpillManager>(FindObjectsSortMode.None).Length;
        if (activeSpills >= maxActiveSpills) return false;

        if (!TryFindSpawnPointAroundOrigin(chairWorldPos, out Vector3 pos))
            return false;

        Instantiate(spillPrefab, pos, Quaternion.Euler(90f, 0f, 0f));
        return true;
    }

    private bool TryFindSpawnPointAroundOrigin(Vector3 origin, out Vector3 spawnPos)
    {
        for (int i = 0; i < attemptsPerSpawn; i++)
        {
            // 1) Pick a point in a RING around the origin (chair)
            Vector2 dir = Random.insideUnitCircle.normalized;
            float dist = Random.Range(innerRing, outerRing);

            Vector3 rayStart = new Vector3(
                origin.x + dir.x * dist,
                origin.y + raycastHeight,
                origin.z + dir.y * dist
            );

            // 2) Raycast DOWN to find the floor mesh
            if (!Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit,
                    raycastHeight * 2f, floorMask, QueryTriggerInteraction.Ignore))
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
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(transform.position,
            new Vector3(spillHalfExtents * 2f, 0.4f, spillHalfExtents * 2f));
    }
}
