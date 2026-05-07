using System;
using UnityEngine;

public class TargetSelectionState : IGameState
{
    private DiceAbilitySO _pendingSpell;
    private Action _onComplete;

    public TargetSelectionState(DiceAbilitySO spell, Action onComplete)
    {
        _pendingSpell = spell;
        _onComplete = onComplete;
    }

    public void Enter()
    {
        GameFlowController.SelectionModeTip?.SetActive(true);
        MagicCircleDisplay.Instance?.SetSelectionMode(true);
    }

    public void Exit()
    {
        GameFlowController.SelectionModeTip?.SetActive(false);
        MagicCircleDisplay.Instance?.SetSelectionMode(false);
    }

    public void OnSlotClicked(MagicCircleSlot slotData, Vector3 uiPos)
    {
        if (!slotData.isUnlocked || slotData.currentDice == null) return;

        MagicCircleManager.Instance.ImprintAbilityToDice(slotData.currentDice, _pendingSpell);
        Debug.Log("附魔成功！");

        _onComplete?.Invoke();
        GameFlowController.Instance.ChangeState(new MapState());
    }
}
