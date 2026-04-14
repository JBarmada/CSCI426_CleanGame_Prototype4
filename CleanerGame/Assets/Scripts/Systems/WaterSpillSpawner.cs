using UnityEngine;

/// <summary>
/// Spawns water stains randomly around the edges of the restaurant during Day 3.
/// </summary>
public class WaterSpillSpawner : MonoBehaviour
{
    [SerializeField] private RestaurantDayCycle dayCycle;
    [SerializeField] private GameObject waterSpillPrefab; // Your water spill with SpillManager

    [Header("Edge Boundaries")]
    [SerializeField] private float minX = -5f;
    [SerializeField] private float maxX = 5f;
    [SerializeField] private float minZ = -5f;
    [SerializeField] private float maxZ = 5f;
    [SerializeField] private float edgeMargin = 0.5f; // How close to edges to spawn (in units)
    [SerializeField] private float floorY = 0f; // The Y position of your floor

    [Header("Spawn Settings")]
    [SerializeField] private float spawnInterval = 5f; // Spawn a water spill every 5 seconds
    [SerializeField] private float maxSpillsPerDay = 8; // Max number of spills to spawn in one day

    private float spawnTimer;
    private int spillsSpawnedToday;
    private bool isDay3;

    private void OnEnable()
    {
        if (dayCycle == null)
            dayCycle = FindFirstObjectByType<RestaurantDayCycle>();

        if (dayCycle != null)
        {
            dayCycle.DayStarted += HandleDayStarted;
            
            // Check if we're already on Day 3 (in case OnEnable is called after Day 3 starts)
            if (dayCycle.DayCount == 3)
            {
                HandleDayStarted(3);
            }
        }
    }

    private void OnDisable()
    {
        if (dayCycle != null)
            dayCycle.DayStarted -= HandleDayStarted;
    }

    private void Update()
    {
        if (!isDay3 || waterSpillPrefab == null)
        {
            if (!isDay3 && dayCycle != null)
            {
                // Debug: show current day
                if (Time.frameCount % 300 == 0)
                    Debug.Log($"[WaterSpillSpawner] Current day: {dayCycle.DayCount}, waiting for Day 3");
            }
            return;
        }

        spawnTimer += Time.deltaTime;

        if (spawnTimer >= spawnInterval && spillsSpawnedToday < maxSpillsPerDay)
        {
            SpawnWaterSpill();
            spawnTimer = 0f;
        }
    }

    private void HandleDayStarted(int dayNumber)
    {
        if (dayNumber == 3)
        {
            isDay3 = true;
            spillsSpawnedToday = 0;
            spawnTimer = 0f;
            Debug.Log($"[WaterSpillSpawner] Day 3 started - water spills will spawn at edges. Using Floor Y = {floorY}");
        }
        else
        {
            isDay3 = false;
        }
    }

    private void SpawnWaterSpill()
    {
        // Choose a random edge (0=left, 1=right, 2=near, 3=far)
        int edge = Random.Range(0, 4);
        Vector3 spawnPos = GetRandomEdgePosition(edge);
        spawnPos.y = floorY; // Use the floor Y position

        // Debug: show where it's spawning (purple sphere visible in editor)
        Debug.DrawLine(spawnPos, spawnPos + Vector3.up * 2f, Color.magenta, 5f);

        // Instantiate the water spill
        GameObject newSpill = Instantiate(waterSpillPrefab, spawnPos, Quaternion.identity);
        newSpill.name = $"WaterSpill_Day3_{spillsSpawnedToday}";

        spillsSpawnedToday++;
        Debug.Log($"[WaterSpillSpawner] Spawned water spill #{spillsSpawnedToday} at position ({spawnPos.x:F2}, {spawnPos.y:F2}, {spawnPos.z:F2})");
    }

    private Vector3 GetRandomEdgePosition(int edge)
    {
        float x, z;

        switch (edge)
        {
            case 0: // Left edge
                x = minX + edgeMargin;
                z = Random.Range(minZ, maxZ);
                break;
            case 1: // Right edge
                x = maxX - edgeMargin;
                z = Random.Range(minZ, maxZ);
                break;
            case 2: // Near edge
                x = Random.Range(minX, maxX);
                z = minZ + edgeMargin;
                break;
            case 3: // Far edge
                x = Random.Range(minX, maxX);
                z = maxZ - edgeMargin;
                break;
            default:
                x = 0f;
                z = 0f;
                break;
        }

        return new Vector3(x, 0f, z);
    }
}
