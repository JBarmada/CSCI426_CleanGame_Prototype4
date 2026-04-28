using System.Collections.Generic;
using UnityEngine;

public static class PowerupHudLayout
{
    private static readonly HashSet<int> alignedRoots = new HashSet<int>();

    public static void MoveBelowCoinHud(RectTransform powerupRoot)
    {
        if (powerupRoot == null)
            return;

        int rootId = powerupRoot.GetInstanceID();
        if (alignedRoots.Contains(rootId))
            return;

        CoinHud coinHud = Object.FindFirstObjectByType<CoinHud>();
        if (coinHud == null)
            return;

        RectTransform coinRect = coinHud.GetComponent<RectTransform>();
        if (coinRect == null)
            return;

        if (powerupRoot.parent == coinRect.parent)
        {
            int coinIndex = coinRect.GetSiblingIndex();
            int powerupIndex = powerupRoot.GetSiblingIndex();
            if (powerupIndex <= coinIndex)
                powerupRoot.SetSiblingIndex(coinIndex + 1);
        }

        alignedRoots.Add(rootId);
    }
}