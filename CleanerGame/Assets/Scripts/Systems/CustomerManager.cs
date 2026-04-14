using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CustomerManager : MonoBehaviour
{
    [Header("Spawn")]
    [SerializeField] private Customer customerPrefab;
    [SerializeField] private Customer bonkCustomerPrefab;
    [SerializeField] private CustomerPartyAI partyCustomerPrefab;
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] private Transform exitPoint;
    [SerializeField] private CustomerSpawnTuning spawnTuning;
    [SerializeField] private RestaurantManager restaurantManager;
    [SerializeField] private RestaurantDayCycle dayCycle;
    [SerializeField] private RestaurantReputation reputation;
    [SerializeField] private bool logSpawnCaps;
    [SerializeField] private float spawnClearRadius = 1.2f;
    [SerializeField] private float spawnJitterRadius = 0.45f;
    [Header("Day 3 Occupancy")]
    [Range(0f, 1f)] [SerializeField] private float day3MorningOccupancy = 0.35f;
    [Range(0f, 1f)] [SerializeField] private float day3RushOccupancy = 0.95f;
    [Range(0f, 1f)] [SerializeField] private float day3AfternoonOccupancy = 0.55f;
    [Range(0f, 1f)] [SerializeField] private float day3ClosingOccupancy = 0.25f;
    [Header("Day 3 Bonk Customers")]
    [SerializeField] private int day3MaxBonkCustomers = 2;
    [Range(0f, 1f)] [SerializeField] private float day3SecondBonkChance = 0.5f;
    [Header("Day 3 Table Expansion")]
    [SerializeField] private Transform day3TableSource;
    [SerializeField] private Vector3 day3TableOffset = new Vector3(0f, 0f, -8f);
    [Header("Day 4 — Peak Night (hardest)")]
    [Range(0f, 1f)] [SerializeField] private float day4MorningOccupancy = 0.55f;
    [Range(0f, 1f)] [SerializeField] private float day4RushOccupancy = 1f;
    [Range(0f, 1f)] [SerializeField] private float day4AfternoonOccupancy = 0.78f;
    [Range(0f, 1f)] [SerializeField] private float day4ClosingOccupancy = 0.42f;
    [SerializeField] private int day4MaxBonkCustomers = 3;
    [Tooltip("Chance the first bonk of the day spawns at all (lower = more seated customers = more spills).")]
    [Range(0f, 1f)] [SerializeField] private float day4FirstBonkChance = 0.42f;
    [Range(0f, 1f)] [SerializeField] private float day4SecondBonkChance = 0.48f;
    [SerializeField] private float day4SpawnIntervalMultiplier = 0.78f;
    [Header("Party Day Tuning")]
    [Range(0f, 1f)]
    [SerializeField] private float partyDayPartyCustomerChance = 0.7f;
    [Range(0f, 1f)]
    [SerializeField] private float partyDaySpillBaseChance = 1f;
    [Header("Day 1 Bonk Customers")]
    [Range(0f, 1f)]
    [SerializeField] private float day1BonkCustomerChance = 0.35f;
    [Header("Spills")]
    [SerializeField] private SpillSpawner spillSpawner;

    private readonly List<Customer> activeCustomers = new List<Customer>();
    private readonly List<CustomerPartyAI> partyCustomers = new List<CustomerPartyAI>();
    private int activeCustomerCount;
    private Chair[] chairs;
    private GameObject day3TableClone;

    // ✅ Use this for your spill spawner
    public int CustomerCount => activeCustomerCount;

    public int ActiveCustomerCount => activeCustomerCount;
    public Transform ExitPoint => exitPoint;

    private void Start()
    {
        // ✅ Unity 6 replacement for FindObjectsOfType
        RefreshChairs();
        if (restaurantManager == null)
            restaurantManager = RestaurantManager.Instance;
        if (dayCycle == null && restaurantManager != null)
            dayCycle = restaurantManager.GetComponent<RestaurantDayCycle>();
        if (reputation == null)
            reputation = FindFirstObjectByType<RestaurantReputation>();
        if (spawnTuning == null)
            spawnTuning = FindFirstObjectByType<CustomerSpawnTuning>();

        if (dayCycle != null)
            dayCycle.DayStarted += HandleDayStarted;

        StartCoroutine(SpawnLoop());
    }

    private void OnDestroy()
    {
        if (dayCycle != null)
            dayCycle.DayStarted -= HandleDayStarted;
    }

    public Chair GetNearestAvailableChair(Vector3 position)
    {
        Chair nearest = null;
        float minDistance = Mathf.Infinity;

        foreach (Chair chair in chairs)
        {
            if (chair == null) continue;
            if (chair.IsOccupied || chair.IsReserved) continue;

            float distance = Vector3.Distance(position, chair.transform.position);
            if (distance < minDistance)
            {
                minDistance = distance;
                nearest = chair;
            }
        }

        return nearest;
    }

    public bool TryAssignChair(Customer customer)
    {
        if (customer == null) return false;

        Chair chair = GetNearestAvailableChair(customer.transform.position);
        if (chair == null) return false;

        if (!chair.TryReserve(customer)) return false;

        customer.AssignChair(chair, exitPoint);
        return true;
    }

    public void DespawnCustomer(Customer customer)
    {
        activeCustomers.Remove(customer);
        if (customer != null && customer.CountsTowardCapacity)
            activeCustomerCount = Mathf.Max(0, activeCustomerCount - 1);
        Destroy(customer.gameObject);
    }

    private IEnumerator SpawnLoop()
    {
        while (true)
        {
            TrySpawnCustomer();
            float waitSeconds = GetCurrentSpawnIntervalSeconds();
            yield return new WaitForSeconds(waitSeconds);
        }
    }

    private void TrySpawnCustomer()
    {
        PruneDeadCustomers();

        if (customerPrefab == null || spawnPoints == null || spawnPoints.Length == 0)
        {
            if (logSpawnCaps)
                Debug.Log("Spawn skip -> missing prefab or spawn points.", this);
            return;
        }
        if (dayCycle != null && dayCycle.IsClosed)
        {
            if (logSpawnCaps)
                Debug.Log("Spawn skip -> day is closed.", this);
            return;
        }

        int globalMax = spawnTuning == null ? 12 : spawnTuning.MaxActiveCustomers;
        int reputationCap = reputation == null
            ? globalMax
            : reputation.GetCustomerCapForReputation();

        int baseCap = Mathf.Min(globalMax, reputationCap);
        float dirtinessMultiplier = restaurantManager == null
            ? 1f
            : restaurantManager.GetDirtinessCapMultiplier();
        int dirtinessAdjustedCap = Mathf.FloorToInt(baseCap * dirtinessMultiplier);

        if (dayCycle != null && dayCycle.DayCount == 1 && spawnTuning != null)
            dirtinessAdjustedCap = Mathf.Max(dirtinessAdjustedCap, Mathf.FloorToInt(dirtinessAdjustedCap * spawnTuning.Day1CustomerCapMultiplier));

        float dayMultiplier = dayCycle == null ? 1f : dayCycle.GetSpawnMultiplier();
        int cap = Mathf.Max(1, Mathf.FloorToInt(dirtinessAdjustedCap * dayMultiplier));
        if (dayCycle != null && (dayCycle.DayCount == 3 || dayCycle.DayCount == 4))
            cap = Mathf.Max(cap, GetHighPressureFloorTargetCount());
        if (logSpawnCaps)
        {
            string phase = dayCycle == null ? "None" : dayCycle.GetPhase().ToString();
            int dayCount = dayCycle == null ? 0 : dayCycle.DayCount;
            int chairsAvailable = GetAvailableChairCount();
            Debug.Log($"Spawn cap calc -> day {dayCount}, phase {phase}, baseCap {baseCap}, dirtMult {dirtinessMultiplier:0.00}, dayMult {dayMultiplier:0.00}, cap {cap}, activeList {activeCustomers.Count}, activeCount {activeCustomerCount}, capCount {GetCustomersTowardCapacity()}, chairsOpen {chairsAvailable}", this);
        }
        int customersTowardCap = GetCustomersTowardCapacity();
        if (customersTowardCap >= cap)
        {
            if (logSpawnCaps)
                Debug.Log($"Spawn skip -> cap reached ({customersTowardCap}/{cap}).", this);
            return;
        }

        if (!TryGetSpawnPosition(out Vector3 spawnPosition, out Quaternion spawnRotation))
        {
            if (logSpawnCaps)
                Debug.Log($"Spawn skip -> no clear spawn point. clearRadius={spawnClearRadius:0.00}, jitterRadius={spawnJitterRadius:0.00}", this);
            return;
        }
        bool isPartyDay = dayCycle != null && dayCycle.DayCount == 2;
        bool isDay3 = dayCycle != null && dayCycle.DayCount == 3;
        bool isDay4 = dayCycle != null && dayCycle.DayCount == 4;

        if (isPartyDay && partyCustomerPrefab != null)
        {
            if (Random.value <= partyDayPartyCustomerChance)
            {
                CustomerPartyAI partyCustomer = Instantiate(partyCustomerPrefab, spawnPosition, spawnRotation);
                partyCustomer.gameObject.SetActive(true);
                partyCustomer.Initialize(this);
                partyCustomers.Add(partyCustomer);
                activeCustomerCount++;
                return;
            }
        }

        bool isDay1 = dayCycle != null && dayCycle.DayCount == 1;
        bool spawnBonkCustomer = ShouldSpawnBonkCustomer(isDay1, isDay3, isDay4);
        Customer prefabToSpawn = spawnBonkCustomer && bonkCustomerPrefab != null
            ? bonkCustomerPrefab
            : customerPrefab;

        Customer customer = Instantiate(prefabToSpawn, spawnPosition, spawnRotation);
        customer.Initialize(this);

        if (spawnBonkCustomer)
        {
            customer.ConfigureAsBonkCustomer();
            activeCustomers.Add(customer);
            if (logSpawnCaps)
                Debug.Log($"Spawned bonk customer at {spawnPosition}. ActiveList={activeCustomers.Count}, CapCount={GetCustomersTowardCapacity()}", this);
            return;
        }

        if (!TryAssignChair(customer))
        {
            if (logSpawnCaps)
                Debug.Log($"Spawn skip -> no available chair. ChairsOpen={GetAvailableChairCount()}, SpawnPos={spawnPosition}", this);
            Destroy(customer.gameObject);
            return;
        }

        activeCustomers.Add(customer);
        activeCustomerCount++;
        if (logSpawnCaps)
            Debug.Log($"Spawned normal customer at {spawnPosition}. ActiveList={activeCustomers.Count}, ActiveCount={activeCustomerCount}, CapCount={GetCustomersTowardCapacity()}", this);
    }

    public Vector3 GetRandomBonkTarget()
    {
        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            Transform point = spawnPoints[Random.Range(0, spawnPoints.Length)];
            if (point != null)
            {
                Vector3 candidate = point.position;
                Vector2 jitter = Random.insideUnitCircle * 2.5f;
                candidate.x += jitter.x;
                candidate.z += jitter.y;
                return candidate;
            }
        }

        if (ThirdPersonController.Instance != null)
        {
            Vector3 candidate = ThirdPersonController.Instance.transform.position;
            Vector2 jitter = Random.insideUnitCircle * 2f;
            candidate.x += jitter.x;
            candidate.z += jitter.y;
            return candidate;
        }

        return transform.position;
    }

    private bool TryGetSpawnPosition(out Vector3 spawnPosition, out Quaternion spawnRotation)
    {
        spawnPosition = Vector3.zero;
        spawnRotation = Quaternion.identity;

        if (spawnPoints == null || spawnPoints.Length == 0)
            return false;

        int startIndex = Random.Range(0, spawnPoints.Length);
        for (int i = 0; i < spawnPoints.Length; i++)
        {
            Transform spawnPoint = spawnPoints[(startIndex + i) % spawnPoints.Length];
            if (spawnPoint == null) continue;

            Vector3 candidate = spawnPoint.position;
            Vector2 jitter = Random.insideUnitCircle * spawnJitterRadius;
            candidate.x += jitter.x;
            candidate.z += jitter.y;

            if (IsSpawnBlocked(candidate, out string blockReason))
            {
                if (logSpawnCaps)
                    Debug.Log($"Spawn point blocked -> {spawnPoint.name} at {candidate}. {blockReason}", this);
                continue;
            }

            spawnPosition = candidate;
            spawnRotation = spawnPoint.rotation;
            return true;
        }

        return false;
    }

    private bool IsSpawnBlocked(Vector3 position, out string blockReason)
    {
        blockReason = "clear";
        return false;
    }

    private int GetCustomersTowardCapacity()
    {
        int count = 0;
        for (int i = 0; i < activeCustomers.Count; i++)
        {
            Customer customer = activeCustomers[i];
            if (customer == null) continue;
            if (!customer.CountsTowardCapacity) continue;
            count++;
        }

        return count;
    }

    private int GetAvailableChairCount()
    {
        if (chairs == null) return 0;

        int count = 0;
        for (int i = 0; i < chairs.Length; i++)
        {
            Chair chair = chairs[i];
            if (chair == null) continue;
            if (chair.IsOccupied || chair.IsReserved) continue;
            count++;
        }

        return count;
    }

    private int GetTotalChairCount()
    {
        if (chairs == null) return 0;

        int count = 0;
        for (int i = 0; i < chairs.Length; i++)
            if (chairs[i] != null) count++;

        return count;
    }

    private int GetActiveBonkCustomerCount()
    {
        int count = 0;
        for (int i = 0; i < activeCustomers.Count; i++)
        {
            Customer customer = activeCustomers[i];
            if (customer == null || !customer.IsBonkCustomer) continue;
            count++;
        }

        return count;
    }

    private bool ShouldSpawnBonkCustomer(bool isDay1, bool isDay3, bool isDay4)
    {
        if (isDay4)
        {
            int activeBonks = GetActiveBonkCustomerCount();
            if (activeBonks >= Mathf.Max(0, day4MaxBonkCustomers))
                return false;

            if (activeBonks <= 0)
                return Random.value <= day4FirstBonkChance;

            return Random.value <= day4SecondBonkChance;
        }

        if (isDay3)
        {
            int activeBonks = GetActiveBonkCustomerCount();
            if (activeBonks >= Mathf.Max(0, day3MaxBonkCustomers))
                return false;

            if (activeBonks <= 0)
                return true;

            return Random.value <= day3SecondBonkChance;
        }

        return isDay1 && Random.value <= day1BonkCustomerChance;
    }

    private int GetHighPressureFloorTargetCount()
    {
        int totalChairs = GetTotalChairCount();
        if (totalChairs <= 0 || dayCycle == null)
            return 0;

        bool useDay4 = dayCycle.DayCount == 4;
        float occupancy = dayCycle.GetPhase() switch
        {
            RestaurantDayCycle.DayPhase.Morning => useDay4 ? day4MorningOccupancy : day3MorningOccupancy,
            RestaurantDayCycle.DayPhase.RushHour => useDay4 ? day4RushOccupancy : day3RushOccupancy,
            RestaurantDayCycle.DayPhase.AfternoonSlowdown => useDay4 ? day4AfternoonOccupancy : day3AfternoonOccupancy,
            _ => useDay4 ? day4ClosingOccupancy : day3ClosingOccupancy
        };

        return Mathf.CeilToInt(totalChairs * Mathf.Clamp01(occupancy));
    }

    private void RefreshChairs()
    {
        chairs = FindObjectsByType<Chair>(FindObjectsSortMode.None);
    }

    private void ApplyDay3Tables(bool enable)
    {
        if (!enable)
        {
            if (day3TableClone != null)
            {
                Destroy(day3TableClone);
                day3TableClone = null;
                RefreshChairs();
            }
            return;
        }

        if (day3TableClone != null || day3TableSource == null)
            return;

        day3TableClone = Instantiate(day3TableSource.gameObject, day3TableSource.parent);
        day3TableClone.name = day3TableSource.gameObject.name + " Day3";
        day3TableClone.transform.position = day3TableSource.position + day3TableOffset;
        day3TableClone.transform.rotation = day3TableSource.rotation;
        day3TableClone.transform.localScale = day3TableSource.localScale;
        RefreshChairs();
    }

    private void PruneDeadCustomers()
    {
        activeCustomers.RemoveAll(customer => customer == null);
        partyCustomers.RemoveAll(customer => customer == null);

        int recountedActive = 0;
        for (int i = 0; i < activeCustomers.Count; i++)
        {
            Customer customer = activeCustomers[i];
            if (customer == null || !customer.CountsTowardCapacity) continue;
            recountedActive++;
        }

        if (recountedActive != activeCustomerCount)
        {
            if (logSpawnCaps)
                Debug.Log($"Corrected active customer count -> old {activeCustomerCount}, new {recountedActive}", this);
            activeCustomerCount = recountedActive;
        }
    }
    
    //function that spawns the spill
    public bool OnCustomerLeftChair(Vector3 chairPos)
    {
        if (spillSpawner == null) return false;

        bool didSpill = true;

        bool isPartyDay = dayCycle != null && dayCycle.DayCount == 2;
        if (isPartyDay)
        {
            int activeSpills = spillSpawner.GetActiveSpillCount();
            int maxSpills = Mathf.Max(1, spillSpawner.MaxActiveSpills);
            float throttle = 1f - Mathf.Clamp01((float)activeSpills / maxSpills);
            float chance = Mathf.Clamp01(partyDaySpillBaseChance * throttle);

            didSpill = (Random.value <= chance);
        }

        if (didSpill)
            spillSpawner.TrySpawnSpillNearChair(chairPos);

        return didSpill;
    }

    private float GetCurrentSpawnIntervalSeconds()
    {
        float interval = Mathf.Max(0.1f, spawnTuning == null ? 6f : spawnTuning.BaseSpawnIntervalSeconds);

        float reputationBonus = reputation == null ? 0f : reputation.GetSpawnIntervalBonusSeconds();
        interval = Mathf.Max(0.1f, interval - reputationBonus);

        int dirtinessPenalty = 0;
        if (restaurantManager != null)
        {
            if (spawnTuning == null)
                dirtinessPenalty = restaurantManager.GetDirtinessLevelIndex();
            else
                dirtinessPenalty = spawnTuning.GetDirtinessSpawnPenaltySeconds(restaurantManager.GetDirtinessLevel());
        }

        interval = Mathf.Max(0.1f, interval + dirtinessPenalty);

        if (TutorialMode.IsActive)
            interval *= TutorialMode.CustomerSpawnIntervalMultiplier;

        if (dayCycle != null && dayCycle.DayCount == 1 && spawnTuning != null)
            interval *= Mathf.Clamp(spawnTuning.Day1SpawnIntervalMultiplier, 0.1f, 2f);

        if (dayCycle != null && dayCycle.DayCount == 4)
            interval *= Mathf.Clamp(day4SpawnIntervalMultiplier, 0.35f, 1f);

        return interval;
    }

    private void HandleDayStarted(int dayNumber)
    {
        ApplyDay3Tables(dayNumber >= 3);

        if (dayNumber == 2) return;

        if (partyCustomers.Count == 0) return;

        for (int i = partyCustomers.Count - 1; i >= 0; i--)
        {
            if (partyCustomers[i] == null) continue;
            partyCustomers[i].CleanupSeats(false);
            Destroy(partyCustomers[i].gameObject);
            activeCustomerCount = Mathf.Max(0, activeCustomerCount - 1);
        }

        partyCustomers.Clear();
    }

}
