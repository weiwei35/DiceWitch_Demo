using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Events; // 用于回调

public class SpellDraftCard : MonoBehaviour
{
    [Header("UI Refs")]
    public Image iconImage;
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI descText;
    public Button selectButton;

    private DiceAbilitySO _data;

    // 初始化卡牌
    // callback 是当点击这张卡时要执行的方法
    public void Setup(DiceAbilitySO data, UnityAction<DiceAbilitySO> callback)
    {
        _data = data;

        iconImage.sprite = data.icon;
        nameText.text = data.abilityName;
        descText.text = data.description;

        // 绑定点击事件
        selectButton.onClick.RemoveAllListeners();
        selectButton.onClick.AddListener(() => {
            callback?.Invoke(_data);
        });
    }
}