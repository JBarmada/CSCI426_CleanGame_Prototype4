using UnityEngine;
using UnityEngine.Serialization;

public class CustomerPartyAI : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 2.2f;
    [SerializeField] private float arrivalDistance = 0.2f;
    [SerializeField] private CrowdContactSettings crowdContact = new CrowdContactSettings();

    [Header("Shuffle Timing")]
    [SerializeField] private Vector2 shuffleSecondsRange = new Vector2(3f, 8f);

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
    [SerializeField] private Color angryTintColor = new Color(1f, 0.35f, 0.35f, 1f);

    [Header("Wall Collision")]
    [Tooltip("Set this to the layer(s) your wall colliders live on.")]
    [SerializeField] private LayerMask wallLayerMask;

    private CustomerManager manager;
    private Chair assignedChair;
    private Chair reservedChair;
    private float shuffleTimer;
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

        reservedChair = chair;
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
        MoveWithWallCheck(target, moveSpeed, isAngryCustomer: false);

        if (Vector3.Distance(transform.position, target) <= arrivalDistance)
        {
            if (reservedChair.TrySit(this))
            {
                assignedChair = reservedChair;
                reservedChair = null;
                shuffleTimer = Random.Range(shuffleSecondsRange.x, shuffleSecondsRange.y);
                state = PartyState.Sitting;
                if (emotions != null)
                    emotions.BeginSitting();
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
            bool didSpill = false;
            if (manager != null)
                didSpill = manager.OnCustomerLeftChair(assignedChair.transform.position);

            if (didSpill && emotions != null)
                emotions.BeginLeaving();

            assignedChair.CustomerLeft();
            assignedChair = null;
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

        // Move to just before the wall
        float   safeDistance = Mathf.Max(0f, hit.distance - 0.05f);
        Vector3 safePos      = prevPos + movement.normalized * safeDistance;

        // Slide the remaining movement along the wall face (XZ plane)
        float remaining = moveDist - safeDistance;
        if (remaining > 0.001f)
        {
            Vector3 wallNormal = new Vector3(hit.normal.x, 0f, hit.normal.z).normalized;
            Vector3 slideDir   = Vector3.ProjectOnPlane(movement.normalized, wallNormal);
            if (slideDir.sqrMagnitude > 0.001f)
            {
                slideDir.Normalize();
                // Second cast along the slide to prevent corner-locking
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

        // Could not slide (corner) — just park at the wall
        transform.position = safePos;
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
