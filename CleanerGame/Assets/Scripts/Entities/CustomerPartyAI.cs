using UnityEngine;

public class CustomerPartyAI : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 2.2f;
    [SerializeField] private float arrivalDistance = 0.2f;

    [Header("Shuffle Timing")]
    [SerializeField] private Vector2 shuffleSecondsRange = new Vector2(3f, 8f);

    [Header("Setup")]
    [SerializeField] private bool disableCustomerComponent = true;

    private CustomerManager manager;
    private Chair assignedChair;
    private Chair reservedChair;
    private float shuffleTimer;
    private Customer customerProxy;
    private Chair[] chairs;

    private enum PartyState
    {
        PickingSeat,
        WalkingToSeat,
        Sitting
    }

    private PartyState state;

    public void Initialize(CustomerManager owner)
    {
        manager = owner;
    }

    private void Awake()
    {
        customerProxy = GetComponent<Customer>();
        if (customerProxy != null && disableCustomerComponent)
            customerProxy.enabled = false;

        EnableRenderers();

        chairs = FindObjectsByType<Chair>(FindObjectsSortMode.None);
    }

    private void EnableRenderers()
    {
        var renderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
            renderers[i].enabled = true;
    }

    private void Start()
    {
        state = PartyState.PickingSeat;
    }

    private void OnDisable()
    {
        CleanupSeats(false);
    }

    private void OnDestroy()
    {
        CleanupSeats(false);
    }

    private void Update()
    {
        switch (state)
        {
            case PartyState.PickingSeat:
                PickNewSeat();
                break;
            case PartyState.WalkingToSeat:
                WalkToSeat();
                break;
            case PartyState.Sitting:
                UpdateSitting();
                break;
        }
    }

    private void PickNewSeat()
    {
        Chair next = FindAvailableChair();
        if (next == null)
            return;

        if (!TryReserve(next))
            return;

        reservedChair = next;
        state = PartyState.WalkingToSeat;
    }

    private void WalkToSeat()
    {
        if (reservedChair == null)
        {
            state = PartyState.PickingSeat;
            return;
        }

        Vector3 target = reservedChair.GetSeatPosition();
        transform.position = Vector3.MoveTowards(transform.position, target, moveSpeed * Time.deltaTime);

        if (Vector3.Distance(transform.position, target) <= arrivalDistance)
        {
            if (reservedChair.TrySit(customerProxy))
            {
                assignedChair = reservedChair;
                reservedChair = null;
                shuffleTimer = Random.Range(shuffleSecondsRange.x, shuffleSecondsRange.y);
                state = PartyState.Sitting;
            }
            else
            {
                ReleaseReservation(reservedChair);
                reservedChair = null;
                state = PartyState.PickingSeat;
            }
        }
    }

    private void UpdateSitting()
    {
        shuffleTimer -= Time.deltaTime;
        if (shuffleTimer > 0f) return;

        if (assignedChair != null)
        {
            if (manager != null)
                manager.OnCustomerLeftChair(assignedChair.transform.position);

            assignedChair.CustomerLeft();
            assignedChair = null;
        }

        state = PartyState.PickingSeat;
    }

    private Chair FindAvailableChair()
    {
        if (chairs == null || chairs.Length == 0) return null;

        for (int i = 0; i < chairs.Length; i++)
        {
            Chair chair = chairs[Random.Range(0, chairs.Length)];
            if (chair == null) continue;
            if (chair.IsOccupied || chair.IsReserved) continue;
            return chair;
        }

        return null;
    }

    private bool TryReserve(Chair chair)
    {
        if (chair == null || customerProxy == null) return false;
        return chair.TryReserve(customerProxy);
    }

    private void ReleaseReservation(Chair chair)
    {
        if (chair == null || customerProxy == null) return;
        chair.ReleaseReservation(customerProxy);
    }

    public void CleanupSeats(bool spawnDirt)
    {
        if (reservedChair != null)
        {
            ReleaseReservation(reservedChair);
            reservedChair = null;
        }

        if (assignedChair != null)
        {
            if (spawnDirt)
                assignedChair.CustomerLeft();
            else
                assignedChair.ClearSeat(false);

            assignedChair = null;
        }
    }
}
