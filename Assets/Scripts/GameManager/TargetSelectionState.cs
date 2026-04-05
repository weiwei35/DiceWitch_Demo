using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TargetSelectionState : IGameState
{
    private GameFlowController _flow;
    private DiceAbilitySO _pendingSpell;
    private Action _onComplete;

    public TargetSelectionState(GameFlowController flow, DiceAbilitySO spell, Action onComplete)
    {
        _flow = flow;
        _pendingSpell = spell;
        _onComplete = onComplete;
    }

    public void Enter()
    {
        if (_flow._selectionModeTip) _flow._selectionModeTip.SetActive(true);
        UnityEngine.Object.FindObjectOfType<MagicCircleDisplay>()?.SetSelectionMode(true);
    }

    public void Exit()
    {
        if (_flow._selectionModeTip) _flow._selectionModeTip.SetActive(false);
        UnityEngine.Object.FindObjectOfType<MagicCircleDisplay>()?.SetSelectionMode(false);
    }

    public void OnSlotClicked(MagicCircleSlot slotData, Vector3 uiPos)
    {
        if (!slotData.isUnlocked || slotData.currentDice == null) return;

        PlayerProgressionManager.Instance.ImprintAbilityToDice(slotData.currentDice, _pendingSpell);
        Debug.Log("附魔成功！");

        _onComplete?.Invoke();
        _flow.ChangeState(new MapState(_flow));
    }
}