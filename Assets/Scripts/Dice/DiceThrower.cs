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
    public float diceSpacing = 1.2f;         
    public float layoutTweenDuration = 0.4f; 

    void Awake() { Instance = this; }

    private Transform _container;
    private List<PhysicsDice> _highlightedDiceList = new List<PhysicsDice>();
    private Dictionary<PhysicsDice, Color> _highlightOriginalColors = new Dictionary<PhysicsDice, Color>();
    private List<PhysicsDice> activeDiceList = new List<PhysicsDice>();

    private struct LayoutSlot
    {
        public PhysicsDice dice;
        public DiceSquadGroup squad;
    }
    
    private int _settledDiceCount = 0;
    private int _totalDiceExpected = 0; 
    private Coroutine _throwCoroutine;  

    public void RegisterDice(PhysicsDice dice)
    {
        if (!activeDiceList.Contains(dice))
        {
            AttachDiceToContainer(dice);
            activeDiceList.Add(dice);
            dice.OnDiceSettled += HandleSingleDiceSettled;
            _totalDiceExpected++;
        }
    }

    public void RegisterSettledDice(PhysicsDice dice)
    {
        if (dice == null || activeDiceList.Contains(dice)) return;

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
        float totalWidth = (count - 1) * diceSpacing;
        float startX = -totalWidth / 2f;
        Vector3 centerPos = layoutCenter != null ? layoutCenter.position : spawnPoint.position;

        for (int i = 0; i < count; i++)
        {
            var entry = diceEntries[i];
            Vector3 targetPos = centerPos + Vector3.right * (startX + i * diceSpacing);
            
            GameObject newDiceObj = Instantiate(dicePrefab, targetPos, Random.rotation);
            newDiceObj.transform.SetParent(_container);
        
            PhysicsDice pDice = newDiceObj.GetComponent<PhysicsDice>();
            if (pDice != null)
            {
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
        Vector3 targetPos = layoutCenter != null ? layoutCenter.position : spawnPoint.position;
        GameObject newDiceObj = Instantiate(dicePrefab, targetPos, Random.rotation);
        if (_container != null) newDiceObj.transform.SetParent(_container);

        PhysicsDice pDice = newDiceObj.GetComponent<PhysicsDice>();
        if (pDice != null)
        {
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
        RollDiceInPlace(dice, 0);
    }

    public void RollDiceInPlace(PhysicsDice dice, int stopOrderIndex = 0)
    {
        if (dice == null) return;

        int resultFaceIndex = dice.DetermineRollResultFaceIndex();
        float duration = Mathf.Max(rollSettleDuration, rollSpinDuration + Mathf.Max(0, stopOrderIndex) * rollStopInterval);
        dice.RollInPlace(resultFaceIndex, duration, rollSettleDuration, randomSpinSpeed);
    }

    // ---------------------------------------------------------
    // 阶段 3：落地静止后，重新吸附排队
    // ---------------------------------------------------------
    private void HandleSingleDiceSettled(int value)
    {
        _settledDiceCount++;
        TryOrganizeDiceLayout();
    }

    private void OrganizeDiceLayout()
    {
        // 清理已被外部销毁的骰子引用
        activeDiceList.RemoveAll(d => d == null);

        List<LayoutSlot> layoutSlots = BuildLayoutSlots();

        int count = layoutSlots.Count;
        if (count == 0) return;

        float totalWidth = (count - 1) * diceSpacing;
        float startX = -totalWidth / 2f;
        Vector3 centerPos = layoutCenter != null ? layoutCenter.position : spawnPoint.position - Vector3.up * 2f;

        for (int i = 0; i < count; i++)
        {
            LayoutSlot slot = layoutSlots[i];

            // 再次冻结物理，让动画接管整理
            Vector3 targetPos = centerPos + Vector3.right * (startX + i * diceSpacing);

            if (slot.squad != null)
            {
                slot.squad.ArrangeAt(targetPos, layoutTweenDuration);
                continue;
            }

            PhysicsDice dice = slot.dice;
            if (dice == null) continue;

            dice.StopMotionAndSetKinematic(true);

            dice.transform.DOMove(targetPos, layoutTweenDuration).SetEase(Ease.OutQuad);
            dice.transform.DORotateQuaternion(dice.GetCurrentResultRotation(), layoutTweenDuration).SetEase(Ease.OutQuad);
        }

    }

    private List<LayoutSlot> BuildLayoutSlots()
    {
        List<LayoutSlot> slots = new List<LayoutSlot>();
        HashSet<DiceSquadGroup> addedSquads = new HashSet<DiceSquadGroup>();

        foreach (var dice in activeDiceList)
        {
            if (dice == null) continue;

            DiceDragger dragger = dice.GetComponent<DiceDragger>();
            DiceSquadGroup squad = dragger != null ? dragger.squadGroup : null;

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

        StopHighlight();
        _settledDiceCount = 0; 
        _totalDiceExpected = 0;

        for (int i = activeDiceList.Count - 1; i >= 0; i--)
        {
            if (activeDiceList[i] != null) Destroy(activeDiceList[i].gameObject);
        }
        activeDiceList.Clear();

        var allSquads = FindObjectsOfType<DiceSquadGroup>();
        foreach (var squad in allSquads) Destroy(squad.gameObject);
    }

    public int GetValidDiceCount()
    {
        int count = 0;
        foreach (var dice in activeDiceList)
            if (dice != null && dice.gameObject != null) count++;
        return count;
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
