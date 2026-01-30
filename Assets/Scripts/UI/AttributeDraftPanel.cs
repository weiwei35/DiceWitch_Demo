using UnityEngine;
using System.Collections.Generic;

public class AttributeDraftPanel : MonoBehaviour
{
    public GameObject panelRoot;
    public Transform cardsContainer;
    public GameObject cardPrefab; // 拖入 AttributeDraftCard Prefab

    // 回调：当选好一个属性时
    public System.Action<SlotAttributeSO> OnAttributeSelected;

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
        foreach (Transform child in cardsContainer) Destroy(child.gameObject);

        // 获取 3 个随机属性
        var randomAttrs = PlayerProgressionManager.Instance.GetRandomAttributes(3);

        foreach (var attr in randomAttrs)
        {
            GameObject cardObj = Instantiate(cardPrefab, cardsContainer);
            cardObj.GetComponent<AttributeDraftCard>().Setup(attr, OnCardClicked);
        }
    }

    void OnCardClicked(SlotAttributeSO selectedAttr)
    {
        Hide();
        OnAttributeSelected?.Invoke(selectedAttr);
    }
}