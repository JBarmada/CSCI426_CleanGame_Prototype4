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

    [Header("Audio")]
    [SerializeField] private AudioSource loseAudioSource;
    [SerializeField] private AudioClip loseScreenClip;
    [Range(0f, 1f)]
    [SerializeField] private float loseScreenVolume = 1f;

    [Header("Layering")]
    [SerializeField] private int sortingOrder = 200;

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

        UISortingUtility.EnsureSorting(root != null ? root : gameObject, sortingOrder, true);

        // Auto-find buttons if not assigned
        if (newGameButton == null)
            newGameButton = root != null ? root.GetComponentInChildren<Button>() : GetComponentInChildren<Button>();
        if (quitButton == null && newGameButton != null)
        {
            Button[] allButtons = root != null ? root.GetComponentsInChildren<Button>() : GetComponentsInChildren<Button>();
            for (int i = 0; i < allButtons.Length; i++)
            {
                if (allButtons[i] != newGameButton)
                {
                    quitButton = allButtons[i];
                    break;
                }
            }
        }

        if (newGameButton != null)
        {
            newGameButton.onClick.RemoveListener(OnNewGamePressed);
            newGameButton.onClick.AddListener(OnNewGamePressed);
        }
        if (quitButton != null)
        {
            quitButton.onClick.RemoveListener(OnQuitPressed);
            quitButton.onClick.AddListener(OnQuitPressed);
        }

        EnsureLoseAudioSource();
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
        PlayLoseScreenAudio();
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

    private void EnsureLoseAudioSource()
    {
        if (loseAudioSource == null)
            loseAudioSource = GetComponent<AudioSource>();

        if (loseAudioSource == null)
            loseAudioSource = gameObject.AddComponent<AudioSource>();

        if (loseAudioSource != null)
            loseAudioSource.playOnAwake = false;
    }

    private void PlayLoseScreenAudio()
    {
        if (loseScreenClip == null)
            return;

        EnsureLoseAudioSource();
        if (loseAudioSource == null)
            return;

        loseAudioSource.PlayOneShot(loseScreenClip, loseScreenVolume);
    }
}
