using UnityEngine;

public class Customer : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 2.2f;
    [SerializeField] private float sitDurationSeconds = 8f;
    [SerializeField] private float arrivalDistance = 0.2f;
    [SerializeField] private CrowdContactSettings crowdContact = new CrowdContactSettings();
    [SerializeField] private PartyCustomerEmotions emotions;
    [SerializeField] private RegularCustomerEmotions customeremotions;
    [Header("Bonk Customer")]
    [SerializeField] private bool startAsBonkCustomer;
    [SerializeField] private float bonkMoveSpeedMultiplier = 2.4f;
    [SerializeField] private float bonkLifetimeSeconds = 18f;
    [SerializeField] private float bonkLeaveSpeedMultiplier = 1.25f;
    [SerializeField] private float bonkSpawnLift = 0.5f;
    [SerializeField] private Color bonkColorTint = new Color(1f, 0.35f, 0.35f, 1f);
    
    private CustomerManager manager;
    private Chair assignedChair;
    private Transform exitPoint;
    private float sitTimer;
    private SphereCollider crowdCollider;
    private bool isBonkCustomer;
    private float bonkTimer;
    private Vector3 bonkTarget;
    private Renderer[] cachedRenderers;
    private Vector3 bonkSpawnPosition;
    private bool bonkRetargeted;
    private bool bonkLeaving;

    private enum CustomerState
    {
        WalkingToSeat,
        Sitting,
        Leaving,
        Bonking,
        BonkReposition
    }

    private void Awake()
    {
        if (emotions == null)
            emotions = GetComponentInChildren<PartyCustomerEmotions>();
        if (customeremotions == null)
            customeremotions = GetComponentInChildren<RegularCustomerEmotions>();
        crowdCollider = GetComponent<SphereCollider>();
        cachedRenderers = GetComponentsInChildren<Renderer>(true);

        if (startAsBonkCustomer)
            SetBonkCustomerVisuals(true);
    }
    
    private CustomerState state;
    public bool IsBonkCustomer => isBonkCustomer || startAsBonkCustomer;
    public bool CountsTowardCapacity => !IsBonkCustomer;

    public void Initialize(CustomerManager owner)
    {
        manager = owner;

        if (startAsBonkCustomer)
            ConfigureAsBonkCustomer();
    }
    
    private void Start()
    {
        if (customeremotions != null)
            customeremotions.SetWhistling();
    }

    public void AssignChair(Chair chair, Transform exit)
    {
        if (isBonkCustomer)
            return;

        assignedChair = chair;
        exitPoint = exit;
        state = CustomerState.WalkingToSeat;
    }

    public void ConfigureAsBonkCustomer()
    {
        if (isBonkCustomer)
            return;

        isBonkCustomer = true;
        bonkTimer = bonkLifetimeSeconds;
        state = CustomerState.Bonking;
        ReleaseReservationIfNeeded();
        if (manager != null)
            exitPoint = manager.ExitPoint;
        transform.position += Vector3.up * bonkSpawnLift;
        bonkSpawnPosition = transform.position;
        bonkRetargeted = false;
        bonkLeaving = false;
        SetBonkCustomerVisuals(true);
        PickBonkTargetFromPlayer();
    }

    private void Update()
    {
        if (state == CustomerState.Bonking)
        {
            UpdateBonkCustomer();
        }
        else if (state == CustomerState.BonkReposition)
        {
            UpdateBonkReposition();
        }
        else if (state == CustomerState.WalkingToSeat)
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

            Vector3 currentPlanar = new Vector3(transform.position.x, 0f, transform.position.z);
            Vector3 exitPlanar = new Vector3(exitPoint.position.x, 0f, exitPoint.position.z);
            if (Vector3.Distance(currentPlanar, exitPlanar) <= arrivalDistance)
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
        float speed = isBonkCustomer ? moveSpeed * bonkMoveSpeedMultiplier : moveSpeed;
        if (bonkLeaving && state == CustomerState.Leaving && isBonkCustomer)
            speed = moveSpeed * bonkLeaveSpeedMultiplier;

        Vector3 adjustedTarget = target;
        if (isBonkCustomer)
            adjustedTarget.y = transform.position.y;

        CustomerCrowdUtility.MoveTowardsWithCrowdContact(transform, crowdCollider, adjustedTarget, speed, crowdContact, isBonkCustomer);
    }

    private void UpdateBonkCustomer()
    {
        bonkTimer -= Time.deltaTime;
        if (manager == null)
        {
            manager?.DespawnCustomer(this);
            return;
        }

        if (bonkTimer <= 0f)
        {
            bonkLeaving = true;
            state = CustomerState.Leaving;
            return;
        }

        if (!bonkRetargeted)
        {
            Vector3 startPlanar = new Vector3(bonkSpawnPosition.x, 0f, bonkSpawnPosition.z);
            Vector3 targetPlanar = new Vector3(bonkTarget.x, 0f, bonkTarget.z);
            Vector3 currentPlanar = new Vector3(transform.position.x, 0f, transform.position.z);

            float totalDistance = Vector3.Distance(startPlanar, targetPlanar);
            float travelledDistance = Vector3.Distance(startPlanar, currentPlanar);
            if (totalDistance > 0.01f && travelledDistance >= totalDistance * 0.5f)
            {
                bonkRetargeted = true;
                PickBonkTargetFromPlayer();
            }
        }

        MoveTowards(bonkTarget);
        if (Vector3.Distance(transform.position, bonkTarget) <= arrivalDistance)
        {
            if (bonkTimer <= 0f)
            {
                bonkLeaving = true;
                state = CustomerState.Leaving;
            }
            else
            {
                state = CustomerState.BonkReposition;
                PickBonkRepositionTarget();
            }
        }
    }

    private void UpdateBonkReposition()
    {
        bonkTimer -= Time.deltaTime;
        if (manager == null)
        {
            manager?.DespawnCustomer(this);
            return;
        }

        if (bonkTimer <= 0f)
        {
            bonkLeaving = true;
            state = CustomerState.Leaving;
            return;
        }

        MoveTowards(bonkTarget);
        if (Vector3.Distance(transform.position, bonkTarget) <= arrivalDistance)
        {
            bonkSpawnPosition = transform.position;
            bonkRetargeted = false;
            state = CustomerState.Bonking;
            PickBonkTargetFromPlayer();
        }
    }

    private void PickBonkTargetFromPlayer()
    {
        if (ThirdPersonController.Instance == null)
        {
            bonkTarget = transform.position;
            return;
        }

        bonkTarget = ThirdPersonController.Instance.transform.position;
        bonkTarget.y = transform.position.y;
    }

    private void PickBonkRepositionTarget()
    {
        Vector2 dir2D = Random.insideUnitCircle.normalized;
        if (dir2D.sqrMagnitude <= 0.0001f)
            dir2D = Vector2.right;

        float distance = Random.Range(1.8f, 3.5f);
        bonkTarget = transform.position + new Vector3(dir2D.x, 0f, dir2D.y) * distance;
        bonkTarget.y = transform.position.y;
    }

    private void SetBonkCustomerVisuals(bool bonkEnabled)
    {
        if (cachedRenderers == null) return;

        Color tint = bonkEnabled ? bonkColorTint : Color.white;
        MaterialPropertyBlock block = new MaterialPropertyBlock();

        for (int i = 0; i < cachedRenderers.Length; i++)
        {
            Renderer renderer = cachedRenderers[i];
            if (renderer == null) continue;

            renderer.GetPropertyBlock(block);
            if (renderer.sharedMaterial != null && renderer.sharedMaterial.HasProperty("_BaseColor"))
                block.SetColor("_BaseColor", tint);
            else
                block.SetColor("_Color", tint);
            renderer.SetPropertyBlock(block);
        }
    }
}

[System.Serializable]
public class CrowdContactSettings
{
    [Min(0f)] public float contactPadding = 0.1f;
    [Min(0.1f)] public float customerHeaviness = 1.5f;
    [Min(0f)] public float playerPushStrength = 1.2f;
    [Min(0f)] public float playerPushWhilePushing = 0.35f;
    [Min(0f)] public float customerPushStrength = 0.75f;
    [Range(0f, 1f)] public float playerPushIntentThreshold = 0.25f;
}

public static class CustomerCrowdUtility
{
    public static void MoveTowardsWithCrowdContact(
        Transform customerTransform,
        SphereCollider customerCollider,
        Vector3 target,
        float moveSpeed,
        CrowdContactSettings settings,
        bool isBonkCustomer = false)
    {
        if (customerTransform == null)
            return;

        float deltaTime = Time.deltaTime;
        if (deltaTime <= 0f)
            return;

        Vector3 currentPosition = customerTransform.position;
        Vector3 desiredPosition = Vector3.MoveTowards(currentPosition, target, moveSpeed * deltaTime);

        if (settings == null)
        {
            customerTransform.position = desiredPosition;
            return;
        }

        ThirdPersonController player = ThirdPersonController.Instance;
        if (player == null)
        {
            customerTransform.position = desiredPosition;
            return;
        }

        Vector3 planarCustomer = new Vector3(desiredPosition.x, 0f, desiredPosition.z);
        Vector3 planarPlayer = player.PlanarPosition;
        Vector3 playerToCustomer = planarCustomer - planarPlayer;
        float distance = playerToCustomer.magnitude;

        float customerRadius = GetCustomerRadius(customerTransform, customerCollider);
        float combinedRadius = customerRadius + player.CharacterRadius + Mathf.Max(0f, settings.contactPadding);

        if (distance >= combinedRadius)
        {
            customerTransform.position = desiredPosition;
            return;
        }

        Vector3 pushDirectionToCustomer = distance > 0.001f
            ? playerToCustomer / distance
            : GetFallbackDirection(player);

        float overlap = combinedRadius - distance;
        float heaviness = Mathf.Max(0.1f, settings.customerHeaviness);
        bool playerPushingIntoCustomer = player.HasMovementInput &&
            Vector3.Dot(player.DesiredPlanarMoveDirection, pushDirectionToCustomer) >= settings.playerPushIntentThreshold;

        Vector3 adjustedPosition = desiredPosition;

        if (playerPushingIntoCustomer)
        {
            float customerDisplacement = overlap * settings.customerPushStrength / heaviness;
            adjustedPosition += pushDirectionToCustomer * customerDisplacement;

            float playerRecoil = overlap * settings.playerPushWhilePushing * heaviness;
            if (playerRecoil > 0f)
            {
                if (isBonkCustomer)
                    player.ApplyBonk(-pushDirectionToCustomer, playerRecoil);
                else
                    player.ApplyExternalDisplacement(-pushDirectionToCustomer * playerRecoil);
            }
        }
        else
        {
            float playerPushback = overlap * settings.playerPushStrength * heaviness;
            if (playerPushback > 0f)
            {
                if (isBonkCustomer)
                    player.ApplyBonk(-pushDirectionToCustomer, playerPushback);
                else
                    player.ApplyExternalDisplacement(-pushDirectionToCustomer * playerPushback);
            }
        }

        customerTransform.position = adjustedPosition;
    }

    private static float GetCustomerRadius(Transform customerTransform, SphereCollider customerCollider)
    {
        if (customerCollider == null)
            return 0.5f;

        Vector3 lossyScale = customerTransform.lossyScale;
        float scale = Mathf.Max(Mathf.Abs(lossyScale.x), Mathf.Abs(lossyScale.z));
        return Mathf.Max(0.05f, customerCollider.radius * scale);
    }

    private static Vector3 GetFallbackDirection(ThirdPersonController player)
    {
        if (player != null && player.HasMovementInput)
            return player.DesiredPlanarMoveDirection;

        return Vector3.forward;
    }
}
