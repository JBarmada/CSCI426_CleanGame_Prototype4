using UnityEngine;
using UnityEngine.SceneManagement;

public class GameFlowManager : MonoBehaviour
{
    public static GameFlowManager Instance { get; private set; }

    public bool IsPaused => isPaused;

    [SerializeField] private bool startPaused = true;

    [Header("UI Bootstrap")]
    [Tooltip("Any UI parent GameObjects that start disabled in the scene. " +
             "GameFlowManager activates them all on Awake so their scripts can " +
             "subscribe to events and manage their own visibility from there.\n\n" +
             "Suggested: Menus parent, GameOverUI parent, PauseMenuUI parent, " +
             "DayEndSummaryUI parent, GracePeriodHud parent.")]
    [SerializeField] private GameObject[] uiParentsToActivate;

    private bool isPaused;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // Activate any UI parents that start disabled so their scripts can run
        // Awake/OnEnable and subscribe to events. Each script is responsible for
        // hiding itself immediately in OnEnable if it shouldn't be visible yet.
        if (uiParentsToActivate != null)
        {
            for (int i = 0; i < uiParentsToActivate.Length; i++)
            {
                GameObject ui = uiParentsToActivate[i];
                if (ui != null && !ui.activeSelf)
                    ui.SetActive(true);
            }
        }

        if (startPaused)
        {
            isPaused = true;
            Time.timeScale = 0f;
        }
    }

    private void LateUpdate()
    {
        if (isPaused && Time.timeScale != 0f)
        {
            Debug.Log("[GameFlow] Re-pausing: Time.timeScale was changed externally.");
            Time.timeScale = 0f;
        }
    }

    public void PauseGame()
    {
        if (isPaused) return;
        isPaused = true;
        Time.timeScale = 0f;
    }

    public void ResumeGame()
    {
        if (!isPaused) return;
        isPaused = false;
        Time.timeScale = 1f;
    }

    public void TogglePause()
    {
        if (isPaused) ResumeGame();
        else PauseGame();
    }

    public void RestartGame()
    {
        TutorialMode.End();
        CoinWallet.Instance?.ResetForNewGame();
        Time.timeScale = 1f;
        Scene active = SceneManager.GetActiveScene();
        SceneManager.LoadScene(active.buildIndex);
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
