using UnityEngine;

public class PartyDayDecor : MonoBehaviour
{
    [SerializeField] private RestaurantDayCycle dayCycle;
    [SerializeField] private GameObject[] decorObjects;

    private void Awake()
    {
        if (dayCycle == null)
            dayCycle = FindFirstObjectByType<RestaurantDayCycle>();
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
}
