using System.Collections;
using TMPro;
using UnityEngine;

public class BossFilthReaction : MonoBehaviour
{
    [Header("Patrol")]
    [SerializeField] private Transform pointA;
    [SerializeField] private Transform pointB;
    [SerializeField] private float moveSpeed = 1.5f;
    [SerializeField] private float turnSpeed = 6f;

    [Header("Cutscene")]
    [SerializeField] private RestaurantManager restaurantManager;
    [SerializeField] private TMP_Text yellText;
    [SerializeField] private string yellMessage = "HEY!";
    [SerializeField] private float yellSeconds = 1f;

    [Header("Camera")]
    [SerializeField] private Vector3 cameraOffset = new Vector3(0f, 2f, -3f);
    [SerializeField] private Vector3 lookAtOffset = new Vector3(0f, 1.5f, 0f);
    [SerializeField] private float zoomFov = 35f;
    [SerializeField] private float zoomInSeconds = 0.6f;
    [SerializeField] private float zoomOutSeconds = 0.6f;

    [Header("Camera Shake")]
    [SerializeField] private bool shakeOnYell = true;
    [SerializeField] private float shakePosition = 0.08f;
    [SerializeField] private float shakeRotation = 1.5f;

    [Header("Boss Turn")]
    [SerializeField] private float turnDegrees = 180f;
    [SerializeField] private float turnSeconds = 0.2f;

    private Transform currentTarget;
    private bool isCutscene;
    private Coroutine cutsceneRoutine;
    private Camera mainCam;
    private float previousTimeScale = 1f;

    private void Awake()
    {
        if (restaurantManager == null)
            restaurantManager = RestaurantManager.Instance;

        mainCam = Camera.main;

        if (yellText != null)
            yellText.gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        if (restaurantManager != null)
            restaurantManager.FilthyStrikeTriggered += HandleFilthyStrikeTriggered;
    }

    private void OnDisable()
    {
        if (restaurantManager != null)
            restaurantManager.FilthyStrikeTriggered -= HandleFilthyStrikeTriggered;
    }

    private void Start()
    {
        if (pointA != null && pointB != null)
            currentTarget = pointB;
    }

    private void Update()
    {
        if (isCutscene) return;
        Patrol();
    }

    private void Patrol()
    {
        if (pointA == null || pointB == null) return;
        if (currentTarget == null) currentTarget = pointB;

        transform.position = Vector3.MoveTowards(transform.position, currentTarget.position, moveSpeed * Time.deltaTime);

        Vector3 toTarget = currentTarget.position - transform.position;
        toTarget.y = 0f;
        if (toTarget.sqrMagnitude > 0.001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(toTarget, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, turnSpeed * Time.deltaTime);
        }

        if (Vector3.Distance(transform.position, currentTarget.position) <= 0.05f)
            currentTarget = currentTarget == pointA ? pointB : pointA;
    }

    private void HandleFilthyStrikeTriggered()
    {
        if (isCutscene) return;

        if (cutsceneRoutine != null)
            StopCoroutine(cutsceneRoutine);

        cutsceneRoutine = StartCoroutine(FilthyCutscene());
    }

    private IEnumerator FilthyCutscene()
    {
        isCutscene = true;
        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;

        if (mainCam == null)
            mainCam = Camera.main;

        if (mainCam == null)
        {
            FinishCutscene();
            yield break;
        }

        Vector3 camStartPos = mainCam.transform.position;
        Quaternion camStartRot = mainCam.transform.rotation;
        float camStartFov = mainCam.fieldOfView;

        Vector3 camTargetPos = transform.TransformPoint(cameraOffset);
        Vector3 lookTarget = transform.TransformPoint(lookAtOffset);
        Quaternion camTargetRot = Quaternion.LookRotation(lookTarget - camTargetPos, Vector3.up);

        Quaternion bossStartRot = transform.rotation;
        Quaternion bossTargetRot = bossStartRot * Quaternion.Euler(0f, turnDegrees, 0f);

        yield return LerpCameraAndBoss(camStartPos, camTargetPos, camStartRot, camTargetRot, camStartFov, zoomFov,
            bossStartRot, bossTargetRot, zoomInSeconds);

        if (yellText != null)
        {
            yellText.text = yellMessage;
            yellText.gameObject.SetActive(true);
        }

        if (yellSeconds > 0f)
        {
            if (shakeOnYell)
                yield return ShakeCamera(yellSeconds, camTargetPos, camTargetRot);
            else
                yield return new WaitForSecondsRealtime(yellSeconds);
        }

        if (yellText != null)
            yellText.gameObject.SetActive(false);

        yield return LerpCameraAndBoss(camTargetPos, camStartPos, camTargetRot, camStartRot, zoomFov, camStartFov,
            bossTargetRot, bossStartRot, zoomOutSeconds);

        FinishCutscene();
    }

    private IEnumerator LerpCameraAndBoss(
        Vector3 camFromPos,
        Vector3 camToPos,
        Quaternion camFromRot,
        Quaternion camToRot,
        float fovFrom,
        float fovTo,
        Quaternion bossFromRot,
        Quaternion bossToRot,
        float duration)
    {
        float t = 0f;
        float safeDuration = Mathf.Max(0.01f, duration);
        float bossTurnDuration = Mathf.Max(0.01f, turnSeconds);

        while (t < safeDuration)
        {
            t += Time.unscaledDeltaTime;
            float lerp = Mathf.Clamp01(t / safeDuration);

            mainCam.transform.position = Vector3.Lerp(camFromPos, camToPos, lerp);
            mainCam.transform.rotation = Quaternion.Slerp(camFromRot, camToRot, lerp);
            mainCam.fieldOfView = Mathf.Lerp(fovFrom, fovTo, lerp);

            float bossLerp = Mathf.Clamp01(t / bossTurnDuration);
            transform.rotation = Quaternion.Slerp(bossFromRot, bossToRot, bossLerp);

            yield return null;
        }

        mainCam.transform.position = camToPos;
        mainCam.transform.rotation = camToRot;
        mainCam.fieldOfView = fovTo;
        transform.rotation = bossToRot;
    }

    private void FinishCutscene()
    {
        isCutscene = false;
        Time.timeScale = previousTimeScale;

        if (restaurantManager != null)
            restaurantManager.ConfirmFilthyStrike();
    }

    private IEnumerator ShakeCamera(float duration, Vector3 basePos, Quaternion baseRot)
    {
        if (mainCam == null)
            yield break;

        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;

            Vector3 posOffset = Random.insideUnitSphere * shakePosition;
            Vector3 rotOffset = new Vector3(
                Random.Range(-shakeRotation, shakeRotation),
                Random.Range(-shakeRotation, shakeRotation),
                Random.Range(-shakeRotation, shakeRotation));

            mainCam.transform.position = basePos + posOffset;
            mainCam.transform.rotation = baseRot * Quaternion.Euler(rotOffset);

            yield return null;
        }

        mainCam.transform.position = basePos;
        mainCam.transform.rotation = baseRot;
    }
}
