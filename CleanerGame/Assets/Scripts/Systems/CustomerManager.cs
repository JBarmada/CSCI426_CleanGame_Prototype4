using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class CustomerManager : MonoBehaviour
{
    [Header("Spawn")]
    [SerializeField] private Customer customerPrefab;
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
    [Header("Day 3 Table Expansion")]
    [SerializeField] private Transform day3TableSource;
    [SerializeField] private Vector3 day3TableOffset = new Vector3(0f, 0f, -8f);
    [Header("Party Day Tuning")]
    [Range(0f, 1f)]
    [SerializeField] private float partyDayPartyCustomerChance = 0.7f;
    [Range(0f, 1f)]
    [SerializeField] private float partyDaySpillBaseChance = 1f;
    [Header("Spills")]
    [SerializeField] private SpillSpawner spillSpawner;
    [Header("Party Day Rage")]
    [Tooltip("Probability that a newly spawned party customer can turn angry near spills.")]
    [Range(0f, 1f)]
    [FormerlySerializedAs("angryConversionChance")]
    [SerializeField] private float partyCustomerAngryChance = 0.40f;
    [Header("Angry Customer Throttle")]
    [SerializeField] private int maxActiveAngryCustomers = 1;
    [SerializeField] private float angryConversionCooldownSeconds = 7f;

    private readonly List<Customer> activeCustomers = new List<Customer>();
    private readonly List<CustomerPartyAI> partyCustomers = new List<CustomerPartyAI>();
    private int activeCustomerCount;
    private Chair[] chairs;
    private GameObject day3TableClone;
    private float nextAngryConversionAllowedTime;

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
        return GetNearestAvailableChair(position, null);
    }

    public Chair GetNearestAvailableChair(Vector3 position, Chair excludedChair)
    {
        Chair nearest = null;
        float minDistance = Mathf.Infinity;

        foreach (Chair chair in chairs)
        {
            if (chair == null) continue;
            if (chair == excludedChair) continue;
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

    public bool TryAssignChair(CustomerPartyAI partyCustomer)
    {
        return TryAssignChair(partyCustomer, null);
    }

    public bool TryAssignChair(CustomerPartyAI partyCustomer, Chair excludedChair)
    {
        if (partyCustomer == null) return false;

        Chair chair = GetNearestAvailableChair(partyCustomer.transform.position, excludedChair);
        if (chair == null) return false;

        if (!chair.TryReserve(partyCustomer)) return false;

        partyCustomer.AssignReservedChair(chair);
        return true;
    }

    public bool TryStartPartySeatSwap(CustomerPartyAI partyCustomer, Chair currentChair)
    {
        if (partyCustomer == null || currentChair == null) return false;

        Chair targetChair = GetNearestOccupiedPartyChair(partyCustomer.transform.position, currentChair);
        if (targetChair == null) return false;

        CustomerPartyAI otherPartyCustomer = targetChair.CurrentOccupant as CustomerPartyAI;
        if (otherPartyCustomer == null || !otherPartyCustomer.CanStartSeatSwap())
            return false;

        if (!targetChair.TryReserveForShuffle(partyCustomer))
            return false;

        if (!currentChair.TryReserveForShuffle(otherPartyCustomer))
        {
            targetChair.ReleaseReservation(partyCustomer);
            return false;
        }

        partyCustomer.BeginSeatShuffle(targetChair);
        otherPartyCustomer.BeginSeatShuffle(currentChair);
        return true;
    }

    private Chair GetNearestOccupiedPartyChair(Vector3 position, Chair excludedChair)
    {
        Chair nearest = null;
        float minDistance = Mathf.Infinity;

        foreach (Chair chair in chairs)
        {
            if (chair == null) continue;
            if (chair == excludedChair) continue;
            if (!chair.IsOccupied || chair.IsReserved) continue;
            if (!(chair.CurrentOccupant is CustomerPartyAI partyCustomer)) continue;
            if (!partyCustomer.CanStartSeatSwap()) continue;

            float distance = Vector3.Distance(position, chair.transform.position);
            if (distance < minDistance)
            {
                minDistance = distance;
                nearest = chair;
            }
        }

        return nearest;
    }

    public void DespawnCustomer(Customer customer)
    {
        activeCustomers.Remove(customer);
        if (customer != null && customer.CountsTowardCapacity)
            activeCustomerCount = Mathf.Max(0, activeCustomerCount - 1);
        Destroy(customer.gameObject);
    }

    public void DespawnPartyCustomer(CustomerPartyAI partyCustomer)
    {
        partyCustomers.Remove(partyCustomer);
        activeCustomerCount = Mathf.Max(0, activeCustomerCount - 1);
        Destroy(partyCustomer.gameObject);
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

        int cap = CalculateTargetCustomerCapacity();
        if (logSpawnCaps)
        {
            string phase = dayCycle == null ? "None" : dayCycle.GetPhase().ToString();
            int dayCount = dayCycle == null ? 0 : dayCycle.DayCount;
            int chairsAvailable = GetAvailableChairCount();
            Debug.Log($"Spawn cap calc -> day {dayCount}, phase {phase}, cap {cap}, activeList {activeCustomers.Count}, activeCount {activeCustomerCount}, capCount {GetCustomersTowardCapacity()}, chairsOpen {chairsAvailable}", this);
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

        if (isPartyDay && partyCustomerPrefab != null)
        {
            if (Random.value <= partyDayPartyCustomerChance)
            {
                CustomerPartyAI partyCustomer = Instantiate(partyCustomerPrefab, spawnPosition, spawnRotation);
                partyCustomer.gameObject.SetActive(true);
                partyCustomer.Initialize(this);
                partyCustomer.SetCanTurnAngry(Random.value <= partyCustomerAngryChance);

                if (!TryAssignChair(partyCustomer))
                {
                    if (logSpawnCaps)
                        Debug.Log($"Spawn skip -> no available party chair. ChairsOpen={GetAvailableChairCount()}, SpawnPos={spawnPosition}", this);
                    Destroy(partyCustomer.gameObject);
                    return;
                }

                partyCustomers.Add(partyCustomer);
                activeCustomerCount++;
                return;
            }
        }

        Customer customer = Instantiate(customerPrefab, spawnPosition, spawnRotation);
        customer.Initialize(this);

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

    public bool TryRegisterAngryCustomerRush()
    {
        int activeAngryCustomers = GetActiveAngryCustomerCount();
        if (activeAngryCustomers >= Mathf.Max(0, maxActiveAngryCustomers))
            return false;

        if (Time.time < nextAngryConversionAllowedTime)
            return false;

        nextAngryConversionAllowedTime = Time.time + Mathf.Max(0f, angryConversionCooldownSeconds);
        return true;
    }

    private int CalculateTargetCustomerCapacity()
    {
        int baselineCap = GetBaselineCustomerCap();
        int dirtinessAdjustedCap = ApplyDirtinessCapPressure(baselineCap);
        int phaseAdjustedCap = ApplyPhaseSpawnPressure(dirtinessAdjustedCap);

        if (dayCycle != null && dayCycle.DayCount == 3)
            phaseAdjustedCap = Mathf.Max(phaseAdjustedCap, GetHighPressureFloorTargetCount());

        return Mathf.Max(1, phaseAdjustedCap);
    }

    private int GetBaselineCustomerCap()
    {
        int dayNumber = dayCycle == null ? 1 : dayCycle.DayCount;
        int globalMax = spawnTuning == null ? 12 : spawnTuning.GetMaxActiveCustomersForDay(dayNumber);
        int reputationCap = reputation == null
            ? globalMax
            : reputation.GetCustomerCapForReputation();

        return Mathf.Min(globalMax, reputationCap);
    }

    private int ApplyDirtinessCapPressure(int baselineCap)
    {
        float dirtinessMultiplier = restaurantManager == null
            ? 1f
            : restaurantManager.GetDirtinessCapMultiplier();

        int adjustedCap = Mathf.FloorToInt(baselineCap * dirtinessMultiplier);
        if (dayCycle != null && dayCycle.DayCount == 1 && spawnTuning != null)
            adjustedCap = Mathf.Max(adjustedCap, Mathf.FloorToInt(adjustedCap * spawnTuning.Day1CustomerCapMultiplier));

        return adjustedCap;
    }

    private int ApplyPhaseSpawnPressure(int dirtinessAdjustedCap)
    {
        float phaseMultiplier = dayCycle == null ? 1f : dayCycle.GetSpawnMultiplier();
        return Mathf.FloorToInt(dirtinessAdjustedCap * phaseMultiplier);
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

        for (int i = 0; i < partyCustomers.Count; i++)
        {
            CustomerPartyAI customer = partyCustomers[i];
            if (customer == null) continue;
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

    private int GetActiveAngryCustomerCount()
    {
        int count = 0;
        for (int i = 0; i < activeCustomers.Count; i++)
        {
            Customer customer = activeCustomers[i];
            if (customer == null || !customer.IsAngryCustomer) continue;
            count++;
        }

        for (int i = 0; i < partyCustomers.Count; i++)
        {
            CustomerPartyAI customer = partyCustomers[i];
            if (customer == null || !customer.IsAngryCustomer) continue;
            count++;
        }

        return count;
    }

    private int GetHighPressureFloorTargetCount()
    {
        int totalChairs = GetTotalChairCount();
        if (totalChairs <= 0 || dayCycle == null)
            return 0;

        float occupancy = dayCycle.GetPhase() switch
        {
            RestaurantDayCycle.DayPhase.Morning => day3MorningOccupancy,
            RestaurantDayCycle.DayPhase.RushHour => day3RushOccupancy,
            RestaurantDayCycle.DayPhase.AfternoonSlowdown => day3AfternoonOccupancy,
            _ => day3ClosingOccupancy
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

        for (int i = 0; i < partyCustomers.Count; i++)
        {
            CustomerPartyAI customer = partyCustomers[i];
            if (customer == null) continue;
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

        return interval;
    }

    private void HandleDayStarted(int dayNumber)
    {
        // Despawn all regular customers so no one lingers between days.
        for (int i = activeCustomers.Count - 1; i >= 0; i--)
        {
            Customer c = activeCustomers[i];
            if (c != null) Destroy(c.gameObject);
        }
        activeCustomers.Clear();
        activeCustomerCount = 0;

        // Despawn ALL party customers
        for (int i = partyCustomers.Count - 1; i >= 0; i--)
        {
            CustomerPartyAI p = partyCustomers[i];
            if (p == null) continue;
            p.CleanupSeats(false);
            Destroy(p.gameObject);
        }
        partyCustomers.Clear();

        ApplyDay3Tables(dayNumber >= 3);
    }

}
