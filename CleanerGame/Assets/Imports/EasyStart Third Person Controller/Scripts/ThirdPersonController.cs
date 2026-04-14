
using UnityEngine;

/*
    This file has a commented version with details about how each line works. 
    The commented version contains code that is easier and simpler to read. This file is minified.
*/


/// <summary>
/// Main script for third-person movement of the character in the game.
/// Make sure that the object that will receive this script (the player) 
/// has the Player tag and the Character Controller component.
/// </summary>
public class ThirdPersonController : MonoBehaviour
{
    public static ThirdPersonController Instance { get; private set; }

    [Tooltip("Speed ​​at which the character moves. It is not affected by gravity or jumping.")]
    public float velocity = 5f;
    [Tooltip("This value is added to the speed value while the character is sprinting.")]
    public float sprintAdittion = 3.5f;
    [Space]
    [Tooltip("Force that pulls the player down. Changing this value causes all movement, jumping and falling to be changed as well.")]
    public float gravity = 9.8f;

    [Header("Sweep Movement")]
    [Range(0.1f, 1f)]
    public float sweepMoveMultiplier = 0.55f;
    public bool disableSprintWhileSweeping = true;

    [Header("Slip")]
    public float spillCheckRadius = 0.65f;
    public float slipLaunchSpeed = 6f;
    public float slipDuration = 0.55f;
    public float slipControlMultiplier = 0.15f;
    public float slipCooldownSeconds = 0.45f;
    public float slipInvulnerabilitySeconds = 0.8f;
    public float slipTumbleAngle = 22f;
    public float slipTumblePitch = 10f;
    public AudioClip slipClip;
    [Range(0f, 1f)]
    public float slipVolume = 0.65f;

    [Header("Bonk Feedback")]
    public AudioClip bonkClip;
    public AudioClip bonkImpactClip;
    [Range(0f, 1f)]
    public float bonkVolume = 0.8f;
    [Range(0f, 1f)]
    public float bonkImpactVolume = 0.95f;
    public float bonkShakeDuration = 0.14f;
    public float bonkShakeMagnitude = 0.12f;
    public float bonkImpactShakeDuration = 0.18f;
    public float bonkImpactShakeMagnitude = 0.22f;
    public float bonkTumbleAngle = 26f;
    public float bonkTumblePitch = 14f;
    public float bonkTumbleSpeed = 14f;
    public float bonkRecoverySpeed = 8f;
    public float bonkKnockbackDuration = 0.3f;
    public float bonkInvulnerabilitySeconds = 0.7f;
    public float bonkLaunchSpeed = 9f;
    public float bonkMomentumDecay = 1.35f;
    public float bonkSpinSpeed = 720f;

    // Player states
    bool isSprinting = false;
    bool isCrouching = false;

    // Inputs
    float inputHorizontal;
    float inputVertical;
    bool inputCrouch;
    bool inputSprint;

    Animator animator;
    CharacterController cc;

    [Header("Broom Sweep")]
    public Transform broom;
    public Vector3 broomSweepLocalOffset = new Vector3(0f, 0f, 0.6f);
    public Vector3 broomSweepLocalEuler = Vector3.zero;
    public Vector3 broomSweepAxis = Vector3.up;
    public float broomMoveSpeed = 10f;
    public float broomSweepAngle = 30f;
    public float broomSweepSpeed = 6f;
    public AudioSource broomSweepAudioSource;
    public AudioClip broomSweepClip;
    [Range(0f, 1f)]
    public float broomSweepVolume = 1f;
    public GameObject broomSweepParticlePrefab;
    public GameObject broomSweepLightPrefab;
    public Vector3 broomTipLocalOffset = new Vector3(0f, 0f, 0.6f);
    public float broomSweepBurstInterval = 1f;
    public float broomSweepBurstLifetime = 1.2f;

    bool isSweeping = false;
    bool wasSweeping = false;
    float broomSweepBurstTimer = 0f;
    Vector3 broomDefaultLocalPos;
    Quaternion broomDefaultLocalRot;
    Vector3 desiredPlanarMoveDirection = Vector3.zero;
    Vector3 pendingExternalDisplacement = Vector3.zero;
    Vector3 bonkVelocity = Vector3.zero;
    float cameraShakeTimer = 0f;
    float cameraShakeDurationActive = 0f;
    float cameraShakeMagnitudeActive = 0f;
    float slipTimer = 0f;
    float slipCooldownTimer = 0f;
    float reactionInvulnerabilityTimer = 0f;
    Vector3 slipVelocity = Vector3.zero;
    float bonkTimer = 0f;
    float bonkRoll = 0f;
    float bonkPitch = 0f;
    float bonkImpactCooldown = 0f;
    bool bonkCrashPlayed = false;
    Vector3 lastCameraShakeOffset = Vector3.zero;
    float facingYaw;

    public bool IsSweeping => isSweeping;
    public bool HasMovementInput => new Vector2(inputHorizontal, inputVertical).sqrMagnitude > 0.001f;
    public Vector3 DesiredPlanarMoveDirection => desiredPlanarMoveDirection;
    public Vector3 PlanarPosition => new Vector3(transform.position.x, 0f, transform.position.z);
    public float CharacterRadius => cc != null ? cc.radius : 0.5f;
    public bool IsSlipping => slipTimer > 0f;
    public bool IsReactionLocked => reactionInvulnerabilityTimer > 0f;


    void Start()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        cc = GetComponent<CharacterController>();
        animator = GetComponent<Animator>();
        facingYaw = transform.eulerAngles.y;

        if (broom != null)
        {
            broomDefaultLocalPos = broom.localPosition;
            broomDefaultLocalRot = broom.localRotation;
        }

        if (broomSweepAudioSource == null)
            broomSweepAudioSource = GetComponent<AudioSource>();

        if (broomSweepAudioSource != null && broomSweepClip != null)
        {
            broomSweepAudioSource.clip = broomSweepClip;
            broomSweepAudioSource.loop = true;
            broomSweepAudioSource.playOnAwake = false;
            broomSweepAudioSource.volume = broomSweepVolume;
        }

        // Message informing the user that they forgot to add an animator
        if (animator == null)
            Debug.LogWarning("Hey buddy, you don't have the Animator component in your player. Without it, the animations won't work.");
    }


    // Update is only being used here to identify keys and trigger animations
    void Update()
    {

        // Input checkers
        inputHorizontal = Input.GetAxis("Horizontal");
        inputVertical = Input.GetAxis("Vertical");
        inputSprint = Input.GetAxis("Fire3") == 1f;
        // Unfortunately GetAxis does not work with GetKeyDown, so inputs must be taken individually
        inputCrouch = Input.GetKeyDown(KeyCode.LeftControl) || Input.GetKeyDown(KeyCode.JoystickButton1);
        isSweeping = Input.GetKey(KeyCode.Space);
        UpdateDesiredPlanarMoveDirection();

        // Check if you pressed the crouch input key and change the player's state
        if ( inputCrouch )
            isCrouching = !isCrouching;

        // Run and Crouch animation
        // If dont have animator component, this block wont run
        if ( cc.isGrounded && animator != null )
        {

            // Crouch
            // Note: The crouch animation does not shrink the character's collider
            animator.SetBool("crouch", isCrouching);
            
            // Run
            float minimumSpeed = 0.9f;
            animator.SetBool("run", cc.velocity.magnitude > minimumSpeed );

            // Sprint
            bool sprintAllowed = inputSprint && (!disableSprintWhileSweeping || !isSweeping);
            isSprinting = cc.velocity.magnitude > minimumSpeed && sprintAllowed;
            animator.SetBool("sprint", isSprinting );

        }

        UpdateSweepAudio();
        UpdateSweepEffects();
        UpdateSlipAndBonkTimers();

    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    void UpdateSweepAudio()
    {
        if (broomSweepAudioSource == null || broomSweepClip == null)
        {
            wasSweeping = isSweeping;
            return;
        }

        if (isSweeping && !wasSweeping)
            broomSweepAudioSource.Play();
        else if (!isSweeping && wasSweeping)
            broomSweepAudioSource.Stop();

        if (isSweeping)
            broomSweepAudioSource.volume = broomSweepVolume;

        wasSweeping = isSweeping;
    }

    void UpdateSweepEffects()
    {
        if (broom == null)
            return;

        if (!isSweeping)
        {
            broomSweepBurstTimer = 0f;
            return;
        }

        broomSweepBurstTimer += Time.deltaTime;
        if (broomSweepBurstTimer < broomSweepBurstInterval)
            return;

        broomSweepBurstTimer = 0f;
        Vector3 spawnPos = broom.TransformPoint(broomTipLocalOffset);
        Quaternion spawnRot = broom.rotation;

        if (broomSweepParticlePrefab != null)
        {
            GameObject fx = Instantiate(broomSweepParticlePrefab, spawnPos, spawnRot);
            if (broomSweepBurstLifetime > 0f)
                Destroy(fx, broomSweepBurstLifetime);
        }

        if (broomSweepLightPrefab != null)
        {
            GameObject lightFx = Instantiate(broomSweepLightPrefab, spawnPos, spawnRot);
            if (broomSweepBurstLifetime > 0f)
                Destroy(lightFx, broomSweepBurstLifetime);
        }
    }

    private void LateUpdate()
    {
        ApplyCameraShake();

        if (broom == null)
        {
            ApplyBonkVisualRoll();
            return;
        }

        Vector3 targetPos = isSweeping ? broomSweepLocalOffset : broomDefaultLocalPos;
        broom.localPosition = Vector3.Lerp(broom.localPosition, targetPos, Time.deltaTime * broomMoveSpeed);

        Vector3 sweepAxis = broomSweepAxis.sqrMagnitude > 0.0001f ? broomSweepAxis.normalized : Vector3.up;
        Quaternion baseRot = isSweeping ? Quaternion.Euler(broomSweepLocalEuler) : broomDefaultLocalRot;
        Quaternion sweepRot = isSweeping
            ? Quaternion.AngleAxis(Mathf.Sin(Time.time * broomSweepSpeed) * broomSweepAngle, sweepAxis)
            : Quaternion.identity;
        Quaternion targetRot = baseRot * sweepRot;
        broom.localRotation = Quaternion.Slerp(broom.localRotation, targetRot, Time.deltaTime * broomMoveSpeed);
        ApplyBonkVisualRoll();
    }


    // With the inputs and animations defined, FixedUpdate is responsible for applying movements and actions to the player
    private void FixedUpdate()
    {
        // Sprinting velocity boost or crounching desacelerate
        float velocityAdittion = 0;
        bool sprintAllowed = inputSprint && (!disableSprintWhileSweeping || !isSweeping);
        if ( isSprinting && sprintAllowed )
            velocityAdittion = sprintAdittion;
        if (isCrouching)
            velocityAdittion =  - (velocity * 0.50f); // -50% velocity

        float sweepMultiplier = isSweeping ? Mathf.Clamp(sweepMoveMultiplier, 0.1f, 1f) : 1f;
        float slipControl = IsSlipping ? Mathf.Clamp01(slipControlMultiplier) : 1f;
        float sneakerMult = SneakerPowerupSystem.Instance != null ? SneakerPowerupSystem.Instance.CurrentMultiplier : 1f;

        // Direction movement
        float directionX = inputHorizontal * (velocity + velocityAdittion) * sweepMultiplier * slipControl * sneakerMult * Time.deltaTime;
        float directionZ = inputVertical * (velocity + velocityAdittion) * sweepMultiplier * slipControl * sneakerMult * Time.deltaTime;
        float directionY = 0;

        // Add gravity to Y axis
        directionY = directionY - gravity * Time.deltaTime;

        
        // --- Character rotation --- 

        Vector3 forward = Camera.main.transform.forward;
        Vector3 right = Camera.main.transform.right;

        forward.y = 0;
        right.y = 0;

        forward.Normalize();
        right.Normalize();

        // Relate the front with the Z direction (depth) and right with X (lateral movement)
        forward = forward * directionZ;
        right = right * directionX;

        if (directionX != 0 || directionZ != 0)
        {
            float angle = Mathf.Atan2(forward.x + right.x, forward.z + right.z) * Mathf.Rad2Deg;
            facingYaw = Mathf.LerpAngle(facingYaw, angle, 0.15f);
        }

        // --- End rotation ---

        
        Vector3 verticalDirection = Vector3.up * directionY;
        Vector3 horizontalDirection = forward + right;

        TryTriggerSlip();

        Vector3 movement = verticalDirection + horizontalDirection + pendingExternalDisplacement;
        if (IsSlipping)
            movement += slipVelocity * Time.deltaTime;
        if (bonkTimer > 0f)
            movement += bonkVelocity * Time.deltaTime;

        pendingExternalDisplacement = Vector3.zero;
        cc.Move( movement );

    }

    void UpdateDesiredPlanarMoveDirection()
    {
        Camera activeCamera = Camera.main;
        if (activeCamera == null)
        {
            desiredPlanarMoveDirection = new Vector3(inputHorizontal, 0f, inputVertical).normalized;
            return;
        }

        Vector3 forward = activeCamera.transform.forward;
        Vector3 right = activeCamera.transform.right;
        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();

        Vector3 desiredMove = (right * inputHorizontal) + (forward * inputVertical);
        desiredPlanarMoveDirection = desiredMove.sqrMagnitude > 0.001f ? desiredMove.normalized : Vector3.zero;
    }

    public void ApplyExternalDisplacement(Vector3 displacement)
    {
        if (displacement.sqrMagnitude <= 0f)
            return;

        pendingExternalDisplacement += displacement;
    }

    public void ApplyBonk(Vector3 direction, float strength)
    {
        if (direction.sqrMagnitude <= 0.0001f || strength <= 0f || IsReactionLocked)
            return;

        Vector3 planarDirection = new Vector3(direction.x, 0f, direction.z).normalized;
        if (planarDirection.sqrMagnitude <= 0.0001f)
            planarDirection = -transform.forward;

        reactionInvulnerabilityTimer = Mathf.Max(0.05f, bonkInvulnerabilitySeconds);
        bonkVelocity = planarDirection * Mathf.Max(4f, bonkLaunchSpeed * Mathf.Max(0.75f, strength));
        bonkTimer = Mathf.Max(0.15f, bonkKnockbackDuration);
        bonkRoll = Mathf.Sign(Vector3.Dot(planarDirection, transform.right)) * bonkTumbleAngle;
        bonkPitch = bonkTumblePitch;
        bonkCrashPlayed = false;
        pendingExternalDisplacement += planarDirection * 0.45f;
        TriggerCameraShake(bonkShakeDuration, bonkShakeMagnitude);
        PlayImpactClip(bonkClip, bonkVolume);
    }

    void UpdateSlipAndBonkTimers()
    {
        if (slipCooldownTimer > 0f)
            slipCooldownTimer -= Time.deltaTime;

        if (reactionInvulnerabilityTimer > 0f)
            reactionInvulnerabilityTimer -= Time.deltaTime;

        if (slipTimer > 0f)
        {
            slipTimer -= Time.deltaTime;
            slipVelocity = Vector3.Lerp(slipVelocity, Vector3.zero, Time.deltaTime * 1.6f);
            if (slipTimer <= 0f)
                slipVelocity = Vector3.zero;
        }

        if (bonkTimer > 0f)
        {
            bonkTimer -= Time.deltaTime;
            bonkVelocity = Vector3.Lerp(bonkVelocity, Vector3.zero, Time.deltaTime * Mathf.Max(0.01f, bonkMomentumDecay));
            if (bonkTimer <= 0f)
                bonkVelocity = Vector3.zero;
        }

        if (bonkTimer > 0f && bonkVelocity.sqrMagnitude > 0.01f)
        {
            float spinDirection = Mathf.Sign(Vector3.Dot(bonkVelocity.normalized, transform.right));
            if (Mathf.Abs(spinDirection) < 0.1f)
                spinDirection = 1f;

            bonkRoll += spinDirection * bonkSpinSpeed * Time.deltaTime;
            bonkPitch = Mathf.Lerp(bonkPitch, bonkTumblePitch, Time.deltaTime * bonkTumbleSpeed);
        }
        else
        {
            bonkRoll = Mathf.Lerp(bonkRoll, 0f, Time.deltaTime * bonkRecoverySpeed);
            bonkPitch = Mathf.Lerp(bonkPitch, 0f, Time.deltaTime * bonkRecoverySpeed);
        }

        if (bonkImpactCooldown > 0f)
            bonkImpactCooldown -= Time.deltaTime;
    }

    void TryTriggerSlip()
    {
        if (slipCooldownTimer > 0f || !HasMovementInput || IsSlipping || IsReactionLocked)
            return;

        Vector3 checkPosition = transform.position + Vector3.down * 0.45f;
        Collider[] hits = Physics.OverlapSphere(checkPosition, spillCheckRadius, ~0, QueryTriggerInteraction.Collide);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider hit = hits[i];
            if (hit == null || !hit.CompareTag("Spill"))
                continue;

            slipTimer = Mathf.Max(0.1f, slipDuration);
            slipCooldownTimer = Mathf.Max(0.05f, slipCooldownSeconds);
            reactionInvulnerabilityTimer = Mathf.Max(reactionInvulnerabilityTimer, slipInvulnerabilitySeconds);
            Vector3 slipDir = DesiredPlanarMoveDirection.sqrMagnitude > 0.001f ? DesiredPlanarMoveDirection : transform.forward;
            slipVelocity = slipDir.normalized * Mathf.Max(1f, slipLaunchSpeed);
            pendingExternalDisplacement += slipDir.normalized * 0.1f;
            bonkRoll = Mathf.Sign(Vector3.Dot(slipDir.normalized, transform.right)) * slipTumbleAngle;
            bonkPitch = slipTumblePitch;
            TriggerCameraShake(0.08f, 0.08f);
            PlayImpactClip(slipClip != null ? slipClip : bonkClip, slipVolume);
            break;
        }
    }

    void TriggerCameraShake(float duration, float magnitude)
    {
        cameraShakeDurationActive = Mathf.Max(duration, 0.01f);
        cameraShakeTimer = cameraShakeDurationActive;
        cameraShakeMagnitudeActive = Mathf.Max(cameraShakeMagnitudeActive, magnitude);
    }

    void ApplyCameraShake()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
            return;

        mainCamera.transform.position -= lastCameraShakeOffset;
        lastCameraShakeOffset = Vector3.zero;

        if (cameraShakeTimer <= 0f)
            return;

        cameraShakeTimer -= Time.deltaTime;
        float damper = cameraShakeDurationActive <= 0.001f ? 0f : Mathf.Clamp01(cameraShakeTimer / cameraShakeDurationActive);
        Vector2 shake2D = Random.insideUnitCircle * (cameraShakeMagnitudeActive * damper);
        lastCameraShakeOffset = new Vector3(shake2D.x, shake2D.y, 0f);
        mainCamera.transform.position += lastCameraShakeOffset;

        if (cameraShakeTimer <= 0f)
            cameraShakeMagnitudeActive = 0f;
    }

    void ApplyBonkVisualRoll()
    {
        transform.rotation = Quaternion.Euler(bonkPitch, facingYaw, bonkRoll);
    }

    void PlayImpactClip(AudioClip clip, float volume)
    {
        if (clip == null || broomSweepAudioSource == null)
            return;

        broomSweepAudioSource.PlayOneShot(clip, volume);
    }

    void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (bonkTimer <= 0f || bonkImpactCooldown > 0f || bonkCrashPlayed || hit.collider == null || hit.collider.isTrigger)
            return;

        if (hit.collider.CompareTag("Customer") || hit.collider.CompareTag("Player") || hit.collider.CompareTag("Spill"))
            return;

        Vector3 planarBonkVelocity = new Vector3(bonkVelocity.x, 0f, bonkVelocity.z);
        Vector3 planarNormal = new Vector3(hit.normal.x, 0f, hit.normal.z).normalized;
        if (planarBonkVelocity.sqrMagnitude <= 0.01f || planarNormal.sqrMagnitude <= 0.01f)
            return;

        float stopDot = Vector3.Dot(planarBonkVelocity.normalized, -planarNormal);
        if (stopDot < 0.55f)
            return;

        bonkImpactCooldown = 0.12f;
        bonkCrashPlayed = true;
        bonkVelocity = Vector3.zero;
        bonkTimer = 0f;
        bonkRoll *= 0.35f;
        bonkPitch *= 0.35f;
        TriggerCameraShake(bonkImpactShakeDuration, bonkImpactShakeMagnitude);
        PlayImpactClip(bonkImpactClip != null ? bonkImpactClip : bonkClip, bonkImpactVolume);
    }


}
