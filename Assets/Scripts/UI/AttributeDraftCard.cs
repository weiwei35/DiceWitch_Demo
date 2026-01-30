using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Events;

public class AttributeDraftCard : MonoBehaviour
{
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI descText;
    public Button selectButton;

    public void Setup(SlotAttributeSO data, UnityAction<SlotAttributeSO> callback)
    {
        nameText.text = data.attributeName;
        // 简单处理描述中的变量，这里假设 description 已经写死了或者没有变量
        descText.text = data.description; 

        selectButton.onClick.RemoveAllListeners();
        selectButton.onClick.AddListener(() => callback?.Invoke(data));
    }
}