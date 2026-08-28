using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;

public class DiceThrower : MonoBehaviour
{
    public static DiceThrower Instance;

    [Header("Dice Prefab")]
    public GameObject dicePrefab;
    public Transform spawnPoint;

    [Header("In-place Roll Settings")]
    public float rollSpinDuration = 1.1f;
    public float rollSettleDuration = 0.32f;
    public float rollStopInterval = 0.16f;
    public float randomSpinSpeed = 1260f;

    [Header("Layout Settings (整理排版)")]
    public Transform layoutCenter;           // 最终整齐排列的中心点
    public float diceSpacing = 0.16f;
    public float layoutTweenDuration = 0.4f;

    [Header("Row Layout Settings (分行排版)")]
    [Tooltip("每行最多摆放的骰子数，超过则换到下一行。")]
    public int maxDicePerRow = 3;
    [Tooltip("多行时，行与行之间的间距（z 方向）。")]
    public float rowSpacing = 0.14f;
    [Tooltip("骰子世界尺寸，用于相机包围盒估算。")]
    public float diceSize = 0.1f;

    [Header("Elemental Split")]
    [Tooltip("元素学派生成的1点小骰预制体。")]
    public GameObject elementMiniDicePrefab;
    [Min(0.01f)] public float miniDiceColumnSpacing = 0.07f;
    [Min(0.01f)] public float miniDiceRowSpacing = 0.07f;
    [Min(0.05f)] public float splitAnimationDuration = 0.3f;

    [Header("Spell Response Animation")]
    [Min(0.05f)] public float flipDuration = 0.35f;
    [Min(0f)] public float responseInterval = 0.08f;
    [Min(0f)] public float alchemyShakeStrength = 0.035f;
    [Tooltip("任意学派响应特效的自动销毁时间。")]
    [Min(0f)] public float spellVfxLifetime = 1.2f;
    [Min(0f)] public float arcaneSpinSpeed = 900f;

    [Header("Battle Dice Hand-Drawn Visual")]
    [Tooltip("只在战斗骰子实例上使用；材质中的参数统一控制所有骰子的手绘风格。")]
    public Material battleDiceBodyMaterial;
    public Material battleDiceFaceMaterial;

    void Awake() { Instance = this; }

    private Transform _container;
    private List<PhysicsDice> _highlightedDiceList = new List<PhysicsDice>();
    private Dictionary<PhysicsDice, Color> _highlightOriginalColors = new Dictionary<PhysicsDice, Color>();
    private List<PhysicsDice> activeDiceList = new List<PhysicsDice>();

    private struct LayoutSlot
    {
        public PhysicsDice dice;
        public DiceSquadGroup squad;
        public MiniDiceCluster miniCluster;
    }
    
    private int _settledDiceCount = 0;
    private int _totalDiceExpected = 0; 
    private Coroutine _throwCoroutine;
    private Coroutine _readyCoroutine;
    private bool _diceReadyForInput;
    private bool _roundOpeningPrepared;
    private bool _isSpellResponding;
    private Coroutine _spellResponseCoroutine;
    private bool _battleDiceVisualsActive;

    public bool AreDiceReadyForInput => _diceReadyForInput && !IsAnyDiceRolling();

    public void RegisterDice(PhysicsDice dice)
    {
        if (!activeDiceList.Contains(dice))
        {
            ApplyBattleHandDraw(dice);
            AttachDiceToContainer(dice);
            activeDiceList.Add(dice);
            dice.OnDiceSettled += HandleSingleDiceSettled;
            _totalDiceExpected++;
        }
    }

    public void RegisterSettledDice(PhysicsDice dice)
    {
        if (dice == null || activeDiceList.Contains(dice)) return;

        ApplyBattleHandDraw(dice);
        AttachDiceToContainer(dice);
        activeDiceList.Add(dice);
        dice.OnDiceSettled += HandleSingleDiceSettled;
        _totalDiceExpected++;
        _settledDiceCount++;
    }

    public void TryOrganizeDiceLayout()
    {
        if (_settledDiceCount >= _totalDiceExpected && _totalDiceExpected > 0 && !IsAnyDiceRolling())
        {
            OrganizeDiceLayout();
        }
    }

    public bool IsAnyDiceRolling()
    {
        foreach (var dice in activeDiceList)
            if (dice != null && dice.isRolling) return true;
        return false;
    }

    private void AttachDiceToContainer(PhysicsDice dice)
    {
        if (dice == null) return;

        if (_container == null) _container = new GameObject("--- Dice Container ---").transform;
        dice.transform.SetParent(_container);

        if (dicePrefab != null)
            SetLayerRecursively(dice.gameObject, dicePrefab.layer);
    }

    private void SetLayerRecursively(GameObject obj, int layer)
    {
        if (obj == null) return;

        obj.layer = layer;
        foreach (Transform child in obj.transform)
        {
            SetLayerRecursively(child.gameObject, layer);
        }
    }

    public void SpawnAndThrow(List<BattleDiceEntry> diceEntries)
    {
        ClearOldDice();
        _battleDiceVisualsActive = true;
        _diceReadyForInput = false;
        _roundOpeningPrepared = false;

        _totalDiceExpected = diceEntries.Count; 
        if (_container == null) _container = new GameObject("--- Dice Container ---").transform;

        if (_throwCoroutine != null) StopCoroutine(_throwCoroutine);
        _throwCoroutine = StartCoroutine(CinematicThrowSequence(diceEntries));
    }

    // =========================================================
    // 【核心演出】固定位置生成 -> 原地随机旋转 -> 依次吸附到结果面
    // =========================================================
    private IEnumerator CinematicThrowSequence(List<BattleDiceEntry> diceEntries)
    {
        int count = diceEntries.Count;
        if (count == 0)
        {
            _roundOpeningPrepared = true;
            _diceReadyForInput = true;
            BattleManager.Instance?.SetDiceSpellResponseActive(false);
            yield break;
        }

        for (int i = 0; i < count; i++)
        {
            var entry = diceEntries[i];
            Vector3 targetPos = GetLayoutSlotPos(i, count);

            GameObject newDiceObj = Instantiate(dicePrefab, targetPos, Random.rotation);
            newDiceObj.transform.SetParent(_container);
        
            PhysicsDice pDice = newDiceObj.GetComponent<PhysicsDice>();
            if (pDice != null)
            {
                ApplyBattleHandDraw(pDice);
                pDice.Initialize(entry.combatData, entry.sourceRef, entry.forcedResultValue); 
                pDice.SnapFaceUp(pDice.GetMaxValueFaceIndex());
                activeDiceList.Add(pDice);
                pDice.OnDiceSettled += HandleSingleDiceSettled;
                
                pDice.StopMotionAndSetKinematic(true);
            }
        }

        yield return null;

        for (int i = 0; i < activeDiceList.Count; i++)
        {
            PhysicsDice pDice = activeDiceList[i];
            if (pDice == null) continue;

            RollDiceInPlace(pDice, i);
        }
    }

    public PhysicsDice SpawnSingleDice(RuntimeDiceData data, PlayerDice sourceRef = null)
    {
        _battleDiceVisualsActive = true;
        _diceReadyForInput = false;
        Vector3 targetPos = layoutCenter != null ? layoutCenter.position : spawnPoint.position;
        GameObject newDiceObj = Instantiate(dicePrefab, targetPos, Random.rotation);
        if (_container != null) newDiceObj.transform.SetParent(_container);

        PhysicsDice pDice = newDiceObj.GetComponent<PhysicsDice>();
        if (pDice != null)
        {
            ApplyBattleHandDraw(pDice);
            pDice.Initialize(data, sourceRef);
            pDice.SnapFaceUp(pDice.GetMaxValueFaceIndex());
            activeDiceList.Add(pDice);
            pDice.OnDiceSettled += HandleSingleDiceSettled;
            _totalDiceExpected++;

            RollDiceInPlace(pDice, 0);

            return pDice;
        }
        return null;
    }

    public PhysicsDice SpawnIdleSingleDice(RuntimeDiceData data, PlayerDice sourceRef = null)
    {
        _battleDiceVisualsActive = false;
        Vector3 targetPos = layoutCenter != null ? layoutCenter.position : spawnPoint.position;
        GameObject newDiceObj = Instantiate(dicePrefab, targetPos, Random.rotation);

        PhysicsDice pDice = newDiceObj.GetComponent<PhysicsDice>();
        if (pDice == null) return null;

        pDice.Initialize(data, sourceRef);
        pDice.SnapFaceUp(pDice.GetMaxValueFaceIndex());
        RegisterDice(pDice);

        pDice.StopMotionAndSetKinematic(true);

        _settledDiceCount = 0;
        _totalDiceExpected = 1;
        _diceReadyForInput = true;
        return pDice;
    }

    public void RollExistingSingleDice(PhysicsDice dice)
    {
        if (dice == null) return;

        if (!activeDiceList.Contains(dice))
        {
            RegisterDice(dice);
        }

        _settledDiceCount = 0;
        _totalDiceExpected = 1;
        _diceReadyForInput = false;
        RollDiceInPlace(dice, 0);
    }

    public void RollDiceInPlace(PhysicsDice dice, int stopOrderIndex = 0)
    {
        if (dice == null) return;

        dice.SetHandDrawSettled(false);
        int resultFaceIndex = dice.DetermineRollResultFaceIndex();
        float duration = Mathf.Max(rollSettleDuration, rollSpinDuration + Mathf.Max(0, stopOrderIndex) * rollStopInterval);
        dice.RollInPlace(resultFaceIndex, duration, rollSettleDuration, randomSpinSpeed);
    }

    // ---------------------------------------------------------
    // 阶段 3：落地静止后，重新吸附排队
    // ---------------------------------------------------------
    private void HandleSingleDiceSettled(int value)
    {
        if (_isSpellResponding) return;
        _settledDiceCount++;
        TryOrganizeDiceLayout();
    }

    private void OrganizeDiceLayout(bool scheduleReady = true)
    {
        // 清理已被外部销毁的骰子引用
        activeDiceList.RemoveAll(d => d == null);

        List<LayoutSlot> layoutSlots = BuildLayoutSlots();

        int count = layoutSlots.Count;
        if (count == 0) return;

        SetAllHandDrawSettled(false);

        for (int i = 0; i < count; i++)
        {
            LayoutSlot slot = layoutSlots[i];

            // 再次冻结物理，让动画接管整理
            Vector3 targetPos = GetLayoutSlotPos(i, count);

            if (slot.squad != null)
            {
                slot.squad.ArrangeAt(targetPos, layoutTweenDuration);
                continue;
            }

            if (slot.miniCluster != null)
            {
                slot.miniCluster.ArrangeAt(targetPos, 3, miniDiceColumnSpacing, miniDiceRowSpacing, layoutTweenDuration);
                continue;
            }

            PhysicsDice dice = slot.dice;
            if (dice == null) continue;

            dice.StopMotionAndSetKinematic(true);

            dice.transform.DOMove(targetPos, layoutTweenDuration).SetEase(Ease.OutQuad);
            dice.transform.DORotateQuaternion(dice.GetCurrentResultRotation(), layoutTweenDuration).SetEase(Ease.OutQuad);
        }

        if (scheduleReady)
        {
            if (_readyCoroutine != null)
                StopCoroutine(_readyCoroutine);
            _readyCoroutine = StartCoroutine(MarkDiceReadyAfterLayout());
        }
    }

    private IEnumerator MarkDiceReadyAfterLayout()
    {
        if (layoutTweenDuration > 0f)
            yield return new WaitForSeconds(layoutTweenDuration);

        if (!_roundOpeningPrepared)
        {
            _roundOpeningPrepared = true;
            yield return ProcessRoundOpening();
        }

        SetAllHandDrawSettled(true);
        _diceReadyForInput = true;
        BattleManager.Instance?.SetDiceSpellResponseActive(false);
        _readyCoroutine = null;
    }

    private List<LayoutSlot> BuildLayoutSlots()
    {
        List<LayoutSlot> slots = new List<LayoutSlot>();
        HashSet<DiceSquadGroup> addedSquads = new HashSet<DiceSquadGroup>();
        HashSet<MiniDiceCluster> addedMiniClusters = new HashSet<MiniDiceCluster>();

        foreach (var dice in activeDiceList)
        {
            if (dice == null) continue;

            DiceDragger dragger = dice.GetComponent<DiceDragger>();
            DiceSquadGroup squad = dragger != null ? dragger.squadGroup : null;

            if (dice.miniCluster != null)
            {
                if (addedMiniClusters.Add(dice.miniCluster))
                    slots.Add(new LayoutSlot { miniCluster = dice.miniCluster });
                continue;
            }

            if (squad != null)
            {
                if (addedSquads.Add(squad))
                    slots.Add(new LayoutSlot { squad = squad });
            }
            else
            {
                slots.Add(new LayoutSlot { dice = dice });
            }
        }

        return slots;
    }

    // ---------------------------------------------------------
    // 多行网格排版：每行最多 maxDicePerRow 个，逐行水平居中，行沿 z 方向排列
    // ---------------------------------------------------------
    public Vector3 GetLayoutSlotPos(int index, int count)
    {
        int perRow = Mathf.Max(1, maxDicePerRow);
        int row = index / perRow;
        int colInRow = index % perRow;
        int colsInThisRow = Mathf.Clamp(count - row * perRow, 1, perRow);
        int rows = Mathf.Max(1, Mathf.CeilToInt(count / (float)perRow));

        float rowWidth = (colsInThisRow - 1) * diceSpacing;
        float startX = -rowWidth * 0.5f;
        float z = (row - (rows - 1) * 0.5f) * rowSpacing;

        Vector3 centerPos = layoutCenter != null ? layoutCenter.position
            : (spawnPoint != null ? spawnPoint.position : transform.position);
        return centerPos + Vector3.right * (startX + colInRow * diceSpacing) + Vector3.forward * z;
    }

    // 当前骰子网格的包围盒尺寸（含旋转外接球余量），供相机适配使用。
    public Vector2 GetLayoutBoundsSize(int count)
    {
        int perRow = Mathf.Max(1, maxDicePerRow);
        int rows = Mathf.Max(1, Mathf.CeilToInt(count / (float)perRow));
        int cols = Mathf.Min(Mathf.Max(1, count), perRow);

        float outerRadius = diceSize * Mathf.Sqrt(3f) * 0.5f; // 立方体旋转时的外接球半径
        float width = (cols - 1) * diceSpacing + outerRadius * 2f;
        float height = (rows - 1) * rowSpacing + outerRadius * 2f;

        // 单颗元素骰也可能分裂成 2×3 小骰，固定机位至少要能完整容纳这一组。
        width = Mathf.Max(width, miniDiceColumnSpacing * 2f + diceSize);
        height = Mathf.Max(height, miniDiceRowSpacing + diceSize);
        return new Vector2(width, height);
    }

    public Vector3 GetLayoutCenterWorld()
    {
        return layoutCenter != null ? layoutCenter.position
            : (spawnPoint != null ? spawnPoint.position : transform.position);
    }

    public Vector3 GetOrganizedEuler(PhysicsDice dice, Camera cam, int forcedUpFaceIndex = -1)
    {
        Vector3 euler = dice.transform.eulerAngles;
        euler.x = Mathf.Round(euler.x / 90f) * 90f;
        euler.y = Mathf.Round(euler.y / 90f) * 90f;
        euler.z = Mathf.Round(euler.z / 90f) * 90f;

        if (forcedUpFaceIndex >= 0)
            return OrientFaceToCamera(dice, euler, forcedUpFaceIndex, cam);

        return OrientUpFaceToCamera(dice, euler, cam);
    }

    private Vector3 OrientFaceToCamera(PhysicsDice dice, Vector3 snappedEuler, int faceIndex, Camera cam)
    {
        Transform[] faces = dice.visualManager?.faceTransforms;
        if (faces == null || faceIndex < 0 || faceIndex >= faces.Length || faces[faceIndex] == null)
            return snappedEuler;

        Quaternion snappedRot = Quaternion.Euler(snappedEuler);
        Transform face = faces[faceIndex];
        Vector3 localFaceDir = face.localPosition.sqrMagnitude > 0.0001f ? face.localPosition.normalized : face.localRotation * Vector3.forward;
        Quaternion target = Quaternion.FromToRotation(snappedRot * localFaceDir, Vector3.up) * snappedRot;

        if (cam == null) return target.eulerAngles;

        Vector3 faceWorldUp = target * face.localRotation * Vector3.up;
        Vector3 faceUpXZ = new Vector3(faceWorldUp.x, 0, faceWorldUp.z);
        Vector3 toCamera = cam.transform.position - dice.transform.position;
        Vector3 toCameraXZ = new Vector3(toCamera.x, 0, toCamera.z);

        if (faceUpXZ.sqrMagnitude < 0.0001f || toCameraXZ.sqrMagnitude < 0.0001f)
            return target.eulerAngles;

        float angle = Vector3.SignedAngle(faceUpXZ.normalized, toCameraXZ.normalized, Vector3.up);
        float yAdjust = Mathf.Round(angle / 90f) * 90f - 90f;
        return (Quaternion.AngleAxis(yAdjust, Vector3.up) * target).eulerAngles;
    }

    private Vector3 OrientUpFaceToCamera(PhysicsDice dice, Vector3 snappedEuler, Camera cam)
    {
        if (cam == null) return snappedEuler;

        Transform[] faces = dice.visualManager?.faceTransforms;
        if (faces == null || faces.Length == 0) return snappedEuler;

        Quaternion snappedRot = Quaternion.Euler(snappedEuler);

        float maxY = -999f;
        int upFaceIndex = -1;
        for (int i = 0; i < faces.Length; i++)
        {
            Vector3 worldPos = dice.transform.position + snappedRot * faces[i].localPosition;
            if (worldPos.y > maxY)
            {
                maxY = worldPos.y;
                upFaceIndex = i;
            }
        }

        if (upFaceIndex < 0) return snappedEuler;

        Transform upFace = faces[upFaceIndex];

        Vector3 faceWorldUp = snappedRot * upFace.localRotation * Vector3.up;
        Vector3 faceUpXZ = new Vector3(faceWorldUp.x, 0, faceWorldUp.z);
        if (faceUpXZ.sqrMagnitude < 0.0001f) return snappedEuler;
        faceUpXZ.Normalize();

        Vector3 toCamera = cam.transform.position - dice.transform.position;
        Vector3 toCameraXZ = new Vector3(toCamera.x, 0, toCamera.z);
        if (toCameraXZ.sqrMagnitude < 0.0001f) return snappedEuler;
        toCameraXZ.Normalize();

        float angle = Vector3.SignedAngle(faceUpXZ, toCameraXZ, Vector3.up);
        float yAdjust = Mathf.Round(angle / 90f) * 90f - 90f;

        snappedEuler.y = (snappedEuler.y + yAdjust) % 360f;
        if (snappedEuler.y < 0) snappedEuler.y += 360f;

        return snappedEuler;
    }

    public void ClearOldDice()
    {
        if (_throwCoroutine != null) StopCoroutine(_throwCoroutine);
        if (_readyCoroutine != null) StopCoroutine(_readyCoroutine);
        if (_spellResponseCoroutine != null) StopCoroutine(_spellResponseCoroutine);

        StopHighlight();
        _throwCoroutine = null;
        _readyCoroutine = null;
        _spellResponseCoroutine = null;
        _roundOpeningPrepared = false;
        _isSpellResponding = false;
        _diceReadyForInput = false;
        _battleDiceVisualsActive = false;
        _settledDiceCount = 0; 
        _totalDiceExpected = 0;

        for (int i = activeDiceList.Count - 1; i >= 0; i--)
        {
            if (activeDiceList[i] != null) Destroy(activeDiceList[i].gameObject);
        }
        activeDiceList.Clear();

        var allSquads = FindObjectsOfType<DiceSquadGroup>();
        foreach (var squad in allSquads) Destroy(squad.gameObject);
        var allMiniClusters = FindObjectsOfType<MiniDiceCluster>();
        foreach (var cluster in allMiniClusters) Destroy(cluster.gameObject);

        BattleManager.Instance?.SetDiceSpellResponseActive(false);
    }

    public int GetValidDiceCount()
    {
        int count = 0;
        foreach (var dice in activeDiceList)
            if (dice != null && dice.gameObject != null) count++;
        return count;
    }

    public void HandlePlayerDiceResolved(PhysicsDice usedDice, int usedPhysicalValue)
    {
        if (usedDice == null) return;

        activeDiceList.Remove(usedDice);
        RemoveFromMiniCluster(usedDice);
        if (BattleManager.Instance != null && !BattleManager.Instance.IsBattleActive)
            return;
        _diceReadyForInput = false;

        if (_spellResponseCoroutine != null) StopCoroutine(_spellResponseCoroutine);
        _spellResponseCoroutine = StartCoroutine(ProcessUsedDiceResponses(usedPhysicalValue));
    }

    private IEnumerator ProcessRoundOpening()
    {
        _isSpellResponding = true;
        BattleManager.Instance?.SetDiceSpellResponseActive(true);

        List<PhysicsDice> elementDice = activeDiceList.FindAll(dice => IsSpell(dice, DiceSpellType.Elemental));
        foreach (PhysicsDice dice in elementDice)
        {
            if (dice == null) continue;
            yield return SplitElementDice(dice);
        }

        if (elementDice.Count > 0)
        {
            OrganizeDiceLayout(false);
            if (layoutTweenDuration > 0f)
                yield return new WaitForSeconds(layoutTweenDuration);
        }

        List<SpellResponse> blackCoffinResponses = BuildResponses(0, DiceSpellType.BlackCoffin);
        yield return PlayResponses(blackCoffinResponses);
        _isSpellResponding = false;
    }

    private IEnumerator ProcessUsedDiceResponses(int usedPhysicalValue)
    {
        _isSpellResponding = true;
        SetAllHandDrawSettled(false);
        BattleManager.Instance?.SetDiceSpellResponseActive(true);

        List<SpellResponse> responses = BuildResponses(usedPhysicalValue);
        yield return PlayResponses(responses);

        activeDiceList.RemoveAll(dice => dice == null);
        if (activeDiceList.Count > 0)
        {
            OrganizeDiceLayout();
        }
        else
        {
            _diceReadyForInput = true;
            BattleManager.Instance?.SetDiceSpellResponseActive(false);
        }

        _spellResponseCoroutine = null;
        _isSpellResponding = false;
    }

    private struct SpellResponse
    {
        public PhysicsDice dice;
        public DiceSpellSO spell;
        public int targetValue;
    }

    private List<SpellResponse> BuildResponses(int usedPhysicalValue, DiceSpellType? onlyType = null)
    {
        activeDiceList.RemoveAll(dice => dice == null);
        int remainingCount = activeDiceList.Count;
        List<SpellResponse> result = new List<SpellResponse>();

        foreach (PhysicsDice dice in activeDiceList)
        {
            DiceSpellSO spell = dice != null ? dice.Spell : null;
            if (spell == null || (onlyType.HasValue && spell.spellType != onlyType.Value)) continue;

            bool responds = onlyType.HasValue
                ? spell.spellType == onlyType.Value
                : spell.spellType == DiceSpellType.Nature
                    || spell.spellType == DiceSpellType.Alchemy
                    || spell.spellType == DiceSpellType.BlackCoffin
                    || spell.spellType == DiceSpellType.Arcane;
            if (!responds) continue;

            result.Add(new SpellResponse
            {
                dice = dice,
                spell = spell,
                targetValue = DiceSpellRules.GetTargetValue(spell.spellType, dice.PhysicalValue, usedPhysicalValue, remainingCount)
            });
        }

        return result;
    }

    private IEnumerator PlayResponses(List<SpellResponse> responses)
    {
        foreach (SpellResponse response in responses)
        {
            if (response.dice == null) continue;
            yield return PlayResponse(response);
            if (responseInterval > 0f)
                yield return new WaitForSeconds(responseInterval);
        }
    }

    private IEnumerator PlayResponse(SpellResponse response)
    {
        PhysicsDice dice = response.dice;
        SpawnSpellVfx(response.spell, dice.transform.position);

        switch (response.spell.spellType)
        {
            case DiceSpellType.Alchemy:
                yield return dice.transform.DOShakePosition(flipDuration * 0.45f, alchemyShakeStrength, 12, 45f).WaitForCompletion();
                yield return FlipToValue(dice, response.targetValue, flipDuration * 0.55f);
                break;
            case DiceSpellType.Arcane:
                dice.RollPhysicalInPlace(dice.FindFaceIndexForValue(response.targetValue), flipDuration, flipDuration * 0.35f, arcaneSpinSpeed);
                yield return new WaitUntil(() => dice == null || !dice.isRolling);
                break;
            default:
                yield return FlipToValue(dice, response.targetValue, flipDuration);
                break;
        }
    }

    private IEnumerator FlipToValue(PhysicsDice dice, int value, float duration)
    {
        int faceIndex = dice.FindFaceIndexForValue(value);
        if (faceIndex < 0) yield break;

        Quaternion target = dice.GetFaceUpRotation(faceIndex);
        Quaternion raised = Quaternion.AngleAxis(180f, Vector3.right) * target;
        yield return dice.transform.DORotateQuaternion(raised, duration * 0.55f).SetEase(Ease.InOutQuad).WaitForCompletion();
        dice.SetPhysicalResult(value);
        yield return dice.transform.DORotateQuaternion(target, duration * 0.45f).SetEase(Ease.OutCubic).WaitForCompletion();
    }

    private IEnumerator SplitElementDice(PhysicsDice source)
    {
        int count = Mathf.Clamp(source.PhysicalValue, 1, 6);
        Vector3 center = source.transform.position;
        Vector3 originalScale = source.transform.localScale;
        PlayerDice parentSource = source.sourceDataRef;
        DiceSpellSO sourceSpell = source.Spell;
        SpawnSpellVfx(source.Spell, center);

        yield return source.transform.DOScale(Vector3.zero, splitAnimationDuration).SetEase(Ease.InBack).WaitForCompletion();

        int sourceIndex = activeDiceList.IndexOf(source);
        activeDiceList.Remove(source);
        Destroy(source.gameObject);

        GameObject prefab = elementMiniDicePrefab != null ? elementMiniDicePrefab : dicePrefab;
        GameObject clusterObject = new GameObject($"ElementMiniCluster_{Time.frameCount}");
        clusterObject.transform.SetParent(_container);
        MiniDiceCluster cluster = clusterObject.AddComponent<MiniDiceCluster>();

        for (int i = 0; i < count; i++)
        {
            GameObject child = Instantiate(prefab, center, Quaternion.identity, _container);
            PhysicsDice mini = child.GetComponent<PhysicsDice>();
            if (mini == null)
            {
                Destroy(child);
                continue;
            }

            ApplyBattleHandDraw(mini);
            RuntimeDiceData data = BuildOnePointMiniData(sourceSpell);
            PlayerDice inspirationSource = i == 0 ? parentSource : null;
            mini.Initialize(data, inspirationSource);
            mini.ForceSetValue(0);
            mini.SnapFaceUp(0);
            mini.StopMotionAndSetKinematic(true);
            mini.miniCluster = cluster;
            cluster.members.Add(mini);
            activeDiceList.Insert(Mathf.Clamp(sourceIndex + i, 0, activeDiceList.Count), mini);

            Vector3 configuredScale = child.transform.localScale;
            child.transform.localScale = Vector3.zero;
            child.transform.DOScale(configuredScale == Vector3.zero ? originalScale * 0.45f : configuredScale, splitAnimationDuration).SetEase(Ease.OutBack);
        }

        yield return new WaitForSeconds(splitAnimationDuration);
    }

    private static RuntimeDiceData BuildOnePointMiniData(DiceSpellSO sourceSpell)
    {
        RuntimeDiceData data = new RuntimeDiceData
        {
            diceName = "元素小骰",
            bodyColor = sourceSpell != null ? sourceSpell.diceColor : Color.white
        };
        for (int i = 0; i < 6; i++)
            data.faces[i] = new DiceFaceData
            {
                value = 1,
                color = Color.black,
                icon = sourceSpell != null ? sourceSpell.GetFaceSprite(1) : null
            };
        return data;
    }

    private void SpawnSpellVfx(DiceSpellSO spell, Vector3 position)
    {
        if (spell == null || spell.triggerVfx == null) return;
        GameObject instance = Instantiate(spell.triggerVfx, position, Quaternion.identity);
        Destroy(instance, Mathf.Max(0.05f, spellVfxLifetime));
    }

    private static bool IsSpell(PhysicsDice dice, DiceSpellType type)
    {
        return dice != null && dice.Spell != null && dice.Spell.spellType == type;
    }

    private void ApplyBattleHandDraw(PhysicsDice dice)
    {
        if (!_battleDiceVisualsActive || dice == null) return;
        dice.EnableBattleHandDraw(battleDiceBodyMaterial, battleDiceFaceMaterial);
    }

    private void SetAllHandDrawSettled(bool settled)
    {
        if (!_battleDiceVisualsActive) return;
        foreach (PhysicsDice dice in activeDiceList)
            if (dice != null) dice.SetHandDrawSettled(settled);
    }

    private static void RemoveFromMiniCluster(PhysicsDice dice)
    {
        MiniDiceCluster cluster = dice.miniCluster;
        if (cluster == null) return;
        cluster.members.Remove(dice);
        if (cluster.members.Count == 0) Destroy(cluster.gameObject);
    }

    public PhysicsDice GetFirstAvailableBattleDice()
    {
        if (!AreDiceReadyForInput) return null;

        foreach (PhysicsDice dice in activeDiceList)
        {
            if (dice == null || !dice.gameObject.activeInHierarchy || dice.isRolling)
                continue;

            DiceDragger dragger = dice.GetComponent<DiceDragger>();
            if (dragger != null && dragger.enabled)
                return dice;
        }

        return null;
    }

    public void HighlightDice(PlayerDice targetData)
    {
        StopHighlight();
        foreach (var dice in activeDiceList)
        {
            if (dice == null) continue;
            if (dice.sourceDataRef == targetData)
            {
                _highlightedDiceList.Add(dice);
                if (dice.diceRenderer != null)
                {
                    Color originalColor = dice.diceRenderer.material.color;
                    _highlightOriginalColors[dice] = originalColor;
                    dice.diceRenderer.material.color = Color.Lerp(originalColor, Color.yellow, 0.45f);
                }
            }
        }
    }

    public void StopHighlight()
    {
        if (_highlightedDiceList.Count > 0)
        {
            foreach (var dice in _highlightedDiceList)
            {
                if (dice != null)
                {
                    if (dice.diceRenderer != null && _highlightOriginalColors.TryGetValue(dice, out Color originalColor))
                    {
                        dice.diceRenderer.material.color = originalColor;
                    }
                }
            }
            _highlightedDiceList.Clear();
            _highlightOriginalColors.Clear();
        }
    }
}
