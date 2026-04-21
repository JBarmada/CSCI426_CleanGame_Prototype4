using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class DebugGameManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameFlowManager gameFlow;
    [SerializeField] private RestaurantDayCycle dayCycle;
    [SerializeField] private RestaurantReputation reputation;
    [SerializeField] private CoinWallet coinWallet;
    [SerializeField] private DayEndSummaryUI dayEndSummaryUI;

    [Header("Debug")]
    [SerializeField] private bool debugEnable = true;
    [SerializeField] private bool debugLog = true;
    [SerializeField] private bool debugWaitForGameStart = true;
    [SerializeField] private bool debugApplyOnce = true;

    [Header("Override Payload")]
    [SerializeField] private bool debugForceShowSummary;
    [SerializeField] private int debugSummaryDay = 3;
    [SerializeField] private bool debugSummaryIsFinalDay = true;
    [SerializeField] private bool debugOverrideReputation;
    [SerializeField] private int debugReputationValue;
    [SerializeField] private bool debugOverrideCoins;
    [SerializeField] private int debugCoinsValue;

    [Header("Debug Hotkeys")]
    [SerializeField] private int debugGiveCoinsAmount = 5;
    [SerializeField] private int debugGiveReputationAmount = 1;
    [SerializeField] private KeyCode debugGiveCoinsKey = KeyCode.F5;
    [SerializeField] private KeyCode debugGiveRepKey = KeyCode.F6;
    [SerializeField] private KeyCode debugApplyOverridesKey = KeyCode.F7;
    [SerializeField] private KeyCode debugNextDayKey = KeyCode.F8;
    [SerializeField] private KeyCode debugRestartGameKey = KeyCode.R;
    [SerializeField] private KeyCode debugSkipDay1Key = KeyCode.Alpha1;
    [SerializeField] private KeyCode debugSkipDay2Key = KeyCode.Alpha2;
    [SerializeField] private KeyCode debugSkipDay3Key = KeyCode.Alpha3;
    [SerializeField] private KeyCode debugSkipDay1NumpadKey = KeyCode.Keypad1;
    [SerializeField] private KeyCode debugSkipDay2NumpadKey = KeyCode.Keypad2;
    [SerializeField] private KeyCode debugSkipDay3NumpadKey = KeyCode.Keypad3;

    private bool debugApplied;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        if (!debugEnable) return;
        StartCoroutine(ApplyDebugWhenReady());
    }

    private void Update()
    {
        if (!debugEnable) return;

        ResolveReferences();

        if (WasPressed(debugRestartGameKey))
            DebugRestartGame();

        if (WasPressed(debugSkipDay1Key) || WasPressed(debugSkipDay1NumpadKey))
            DebugSkipToDay(1);
        else if (WasPressed(debugSkipDay2Key) || WasPressed(debugSkipDay2NumpadKey))
            DebugSkipToDay(2);
        else if (WasPressed(debugSkipDay3Key) || WasPressed(debugSkipDay3NumpadKey))
            DebugSkipToDay(3);

        if (!HasGameStarted()) return;

        if (WasPressed(debugGiveCoinsKey))
            DebugGiveCoins();
        if (WasPressed(debugGiveRepKey))
            DebugGiveReputation();
        if (WasPressed(debugApplyOverridesKey))
            ApplyDebugOverrides();
        if (WasPressed(debugNextDayKey))
            DebugAdvanceDay();
    }

    private IEnumerator ApplyDebugWhenReady()
    {
        if (debugApplyOnce && debugApplied)
            yield break;

        if (debugWaitForGameStart)
        {
            while (!HasGameStarted())
                yield return null;
        }

        ApplyDebugOverrides();

        if (debugLog)
            Debug.Log("[DebugGameManager] Overrides applied.", this);
    }

    private void ResolveReferences()
    {
        if (gameFlow == null)
            gameFlow = GameFlowManager.Instance != null ? GameFlowManager.Instance : FindFirstObjectByType<GameFlowManager>();
        if (dayCycle == null)
            dayCycle = FindFirstObjectByType<RestaurantDayCycle>();
        if (reputation == null)
            reputation = FindFirstObjectByType<RestaurantReputation>();
        if (coinWallet == null)
            coinWallet = CoinWallet.Instance != null ? CoinWallet.Instance : FindFirstObjectByType<CoinWallet>();
        if (dayEndSummaryUI == null)
            dayEndSummaryUI = FindFirstObjectByType<DayEndSummaryUI>(FindObjectsInactive.Include);
    }

    private bool HasGameStarted()
    {
        if (gameFlow != null)
            return !gameFlow.IsPaused;

        return Time.timeScale > 0f;
    }

    private bool WasPressed(KeyCode key)
    {
        return key != KeyCode.None && Input.GetKeyDown(key);
    }

    public void ApplyDebugOverrides()
    {
        if (debugApplyOnce && debugApplied)
            return;

        ResolveReferences();

        if (debugOverrideReputation && reputation != null)
            reputation.DebugSetReputation(debugReputationValue);

        if (debugOverrideCoins && coinWallet != null)
            coinWallet.DebugSetCoins(debugCoinsValue);

        dayEndSummaryUI?.DebugRefreshDisplay();

        if (debugForceShowSummary)
            dayEndSummaryUI?.DebugShowSummary(debugSummaryDay, debugSummaryIsFinalDay);

        debugApplied = true;
    }

    public void DebugGiveCoins()
    {
        if (coinWallet == null) return;

        int amount = Mathf.Max(0, debugGiveCoinsAmount);
        if (amount <= 0) return;

        coinWallet.AddCoins(amount);
        dayEndSummaryUI?.DebugRefreshDisplay();

        if (debugLog)
            Debug.Log($"[DebugGameManager] Added {amount} coins.", this);
    }

    public void DebugGiveReputation()
    {
        if (reputation == null) return;

        int amount = Mathf.Max(1, debugGiveReputationAmount);
        int added = reputation.DebugAddReputation(amount);
        dayEndSummaryUI?.DebugRefreshDisplay();

        if (!debugLog) return;

        if (added > 0)
            Debug.Log($"[DebugGameManager] Added {added} reputation. Current={reputation.Reputation}", this);
        else
            Debug.Log("[DebugGameManager] Reputation already at max.", this);
    }

    public void DebugAdvanceDay()
    {
        if (dayEndSummaryUI != null)
        {
            dayEndSummaryUI.DebugAdvanceDay();
            if (debugLog)
                Debug.Log("[DebugGameManager] Advance day requested.", this);
            return;
        }

        dayCycle?.DebugAdvanceDay();
    }

    public void DebugSkipToDay(int targetDay)
    {
        dayCycle?.DebugSkipToDay(targetDay);

        if (debugLog)
            Debug.Log($"[DebugGameManager] Skipped to day {targetDay}.", this);
    }

    public void DebugRestartGame()
    {
        gameFlow?.RestartGame();

        if (debugLog)
            Debug.Log("[DebugGameManager] Restart game requested.", this);
    }
}
