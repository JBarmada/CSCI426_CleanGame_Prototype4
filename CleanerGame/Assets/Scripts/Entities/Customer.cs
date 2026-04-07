using UnityEngine;

public class Customer : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 2.2f;
    [SerializeField] private float sitDurationSeconds = 8f;
    [SerializeField] private float arrivalDistance = 0.2f;
    [SerializeField] private PartyCustomerEmotions emotions;
    [SerializeField] private RegularCustomerEmotions customeremotions;
    
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

    private void Awake()
    {
        if (emotions == null)
            emotions = GetComponentInChildren<PartyCustomerEmotions>();
        if (customeremotions == null)
            customeremotions = GetComponentInChildren<RegularCustomerEmotions>();
    }
    
    private CustomerState state;

    public void Initialize(CustomerManager owner)
    {
        manager = owner;
    }
    
    private void Start()
    {
        if (customeremotions != null)
            customeremotions.SetWhistling();
    }

    public void AssignChair(Chair chair, Transform exit)
    {
        assignedChair = chair;
        exitPoint = exit;
        state = CustomerState.WalkingToSeat;
    }

    private void Update()
    {
        if (state == CustomerState.WalkingToSeat)
        {
            if (assignedChair == null)
            {
                if (manager != null)
                    manager.TryAssignChair(this);
                return;
            }

            MoveTowards(assignedChair.GetSeatPosition());

            if (Vector3.Distance(transform.position, assignedChair.GetSeatPosition()) <= arrivalDistance)
            {
                if (assignedChair.TrySit(this))
                {
                    sitTimer = sitDurationSeconds;
                    state = CustomerState.Sitting;
                    if (emotions != null)
                        emotions.BeginSitting();
                }
                else
                {
                    assignedChair.ReleaseReservation(this);
                    assignedChair = null;

                    if (manager != null)
                        manager.TryAssignChair(this);
                }
            }
        }
        else if (state == CustomerState.Sitting)
        {
            sitTimer -= Time.deltaTime;
            if (emotions != null)
                emotions.UpdateSittingTimer(sitTimer);
            if (sitTimer <= 0f)
            {
                // ✅ Customer stands up: spawn spill on floor near chair, then free chair
                if (assignedChair != null)
                {
                    //spawning logic
                    bool didSpill = false;

                    if (manager != null)
                        didSpill = manager.OnCustomerLeftChair(assignedChair.transform.position);

                    if (didSpill && emotions != null)
                        emotions.BeginLeaving(); // crying ONLY if spill
                    if (didSpill && customeremotions != null)
                        customeremotions.ShowSpillForThreeSeconds();


                    assignedChair.CustomerLeft(); // frees occupancy
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
                if (manager != null) manager.DespawnCustomer(this);
                return;
            }

            MoveTowards(exitPoint.position);

            if (Vector3.Distance(transform.position, exitPoint.position) <= arrivalDistance)
            {
                ReleaseReservationIfNeeded();
                if (manager != null) manager.DespawnCustomer(this);
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
        transform.position = Vector3.MoveTowards(transform.position, target, moveSpeed * Time.deltaTime);
    }
}
