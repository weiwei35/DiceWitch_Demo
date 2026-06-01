using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using DG.Tweening;

public class PhysicsDice : MonoBehaviour
{
    public DiceVisualManager visualManager;
    public Renderer diceRenderer;
    private Rigidbody rb;
    public bool isRolling = false;
    public int finalValue = 0;

    // 存储计算出来的结果数据
    private RuntimeDiceData _sourceData;
    public DiceFaceData currentResultData; 
    private List<DiceAbilitySO> myAbilities = new List<DiceAbilitySO>(); // 运行时缓存能力
    private int _forcedResultValue = 0;
    private bool _forcedResultApplied = false;
    private int _currentResultIndex = -1;
    
    public PlayerDice sourceDataRef; 
    public bool HasPendingForcedResult => _forcedResultValue > 0 && !_forcedResultApplied;
    public int CurrentResultIndex => _currentResultIndex;
    
    // 当骰子停下并算出结果时触发的事件
    public event Action<int> OnDiceSettled;
    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    public void Initialize(RuntimeDiceData data, PlayerDice sourceRef = null, int forcedResultValue = 0)
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
        sourceDataRef = sourceRef; // 记录引用
        _forcedResultValue = forcedResultValue;
        _forcedResultApplied = false;
        _currentResultIndex = -1;
    }

    public void Roll(Vector3 throwForce, Vector3 rotationTorque)
    {
        isRolling = true;
        rb.isKinematic = false; // 开启物理
        rb.maxAngularVelocity = 50f; 
        rb.AddForce(throwForce, ForceMode.Impulse); // 施加推力
        rb.AddTorque(rotationTorque, ForceMode.Impulse); // 施加旋转力
        
        StartCoroutine(WaitForStop());
    }

    IEnumerator WaitForStop()
    {
        yield return new WaitForSeconds(0.5f);

        float elapsed = 0f;
        const float maxWait = 5f;
        while (rb.velocity.sqrMagnitude > 0.01f || rb.angularVelocity.sqrMagnitude > 0.01f)
        {
            elapsed += Time.deltaTime;
            if (elapsed >= maxWait) break;
            yield return null;
        }

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

            _currentResultIndex = resultIndex;

            if (HasPendingForcedResult)
            {
                currentResultData = resultData;
                finalValue = resultData.TotalValue;
                Debug.Log($"<color=cyan>【节点Buff待生效】骰子自然停稳为 {finalValue}，排布后将被拨动为 {_forcedResultValue}</color>");
                OnDiceSettled?.Invoke(finalValue);
                return;
            }

            FinalizeRollResult(resultData, resultIndex);
        
            Debug.Log($"检测结束 -> 朝上的面索引: {resultIndex}, 对应名称: {faces[resultIndex].name}, 结果数值: {finalValue}");

            TriggerRollFinished();
            OnDiceSettled?.Invoke(finalValue);
        }
    }

    public void ApplyForcedResultAfterLayout()
    {
        if (!HasPendingForcedResult || visualManager == null || visualManager.faceTransforms == null) return;

        int faceIndex = FindFaceIndexForValue(_forcedResultValue);
        if (faceIndex < 0) faceIndex = _currentResultIndex >= 0 ? _currentResultIndex : 0;

        DiceFaceData resultData = visualManager.GetResultData(faceIndex);
        resultData.value = _forcedResultValue;
        resultData.bonusValue = 0;

        _forcedResultApplied = true;
        _currentResultIndex = faceIndex;
        isRolling = true;

        FinalizeRollResult(resultData, faceIndex);
        Debug.Log($"<color=cyan>【节点Buff生效】骰子被拨动到 {_forcedResultValue}，最终结果: {finalValue}</color>");

        Quaternion targetRotation = GetRotationWithFaceUp(faceIndex);
        transform.DOKill();
        Sequence sequence = DOTween.Sequence();
        sequence.Append(transform.DOJump(transform.position, 0.18f, 1, 0.35f).SetEase(Ease.OutQuad));
        sequence.Join(transform.DORotateQuaternion(targetRotation, 0.35f).SetEase(Ease.OutBack));
        sequence.OnComplete(() =>
        {
            isRolling = false;
            TriggerRollFinished();
        });
    }

    private void FinalizeRollResult(DiceFaceData resultData, int resultIndex)
    {
        finalValue = resultData.TotalValue;

        // ---> 触发钩子：OnRollEnd <---
        // 让所有能力有机会修改最终点数
        foreach (var ability in myAbilities)
        {
            finalValue = ability.OnRollEnd(finalValue, this);
        }
        foreach (var slot in GetActiveForgeSlots())
        {
            int before = finalValue;
            finalValue = slot.affix.OnRollEnd(finalValue, this);
            Debug.Log($"<color=#FF8800>【骰子结算】词条 [{slot.affix.affixName}] OnRollEnd: {before} → {finalValue} (bonus={slot.affix.bonus})</color>");
        }

        int hookDelta = finalValue - resultData.TotalValue;
        currentResultData = resultData;
        if (hookDelta != 0)
            currentResultData.bonusValue += hookDelta;

        visualManager.UpdateFaceVisual(resultIndex, currentResultData);
    }

    private void TriggerRollFinished()
    {
        foreach (var ability in myAbilities)
        {
            ability.OnRollFinished(this);
        }
    }

    private int FindFaceIndexForValue(int value)
    {
        if (visualManager?.faceDatas == null) return -1;

        for (int i = 0; i < visualManager.faceDatas.Length; i++)
        {
            if (visualManager.faceDatas[i] != null && visualManager.faceDatas[i].value == value)
                return i;
        }

        return -1;
    }

    private Quaternion GetRotationWithFaceUp(int faceIndex)
    {
        Transform[] faces = visualManager.faceTransforms;
        if (faces == null || faceIndex < 0 || faceIndex >= faces.Length || faces[faceIndex] == null)
            return transform.rotation;

        Transform face = faces[faceIndex];
        Vector3 localFaceDir = face.localPosition.sqrMagnitude > 0.0001f ? face.localPosition.normalized : face.localRotation * Vector3.forward;
        Quaternion target = Quaternion.FromToRotation(transform.rotation * localFaceDir, Vector3.up) * transform.rotation;

        Camera cam = Camera.main;
        if (cam == null) return target;

        Vector3 faceWorldUp = target * face.localRotation * Vector3.up;
        Vector3 faceUpXZ = new Vector3(faceWorldUp.x, 0, faceWorldUp.z);
        Vector3 toCamera = cam.transform.position - transform.position;
        Vector3 toCameraXZ = new Vector3(toCamera.x, 0, toCamera.z);

        if (faceUpXZ.sqrMagnitude < 0.0001f || toCameraXZ.sqrMagnitude < 0.0001f)
            return target;

        float angle = Vector3.SignedAngle(faceUpXZ.normalized, toCameraXZ.normalized, Vector3.up);
        float yAdjust = Mathf.Round(angle / 90f) * 90f - 90f;
        return Quaternion.AngleAxis(yAdjust, Vector3.up) * target;
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

    public List<ForgeSlot> GetActiveForgeSlots()
    {
        var result = new List<ForgeSlot>();
        if (sourceDataRef?.forgeSlots != null)
        {
            foreach (var slot in sourceDataRef.forgeSlots)
                if (slot.isForged && slot.affix != null)
                    result.Add(slot);
        }
        return result;
    }
    // 【新增】供外部调用，给所有面增加临时属性加成
    public void ApplyTemporaryBonus(int bonusAmount)
    {
        if (bonusAmount == 0) return;

        // 修改 VisualManager 里的数据
        if (visualManager != null && visualManager.faceDatas != null)
        {
            for (int i = 0; i < visualManager.faceDatas.Length; i++)
            {
                visualManager.faceDatas[i].bonusValue += bonusAmount;
                // 刷新视觉显示
                visualManager.UpdateFaceVisual(i, visualManager.faceDatas[i]);
            }
        }
        
        // 同时更新当前缓存的结果（如果有的话）
        if (currentResultData != null)
        {
            currentResultData.bonusValue += bonusAmount;
        }
    }
    // 生成最终的提示文本
    public string GetFullDescription()
    {
        StringBuilder sb = new StringBuilder();
        if (currentResultData != null)
        {
            // 只有当点数大于0时才显示（防止刚生成没扔时显示0）
            if (currentResultData.TotalValue > 0)
            {
                // 如果有加成值
                if (currentResultData.bonusValue != 0)
                {
                    string sign = currentResultData.bonusValue > 0 ? "+" : "";
                    string color = currentResultData.bonusValue > 0 ? "#00FF00" : "#FF5555";
                    // 格式： 3 + 1 = 4 (用富文本上色)
                    // 基础值(白) + 加成(绿) = 总值(黄/大)
                    sb.AppendLine($"点数: <color=white>{currentResultData.value}</color> <color={color}>{sign}{currentResultData.bonusValue}</color> = <size=120%><color=yellow><b>{currentResultData.TotalValue}</b></color></size>");
                }
                else
                {
                    // 没有加成，直接显示大大的数字
                    sb.AppendLine($"点数: <size=120%><b>{currentResultData.value}</b></size>");
                }
                
                // 加个分割线或者空行，把数值和下面的技能描述分开
                sb.AppendLine("<color=#666666>----------------</color>"); 
            }
        }
        // 1. 遍历所有能力
        bool hasProperty = false;
        if (myAbilities != null && myAbilities.Count > 0)
        {
            hasProperty = true;
            foreach (var ability in myAbilities)
            {
                sb.AppendLine($"<color=yellow>★ {ability.abilityName}</color>");
                sb.AppendLine($"{ability.GetDynamicDescription(this)}");
                sb.AppendLine(); // 空一行
            }
        }
        // 2. 遍历所有锻造词条
        var forgeSlots = GetActiveForgeSlots();
        if (forgeSlots != null && forgeSlots.Count > 0)
        {
            hasProperty = true;
            foreach (var slot in forgeSlots)
            {
                sb.AppendLine($"<color=#FF8800>◆ {slot.affix.affixName}</color>");
                if (!string.IsNullOrEmpty(slot.affix.description))
                    sb.AppendLine($"{slot.affix.description}");
                sb.AppendLine();
            }
        }
        if (!hasProperty)
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
            
            // 3. 赋值给当前结果，并走完整 OnRollEnd 管线，确保锻造词条等加成同步生效
            _currentResultIndex = faceIndex;
            FinalizeRollResult(data, faceIndex);
            
            // 4. (可选) 视觉上强制让这一面朝上
            // 如果你想让它掉在地上时正好是 1 点朝上，可以设置 rotation
            // 这里为了简单，暂不修改 transform，因为蛇形跟随会覆盖位置
        }
    }

    private void OnDestroy()
    {
        transform.DOKill();
    }
}
