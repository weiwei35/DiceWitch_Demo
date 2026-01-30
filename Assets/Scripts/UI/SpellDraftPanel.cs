using UnityEngine;
using System.Collections.Generic;

public class SpellDraftPanel : MonoBehaviour
{
    public GameObject panelRoot;
    public Transform cardsContainer; // 用 Horizontal Layout Group 排列
    public GameObject cardPrefab;    // 拖入 UI_SpellCard

    // 这是一个回调函数，用来告诉外部“玩家选好了，请进行下一步(选槽位)”
    // 外部管理器会监听这个事件
    public System.Action<DiceAbilitySO> OnSpellSelected; 

    public void ShowDraft()
    {
        panelRoot.SetActive(true);
        GenerateCards();
    }

    public void Hide()
    {
        panelRoot.SetActive(false);
    }

    void GenerateCards()
    {
        // 1. 清空旧卡
        foreach (Transform child in cardsContainer) Destroy(child.gameObject);

        // 2. 从管理器获取 3 个随机法术
        var randomSpells = PlayerProgressionManager.Instance.GetRandomAbilities(3);

        // 3. 生成卡牌
        foreach (var spell in randomSpells)
        {
            GameObject cardObj = Instantiate(cardPrefab, cardsContainer);
            SpellDraftCard cardScript = cardObj.GetComponent<SpellDraftCard>();
            
            // 传入数据和点击回调
            cardScript.Setup(spell, OnCardClicked);
        }
    }

    // 当某张卡被点击时
    void OnCardClicked(DiceAbilitySO selectedSpell)
    {
        Debug.Log($"玩家选择了法术: {selectedSpell.abilityName}");
        
        // 关闭自己
        Hide();

        // 通知系统进入“选槽位”模式
        OnSpellSelected?.Invoke(selectedSpell);
    }
}