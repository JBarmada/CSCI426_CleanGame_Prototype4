using UnityEngine;
using UnityEngine.Serialization;

public class Customer : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 2.2f;
    [SerializeField] private float sitDurationSeconds = 8f;
    [SerializeField] private float arrivalDistance = 0.2f;
    [SerializeField] private CrowdContactSettings crowdContact = new CrowdContactSettings();
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

    [Header("Wall Collision")]
    [Tooltip("Layer(s) your wall colliders live on. Customers will slide along these instead of stopping dead.")]
    [SerializeField] private LayerMask wallLayerMask;

    // ── Private state ─────────────────────────────────────────────────────────

    private CustomerManager manager;
    private Chair           assignedChair;
    private Transform       exitPoint;
    private float           sitTimer;
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

        MoveTowards(assignedChair.GetSeatPosition());

        if (Vector3.Distance(transform.position, assignedChair.GetSeatPosition()) <= arrivalDistance)
        {
            if (assignedChair.TrySit(this))
            {
                sitTimer = sitDurationSeconds;
                state    = CustomerState.Sitting;
                if (emotions != null) emotions.BeginSitting();
            }
            else
            {
                assignedChair.ReleaseReservation(this);
                assignedChair = null;
                if (manager != null) manager.TryAssignChair(this);
            }
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

        state = CustomerState.AngryRush;
        SetAngryVisuals(true);   // snap to full red
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
        ApplyWallCorrection(prevPos);
    }

    private void ApplyWallCorrection(Vector3 prevPos)
    {
        if (wallLayerMask.value == 0) return;

        Vector3 newPos   = transform.position;
        Vector3 movement = newPos - prevPos;
        float   moveDist = movement.magnitude;
        if (moveDist < 0.001f) return;

        float   radius = crowdCollider != null ? crowdCollider.radius * 0.85f : 0.4f;
        Vector3 bottom = prevPos + Vector3.up * 0.1f;
        Vector3 top    = prevPos + Vector3.up * 1.5f;

        if (!Physics.CapsuleCast(bottom, top, radius, movement.normalized,
                out RaycastHit hit, moveDist, wallLayerMask, QueryTriggerInteraction.Ignore))
            return;

        float   safeDistance = Mathf.Max(0f, hit.distance - 0.05f);
        Vector3 safePos      = prevPos + movement.normalized * safeDistance;

        // Slide remaining movement along the wall surface (XZ plane only)
        float remaining = moveDist - safeDistance;
        if (remaining > 0.001f)
        {
            Vector3 wallNormal = new Vector3(hit.normal.x, 0f, hit.normal.z).normalized;
            Vector3 slideDir   = Vector3.ProjectOnPlane(movement.normalized, wallNormal);
            if (slideDir.sqrMagnitude > 0.001f)
            {
                slideDir.Normalize();
                Vector3 sb = safePos + Vector3.up * 0.1f;
                Vector3 st = safePos + Vector3.up * 1.5f;
                if (!Physics.CapsuleCast(sb, st, radius, slideDir,
                        remaining, wallLayerMask, QueryTriggerInteraction.Ignore))
                {
                    transform.position = safePos + slideDir * remaining;
                    return;
                }
            }
        }

        // Cornered — park at the wall
        transform.position = safePos;
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
}
