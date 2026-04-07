using UnityEngine;

/// <summary>
/// Global flag for the short interactive tutorial started from the title screen.
/// </summary>
public static class TutorialMode
{
    public static bool IsActive { get; private set; }

    /// <summary>
    /// Slows customer spawns relative to normal tuning so the tutorial stays readable.
    /// </summary>
    public const float CustomerSpawnIntervalMultiplier = 2.75f;

    public static void Begin()
    {
        IsActive = true;
    }

    public static void End()
    {
        IsActive = false;
    }
}
