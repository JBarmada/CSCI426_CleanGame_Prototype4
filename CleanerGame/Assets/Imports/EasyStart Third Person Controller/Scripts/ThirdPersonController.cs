
using UnityEditor.VersionControl;
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

    [Tooltip("Speed ​​at which the character moves. It is not affected by gravity or jumping.")]
    public float velocity = 5f;
    [Tooltip("This value is added to the speed value while the character is sprinting.")]
    public float sprintAdittion = 3.5f;
    [Space]
    [Tooltip("Force that pulls the player down. Changing this value causes all movement, jumping and falling to be changed as well.")]
    public float gravity = 9.8f;

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


    void Start()
    {
        cc = GetComponent<CharacterController>();
        animator = GetComponent<Animator>();

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
            isSprinting = cc.velocity.magnitude > minimumSpeed && inputSprint;
            animator.SetBool("sprint", isSprinting );

        }

        UpdateSweepAudio();
        UpdateSweepEffects();

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
        if (broom == null)
            return;

        Vector3 targetPos = isSweeping ? broomSweepLocalOffset : broomDefaultLocalPos;
        broom.localPosition = Vector3.Lerp(broom.localPosition, targetPos, Time.deltaTime * broomMoveSpeed);

        Vector3 sweepAxis = broomSweepAxis.sqrMagnitude > 0.0001f ? broomSweepAxis.normalized : Vector3.up;
        Quaternion baseRot = isSweeping ? Quaternion.Euler(broomSweepLocalEuler) : broomDefaultLocalRot;
        Quaternion sweepRot = isSweeping
            ? Quaternion.AngleAxis(Mathf.Sin(Time.time * broomSweepSpeed) * broomSweepAngle, sweepAxis)
            : Quaternion.identity;
        Quaternion targetRot = baseRot * sweepRot;
        broom.localRotation = Quaternion.Slerp(broom.localRotation, targetRot, Time.deltaTime * broomMoveSpeed);
    }


    // With the inputs and animations defined, FixedUpdate is responsible for applying movements and actions to the player
    private void FixedUpdate()
    {

        // Sprinting velocity boost or crounching desacelerate
        float velocityAdittion = 0;
        if ( isSprinting )
            velocityAdittion = sprintAdittion;
        if (isCrouching)
            velocityAdittion =  - (velocity * 0.50f); // -50% velocity

        // Direction movement
        float directionX = inputHorizontal * (velocity + velocityAdittion) * Time.deltaTime;
        float directionZ = inputVertical * (velocity + velocityAdittion) * Time.deltaTime;
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
            Quaternion rotation = Quaternion.Euler(0, angle, 0);
            transform.rotation = Quaternion.Slerp(transform.rotation, rotation, 0.15f);
        }

        // --- End rotation ---

        
        Vector3 verticalDirection = Vector3.up * directionY;
        Vector3 horizontalDirection = forward + right;

        Vector3 moviment = verticalDirection + horizontalDirection;
        cc.Move( moviment );

    }


}
