using UnityEngine;

public class Chair : MonoBehaviour
{
    [SerializeField] private Transform seatPoint;

    public bool IsOccupied { get; private set; }
    public bool IsReserved { get; private set; }
    private Customer currentCustomer;
    private Customer reservedCustomer;

    public Vector3 GetSeatPosition()
    {
        return seatPoint == null ? transform.position : seatPoint.position;
    }

    public bool TryReserve(Customer customer)
    {
        if (IsOccupied || IsReserved) return false;

        IsReserved = true;
        reservedCustomer = customer;
        return true;
    }

    public void ReleaseReservation(Customer customer)
    {
        if (!IsReserved || reservedCustomer != customer) return;

        IsReserved = false;
        reservedCustomer = null;
    }

    public bool TrySit(Customer customer)
    {
        if (IsOccupied) return false;
        if (IsReserved && reservedCustomer != customer) return false;

        IsOccupied = true;
        IsReserved = false;
        reservedCustomer = null;
        currentCustomer = customer;
        return true;
    }

    public void CustomerLeft()
    {
        IsOccupied = false;
        currentCustomer = null;

        SpawnDirt();
    }

    public void ClearSeat(bool spawnDirt)
    {
        IsOccupied = false;
        IsReserved = false;
        currentCustomer = null;
        reservedCustomer = null;

        if (spawnDirt)
            SpawnDirt();
    }

    void SpawnDirt()
    {
        RestaurantManager.Instance.AddDirt(1);
        // Instantiate dirt prefab here
    }
}

