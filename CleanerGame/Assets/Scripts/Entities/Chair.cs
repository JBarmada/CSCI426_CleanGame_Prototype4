using UnityEngine;

public class Chair : MonoBehaviour
{
    [SerializeField] private Transform seatPoint;
    [SerializeField] private float approachDistance = 0.9f;
    [SerializeField] private float aisleClearance = 1.75f;

    public bool IsOccupied { get; private set; }
    public bool IsReserved { get; private set; }
    public Object CurrentOccupant => currentOccupant;
    private Object currentOccupant;
    private Object reservedOccupant;

    public Vector3 GetSeatPosition()
    {
        return seatPoint == null ? transform.position : seatPoint.position;
    }

    public Vector3 GetApproachPosition()
    {
        Vector3 seatPosition = GetSeatPosition();
        Vector3 awayFromTable = GetAwayFromTableDirection();
        return seatPosition + awayFromTable * approachDistance;
    }

    public Vector3 GetAislePosition(Vector3 fromPosition)
    {
        Vector3 approachPosition = GetApproachPosition();
        Transform tableRoot = transform.parent;
        if (tableRoot == null)
            return approachPosition;

        Vector3 awayFromTable = GetAwayFromTableDirection();
        Vector3 sideA = new Vector3(-awayFromTable.z, 0f, awayFromTable.x);
        if (sideA.sqrMagnitude < 0.001f)
            return approachPosition;

        sideA.Normalize();
        Vector3 sideB = -sideA;
        Vector3 tablePlanar = new Vector3(tableRoot.position.x, approachPosition.y, tableRoot.position.z);
        Vector3 fromPlanar = new Vector3(fromPosition.x, approachPosition.y, fromPosition.z);

        Vector3 aisleA = tablePlanar + sideA * aisleClearance;
        Vector3 aisleB = tablePlanar + sideB * aisleClearance;
        Vector3 selectedAisle = Vector3.Distance(fromPlanar, aisleA) <= Vector3.Distance(fromPlanar, aisleB)
            ? aisleA
            : aisleB;

        return selectedAisle;
    }

    private Vector3 GetAwayFromTableDirection()
    {
        Vector3 seatPosition = GetSeatPosition();
        Vector3 awayFromTable = seatPosition - transform.position;
        awayFromTable.y = 0f;

        if (awayFromTable.sqrMagnitude < 0.001f && transform.parent != null)
        {
            awayFromTable = transform.position - transform.parent.position;
            awayFromTable.y = 0f;
        }

        if (awayFromTable.sqrMagnitude < 0.001f)
            awayFromTable = transform.right;

        awayFromTable.y = 0f;
        return awayFromTable.normalized;
    }

    public bool TryReserve(Object occupant)
    {
        if (occupant == null) return false;
        if (IsOccupied || IsReserved) return false;

        IsReserved = true;
        reservedOccupant = occupant;
        return true;
    }

    public bool TryReserveForShuffle(Object occupant)
    {
        if (occupant == null) return false;
        if (IsReserved) return false;

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
