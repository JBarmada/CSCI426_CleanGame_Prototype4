using UnityEngine;

public class LightFadeOut : MonoBehaviour
{
    [SerializeField] private float lifetime = 0.2f;
    private Light pointLight;
    private float initialIntensity;
    private float elapsed;

    private void Awake()
    {
        pointLight = GetComponent<Light>();
        initialIntensity = pointLight != null ? pointLight.intensity : 1f;
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / lifetime);
        if (pointLight != null)
            pointLight.intensity = Mathf.Lerp(initialIntensity, 0f, t);
    }
}