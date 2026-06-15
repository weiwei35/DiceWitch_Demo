using UnityEngine;

public class MapState : IGameState
{
    public void Enter()
    {
        GameFlowController.MapPanel?.SetActive(true);
        GameFlowController.RoomUIRoot?.SetActive(false);
        GameFlowController.SetBattleUIVisible(false);
        SpellDraftPanel.Instance.Hide();
        GameFlowController.SelectionModeTip?.SetActive(false);
        MagicCircleDisplay.Instance.RefreshAll();
    }

    public void Exit()
    {
        GameFlowController.MapPanel?.SetActive(false);
    }

    public void OnSlotClicked(MagicCircleSlot slotData, Vector3 uiPos) { }
}
