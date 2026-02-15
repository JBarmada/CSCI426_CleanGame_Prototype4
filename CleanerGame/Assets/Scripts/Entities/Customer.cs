using UnityEngine;

public class Customer : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 2.2f;
    [SerializeField] private float sitDurationSeconds = 8f;
    [SerializeField] private float arrivalDistance = 0.2f;

    private CustomerManager manager;
    private Chair assignedChair;
    private Transform exitPoint;
    private float sitTimer;

    private enum CustomerState
    {
        WalkingToSeat,
        Sitting,
        Leaving
    }

    private CustomerState state;

    public void Initialize(CustomerManager owner)
    {
        manager = owner;
    }

    public void AssignChair(Chair chair, Transform exit)
    {
        assignedChair = chair;
        exitPoint = exit;
        state = CustomerState.WalkingToSeat;
    }

    void Update()
    {
        if (state == CustomerState.WalkingToSeat)
        {
            if (assignedChair == null)
            {
                if (manager != null)
                {
                    manager.TryAssignChair(this);
                }
                return;
            }

            MoveTowards(assignedChair == null ? transform.position : assignedChair.GetSeatPosition());

            if (assignedChair != null && Vector3.Distance(transform.position, assignedChair.GetSeatPosition()) <= arrivalDistance)
            {
                if (assignedChair.TrySit(this))
                {
                    sitTimer = sitDurationSeconds;
                    state = CustomerState.Sitting;
                }
                else
                {
                    assignedChair.ReleaseReservation(this);
                    assignedChair = null;
                    manager.TryAssignChair(this);
                }
            }
        }
        else if (state == CustomerState.Sitting)
        {
            sitTimer -= Time.deltaTime;
            if (sitTimer <= 0f)
            {
                if (assignedChair != null)
                {
                    assignedChair.CustomerLeft();
                    assignedChair = null;
                }
                state = CustomerState.Leaving;
            }
        }
        else if (state == CustomerState.Leaving)
        {
            if (exitPoint == null)
            {
                ReleaseReservationIfNeeded();
                manager.DespawnCustomer(this);
                return;
            }

            MoveTowards(exitPoint.position);

            if (Vector3.Distance(transform.position, exitPoint.position) <= arrivalDistance)
            {
                ReleaseReservationIfNeeded();
                manager.DespawnCustomer(this);
            }
        }
    }

    private void ReleaseReservationIfNeeded()
    {
        if (assignedChair == null) return;

        assignedChair.ReleaseReservation(this);
        assignedChair = null;
    }

    private void MoveTowards(Vector3 target)
    {
        Vector3 next = Vector3.MoveTowards(transform.position, target, moveSpeed * Time.deltaTime);
        transform.position = next;
    }
}
