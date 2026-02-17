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
        var wait = new WaitForSeconds(spawnIntervalSeconds);
        while (true)
        {
            TrySpawnCustomer();
            yield return wait;
        }
    }

    private void TrySpawnCustomer()
    {
        if (customerPrefab == null || spawnPoints == null || spawnPoints.Length == 0) return;
        int dirtinessCap = restaurantManager == null
            ? maxActiveCustomers
            : restaurantManager.GetMaxCustomersForDirtiness();

        int cap = Mathf.Min(maxActiveCustomers, dirtinessCap);
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

}
