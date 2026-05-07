using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

public class EventUIManager : MonoBehaviour
{
    public static EventUIManager Instance;

    [Header("UI 引用")]
    public GameObject eventPanelRoot;       // 整个事件UI界面的根节点
    public Image backgroundImage;           // 事件背景图
    public TextMeshProUGUI descriptionText; // 事件描述
    public Transform choicesContainer;      // 放置选项按钮的父节点 (通常挂有 VerticalLayoutGroup)
    public GameObject choiceButtonPrefab;   // 选项按钮的预制体

    private RandomEventSO _currentEvent;
    private Action _onEventComplete;

    void Awake() { Instance = this; }

    /// <summary>
    /// 打开事件面板并加载第一页
    /// </summary>
    public void ShowEvent(RandomEventSO eventData, Action onComplete)
    {
        if (eventData == null || eventData.pages.Count == 0)
        {
            Debug.LogWarning("事件数据为空，直接结束！");
            onComplete?.Invoke();
            return;
        }

        _currentEvent = eventData;
        _onEventComplete = onComplete;
        eventPanelRoot.SetActive(true);

        // 默认加载第 0 页
        LoadPage(0);
    }

    private void LoadPage(int pageIndex)
    {
        if (_currentEvent == null || pageIndex < 0 || pageIndex >= _currentEvent.pages.Count)
        {
            CloseEvent();
            return;
        }

        EventPage page = _currentEvent.pages[pageIndex];

        // 1. 设置视觉和文本
        if (page.backgroundImage != null && backgroundImage != null) 
            backgroundImage.sprite = page.backgroundImage;
            
        if (descriptionText != null) 
            descriptionText.text = page.description;

        // 2. 清理旧选项按钮
        foreach (Transform child in choicesContainer) 
            Destroy(child.gameObject);

        // 3. 生成新选项
        foreach (var choice in page.choices)
        {
            GameObject btnObj = Instantiate(choiceButtonPrefab, choicesContainer);
            Button btn = btnObj.GetComponent<Button>();
            TextMeshProUGUI btnText = btnObj.GetComponentInChildren<TextMeshProUGUI>();

            btnText.text = choice.choiceText;

            // 【细节打磨】如果选项需要扣钱，但玩家钱不够，则置灰按钮
            if (choice.goldChange < 0 && ResourceManager.Instance.manaDust < Mathf.Abs(choice.goldChange))
            {
                btn.interactable = false;
                btnText.text += " <color=red>(粉尘不足)</color>";
            }
            else
            {
                // 绑定点击事件，由于在 foreach 循环中，需要存一个局部变量防闭包陷阱
                EventChoice capturedChoice = choice;
                btn.onClick.AddListener(() => OnChoiceClicked(capturedChoice));
            }
        }
    }

    private void OnChoiceClicked(EventChoice choice)
    {
        // 1. 结算血量变化
        if (choice.hpChange > 0) 
            PlayerManager.Instance.Heal(choice.hpChange);
        else if (choice.hpChange < 0) 
            PlayerManager.Instance.TakeDamage(Mathf.Abs(choice.hpChange));

        // 2. 结算粉尘/金币变化
        if (choice.goldChange > 0) 
            ResourceManager.Instance.AddManaDust(choice.goldChange);
        else if (choice.goldChange < 0) 
            ResourceManager.Instance.TrySpendManaDust(Mathf.Abs(choice.goldChange));

        // 3. 检查是否直接触发战斗 (比如选项是“抢夺宝箱并触发战斗”)
        if (choice.battleToTrigger != null)
        {
            CloseEvent(); // 先关掉事件 UI
            // 路由到战斗房间
            GameFlowController.Instance.EnterRoom(choice.battleToTrigger);
            return;
        }

        // 4. 页面跳转逻辑
        if (choice.targetPageId == -1)
        {
            CloseEvent(); // -1 表示结束事件
        }
        else
        {
            LoadPage(choice.targetPageId); // 跳到指定的下一页
        }
    }

    private void CloseEvent()
    {
        eventPanelRoot.SetActive(false);
        _onEventComplete?.Invoke(); // 通知 GameFlowController 事件结束了
    }
}