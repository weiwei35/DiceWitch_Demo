using UnityEngine;
using System.Collections.Generic;

public class MagicCircleDisplay : MonoBehaviour
{
    public static MagicCircleDisplay Instance;

    public GameObject slotPrefab;   // 拖入 MagicSlotUI 的预制体
    public Transform container;     // 生成在哪个父物体下
    public float radius = 200f;     // 半径

    private List<MagicSlotUI> _spawnedSlots = new List<MagicSlotUI>();

    void Awake() { Instance = this; }

    void Start()
    {
        GenerateCircle();
    }

    void GenerateCircle()
    {
        // 1. 获取数据
        var dataSlots = MagicCircleManager.Instance.magicSlots;
        int count = dataSlots.Count;

        // 2. 环形生成
        for (int i = 0; i < count; i++)
        {
            // 计算角度 (360度 / 数量 * i) + 90度(让第1个在正上方)
            float angle = i * (360f / count) + 90f;
            // 角度转弧度
            float radian = angle * Mathf.Deg2Rad;

            // 计算坐标 (x = cos, y = sin) - 注意Unity UI坐标系
            float x = Mathf.Cos(radian) * radius;
            float y = Mathf.Sin(radian) * radius;

            // 实例化
            GameObject slotObj = Instantiate(slotPrefab, container);
            slotObj.GetComponent<RectTransform>().anchoredPosition = new Vector2(x, y);

            // 初始化 UI
            MagicSlotUI uiScript = slotObj.GetComponent<MagicSlotUI>();
            uiScript.Setup(dataSlots[i]);

            _spawnedSlots.Add(uiScript);
        }
    }

    // 刷新显示 (比如刚注入了属性后调用)
    public void RefreshAll()
    {
        var dataSlots = MagicCircleManager.Instance.magicSlots;
        for (int i = 0; i < _spawnedSlots.Count; i++)
        {
            _spawnedSlots[i].Setup(dataSlots[i]);
        }
    }

    public void SetSelectionMode(bool isActive)
    {
        if (_spawnedSlots == null) return;

        foreach (var ui in _spawnedSlots)
        {
            // 让每个 UI 自己去处理动画
            ui.SetSelectionState(isActive);
        }
    }
}