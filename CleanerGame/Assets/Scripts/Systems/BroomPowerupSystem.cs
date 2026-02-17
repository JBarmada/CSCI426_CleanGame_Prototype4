using System;
using UnityEngine;

public class BroomPowerupSystem : MonoBehaviour
{
    public static BroomPowerupSystem Instance { get; private set; }

    [Header("Limits")]
    [SerializeField] private int maxUsesPerDay = 2;

    [Header("Effect")]
    [SerializeField] private float speedMultiplierPerUse = 1.15f;
    [SerializeField] private bool stackUses = true;

    public event Action OnChanged;

    private const string DateKey = "BroomPowerup_Date";
    private const string UsesKey = "BroomPowerup_Uses";

    private int usesToday;

    public int UsesToday => usesToday;
    public int UsesLeftToday => Mathf.Max(0, maxUsesPerDay - usesToday);

    public float CurrentMultiplier
    {
        get
        {
            if (usesToday <= 0) return 1f;
            if (!stackUses) return speedMultiplierPerUse;
            return Mathf.Pow(speedMultiplierPerUse, usesToday);
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        LoadAndResetIfNewDay();
        OnChanged?.Invoke();
    }

    private void LoadAndResetIfNewDay()
    {
        string today = DateTime.UtcNow.ToString("yyyy-MM-dd");

        string savedDate = PlayerPrefs.GetString(DateKey, "");
        usesToday = PlayerPrefs.GetInt(UsesKey, 0);

        if (savedDate != today)
        {
            usesToday = 0;
            PlayerPrefs.SetString(DateKey, today);
            PlayerPrefs.SetInt(UsesKey, usesToday);
            PlayerPrefs.Save();
        }
    }

    public bool CanUseToday()
    {
        LoadAndResetIfNewDay();
        return usesToday < maxUsesPerDay;
    }

    public bool TryConsumeUse()
    {
        LoadAndResetIfNewDay();
        if (usesToday >= maxUsesPerDay) return false;

        usesToday++;
        PlayerPrefs.SetInt(UsesKey, usesToday);
        PlayerPrefs.SetString(DateKey, DateTime.UtcNow.ToString("yyyy-MM-dd"));
        PlayerPrefs.Save();

        OnChanged?.Invoke();
        return true;
    }
}
