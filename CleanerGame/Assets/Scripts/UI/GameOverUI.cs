using UnityEngine;
using UnityEngine.UI;

public class GameOverUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject root;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private RestaurantManager restaurantManager;
    [SerializeField] private GameFlowManager gameFlow;
    [SerializeField] private Button newGameButton;
    [SerializeField] private Button quitButton;

    private void Awake()
    {
        if (root == null)
            root = gameObject;
        if (canvasGroup == null && root != null)
            canvasGroup = root.GetComponent<CanvasGroup>();
        if (restaurantManager == null)
            restaurantManager = RestaurantManager.Instance;
        if (gameFlow == null)
            gameFlow = GameFlowManager.Instance;

        if (newGameButton != null)
            newGameButton.onClick.AddListener(OnNewGamePressed);
        if (quitButton != null)
            quitButton.onClick.AddListener(OnQuitPressed);
    }

    private void OnEnable()
    {
        if (restaurantManager != null)
            restaurantManager.GameOverByFilth += HandleGameOverByFilth;

        HideRoot();
    }

    private void OnDisable()
    {
        if (restaurantManager != null)
            restaurantManager.GameOverByFilth -= HandleGameOverByFilth;
    }

    private void HandleGameOverByFilth()
    {
        ShowRoot();
        if (gameFlow != null)
            gameFlow.PauseGame();
        else
            Time.timeScale = 0f;
    }

    private void OnNewGamePressed()
    {
        if (gameFlow != null)
            gameFlow.RestartGame();
    }

    private void OnQuitPressed()
    {
        if (gameFlow != null)
            gameFlow.QuitGame();
    }

    private void ShowRoot()
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
            return;
        }

        if (root != null && root != gameObject)
            root.SetActive(true);
    }

    private void HideRoot()
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
            return;
        }

        if (root != null && root != gameObject)
            root.SetActive(false);
    }
}
