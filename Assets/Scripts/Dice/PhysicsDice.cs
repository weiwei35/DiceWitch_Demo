using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using DG.Tweening;

public class PhysicsDice : MonoBehaviour
{
    private static readonly int HandDrawSettledId = Shader.PropertyToID("_HandDrawSettled");

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
    private Coroutine _inPlaceRollCoroutine;
    private bool _usesBattleHandDraw;
    
    public PlayerDice sourceDataRef; 
    public bool HasPendingForcedResult => _forcedResultValue > 0 && !_forcedResultApplied;
    public int CurrentResultIndex => _currentResultIndex;
    public int PhysicalValue => currentResultData != null ? currentResultData.value : 0;
    public DiceSpellSO Spell => _sourceData != null ? _sourceData.spell : null;
    [System.NonSerialized] public MiniDiceCluster miniCluster;
    
    // 当骰子停下并算出结果时触发的事件
    public event Action<int> OnDiceSettled;
    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    public void Initialize(RuntimeDiceData data, PlayerDice sourceRef = null, int forcedResultValue = 0)
    {
        // 1. (可选) 修改骰子本体颜色，方便区分
        if (!_usesBattleHandDraw)
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

    public void EnableBattleHandDraw(Material bodyMaterial, Material faceMaterial)
    {
        _usesBattleHandDraw = bodyMaterial != null || faceMaterial != null;

        if (diceRenderer != null && bodyMaterial != null)
            diceRenderer.sharedMaterial = bodyMaterial;

        visualManager?.SetFaceTextureMaterial(faceMaterial);
        SetHandDrawSettled(false);
    }

    public void SetHandDrawSettled(bool settled)
    {
        if (diceRenderer != null)
        {
            MaterialPropertyBlock properties = new MaterialPropertyBlock();
            diceRenderer.GetPropertyBlock(properties);
            properties.SetFloat(HandDrawSettledId, settled ? 1f : 0f);
            diceRenderer.SetPropertyBlock(properties);
        }

        visualManager?.SetHandDrawSettled(settled);
    }

    public void StopMotionAndSetKinematic(bool isKinematic)
    {
        if (rb == null) return;

        // Unity only allows velocity writes while the Rigidbody is dynamic.
        if (!isKinematic)
            rb.isKinematic = false;

        if (!rb.isKinematic)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        rb.isKinematic = isKinematic;
    }

    public int DetermineRollResultFaceIndex()
    {
        if (visualManager == null || visualManager.faceDatas == null || visualManager.faceDatas.Length == 0)
        {
            Debug.LogError($"{name} 的 DiceVisualManager 未初始化，无法决定投掷结果。", this);
            return 0;
        }

        if (HasPendingForcedResult)
        {
            int forcedIndex = FindFaceIndexForValue(_forcedResultValue);
            if (forcedIndex < 0)
            {
                Debug.LogError($"{name} 找不到点数为 {_forcedResultValue} 的骰子面，请检查骰子数据配置。", this);
                return 0;
            }

            return forcedIndex;
        }

        return UnityEngine.Random.Range(0, visualManager.faceDatas.Length);
    }

    public int GetMaxValueFaceIndex()
    {
        if (visualManager == null || visualManager.faceDatas == null || visualManager.faceDatas.Length == 0)
            return 0;

        int bestIndex = 0;
        int bestValue = int.MinValue;
        for (int i = 0; i < visualManager.faceDatas.Length; i++)
        {
            DiceFaceData data = visualManager.faceDatas[i];
            if (data == null) continue;

            if (data.TotalValue > bestValue)
            {
                bestValue = data.TotalValue;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    public void SnapFaceUp(int faceIndex)
    {
        if (visualManager == null || visualManager.faceTransforms == null) return;

        faceIndex = Mathf.Clamp(faceIndex, 0, visualManager.faceTransforms.Length - 1);
        transform.rotation = GetOrganizedRotationWithFaceUp(faceIndex);
    }

    public Quaternion GetCurrentResultRotation()
    {
        if (_currentResultIndex < 0) return transform.rotation;
        return GetOrganizedRotationWithFaceUp(_currentResultIndex);
    }

    public Quaternion GetFaceUpRotation(int faceIndex)
    {
        return GetOrganizedRotationWithFaceUp(faceIndex);
    }

    public void RollInPlace(int resultFaceIndex, float totalDuration, float settleDuration, float spinSpeed)
    {
        StartInPlaceRoll(resultFaceIndex, totalDuration, settleDuration, spinSpeed, true);
    }

    public void RollPhysicalInPlace(int resultFaceIndex, float totalDuration, float settleDuration, float spinSpeed)
    {
        StartInPlaceRoll(resultFaceIndex, totalDuration, settleDuration, spinSpeed, false);
    }

    private void StartInPlaceRoll(int resultFaceIndex, float totalDuration, float settleDuration, float spinSpeed, bool applyRollHooks)
    {
        if (visualManager == null || visualManager.faceDatas == null || visualManager.faceDatas.Length == 0)
        {
            Debug.LogError($"{name} 的 DiceVisualManager 未初始化，无法原地投掷。", this);
            return;
        }

        resultFaceIndex = Mathf.Clamp(resultFaceIndex, 0, visualManager.faceDatas.Length - 1);
        if (_inPlaceRollCoroutine != null) StopCoroutine(_inPlaceRollCoroutine);
        transform.DOKill();

        StopMotionAndSetKinematic(true);

        _inPlaceRollCoroutine = StartCoroutine(RollInPlaceRoutine(resultFaceIndex, totalDuration, settleDuration, spinSpeed, applyRollHooks));
    }

    private IEnumerator RollInPlaceRoutine(int resultFaceIndex, float totalDuration, float settleDuration, float spinSpeed, bool applyRollHooks)
    {
        isRolling = true;

        float safeSettleDuration = Mathf.Clamp(settleDuration, 0.05f, Mathf.Max(0.05f, totalDuration));
        float randomSpinDuration = Mathf.Max(0f, totalDuration - safeSettleDuration);
        float axisTimer = 0f;
        Vector3 spinAxis = UnityEngine.Random.onUnitSphere;
        if (spinAxis.sqrMagnitude <= 0.0001f) spinAxis = Vector3.up;

        float elapsed = 0f;
        while (elapsed < randomSpinDuration)
        {
            elapsed += Time.deltaTime;
            axisTimer -= Time.deltaTime;
            if (axisTimer <= 0f)
            {
                spinAxis = UnityEngine.Random.onUnitSphere;
                if (spinAxis.sqrMagnitude <= 0.0001f) spinAxis = Vector3.up;
                axisTimer = UnityEngine.Random.Range(0.06f, 0.14f);
            }

            float speedPulse = Mathf.Lerp(0.75f, 1.25f, UnityEngine.Random.value);
            transform.Rotate(spinAxis, spinSpeed * speedPulse * Time.deltaTime, Space.World);
            yield return null;
        }

        DiceFaceData resultData = visualManager.GetResultData(resultFaceIndex);
        _currentResultIndex = resultFaceIndex;
        if (HasPendingForcedResult)
        {
            _forcedResultApplied = true;
        }

        Quaternion targetRotation = GetOrganizedRotationWithFaceUp(resultFaceIndex);
        bool tweenFinished = false;
        transform
            .DORotateQuaternion(targetRotation, safeSettleDuration)
            .SetEase(Ease.OutCubic)
            .OnComplete(() => tweenFinished = true);

        yield return new WaitUntil(() => tweenFinished);

        if (applyRollHooks)
            FinalizeRollResult(resultData, resultFaceIndex);
        else
            SetPhysicalResult(resultData.value);
        Debug.Log($"原地投掷结束 -> 目标面索引: {resultFaceIndex}, 最终结果: {finalValue}");

        isRolling = false;
        _inPlaceRollCoroutine = null;
        if (applyRollHooks)
            TriggerRollFinished();
        OnDiceSettled?.Invoke(finalValue);
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

    public int FindFaceIndexForValue(int value)
    {
        if (visualManager?.faceDatas == null) return -1;

        for (int i = 0; i < visualManager.faceDatas.Length; i++)
        {
            if (visualManager.faceDatas[i] != null && visualManager.faceDatas[i].value == value)
                return i;
        }

        return -1;
    }

    private Quaternion GetOrganizedRotationWithFaceUp(int faceIndex)
    {
        Transform[] faces = visualManager.faceTransforms;
        if (faces == null || faceIndex < 0 || faceIndex >= faces.Length || faces[faceIndex] == null)
            return transform.rotation;

        Vector3 snappedEuler = transform.eulerAngles;
        snappedEuler.x = Mathf.Round(snappedEuler.x / 90f) * 90f;
        snappedEuler.y = Mathf.Round(snappedEuler.y / 90f) * 90f;
        snappedEuler.z = Mathf.Round(snappedEuler.z / 90f) * 90f;
        Quaternion snappedRotation = Quaternion.Euler(snappedEuler);

        Transform face = faces[faceIndex];
        Vector3 localFaceDir = face.localPosition.sqrMagnitude > 0.0001f ? face.localPosition.normalized : face.localRotation * Vector3.forward;
        Quaternion target = Quaternion.FromToRotation(snappedRotation * localFaceDir, Vector3.up) * snappedRotation;

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
    // 供法术等战斗效果给所有面增加临时点数。
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

    public void SetPhysicalResult(int value)
    {
        int faceIndex = FindFaceIndexForValue(value);
        if (faceIndex < 0)
        {
            Debug.LogError($"{name} 找不到点数为 {value} 的物理面。", this);
            return;
        }

        int preservedBonus = currentResultData != null ? currentResultData.bonusValue : 0;
        DiceFaceData data = visualManager.GetResultData(faceIndex);
        data.bonusValue = preservedBonus;
        _currentResultIndex = faceIndex;
        currentResultData = data;
        finalValue = data.TotalValue;
        visualManager.UpdateFaceVisual(faceIndex, data);
    }

    private void OnDestroy()
    {
        transform.DOKill();
    }
}
