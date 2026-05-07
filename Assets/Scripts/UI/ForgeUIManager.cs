using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ForgeUIManager : MonoBehaviour
{
    public static ForgeUIManager Instance;

    [Header("Panel")]
    public GameObject panelRoot;
    public Button closeButton;

    [Header("Dice Selection")]
    public Transform diceSelectContainer;
    public GameObject diceSelectButtonPrefab;

    [Header("Resource Input")]
    public Transform resourceContainer;
    public GameObject resourceButtonPrefab;

    [Header("Options")]
    public Transform optionsContainer;
    public GameObject optionButtonPrefab;

    [Header("Actions")]
    public Button confirmButton;
    public TextMeshProUGUI confirmButtonLabel;
    public TextMeshProUGUI stepText;
    public TextMeshProUGUI statusText;

    private enum Phase { Preparing, Forging }
    private Phase _phase;
    private Action _onComplete;
    private PlayerDice _selectedDice;
    private ForgeResourceSO _selectedResource;
    private List<GameObject> _diceButtons = new List<GameObject>();
    private Dictionary<GameObject, ForgeResourceSO> _resourceMap = new Dictionary<GameObject, ForgeResourceSO>();

    void Awake() { Instance = this; }

    void Start()
    {
        if (closeButton != null) closeButton.onClick.AddListener(OnCloseClicked);
    }

    // =========================================================
    // 入口 / 出口
    // =========================================================
    public void ShowForge(Action onComplete)
    {
        _onComplete = onComplete;
        _phase = Phase.Preparing;
        _selectedDice = null;
        _selectedResource = null;
        panelRoot.SetActive(true);
        RefreshDiceList();
        RefreshResourceList();
        ClearOptions();
        UpdateUI();
    }

    public void Hide()
    {
        panelRoot.SetActive(false);
        TooltipSystem.Instance?.Hide();
    }

    private void OnCloseClicked()
    {
        TooltipSystem.Instance?.Hide();
        Hide();
        _onComplete?.Invoke();
    }

    // =========================================================
    // 骰子列表
    // =========================================================
    private void RefreshDiceList()
    {
        foreach (Transform child in diceSelectContainer) Destroy(child.gameObject);
        _diceButtons.Clear();

        var allDice = MagicCircleManager.Instance.allOwnedDice;
        foreach (var dice in allDice)
        {
            int forgedCount = 0;
            if (dice.forgeSlots != null)
                foreach (var slot in dice.forgeSlots)
                    if (slot.isForged) forgedCount++;
            if (forgedCount >= 3) continue;

            GameObject btnObj = Instantiate(diceSelectButtonPrefab, diceSelectContainer);
            var selector = btnObj.GetComponent<ForgeDiceSelector>();
            if (selector != null) selector.Setup(dice);

            var captured = dice;
            btnObj.GetComponent<Button>().onClick.AddListener(() => OnDiceClicked(captured));
            _diceButtons.Add(btnObj);
        }
    }

    private void OnDiceClicked(PlayerDice dice)
    {
        TooltipSystem.Instance?.Hide();
        if (_phase == Phase.Forging) return; // 锻造开始后不可换骰子

        _selectedDice = dice;

        int forgedCount = 0;
        if (dice.forgeSlots != null)
            foreach (var slot in dice.forgeSlots)
                if (slot.isForged) forgedCount++;

        if (stepText != null)
            stepText.text = $"第 {forgedCount + 1} 个词条槽位 (T{forgedCount + 1})";

        UpdateUI();
    }

    // =========================================================
    // 资源列表
    // =========================================================
    private void RefreshResourceList()
    {
        foreach (Transform child in resourceContainer) Destroy(child.gameObject);
        _resourceMap.Clear();

        foreach (var res in ForgeManager.Instance.allResources)
        {
            GameObject btnObj = Instantiate(resourceButtonPrefab, resourceContainer);
            var resBtn = btnObj.GetComponent<ForgeResourceButton>();
            if (resBtn != null) resBtn.Setup(res);

            _resourceMap[btnObj] = res;
            var capturedRes = res;
            var capturedBtn = btnObj;
            btnObj.GetComponent<Button>().onClick.AddListener(() => OnResourceClicked(capturedRes, capturedBtn));
        }
    }

    private void OnResourceClicked(ForgeResourceSO res, GameObject btnObj)
    {
        TooltipSystem.Instance?.Hide();

        if (_selectedDice == null)
        {
            if (statusText != null) statusText.text = "请先选择骰子";
            return;
        }

        // 已消耗的材料不可再选
        var session = ForgeManager.Instance.CurrentSession;
        if (session != null && session.investedResources.Contains(res))
            return;

        // 已达 3 次上限
        if (_phase == Phase.Forging && !ForgeManager.Instance.CanForgeMore)
            return;

        _selectedResource = res;
        UpdateUI();
    }

    // =========================================================
    // 确认锻造 / 再次锻造
    // =========================================================
    public void OnConfirmClicked()
    {
        TooltipSystem.Instance?.Hide();

        if (_selectedDice == null || _selectedResource == null) return;

        if (_phase == Phase.Preparing)
        {
            // 首次锻造：建立 session，消耗材料，锁定骰子
            ForgeManager.Instance.StartForgeSession(_selectedDice);
            ForgeManager.Instance.AddResource(_selectedResource);
            _phase = Phase.Forging;

            foreach (var btn in _diceButtons)
                btn.GetComponent<Button>().interactable = false;

            RefreshOptions();
        }
        else
        {
            if (!ForgeManager.Instance.CanForgeMore) return;
            ForgeManager.Instance.AddResource(_selectedResource);
            RefreshOptions();
        }

        // 已消耗材料变灰
        _selectedResource = null;
        UpdateResourceInteractable();
        UpdateUI();
    }

    private void UpdateResourceInteractable()
    {
        var session = ForgeManager.Instance.CurrentSession;
        if (session == null) return;

        foreach (var kvp in _resourceMap)
        {
            if (session.investedResources.Contains(kvp.Value))
                kvp.Key.GetComponent<Button>().interactable = false;
        }
    }

    // =========================================================
    // 词条选项 & 附加
    // =========================================================
    private void ClearOptions()
    {
        foreach (Transform child in optionsContainer) Destroy(child.gameObject);
    }

    private void RefreshOptions()
    {
        ClearOptions();

        var options = ForgeManager.Instance.GetCurrentOptions();
        foreach (var affix in options)
        {
            GameObject btnObj = Instantiate(optionButtonPrefab, optionsContainer);
            var optionBtn = btnObj.GetComponent<ForgeOptionButton>();
            if (optionBtn != null)
            {
                optionBtn.Setup(affix, showAttach: true);
                var captured = affix;
                optionBtn.attachButton?.onClick.AddListener(() => OnAttachAffix(captured));
            }
        }
    }

    private void OnAttachAffix(ForgeAffixSO affix)
    {
        TooltipSystem.Instance?.Hide();
        ForgeManager.Instance.CommitAffix(affix);
        Hide();
        _onComplete?.Invoke();
    }

    // =========================================================
    // UI 状态刷新
    // =========================================================
    private void UpdateUI()
    {
        if (_phase == Phase.Preparing)
        {
            bool ready = _selectedDice != null && _selectedResource != null;
            confirmButton.interactable = ready;
            if (confirmButtonLabel != null) confirmButtonLabel.text = "开始锻造";

            if (statusText != null)
            {
                if (_selectedDice == null)
                    statusText.text = "请选择要锻造的骰子";
                else if (_selectedResource == null)
                    statusText.text = "请选择锻造材料";
                else
                    statusText.text = $"骰子: {_selectedDice.diceName}  |  材料: {_selectedResource.resourceName}";
            }
        }
        else
        {
            int count = ForgeManager.Instance.CurrentSession?.ForgeCount ?? 0;
            bool canForge = ForgeManager.Instance.CanForgeMore && _selectedResource != null;

            confirmButton.interactable = canForge;
            if (confirmButtonLabel != null)
                confirmButtonLabel.text = count >= 3 ? "已达上限" : "再次锻造";

            if (statusText != null)
            {
                if (count >= 3)
                    statusText.text = "已达上限，请选择一个词条附加到骰子上";
                else if (_selectedResource == null)
                    statusText.text = $"已锻造 {count}/3 次，可选材料再次锻造或附加已有词条";
                else
                    statusText.text = $"已锻造 {count}/3 次  |  选中: {_selectedResource.resourceName}";
            }
        }
    }
}
