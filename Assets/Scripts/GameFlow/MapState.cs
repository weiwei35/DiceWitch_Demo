using UnityEngine;

public class MapState : IGameState
{
    public void Enter()
    {
        GameFlowController.MapPanel?.SetActive(true);
        GameFlowController.RoomUIRoot?.SetActive(false);
        GameFlowController.SetBattleUIVisible(false);
        SpellDraftPanel.Instance.Hide();
        RewardDiceSelectionPanel.Instance?.Hide();
        GameFlowController.SelectionModeTip?.SetActive(false);
        MagicCircleDisplay.Instance.RefreshAll();

        if (MapManager.Instance != null && MapViewController.Instance != null)
            MapViewController.Instance.UpdateNodeStates(MapManager.Instance.currentPlayerNodeIndex);

        MapInteractionManager.Instance?.EnterMapDiceStage();
    }

    public void Exit()
    {
        MapInteractionManager.Instance?.ExitMapDiceStage();
        GameFlowController.MapPanel?.SetActive(false);
    }

    public void OnSlotClicked(MagicCircleSlot slotData, Vector3 uiPos) { }
}
