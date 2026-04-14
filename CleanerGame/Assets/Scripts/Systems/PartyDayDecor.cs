using UnityEngine;

public class PartyDayDecor : MonoBehaviour
{
    [SerializeField] private RestaurantDayCycle dayCycle;
    [SerializeField] private GameObject[] decorObjects;
    [Header("Balloon Bounce")]
    [SerializeField] private bool autoAddBalloonBounce = true;

    private void Awake()
    {
        if (dayCycle == null)
            dayCycle = FindFirstObjectByType<RestaurantDayCycle>();

        if (autoAddBalloonBounce)
            EnsureBalloonBounceComponents();
    }

    private void OnEnable()
    {
        if (dayCycle != null)
            dayCycle.DayStarted += HandleDayStarted;

        ApplyDecor(dayCycle != null && dayCycle.DayCount == 2);
    }

    private void OnDisable()
    {
        if (dayCycle != null)
            dayCycle.DayStarted -= HandleDayStarted;
    }

    private void HandleDayStarted(int dayNumber)
    {
        ApplyDecor(dayNumber == 2);
    }

    private void ApplyDecor(bool enable)
    {
        if (decorObjects == null) return;
        for (int i = 0; i < decorObjects.Length; i++)
        {
            if (decorObjects[i] == null) continue;
            decorObjects[i].SetActive(enable);
        }
    }

    private void EnsureBalloonBounceComponents()
    {
        if (decorObjects == null) return;

        for (int i = 0; i < decorObjects.Length; i++)
        {
            GameObject root = decorObjects[i];
            if (root == null) continue;

            Transform[] children = root.GetComponentsInChildren<Transform>(true);
            for (int j = 0; j < children.Length; j++)
            {
                Transform candidate = children[j];
                if (candidate == null) continue;
                if (!candidate.name.Contains("Balloon")) continue;

                if (candidate.GetComponent<PartyBalloonBounce>() == null)
                    candidate.gameObject.AddComponent<PartyBalloonBounce>();
            }
        }
    }
}

public class PartyBalloonBounce : MonoBehaviour
{
    [SerializeField] private float bounceRadius = 1.2f;
    [SerializeField] private float bounceStrength = 20f;
    [SerializeField] private float returnStrength = 10f;
    [SerializeField] private float damping = 6f;
    [SerializeField] private float maxOffset = 0.35f;
    [SerializeField] private float wobbleAngle = 16f;

    private Vector3 baseLocalPosition;
    private Quaternion baseLocalRotation;
    private Vector3 offset;
    private Vector3 velocity;

    private void Awake()
    {
        baseLocalPosition = transform.localPosition;
        baseLocalRotation = transform.localRotation;
    }

    private void OnEnable()
    {
        offset = Vector3.zero;
        velocity = Vector3.zero;
        transform.localPosition = baseLocalPosition;
        transform.localRotation = baseLocalRotation;
    }

    private void Update()
    {
        ThirdPersonController player = ThirdPersonController.Instance;
        if (player != null)
            ApplyPlayerBump(player);

        float deltaTime = Time.deltaTime;
        Vector3 acceleration = (-offset * returnStrength) - (velocity * damping);
        velocity += acceleration * deltaTime;
        offset += velocity * deltaTime;
        offset = Vector3.ClampMagnitude(offset, maxOffset);

        transform.localPosition = baseLocalPosition + offset;

        Vector3 tiltAxis = Vector3.Cross(Vector3.up, offset);
        if (tiltAxis.sqrMagnitude > 0.0001f)
        {
            float tiltAmount = Mathf.Clamp(offset.magnitude / Mathf.Max(0.001f, maxOffset), 0f, 1f) * wobbleAngle;
            transform.localRotation = baseLocalRotation * Quaternion.AngleAxis(tiltAmount, tiltAxis.normalized);
        }
        else
        {
            transform.localRotation = baseLocalRotation;
        }
    }

    private void ApplyPlayerBump(ThirdPersonController player)
    {
        Vector3 worldToBalloon = transform.position - player.transform.position;
        float distance = worldToBalloon.magnitude;
        if (distance > bounceRadius || distance <= 0.001f)
            return;

        float falloff = 1f - Mathf.Clamp01(distance / bounceRadius);
        Vector3 direction = worldToBalloon.normalized;
        velocity += direction * (bounceStrength * falloff * Time.deltaTime);
    }
}
