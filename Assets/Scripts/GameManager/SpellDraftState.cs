using System;
using UnityEngine;

public class SpellDraftState : IGameState
{
    private GameFlowController _flow;
    private Action _onComplete;
    public SpellDraftState(GameFlowController flow, Action onComplete) 
    { 
        _flow = flow; 
        _onComplete = onComplete; 
    }

    public void Enter()
    {
        _flow._draftPanel.OnSpellSelected = OnSpellSelected;
        _flow._draftPanel.ShowDraft();
    }

    public void Exit() { _flow._draftPanel.Hide(); }

    private void OnSpellSelected(DiceAbilitySO selectedSpell)
    {
        // 选完卡牌，立刻切换到“目标槽位选择”状态
        _flow.ChangeState(new TargetSelectionState(_flow, selectedSpell, _onComplete));
    }

    public void OnSlotClicked(MagicCircleSlot slotData, Vector3 uiPos) { /* 抽卡时不能点槽位 */ }
}