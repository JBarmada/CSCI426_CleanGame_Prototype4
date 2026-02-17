using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CustomerManager : MonoBehaviour
{
    [Header("Spawn")]
    [SerializeField] private Customer customerPrefab;
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] private Transform exitPoint;
    [SerializeField] private float spawnIntervalSeconds = 6f;
    [SerializeField] private int maxActiveCustomers = 2;
    [SerializeField] private RestaurantManager restaurantManager;
    [SerializeField] private RestaurantDayCycle dayCycle;
    [SerializeField] private RestaurantReputation reputation;
    [Header("Spills")]
    [SerializeField] private SpillSpawner spillSpawner;

    private readonly List<Customer> activeCustomers = new List<Customer>();
    private Chair[] chairs;

    // ✅ Use this for your spill spawner
    public int CustomerCount => activeCustomers.Count;

    public int ActiveCustomerCount => activeCustomers.Count;

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
        StartCoroutine(SpawnLoop());
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
        if (customerPrefab == null || spawnPoints == null || spawnPoints.Length == 0) return;
        if (dayCycle != null && dayCycle.IsClosed) return;

        int reputationCap = reputation == null
            ? maxActiveCustomers
            : reputation.GetCustomerCapForReputation();

        int baseCap = Mathf.Min(maxActiveCustomers, reputationCap);
        float dirtinessMultiplier = restaurantManager == null
            ? 1f
            : restaurantManager.GetDirtinessCapMultiplier();
        int dirtinessAdjustedCap = Mathf.FloorToInt(baseCap * dirtinessMultiplier);

        float dayMultiplier = dayCycle == null ? 1f : dayCycle.GetSpawnMultiplier();
        int cap = Mathf.Max(0, Mathf.FloorToInt(dirtinessAdjustedCap * dayMultiplier));
        if (activeCustomers.Count >= cap) return;

        Transform spawnPoint = spawnPoints[Random.Range(0, spawnPoints.Length)];
        Customer customer = Instantiate(customerPrefab, spawnPoint.position, spawnPoint.rotation);
        customer.Initialize(this);

        if (!TryAssignChair(customer))
        {
            Destroy(customer.gameObject);
            return;
        }

        activeCustomers.Add(customer);
    }
    public void OnCustomerLeftChair(Vector3 chairPos)
{
    if (spillSpawner == null) return;

    spillSpawner.TrySpawnSpillNearChair(chairPos);
}

    private float GetCurrentSpawnIntervalSeconds()
    {
        float interval = Mathf.Max(0.1f, spawnIntervalSeconds);

        float reputationBonus = reputation == null ? 0f : reputation.GetSpawnIntervalBonusSeconds();
        interval = Mathf.Max(0.1f, interval - reputationBonus);

        int dirtinessPenalty = restaurantManager == null ? 0 : restaurantManager.GetDirtinessLevelIndex();
        interval = Mathf.Max(0.1f, interval + dirtinessPenalty);

        return interval;
    }

}
