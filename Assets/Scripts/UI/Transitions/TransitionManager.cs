using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 全局转场入口。
/// 当前用于同 Scene 内 UI/状态切换，预留跨 Scene 加载接口。
/// </summary>
public class TransitionManager : MonoBehaviour
{
    public static TransitionManager Instance;

    [Header("Transitions")]
    public DiceWipeTransition diceWipeTransition;

    private bool _isTransitioning;

    public bool IsTransitioning => _isTransitioning;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            return;
        }

        Destroy(gameObject);
    }

    /// <summary>
    /// 播放骰子横扫转场，并在覆盖阶段执行切换逻辑。
    /// </summary>
    /// <param name="onCovered">屏幕被转场遮住时执行。</param>
    /// <param name="onComplete">转场结束后执行。</param>
    public void PlayDiceWipe(Action onCovered, Action onComplete = null)
    {
        if (_isTransitioning) return;

        if (diceWipeTransition == null)
        {
            Debug.LogError("TransitionManager.diceWipeTransition 未配置，无法播放骰子转场。", this);
            return;
        }

        _isTransitioning = true;
        bool started = diceWipeTransition.Play(onCovered, () =>
        {
            _isTransitioning = false;
            onComplete?.Invoke();
        });

        if (!started)
            _isTransitioning = false;
    }

    /// <summary>
    /// 预留的跨 Scene 骰子转场入口。
    /// </summary>
    /// <param name="sceneName">目标 Scene 名称。</param>
    public void PlayDiceWipeToScene(string sceneName)
    {
        PlayDiceWipe(() => StartCoroutine(LoadSceneRoutine(sceneName)));
    }

    private IEnumerator LoadSceneRoutine(string sceneName)
    {
        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);
        while (operation != null && !operation.isDone)
            yield return null;
    }
}
