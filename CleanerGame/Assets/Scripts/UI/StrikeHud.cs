using UnityEngine;
using UnityEngine.UI;

public class StrikeHud : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RestaurantManager restaurantManager;
    [SerializeField] private Image[] strikes;

    [Header("Colors")]
    [SerializeField] private Color strikeInactiveColor = new Color(0f, 0f, 0f, 1f);
    [SerializeField] private Color strikeActiveColor = new Color(1f, 0.1f, 0.1f, 1f);

    [Header("Animation")]
    [SerializeField] private float popScale = 1.25f;
    [SerializeField] private float popUpSeconds = 0.12f;
    [SerializeField] private float popDownSeconds = 0.12f;

    private Vector3[] baseScales;
    private Coroutine[] popRoutines;
    private int lastCount = -1;

    private void Awake()
    {
        if (restaurantManager == null)
            restaurantManager = RestaurantManager.Instance;
    }

    private void OnEnable()
    {
        if (restaurantManager != null)
            restaurantManager.FilthyCountChanged += HandleFilthyCountChanged;

        CacheBaseScales();
        Refresh();
    }

    private void OnDisable()
    {
        if (restaurantManager != null)
            restaurantManager.FilthyCountChanged -= HandleFilthyCountChanged;
    }

    private void HandleFilthyCountChanged(int _)
    {
        Refresh();
    }

    private void Refresh()
    {
        if (strikes == null || strikes.Length == 0) return;

        int count = restaurantManager == null ? 0 : restaurantManager.GetFilthyCount();
        for (int i = 0; i < strikes.Length; i++)
        {
            if (strikes[i] == null) continue;
            strikes[i].color = i < count ? strikeActiveColor : strikeInactiveColor;
        }

        if (lastCount >= 0 && count > lastCount)
            AnimateStrike(count - 1);

        lastCount = count;
    }

    private void CacheBaseScales()
    {
        if (strikes == null) return;

        baseScales = new Vector3[strikes.Length];
        popRoutines = new Coroutine[strikes.Length];

        for (int i = 0; i < strikes.Length; i++)
        {
            if (strikes[i] == null) continue;
            baseScales[i] = strikes[i].transform.localScale;
        }
    }

    private void AnimateStrike(int index)
    {
        if (strikes == null || index < 0 || index >= strikes.Length) return;
        if (strikes[index] == null) return;

        if (popRoutines == null || popRoutines.Length != strikes.Length)
            CacheBaseScales();

        if (popRoutines[index] != null)
            StopCoroutine(popRoutines[index]);

        popRoutines[index] = StartCoroutine(PopRoutine(index));
    }

    private System.Collections.IEnumerator PopRoutine(int index)
    {
        if (baseScales == null || index < 0 || index >= baseScales.Length)
            yield break;

        Transform target = strikes[index].transform;
        Vector3 baseScale = baseScales[index];
        Vector3 peakScale = baseScale * popScale;

        float t = 0f;
        while (t < popUpSeconds)
        {
            t += Time.unscaledDeltaTime;
            float lerp = popUpSeconds <= 0f ? 1f : Mathf.Clamp01(t / popUpSeconds);
            target.localScale = Vector3.Lerp(baseScale, peakScale, lerp);
            yield return null;
        }

        t = 0f;
        while (t < popDownSeconds)
        {
            t += Time.unscaledDeltaTime;
            float lerp = popDownSeconds <= 0f ? 1f : Mathf.Clamp01(t / popDownSeconds);
            target.localScale = Vector3.Lerp(peakScale, baseScale, lerp);
            yield return null;
        }

        target.localScale = baseScale;
    }
}
