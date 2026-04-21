using UnityEngine;

public class DayThree : MonoBehaviour
{
    [SerializeField] private RestaurantDayCycle dayCycle;
    [SerializeField] private GameObject[] day3OnlyObjects;

    private void OnEnable()
    {
        if (dayCycle == null)
            dayCycle = FindFirstObjectByType<RestaurantDayCycle>();

        if (dayCycle != null)
            dayCycle.DayStarted += HandleDayStarted;

        ApplyState(dayCycle != null && dayCycle.DayCount == 3);
    }

    private void OnDisable()
    {
        if (dayCycle != null)
            dayCycle.DayStarted -= HandleDayStarted;
    }

    private void HandleDayStarted(int dayNumber)
    {
        ApplyState(dayNumber == 3);
    }

    private void ApplyState(bool enable)
    {
        if (day3OnlyObjects != null)
        {
            for (int i = 0; i < day3OnlyObjects.Length; i++)
            {
                if (day3OnlyObjects[i] == null) continue;
                day3OnlyObjects[i].SetActive(enable);
            }
        }
    }
}
