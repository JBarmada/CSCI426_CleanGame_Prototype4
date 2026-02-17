using UnityEngine;
using UnityEngine.UI;

public class PauseMenuUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject root;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Button continueButton;
    [SerializeField] private Button quitButton;
    [SerializeField] private GameFlowManager gameFlow;

    [Header("Input")]
    [SerializeField] private KeyCode toggleKey = KeyCode.Escape;

    private bool isVisible;

    private void Awake()
    {
        if (root == null)
            root = gameObject;
        if (canvasGroup == null && root != null)
            canvasGroup = root.GetComponent<CanvasGroup>();
        if (gameFlow == null)
            gameFlow = GameFlowManager.Instance;

        if (continueButton != null)
            continueButton.onClick.AddListener(OnContinuePressed);
        if (quitButton != null)
            quitButton.onClick.AddListener(OnQuitPressed);
    }

    private void OnEnable()
    {
        HideRoot();
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            if (isVisible) OnContinuePressed();
            else ShowPause();
        }
    }

    private void ShowPause()
    {
        isVisible = true;
        ShowRoot();
        if (gameFlow != null)
            gameFlow.PauseGame();
        else
            Time.timeScale = 0f;
    }

    private void OnContinuePressed()
    {
        isVisible = false;
        HideRoot();
        if (gameFlow != null)
            gameFlow.ResumeGame();
        else
            Time.timeScale = 1f;
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
