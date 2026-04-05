using UnityEngine;

public class MapState : IGameState
{
    private GameFlowController _flow;

    public MapState(GameFlowController flow)
    {
        _flow = flow;
    }

    public void Enter()
    {
        // 显隐控制
        if (_flow._mapPanel) _flow._mapPanel.SetActive(true);
        if (_flow._roomUIRoot) _flow._roomUIRoot.SetActive(false);
        
        // UI 重置
        _flow._draftPanel.Hide();
        if (_flow._selectionModeTip) _flow._selectionModeTip.SetActive(false);
        _flow._circleDisplay.RefreshAll();
    }

    public void Exit()
    {
        if (_flow._mapPanel) _flow._mapPanel.SetActive(false);
    }

    public void OnSlotClicked(MagicCircleSlot slotData, Vector3 uiPos)
    {
    }
}