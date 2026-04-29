using UnityEngine;

public static class RuntimeVisualSettings
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Apply()
    {
        int highestQuality = Mathf.Max(0, QualitySettings.names.Length - 1);
        if (QualitySettings.GetQualityLevel() != highestQuality)
            QualitySettings.SetQualityLevel(highestQuality, true);

        QualitySettings.vSyncCount = 1;
        Application.targetFrameRate = 60;

#if !UNITY_EDITOR
        Screen.SetResolution(1920, 1080, FullScreenMode.Windowed);
#endif
    }
}
