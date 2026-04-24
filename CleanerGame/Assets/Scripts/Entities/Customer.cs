using UnityEngine;
using UnityEngine.Serialization;

public class Customer : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 2.2f;
    [SerializeField] private float sitDurationSeconds = 8f;
    [SerializeField] private float arrivalDistance = 0.2f;
    [SerializeField] private CrowdContactSettings crowdContact = new CrowdContactSettings();

    [Header("Seat Arrival Fail-safe")]
    [Tooltip("If a customer is blocked this close to their reserved chair, complete the sit instead of vibrating against the table.")]
    [SerializeField] private float blockedSitDistance = 0.75f;
    [Tooltip("Seconds with almost no movement near the reserved chair before forcing the sit.")]
    [SerializeField] private float blockedSitSeconds = 0.35f;

    [SerializeField] private PartyCustomerEmotions emotions;
    [FormerlySerializedAs("customeremotions")]
    [SerializeField] private RegularCustomerEmotions regularCustomerEmotions;

    [Header("Angry Rush")]
    [Tooltip("Speed multiplier applied during the straight-line rush toward the player.")]
    [FormerlySerializedAs("bonkMoveSpeedMultiplier")]
    [SerializeField] private float angryRushMoveSpeedMultiplier = 2.4f;
    [Tooltip("Speed multiplier when walking to the exit after a rush.")]
    [FormerlySerializedAs("bonkLeaveSpeedMultiplier")]
    [SerializeField] private float angryExitSpeedMultiplier = 1.25f;
    [Tooltip("Tint color snapped to when the customer goes fully angry.")]
    [FormerlySerializedAs("bonkColorTint")]
    [SerializeField] private Color angryColorTint = new Color(1f, 0.35f, 0.35f, 1f);

    [Header("Rage Conversion")]
    [Tooltip("Chance (0 = never, 1 = always) that this customer can turn angry when seated near spills.")]
    [Range(0f, 1f)]
    [SerializeField] private float canTurnAngryChance = 0.4f;
    [Tooltip("Radius in world units to scan for nearby spills while sitting.")]
    [SerializeField] private float angryCheckRadius = 2.5f;
    [Tooltip("Anger progress gained per second for each spill within range.")]
    [SerializeField] private float angryRatePerSpill = 0.15f;
    [Tooltip("Anger progress lost per second when no spills are nearby.")]
    [SerializeField] private float angryDecayRate = 0.015f;
    [Tooltip("How far ahead of the customer the straight-line rush target is placed.")]
    [SerializeField] private float angryRushDistance = 28f;
    [Tooltip("Distance within which the customer counts as having bonked the player and starts leaving.")]
    [SerializeField] private float angryBonkDetectionRadius = 1.5f;
    [Tooltip("If an angry customer makes almost no progress for this long, they stop rushing and path toward the exit.")]
    [SerializeField] private float angryStuckExitSeconds = 0.8f;

    [Header("Obstacle Collision")]
    [Tooltip("Layer(s) your wall/table/chair colliders live on. Customers will slide along these instead of passing through.")]
    [SerializeField] private LayerMask wallLayerMask;

    // ── Private state ─────────────────────────────────────────────────────────

    private CustomerManager manager;
    private Chair           assignedChair;
    private Transform       exitPoint;
    private float           sitTimer;
    private Vector3         lastWalkPosition;
    private float           blockedSitTimer;
    private bool            reachedSeatAisle;
    private bool            reachedSeatApproach;
    private SphereCollider  crowdCollider;
    private bool            isAngryCustomer;   // true once the angry rush starts
    private bool            angryLeaving;      // true after rush — uses leave-speed to exit

    private Renderer[] cachedRenderers;
    private Color[]    originalTints;         // per-renderer starting colors; lerp baseline

    // Rage conversion
    private bool    canTurnAngry;
    private float   angryProgress;            // 0 → 1
    private float   angerCheckTimer;
    private int     cachedNearbySpills;
    private Vector3 angryRushTarget;
    private Vector3 lastAngryPosition;
    private float angryStuckTimer;

    private enum CustomerState { WalkingToSeat, Sitting, Leaving, AngryRush }
    private CustomerState state;

    // ── Public properties ─────────────────────────────────────────────────────

    public bool IsAngryCustomer      => isAngryCustomer;
    public bool CountsTowardCapacity => !isAngryCustomer;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (emotions == null)
            emotions = GetComponentInChildren<PartyCustomerEmotions>();
        if (regularCustomerEmotions == null)
            regularCustomerEmotions = GetComponentInChildren<RegularCustomerEmotions>();

        crowdCollider   = GetComponent<SphereCollider>();
        cachedRenderers = GetComponentsInChildren<Renderer>(true);

        // Cache each renderer's original material color so tint lerps start
        // from the real material color, not a hard-coded white.
        originalTints = new Color[cachedRenderers.Length];
        for (int i = 0; i < cachedRenderers.Length; i++)
        {
            Renderer r = cachedRenderers[i];
            if (r == null || r.sharedMaterial == null) { originalTints[i] = Color.white; continue; }
            if      (r.sharedMaterial.HasProperty("_BaseColor")) originalTints[i] = r.sharedMaterial.GetColor("_BaseColor");
            else if (r.sharedMaterial.HasProperty("_Color"))     originalTints[i] = r.sharedMaterial.GetColor("_Color");
            else                                                  originalTints[i] = Color.white;
        }

        // Roll whether this customer instance can go angry (uses the per-prefab chance slider)
        canTurnAngry = Random.value <= canTurnAngryChance;
    }

    private void Start()
    {
        if (regularCustomerEmotions != null)
            regularCustomerEmotions.SetWhistling();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void Initialize(CustomerManager owner)
    {
        manager = owner;
    }

    public void AssignChair(Chair chair, Transform exit)
    {
        if (isAngryCustomer) return;

        assignedChair = chair;
        exitPoint     = exit;
        ResetBlockedSitTracking();
        ResetSeatRoute();
        state         = CustomerState.WalkingToSeat;
    }

    // ── Update / state machine ────────────────────────────────────────────────

    private void Update()
    {
        switch (state)
        {
            case CustomerState.WalkingToSeat: UpdateWalkingToSeat(); break;
            case CustomerState.Sitting:       UpdateSitting();       break;
            case CustomerState.Leaving:       UpdateLeaving();       break;
            case CustomerState.AngryRush:     UpdateAngryRush();     break;
        }
    }

    // ── State: WalkingToSeat ──────────────────────────────────────────────────

    private void UpdateWalkingToSeat()
    {
        if (assignedChair == null)
        {
            if (manager != null) manager.TryAssignChair(this);
            return;
        }

        Vector3 seatPosition = assignedChair.GetSeatPosition();
        Vector3 target = GetSeatRouteTarget(assignedChair, seatPosition);
        MoveTowards(target);

        float distanceToSeat = GetPlanarDistance(transform.position, seatPosition);
        if (distanceToSeat <= arrivalDistance || ShouldForceSit(distanceToSeat))
        {
            CompleteSitAtAssignedChair(seatPosition);
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

    private void CompleteSitAtAssignedChair(Vector3 seatPosition)
    {
        if (assignedChair.TrySit(this))
        {
            Vector3 snappedPosition = transform.position;
            snappedPosition.x = seatPosition.x;
            snappedPosition.z = seatPosition.z;
            transform.position = snappedPosition;

            sitTimer = sitDurationSeconds;
            ResetSeatRoute();
            ResetBlockedSitTracking();
            state = CustomerState.Sitting;
            if (emotions != null) emotions.BeginSitting();
        }
        else
        {
            assignedChair.ReleaseReservation(this);
            assignedChair = null;
            ResetSeatRoute();
            ResetBlockedSitTracking();
            if (manager != null) manager.TryAssignChair(this);
        }
    }

    // ── State: Sitting ────────────────────────────────────────────────────────

    private void UpdateSitting()
    {
        // ── Rage conversion ──────────────────────────────────────────────────
        angerCheckTimer += Time.deltaTime;
        if (angerCheckTimer >= 0.25f)
        {
            angerCheckTimer    = 0f;
            cachedNearbySpills = CountNearbySpills();
        }

        if (canTurnAngry)
        {
            if (cachedNearbySpills > 0)
                angryProgress = Mathf.Min(1f, angryProgress + cachedNearbySpills * angryRatePerSpill * Time.deltaTime);
            else
                angryProgress = Mathf.Max(0f, angryProgress - angryDecayRate * Time.deltaTime);

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

        // ── Normal sit-timer countdown ───────────────────────────────────────
        sitTimer -= Time.deltaTime;
        if (emotions != null) emotions.UpdateSittingTimer(sitTimer);

        if (sitTimer <= 0f)
        {
            if (assignedChair != null)
            {
                bool didSpill = manager != null && manager.OnCustomerLeftChair(assignedChair.transform.position);

                if (didSpill && emotions != null)        emotions.BeginLeaving();
                if (didSpill && regularCustomerEmotions != null) regularCustomerEmotions.ShowSpillForThreeSeconds();

                assignedChair.CustomerLeft();
                assignedChair = null;
            }

            state = CustomerState.Leaving;
        }
    }

    // ── State: AngryRush ──────────────────────────────────────────────────────

    private void UpdateAngryRush()
    {
        MoveTowards(angryRushTarget);
        UpdateAngryStuckRecovery();

        if (ThirdPersonController.Instance != null)
        {
            float playerDist = Vector3.Distance(
                new Vector3(transform.position.x, 0f, transform.position.z),
                new Vector3(ThirdPersonController.Instance.transform.position.x, 0f,
                            ThirdPersonController.Instance.transform.position.z));

            if (playerDist <= angryBonkDetectionRadius)
            {
                // Deliver the bonk directly — the crowd utility's contact radius (~0.8 f)
                // is smaller than angryBonkDetectionRadius, so we apply the impulse here.
                Vector3 bonkDir = new Vector3(
                    ThirdPersonController.Instance.transform.position.x - transform.position.x, 0f,
                    ThirdPersonController.Instance.transform.position.z - transform.position.z);
                if (bonkDir.sqrMagnitude > 0.001f) bonkDir.Normalize();
                ThirdPersonController.Instance.ApplyBonk(bonkDir, moveSpeed * angryRushMoveSpeedMultiplier);

                angryLeaving = true;
                state        = CustomerState.Leaving;
                return;
            }
        }

        // Fallback: rush line exhausted without hitting player — leave anyway
        float dist = Vector3.Distance(
            new Vector3(transform.position.x, 0f, transform.position.z),
            new Vector3(angryRushTarget.x,    0f, angryRushTarget.z));

        if (dist <= arrivalDistance + 0.5f)
        {
            angryLeaving = true;
            state        = CustomerState.Leaving;
        }
    }

    // ── State: Leaving ────────────────────────────────────────────────────────

    private void UpdateLeaving()
    {
        if (exitPoint == null)
        {
            ReleaseReservationIfNeeded();
            if (manager != null) manager.DespawnCustomer(this);
            return;
        }

        MoveTowards(exitPoint.position);

        Vector3 currentPlanar = new Vector3(transform.position.x, 0f, transform.position.z);
        Vector3 exitPlanar    = new Vector3(exitPoint.position.x,  0f, exitPoint.position.z);
        if (Vector3.Distance(currentPlanar, exitPlanar) <= arrivalDistance)
        {
            ReleaseReservationIfNeeded();
            if (manager != null) manager.DespawnCustomer(this);
        }
    }

    // ── Rage helpers ──────────────────────────────────────────────────────────

    private int CountNearbySpills()
    {
        // Uses the static registry on SpillManager — no FindObjectsByType allocation
        var spills = SpillManager.ActiveSpills;
        int count  = 0;
        for (int i = 0; i < spills.Count; i++)
        {
            if (spills[i] == null) continue;
            if (Vector3.Distance(transform.position, spills[i].transform.position) <= angryCheckRadius)
                count++;
        }
        return count;
    }

    private void TriggerAngryRush()
    {
        // Release chair without spawning a spill
        if (assignedChair != null)
        {
            assignedChair.CustomerLeft();
            assignedChair = null;
        }

        isAngryCustomer = true;
        if (exitPoint == null && manager != null)
            exitPoint = manager.ExitPoint;

        Vector3 playerPos = ThirdPersonController.Instance != null
            ? ThirdPersonController.Instance.transform.position
            : transform.position + transform.forward;

        Vector3 dir = playerPos - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.01f) dir = transform.forward;
        dir.Normalize();

        angryRushTarget   = transform.position + dir * angryRushDistance;
        angryRushTarget.y = transform.position.y;
        lastAngryPosition = transform.position;
        angryStuckTimer   = 0f;

        state = CustomerState.AngryRush;
        SetAngryVisuals(true);   // snap to full red
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

        angryLeaving = true;
        state = CustomerState.Leaving;
        angryStuckTimer = 0f;
    }

    // ── Visuals ───────────────────────────────────────────────────────────────

    /// <summary>Lerps the customer's tint from its original color toward the angry tint.</summary>
    private void SetTintGradient(float t)
    {
        if (cachedRenderers == null) return;
        var block = new MaterialPropertyBlock();
        for (int i = 0; i < cachedRenderers.Length; i++)
        {
            Renderer r = cachedRenderers[i];
            if (r == null) continue;
            Color origin = (originalTints != null && i < originalTints.Length) ? originalTints[i] : Color.white;
            Color tint   = Color.Lerp(origin, angryColorTint, t);
            r.GetPropertyBlock(block);
            if (r.sharedMaterial != null && r.sharedMaterial.HasProperty("_BaseColor"))
                block.SetColor("_BaseColor", tint);
            else
                block.SetColor("_Color", tint);
            r.SetPropertyBlock(block);
        }
    }

    /// <summary>Snaps the customer to full angry red (true) or restores original colors (false).</summary>
    private void SetAngryVisuals(bool angry)
    {
        if (cachedRenderers == null) return;
        var block = new MaterialPropertyBlock();
        for (int i = 0; i < cachedRenderers.Length; i++)
        {
            Renderer r = cachedRenderers[i];
            if (r == null) continue;
            Color tint = angry
                ? angryColorTint
                : ((originalTints != null && i < originalTints.Length) ? originalTints[i] : Color.white);
            r.GetPropertyBlock(block);
            if (r.sharedMaterial != null && r.sharedMaterial.HasProperty("_BaseColor"))
                block.SetColor("_BaseColor", tint);
            else
                block.SetColor("_Color", tint);
            r.SetPropertyBlock(block);
        }
    }

    // ── Utilities ─────────────────────────────────────────────────────────────

    private void ReleaseReservationIfNeeded()
    {
        if (assignedChair == null) return;
        assignedChair.ReleaseReservation(this);
        assignedChair = null;
    }

    private void MoveTowards(Vector3 target)
    {
        float speed = isAngryCustomer ? moveSpeed * angryRushMoveSpeedMultiplier : moveSpeed;
        if (angryLeaving && state == CustomerState.Leaving && isAngryCustomer)
            speed = moveSpeed * angryExitSpeedMultiplier;

        Vector3 adjustedTarget = target;
        if (isAngryCustomer)
            adjustedTarget.y = transform.position.y;

        Vector3 prevPos = transform.position;
        CustomerCrowdUtility.MoveTowardsWithCrowdContact(
            transform, crowdCollider, adjustedTarget, speed, crowdContact, isAngryCustomer);
        ApplyWallCorrection(prevPos, adjustedTarget);
    }

    private static float GetPlanarDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
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

    private void ApplyWallCorrection(Vector3 prevPos, Vector3 target)
    {
        CustomerCrowdUtility.ApplyObstacleCorrection(transform, crowdCollider, prevPos, target, wallLayerMask);
    }
}

// ── Shared types (used by Customer and CustomerPartyAI) ───────────────────────

[System.Serializable]
public class CrowdContactSettings
{
    [Min(0f)]          public float contactPadding          = 0.1f;
    [Min(0.1f)]        public float customerHeaviness       = 1.5f;
    [Min(0f)]          public float playerPushStrength      = 1.2f;
    [Min(0f)]          public float playerPushWhilePushing  = 0.35f;
    [Min(0f)]          public float customerPushStrength    = 0.75f;
    [Range(0f, 1f)]    public float playerPushIntentThreshold = 0.25f;
}

public static class CustomerCrowdUtility
{
    private const float ObstacleSkin = 0.05f;
    private const float MinSideStepDistance = 0.12f;

    public static void MoveTowardsWithCrowdContact(
        Transform customerTransform,
        SphereCollider customerCollider,
        Vector3 target,
        float moveSpeed,
        CrowdContactSettings settings,
        bool isAngryCustomer = false)
    {
        if (customerTransform == null) return;

        float deltaTime = Time.deltaTime;
        if (deltaTime <= 0f) return;

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

        Vector3 planarCustomer   = new Vector3(desiredPosition.x, 0f, desiredPosition.z);
        Vector3 planarPlayer     = player.PlanarPosition;
        Vector3 playerToCustomer = planarCustomer - planarPlayer;
        float   distance         = playerToCustomer.magnitude;

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

        float overlap    = combinedRadius - distance;
        float heaviness  = Mathf.Max(0.1f, settings.customerHeaviness);
        bool  playerPushingIntoCustomer = player.HasMovementInput &&
            Vector3.Dot(player.DesiredPlanarMoveDirection, pushDirectionToCustomer)
                >= settings.playerPushIntentThreshold;

        Vector3 adjustedPosition = desiredPosition;

        if (playerPushingIntoCustomer)
        {
            float customerDisplacement = overlap * settings.customerPushStrength / heaviness;
            adjustedPosition += pushDirectionToCustomer * customerDisplacement;

            float playerRecoil = overlap * settings.playerPushWhilePushing * heaviness;
            if (playerRecoil > 0f)
            {
                if (isAngryCustomer)
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
                if (isAngryCustomer)
                    player.ApplyBonk(-pushDirectionToCustomer, playerPushback);
                else
                    player.ApplyExternalDisplacement(-pushDirectionToCustomer * playerPushback);
            }
        }

        customerTransform.position = adjustedPosition;
    }

    private static float GetCustomerRadius(Transform t, SphereCollider col)
    {
        if (col == null) return 0.5f;
        Vector3 s = t.lossyScale;
        return Mathf.Max(0.05f, col.radius * Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.z)));
    }

    private static Vector3 GetFallbackDirection(ThirdPersonController player)
    {
        return (player != null && player.HasMovementInput)
            ? player.DesiredPlanarMoveDirection
            : Vector3.forward;
    }

    public static void ApplyObstacleCorrection(
        Transform customerTransform,
        SphereCollider customerCollider,
        Vector3 prevPos,
        Vector3 target,
        LayerMask wallLayerMask)
    {
        if (customerTransform == null || wallLayerMask.value == 0) return;

        Vector3 newPos = customerTransform.position;
        Vector3 movement = newPos - prevPos;
        float moveDist = movement.magnitude;
        if (moveDist < 0.001f) return;

        float radius = customerCollider != null ? customerCollider.radius * 0.85f : 0.4f;
        Vector3 bottom = prevPos + Vector3.up * 0.1f;
        Vector3 top = prevPos + Vector3.up * 1.5f;

        if (!Physics.CapsuleCast(bottom, top, radius, movement.normalized,
                out RaycastHit hit, moveDist, wallLayerMask, QueryTriggerInteraction.Ignore))
            return;

        float safeDistance = Mathf.Max(0f, hit.distance - ObstacleSkin);
        Vector3 safePos = prevPos + movement.normalized * safeDistance;
        float remaining = Mathf.Max(MinSideStepDistance, moveDist - safeDistance);

        if (TrySetCorrectedPosition(customerTransform, customerCollider, safePos, GetSlideDirection(movement, hit.normal), remaining, wallLayerMask))
            return;

        Vector3 wallNormal = new Vector3(hit.normal.x, 0f, hit.normal.z).normalized;
        Vector3 targetDirection = target - safePos;
        targetDirection.y = 0f;
        if (targetDirection.sqrMagnitude < 0.001f)
            targetDirection = movement;
        targetDirection.y = 0f;
        targetDirection.Normalize();

        Vector3 tangentA = new Vector3(-wallNormal.z, 0f, wallNormal.x);
        Vector3 tangentB = -tangentA;
        Vector3 firstTangent = Vector3.Dot(tangentA, targetDirection) >= Vector3.Dot(tangentB, targetDirection)
            ? tangentA
            : tangentB;

        if (TrySetCorrectedPosition(customerTransform, customerCollider, safePos, firstTangent, remaining, wallLayerMask))
            return;

        if (TrySetCorrectedPosition(customerTransform, customerCollider, safePos, -firstTangent, remaining, wallLayerMask))
            return;

        customerTransform.position = safePos;
    }

    private static Vector3 GetSlideDirection(Vector3 movement, Vector3 hitNormal)
    {
        Vector3 wallNormal = new Vector3(hitNormal.x, 0f, hitNormal.z).normalized;
        if (wallNormal.sqrMagnitude < 0.001f)
            return Vector3.zero;

        Vector3 slideDir = Vector3.ProjectOnPlane(movement.normalized, wallNormal);
        slideDir.y = 0f;
        return slideDir.sqrMagnitude > 0.001f ? slideDir.normalized : Vector3.zero;
    }

    private static bool TrySetCorrectedPosition(
        Transform customerTransform,
        SphereCollider customerCollider,
        Vector3 origin,
        Vector3 direction,
        float distance,
        LayerMask wallLayerMask)
    {
        if (direction.sqrMagnitude < 0.001f || distance <= 0f)
            return false;

        direction.Normalize();
        float radius = customerCollider != null ? customerCollider.radius * 0.85f : 0.4f;
        Vector3 bottom = origin + Vector3.up * 0.1f;
        Vector3 top = origin + Vector3.up * 1.5f;

        if (Physics.CapsuleCast(bottom, top, radius, direction,
                distance, wallLayerMask, QueryTriggerInteraction.Ignore))
            return false;

        customerTransform.position = origin + direction * distance;
        return true;
    }
}
