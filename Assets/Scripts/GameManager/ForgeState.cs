using System;
using UnityEngine;

public class ForgeState : IGameState
{
    private Action _onComplete;

    public ForgeState(Action onComplete)
    {
        _onComplete = onComplete;
    }

    public void Enter()
    {
        GameFlowController.MapPanel?.SetActive(false);
        GameFlowController.RoomUIRoot?.SetActive(true);
        ForgeUIManager.Instance.ShowForge(_onComplete);
    }

    public void Exit()
    {
        ForgeUIManager.Instance.Hide();
    }

    public void OnSlotClicked(MagicCircleSlot slotData, Vector3 uiPos) { }
}
