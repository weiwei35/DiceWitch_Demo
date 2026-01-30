using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class XPBarUI : MonoBehaviour
{
    public Slider xpSlider;
    public TMP_Text levelText;

    void Start()
    {
        // 初始更新一次
        UpdateUI(0);
        
        // 订阅事件
        PlayerProgressionManager.Instance.OnXPChanged += UpdateUI;
        // 如果你想在升级瞬间刷新等级文字，也可以监听 OnLevelUp，或者在 UpdateUI 里获取当前等级
    }

    void OnDestroy()
    {
        if (PlayerProgressionManager.Instance != null)
            PlayerProgressionManager.Instance.OnXPChanged -= UpdateUI;
    }

    void UpdateUI(float ratio)
    {
        if (xpSlider) xpSlider.value = ratio;
        if (levelText) levelText.text = $"Lv.{PlayerProgressionManager.Instance.playerLevel}";
    }
}