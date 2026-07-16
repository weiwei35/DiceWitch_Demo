using UnityEngine;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 开始界面按钮入口。
/// 只负责把开始、退出按钮转发到游戏流程，不接管开始界面的动画表现。
/// </summary>
public class StartPanelController : MonoBehaviour
{
    [Header("Buttons")]
    public Button startButton;
    public Button exitButton;

    [Header("Transition")]
    public bool useStartTransition = true;

    private void Awake()
    {
        BindButtons();
    }

    private void OnDestroy()
    {
        if (startButton != null)
            startButton.onClick.RemoveListener(OnStartClicked);

        if (exitButton != null)
            exitButton.onClick.RemoveListener(OnExitClicked);
    }

    public void OnStartClicked()
    {
        if (GameFlowController.Instance == null)
        {
            Debug.LogError("StartPanelController 无法开始游戏：场景中没有 GameFlowController。", this);
            return;
        }

        if (!useStartTransition)
        {
            GameFlowController.Instance.BeginGame();
            return;
        }

        if (TransitionManager.Instance == null)
        {
            Debug.LogError("StartPanelController 无法播放开始转场：场景中没有 TransitionManager。", this);
            return;
        }

        TransitionManager.Instance.PlayDiceWipe(() => GameFlowController.Instance.BeginGame());
    }

    public void OnExitClicked()
    {
#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void BindButtons()
    {
        if (startButton != null)
        {
            startButton.onClick.RemoveListener(OnStartClicked);
            startButton.onClick.AddListener(OnStartClicked);
        }

        if (exitButton != null)
        {
            exitButton.onClick.RemoveListener(OnExitClicked);
            exitButton.onClick.AddListener(OnExitClicked);
        }
    }
}
