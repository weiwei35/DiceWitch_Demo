using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Text;

public class PhysicsDice : MonoBehaviour
{
    public DiceVisualManager visualManager;
    public Renderer diceRenderer;
    private Rigidbody rb;
    public bool isRolling = false;
    public int finalValue = 0;

    // 存储计算出来的结果数据
    private DiceDataSO _sourceData;
    public DiceFaceData currentResultData; 
    private List<DiceAbilitySO> myAbilities = new List<DiceAbilitySO>(); // 运行时缓存能力

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    public void Initialize(DiceDataSO data)
    {
        // 1. (可选) 修改骰子本体颜色，方便区分
        diceRenderer.material.color = data.bodyColor;

        // 2. 将数据传给 VisualManager 去更新显示的文字和图标
        if (visualManager != null)
        {
            // 也就是把你配置的 data.faces 赋值给骰子的 6 个面
            visualManager.InitDice(data.faces);
        }

        _sourceData = data;
        myAbilities = data.abilities;
    }

    public void Roll(Vector3 throwForce, Vector3 rotationTorque)
    {
        isRolling = true;
        rb.isKinematic = false; // 开启物理
        rb.AddForce(throwForce, ForceMode.Impulse); // 施加推力
        rb.AddTorque(rotationTorque, ForceMode.Impulse); // 施加旋转力
        
        StartCoroutine(WaitForStop());
    }

    IEnumerator WaitForStop()
    {
        // 延迟一点时间，防止刚扔出去速度还没起来就被判断停了
        yield return new WaitForSeconds(0.5f);

        // 检测速度是否接近0
        while (rb.velocity.sqrMagnitude > 0.01f || rb.angularVelocity.sqrMagnitude > 0.01f)
        {
            yield return null;
        }

        // 停下了
        isRolling = false;
        CalculateValue();
    }

    void CalculateValue()
    {
        // 1. 获取所有的面（从 VisualManager 里拿，确保顺序一致）
        Transform[] faces = visualManager.faceTransforms;
    
        // 如果还没赋值，就报错
        if (faces == null || faces.Length == 0) return;

        float maxY = -999f;
        int resultIndex = -1;

        // 2. 遍历所有面，看谁的世界坐标 Y 值最大（即位置最高）
        // 因为骰子中心在地面，所以“朝上”的那个面，绝对是 Y 轴坐标最高的
        for (int i = 0; i < faces.Length; i++)
        {
            // 获取面的世界坐标高度
            float height = faces[i].position.y;

            if (height > maxY)
            {
                maxY = height;
                resultIndex = i;
            }
        }

        // 3. 拿到结果
        if (resultIndex != -1)
        {
            // 从 VisualManager 获取对应的数据
            DiceFaceData resultData = visualManager.GetResultData(resultIndex);
        
            // 赋值最终结果
            finalValue = resultData.value;
            // ---> 触发钩子：OnRollEnd <---
            // 让所有能力有机会修改最终点数
            foreach (var ability in myAbilities)
            {
                finalValue = ability.OnRollEnd(finalValue);
            }
            currentResultData.value = finalValue;
        
            Debug.Log($"检测结束 -> 朝上的面索引: {resultIndex}, 对应名称: {faces[resultIndex].name}, 结果数值: {finalValue}");
        }
        // 触发 OnRollFinished
        foreach(var ability in myAbilities) {
            ability.OnRollFinished(this);
        }
    }
    // 供外部调用的接口
    public DiceFaceData GetCurrentData()
    {
        return currentResultData;
    }
    // 提供给外部获取能力的接口
    public List<DiceAbilitySO> GetAbilities()
    {
        return myAbilities;
    }
    // 生成最终的提示文本
    public string GetFullDescription()
    {
        StringBuilder sb = new StringBuilder();

        // 1. 遍历所有能力
        if (myAbilities != null && myAbilities.Count > 0)
        {
            foreach (var ability in myAbilities)
            {
                sb.AppendLine($"<color=yellow>★ {ability.abilityName}</color>");
                sb.AppendLine($"{ability.description}");
                sb.AppendLine(); // 空一行
            }
        }
        else
        {
            sb.Append("<i>没有任何特殊属性</i>");
        }

        return sb.ToString();
    }

    public string GetDiceName()
    {
        return _sourceData != null ? _sourceData.diceName : "未知骰子";
    }

    public void ForceSetValue(int faceIndex)
    {
        // 1. 确保 VisualManager 已经初始化了数据
        if (visualManager != null && visualManager.faceDatas != null)
        {
            // 2. 获取对应面的数据
            DiceFaceData data = visualManager.GetResultData(faceIndex);
            
            // 3. 赋值给当前结果
            currentResultData = data;
            finalValue = data.value;
            
            // 4. (可选) 视觉上强制让这一面朝上
            // 如果你想让它掉在地上时正好是 1 点朝上，可以设置 rotation
            // 这里为了简单，暂不修改 transform，因为蛇形跟随会覆盖位置
        }
    }
}