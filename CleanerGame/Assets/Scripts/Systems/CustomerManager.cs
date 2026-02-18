using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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
    [Header("Spills")]
    [SerializeField] private SpillSpawner spillSpawner;

    private readonly List<Customer> activeCustomers = new List<Customer>();
    private readonly List<CustomerPartyAI> partyCustomers = new List<CustomerPartyAI>();
    private int activeCustomerCount;
    private Chair[] chairs;

    // ✅ Use this for your spill spawner
    public int CustomerCount => activeCustomerCount;

    public int ActiveCustomerCount => activeCustomerCount;

    private void Start()
    {
        // ✅ Unity 6 replacement for FindObjectsOfType
        chairs = FindObjectsByType<Chair>(FindObjectsSortMode.None);
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
        Chair chair = GetNearestAvailableChair(customer.transform.position);
        if (chair == null) return false;

        if (!chair.TryReserve(customer)) return false;

        customer.AssignChair(chair, exitPoint);
        return true;
    }

    public void DespawnCustomer(Customer customer)
    {
        activeCustomers.Remove(customer);
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

        float dayMultiplier = dayCycle == null ? 1f : dayCycle.GetSpawnMultiplier();
        int cap = Mathf.Max(1, Mathf.FloorToInt(dirtinessAdjustedCap * dayMultiplier));
        if (logSpawnCaps)
        {
            string phase = dayCycle == null ? "None" : dayCycle.GetPhase().ToString();
            int dayCount = dayCycle == null ? 0 : dayCycle.DayCount;
            Debug.Log($"Spawn cap calc -> day {dayCount}, phase {phase}, baseCap {baseCap}, dirtMult {dirtinessMultiplier:0.00}, dayMult {dayMultiplier:0.00}, cap {cap}", this);
        }
        if (activeCustomers.Count >= cap)
        {
            if (logSpawnCaps)
                Debug.Log($"Spawn skip -> cap reached ({activeCustomers.Count}/{cap}).", this);
            return;
        }

        Transform spawnPoint = spawnPoints[Random.Range(0, spawnPoints.Length)];
        bool isPartyDay = dayCycle != null && dayCycle.DayCount == 2;

        if (isPartyDay && partyCustomerPrefab != null)
        {
            CustomerPartyAI partyCustomer = Instantiate(partyCustomerPrefab, spawnPoint.position, spawnPoint.rotation);
            partyCustomer.gameObject.SetActive(true);
            partyCustomer.Initialize(this);
            partyCustomers.Add(partyCustomer);
            activeCustomerCount++;
            return;
        }

        Customer customer = Instantiate(customerPrefab, spawnPoint.position, spawnPoint.rotation);
        customer.Initialize(this);

        if (!TryAssignChair(customer))
        {
            if (logSpawnCaps)
                Debug.Log("Spawn skip -> no available chair.", this);
            Destroy(customer.gameObject);
            return;
        }

        activeCustomers.Add(customer);
        activeCustomerCount++;
    }
    public void OnCustomerLeftChair(Vector3 chairPos)
{
    if (spillSpawner == null) return;

    spillSpawner.TrySpawnSpillNearChair(chairPos);
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

        return interval;
    }

    private void HandleDayStarted(int dayNumber)
    {
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
