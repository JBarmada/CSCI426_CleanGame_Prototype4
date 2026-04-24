using UnityEngine;
using UnityEngine.Serialization;

public class CustomerPartyAI : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 2.2f;
    [SerializeField] private float arrivalDistance = 0.2f;
    [SerializeField] private CrowdContactSettings crowdContact = new CrowdContactSettings();

    [Header("Seat Arrival Fail-safe")]
    [Tooltip("If a party customer is blocked this close to their reserved chair, complete the sit instead of vibrating against the table.")]
    [SerializeField] private float blockedSitDistance = 0.75f;
    [Tooltip("Seconds with almost no movement near the reserved chair before forcing the sit.")]
    [SerializeField] private float blockedSitSeconds = 0.35f;

    [Header("Shuffle Timing")]
    [SerializeField] private Vector2 shuffleSecondsRange = new Vector2(8f, 8f);

    [Header("Setup")]
    [SerializeField] private bool disableCustomerComponent = true;

    [SerializeField] private PartyCustomerEmotions emotions;

    [Header("Angry Rush")]
    [Tooltip("Speed multiplier applied during the angry straight-line rush.")]
    [FormerlySerializedAs("bonkMoveSpeedMultiplier")]
    [SerializeField] private float angryRushMoveSpeedMultiplier = 2.4f;

    [Header("Rage Conversion")]
    [Tooltip("Radius in world units to scan for nearby spills while sitting.")]
    [SerializeField] private float angryCheckRadius = 2.5f;
    [Tooltip("How much anger progress is gained per second for each spill within range.")]
    [SerializeField] private float angryRatePerSpill = 0.15f;
    [Tooltip("How fast anger decays per second when no spills are nearby.")]
    [SerializeField] private float angryDecayRate = 0.015f;
    [Tooltip("Distance of the straight-line rush target placed ahead of the customer.")]
    [SerializeField] private float angryRushDistance = 28f;
    [Tooltip("How close the customer must get to the player to count as a bonk and start leaving.")]
    [SerializeField] private float angryBonkDetectionRadius = 1.5f;
    [Tooltip("If an angry party customer makes almost no progress for this long, they stop rushing and path toward the exit.")]
    [SerializeField] private float angryStuckExitSeconds = 0.8f;
    [SerializeField] private Color angryTintColor = new Color(1f, 0.35f, 0.35f, 1f);

    [Header("Obstacle Collision")]
    [Tooltip("Set this to the layer(s) your wall/table/chair colliders live on.")]
    [SerializeField] private LayerMask wallLayerMask;

    private CustomerManager manager;
    private Chair assignedChair;
    private Chair reservedChair;
    private float shuffleTimer;
    private Vector3 lastWalkPosition;
    private float blockedSitTimer;
    private bool reachedSeatAisle;
    private bool reachedSeatApproach;
    private Customer customerProxy;
    private SphereCollider crowdCollider;
    private Renderer[] cachedRenderers;
    private Color[] originalTints;       // material colors cached at Awake; used as lerp baseline

    // Rage conversion runtime state
    private bool    canTurnAngry;
    private float   angryProgress;
    private float   angerCheckTimer;
    private int     cachedNearbySpills;
    private Vector3 angryRushTarget;
    private Vector3 lastAngryPosition;
    private float angryStuckTimer;
    private Transform exitPoint;

    // Post-rush: walking to door
    private bool leavingAfterRush;

    private enum PartyState
    {
        PickingSeat,
        WalkingToSeat,
        Sitting,
        AngryRush,      // converted from seated, straight-line rush toward player
        LeavingAfterRush
    }

    private PartyState state;

    public bool IsAngryCustomer =>
        state == PartyState.AngryRush || state == PartyState.LeavingAfterRush;

    public void Initialize(CustomerManager owner)
    {
        manager = owner;
    }

    /// <summary>
    /// Called by CustomerManager at spawn. Enables rage conversion behaviour.
    /// </summary>
    public void SetCanTurnAngry(bool value) => canTurnAngry = value;

    private void Awake()
    {
        customerProxy = GetComponent<Customer>();
        if (customerProxy != null && disableCustomerComponent)
            customerProxy.enabled = false;
        if (emotions == null)
            emotions = GetComponentInChildren<PartyCustomerEmotions>();
        cachedRenderers = GetComponentsInChildren<Renderer>(true);

        // Cache each renderer's original material color so SetTintGradient
        // lerps FROM the real starting color, not a hard-coded Color.white.
        originalTints = new Color[cachedRenderers.Length];
        for (int i = 0; i < cachedRenderers.Length; i++)
        {
            Renderer r = cachedRenderers[i];
            if (r == null || r.sharedMaterial == null) { originalTints[i] = Color.white; continue; }
            if (r.sharedMaterial.HasProperty("_BaseColor"))
                originalTints[i] = r.sharedMaterial.GetColor("_BaseColor");
            else if (r.sharedMaterial.HasProperty("_Color"))
                originalTints[i] = r.sharedMaterial.GetColor("_Color");
            else
                originalTints[i] = Color.white;
        }

        EnableRenderers();

        crowdCollider = GetComponent<SphereCollider>();
    }

    private void EnableRenderers()
    {
        if (cachedRenderers == null) return;
        for (int i = 0; i < cachedRenderers.Length; i++)
            if (cachedRenderers[i] != null)
                cachedRenderers[i].enabled = true;
    }

    private void Start()
    {
        if (manager == null)
            manager = FindFirstObjectByType<CustomerManager>();

        if (reservedChair == null && assignedChair == null)
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
            case PartyState.AngryRush:
                UpdateAngryRush();
                break;
            case PartyState.LeavingAfterRush:
                UpdateLeavingAfterRush();
                break;
        }
    }

    // ── Normal party states ───────────────────────────────────────────────────

    private void PickNewSeat()
    {
        if (manager == null) return;
        manager.TryAssignChair(this);
    }

    public void AssignReservedChair(Chair chair)
    {
        if (chair == null) return;
        if (reservedChair != null && reservedChair != chair)
            ReleaseReservation(reservedChair);

        reservedChair = chair;
        ResetBlockedSitTracking();
        ResetSeatRoute();
        state = PartyState.WalkingToSeat;
    }

    private void WalkToSeat()
    {
        if (reservedChair == null)
        {
            state = PartyState.PickingSeat;
            return;
        }

        Vector3 seatPosition = reservedChair.GetSeatPosition();
        Vector3 target = GetSeatRouteTarget(reservedChair, seatPosition);
        MoveWithWallCheck(target, moveSpeed, isAngryCustomer: false);

        float distanceToSeat = GetPlanarDistance(transform.position, seatPosition);
        if (distanceToSeat <= arrivalDistance || ShouldForceSit(distanceToSeat))
        {
            CompleteSitAtReservedChair(seatPosition);
        }
    }

    private Vector3 GetSeatRouteTarget(Chair chair, Vector3 seatPosition)
    {
        if (!reachedSeatAisle)
        {
            Vector3 aislePosition = chair.GetAislePosition(transform.position);
            if (GetPlanarDistance(transform.position, aislePosition) > arrivalDistance)
                return aislePosition;

            reachedSeatAisle = true;
        }

        if (!reachedSeatApproach)
        {
            Vector3 approachPosition = chair.GetApproachPosition();
            if (GetPlanarDistance(transform.position, approachPosition) > arrivalDistance)
                return approachPosition;

            reachedSeatApproach = true;
        }

        return seatPosition;
    }

    private bool ShouldForceSit(float distanceToSeat)
    {
        if (distanceToSeat > blockedSitDistance)
        {
            ResetBlockedSitTracking();
            return false;
        }

        float moved = GetPlanarDistance(transform.position, lastWalkPosition);
        if (moved <= 0.025f)
            blockedSitTimer += Time.deltaTime;
        else
            blockedSitTimer = 0f;

        lastWalkPosition = transform.position;
        return blockedSitTimer >= blockedSitSeconds;
    }

    private void CompleteSitAtReservedChair(Vector3 target)
    {
        if (reservedChair.TrySit(this))
        {
            Vector3 snappedPosition = transform.position;
            snappedPosition.x = target.x;
            snappedPosition.z = target.z;
            transform.position = snappedPosition;

            assignedChair = reservedChair;
            reservedChair = null;
            ResetSeatRoute();
            shuffleTimer = Random.Range(shuffleSecondsRange.x, shuffleSecondsRange.y);
            ResetBlockedSitTracking();
            state = PartyState.Sitting;
            if (emotions != null)
                emotions.BeginSitting();
        }
        else
        {
            ReleaseReservation(reservedChair);
            reservedChair = null;
            ResetSeatRoute();
            ResetBlockedSitTracking();
            state = PartyState.PickingSeat;
        }
    }

    private void ResetBlockedSitTracking()
    {
        lastWalkPosition = transform.position;
        blockedSitTimer = 0f;
    }

    private void ResetSeatRoute()
    {
        reachedSeatAisle = false;
        reachedSeatApproach = false;
    }

    private void UpdateSitting()
    {
        // ── Rage conversion check ────────────────────────────────────────────
        angerCheckTimer += Time.deltaTime;
        if (angerCheckTimer >= 0.25f)
        {
            angerCheckTimer = 0f;
            cachedNearbySpills = CountNearbySpills();
        }

        if (canTurnAngry)
        {
            if (cachedNearbySpills > 0)
            {
                angryProgress += cachedNearbySpills * angryRatePerSpill * Time.deltaTime;
                angryProgress  = Mathf.Min(1f, angryProgress);
            }
            else
            {
                angryProgress = Mathf.Max(0f, angryProgress - angryDecayRate * Time.deltaTime);
            }

            SetTintGradient(angryProgress);

            if (angryProgress >= 1f)
            {
                if (manager != null && !manager.TryRegisterAngryCustomerRush())
                {
                    angryProgress = Mathf.Min(angryProgress, 0.85f);
                    SetTintGradient(angryProgress);
                    return;
                }

                TriggerAngryRush();
                return;
            }
        }

        // ── Normal shuffle countdown ─────────────────────────────────────────
        shuffleTimer -= Time.deltaTime;
        if (emotions != null)
            emotions.UpdateSittingTimer(shuffleTimer);

        if (shuffleTimer > 0f) return;

        if (assignedChair != null)
        {
            Chair previousChair = assignedChair;
            if (manager == null || !manager.TryAssignChair(this, previousChair))
            {
                shuffleTimer = 1f;
                return;
            }

            bool didSpill = false;
            didSpill = manager.OnCustomerLeftChair(previousChair.transform.position);

            if (didSpill && emotions != null)
                emotions.BeginLeaving();

            previousChair.CustomerLeft();
            assignedChair = null;
            ResetSeatRoute();
            return;
        }

        state = PartyState.PickingSeat;
    }

    // ── Rage conversion helpers ───────────────────────────────────────────────

    private int CountNearbySpills()
    {
        var allSpills = SpillManager.ActiveSpills;
        int count = 0;
        for (int i = 0; i < allSpills.Count; i++)
        {
            if (allSpills[i] == null) continue;
            if (Vector3.Distance(transform.position, allSpills[i].transform.position) <= angryCheckRadius)
                count++;
        }
        return count;
    }

    private void TriggerAngryRush()
    {
        // Release chair without spawning a spill
        CleanupSeats(false);

        // Cache exit point from manager
        if (manager != null)
            exitPoint = manager.ExitPoint;

        // Lock straight-line target toward player
        Vector3 playerPos = ThirdPersonController.Instance != null
            ? ThirdPersonController.Instance.transform.position
            : transform.position + transform.forward;

        Vector3 dir = playerPos - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.01f)
            dir = transform.forward;
        dir.Normalize();

        angryRushTarget   = transform.position + dir * angryRushDistance;
        angryRushTarget.y = transform.position.y;
        lastAngryPosition = transform.position;
        angryStuckTimer   = 0f;

        state = PartyState.AngryRush;
        SetTintGradient(1f);   // snap to full red
    }

    private void SetTintGradient(float t)
    {
        if (cachedRenderers == null) return;
        var block = new MaterialPropertyBlock();
        for (int i = 0; i < cachedRenderers.Length; i++)
        {
            Renderer r = cachedRenderers[i];
            if (r == null) continue;
            // Lerp from the renderer's true original color so blue/other-tinted
            // customers don't snap to white at t=0.
            Color origin = (originalTints != null && i < originalTints.Length)
                ? originalTints[i] : Color.white;
            Color tint = Color.Lerp(origin, angryTintColor, t);
            r.GetPropertyBlock(block);
            if (r.sharedMaterial != null && r.sharedMaterial.HasProperty("_BaseColor"))
                block.SetColor("_BaseColor", tint);
            else
                block.SetColor("_Color", tint);
            r.SetPropertyBlock(block);
        }
    }

    // ── State: AngryRush ──────────────────────────────────────────────────────

    private void UpdateAngryRush()
    {
        MoveWithWallCheck(angryRushTarget, moveSpeed * angryRushMoveSpeedMultiplier, isAngryCustomer: true);
        UpdateAngryStuckRecovery();

        // Check if we've reached the player — deliver the bonk manually here.
        // The crowd utility only fires ApplyBonk inside combinedRadius (~0.8 f), which
        // is smaller than angryBonkDetectionRadius (1.5 f), so the utility's contact
        // code never runs before this check transitions us to LeavingAfterRush.
        if (ThirdPersonController.Instance != null)
        {
            float playerDist = Vector3.Distance(
                new Vector3(transform.position.x, 0f, transform.position.z),
                new Vector3(ThirdPersonController.Instance.transform.position.x, 0f,
                            ThirdPersonController.Instance.transform.position.z));

            if (playerDist <= angryBonkDetectionRadius)
            {
                // Push the player directly away from the customer.
                Vector3 bonkDir = new Vector3(
                    ThirdPersonController.Instance.transform.position.x - transform.position.x, 0f,
                    ThirdPersonController.Instance.transform.position.z - transform.position.z);
                if (bonkDir.sqrMagnitude > 0.001f) bonkDir.Normalize();
                ThirdPersonController.Instance.ApplyBonk(bonkDir, moveSpeed * angryRushMoveSpeedMultiplier);

                state = PartyState.LeavingAfterRush;
                return;
            }
        }

        // Fallback: rush line exhausted without hitting player — also exit
        float dist = Vector3.Distance(
            new Vector3(transform.position.x, 0f, transform.position.z),
            new Vector3(angryRushTarget.x,    0f, angryRushTarget.z));

        if (dist <= arrivalDistance + 0.5f)
            state = PartyState.LeavingAfterRush;
    }

    private void UpdateAngryStuckRecovery()
    {
        Vector3 currentPlanar = new Vector3(transform.position.x, 0f, transform.position.z);
        Vector3 lastPlanar = new Vector3(lastAngryPosition.x, 0f, lastAngryPosition.z);

        if (Vector3.Distance(currentPlanar, lastPlanar) <= 0.03f)
            angryStuckTimer += Time.deltaTime;
        else
            angryStuckTimer = 0f;

        lastAngryPosition = transform.position;

        if (angryStuckTimer < angryStuckExitSeconds)
            return;

        state = PartyState.LeavingAfterRush;
        angryStuckTimer = 0f;
    }

    // ── State: LeavingAfterRush ───────────────────────────────────────────────

    private void UpdateLeavingAfterRush()
    {
        if (exitPoint == null)
        {
            // No exit point — just destroy
            if (manager != null)
                manager.DespawnPartyCustomer(this);
            return;
        }

        MoveWithWallCheck(exitPoint.position, moveSpeed, isAngryCustomer: false);

        Vector3 currentPlanar = new Vector3(transform.position.x, 0f, transform.position.z);
        Vector3 exitPlanar    = new Vector3(exitPoint.position.x,  0f, exitPoint.position.z);

        if (Vector3.Distance(currentPlanar, exitPlanar) <= arrivalDistance)
        {
            if (manager != null)
                manager.DespawnPartyCustomer(this);
        }
    }

    // ── Movement with wall correction ────────────────────────────────────────

    private void MoveWithWallCheck(Vector3 target, float speed, bool isAngryCustomer)
    {
        Vector3 prevPos = transform.position;
        CustomerCrowdUtility.MoveTowardsWithCrowdContact(
            transform, crowdCollider, target, speed, crowdContact, isAngryCustomer);
        ApplyWallCorrection(prevPos, target);
    }

    private static float GetPlanarDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    private void ApplyWallCorrection(Vector3 prevPos, Vector3 target)
    {
        CustomerCrowdUtility.ApplyObstacleCorrection(transform, crowdCollider, prevPos, target, wallLayerMask);
    }

    private void ReleaseReservation(Chair chair)
    {
        if (chair == null) return;
        chair.ReleaseReservation(this);
    }

    public void CleanupSeats(bool spawnDirt)
    {
        if (reservedChair != null)
        {
            ReleaseReservation(reservedChair);
            reservedChair = null;
            ResetSeatRoute();
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
