using UnityEngine;

public class CustomerManager : MonoBehaviour
{
    [Header("Spawn")]
    [SerializeField] private Customer customerPrefab;
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] private Transform exitPoint;
    [SerializeField] private float spawnIntervalSeconds = 6f;
    [SerializeField] private int maxActiveCustomers = 2;

    private readonly System.Collections.Generic.List<Customer> activeCustomers = new System.Collections.Generic.List<Customer>();
    private Chair[] chairs;

    void Start()
    {
        chairs = FindObjectsOfType<Chair>();
        StartCoroutine(SpawnLoop());
    }

    public Chair GetNearestAvailableChair(Vector3 position)
    {
        Chair nearest = null;
        float minDistance = Mathf.Infinity;

        foreach (Chair chair in chairs)
        {
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

    private System.Collections.IEnumerator SpawnLoop()
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
        if (activeCustomers.Count >= maxActiveCustomers) return;

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
}

