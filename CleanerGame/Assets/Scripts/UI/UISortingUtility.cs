using UnityEngine;
using UnityEngine.UI;

public static class UISortingUtility
{
    public static Canvas EnsureSorting(GameObject target, int sortingOrder, bool ensureRaycaster = false)
    {
        if (target == null)
            return null;

        Canvas canvas = target.GetComponent<Canvas>();
        if (canvas == null)
            canvas = target.AddComponent<Canvas>();

        canvas.overrideSorting = true;
        canvas.sortingOrder = sortingOrder;

        if (ensureRaycaster && target.GetComponent<GraphicRaycaster>() == null)
            target.AddComponent<GraphicRaycaster>();

        return canvas;
    }
}
