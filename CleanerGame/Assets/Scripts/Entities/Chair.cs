using UnityEngine;

public class Chair : MonoBehaviour
{
    [SerializeField] private Transform seatPoint;

    public bool IsOccupied { get; private set; }
    public bool IsReserved { get; private set; }
    private Object currentOccupant;
    private Object reservedOccupant;

    public Vector3 GetSeatPosition()
    {
        return seatPoint == null ? transform.position : seatPoint.position;
    }

    public bool TryReserve(Object occupant)
    {
        if (occupant == null) return false;
        if (IsOccupied || IsReserved) return false;

        IsReserved = true;
        reservedOccupant = occupant;
        return true;
    }

    public void ReleaseReservation(Object occupant)
    {
        if (!IsReserved || reservedOccupant != occupant) return;

        IsReserved = false;
        reservedOccupant = null;
    }

    public bool TrySit(Object occupant)
    {
        if (occupant == null) return false;
        if (IsOccupied) return false;
        if (IsReserved && reservedOccupant != occupant) return false;

        IsOccupied = true;
        IsReserved = false;
        reservedOccupant = null;
        currentOccupant = occupant;
        return true;
    }

    public void CustomerLeft()
    {
        IsOccupied = false;
        currentOccupant = null;

        SpawnDirt();
    }

    public void ClearSeat(bool spawnDirt)
    {
        IsOccupied = false;
        IsReserved = false;
        currentOccupant = null;
        reservedOccupant = null;

        if (spawnDirt)
            SpawnDirt();
    }

    void SpawnDirt()
    {
        RestaurantManager.Instance.AddDirt(1);
        // Instantiate dirt prefab here
    }
}
