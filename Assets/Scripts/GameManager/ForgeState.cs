using System;
using UnityEngine;

/// <summary>
/// 游戏状态机中的冥想/锻造状态。
/// 负责进入锻造 UI、隐藏地图与战斗 UI，并在退出时关闭锻造面板。
/// </summary>
public class ForgeState : IGameState
{
    private Action _onComplete;

    /// <summary>
    /// 创建锻造状态，并记录锻造流程完成后的回调。
    /// </summary>
    /// <param name="onComplete">锻造面板关闭后要执行的流程回调。</param>
    public ForgeState(Action onComplete)
    {
        _onComplete = onComplete;
    }

    /// <summary>
    /// 进入锻造状态，打开冥想面板并切换 UI 可见性。
    /// </summary>
    public void Enter()
    {
        GameFlowController.MapPanel?.SetActive(false);
        GameFlowController.RoomUIRoot?.SetActive(true);
        GameFlowController.SetBattleUIVisible(false);
        ForgeUIManager.Instance.ShowForge(_onComplete);
    }

    /// <summary>
    /// 离开锻造状态时关闭冥想面板。
    /// </summary>
    public void Exit()
    {
        ForgeUIManager.Instance.Hide();
    }

    /// <summary>
    /// 锻造状态暂不响应法阵槽位点击。
    /// </summary>
    /// <param name="slotData">被点击的法阵槽位数据。</param>
    /// <param name="uiPos">点击对应的 UI 坐标。</param>
    public void OnSlotClicked(MagicCircleSlot slotData, Vector3 uiPos) { }
}
