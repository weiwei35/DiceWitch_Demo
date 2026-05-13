using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;

public class DiceThrower : MonoBehaviour
{
    public static DiceThrower Instance;

[Header("Spawn & Fly Settings (入场设定)")]
    public GameObject dicePrefab; 
    public Transform spawnPoint;             // 骰子飞出的起点（比如屏幕外、或者角色的手/袋子）
    public float flyInDuration = 0.3f;       // 飞入排列位置的时长
    public float flyInDelay = 0.05f;         // 每个骰子飞出的间隔 (像发牌一样)[Header("Pop & Roll Settings (原地弹跳设定)")]
    public float upwardForce = 8f;           // 向上弹跳的力度 (给大一点，让它们飞高点)
    public float horizontalScatter = 1.5f;   // 给一点微小的横向随机力，防止它们在空中完美重叠
    public float torqueForce = 80f;          // 疯狂旋转的扭矩
    public float popDelay = 0.05f;           // 弹跳起飞的连发间隔，制造“波浪”起飞感[Header("Layout Settings (整理排版)")]
    public Transform layoutCenter;           // 最终整齐排列的中心点
    public float diceSpacing = 1.2f;         
    public float layoutTweenDuration = 0.4f; 

    void Awake() { Instance = this; }

    private Transform _container;
    private List<PhysicsDice> _highlightedDiceList = new List<PhysicsDice>();
    private Vector3 _originalScale;
    private List<PhysicsDice> activeDiceList = new List<PhysicsDice>();
    
    private int _settledDiceCount = 0;
    private int _totalDiceExpected = 0; 
    private Coroutine _throwCoroutine;  

    public void RegisterDice(PhysicsDice dice)
    {
        if (!activeDiceList.Contains(dice))
        {
            activeDiceList.Add(dice);
            dice.OnDiceSettled += HandleSingleDiceSettled;
            _totalDiceExpected++;
        }
    }

    public bool IsAnyDiceRolling()
    {
        foreach (var dice in activeDiceList)
            if (dice != null && dice.isRolling) return true;
        return false;
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
    // 【核心演出】飞入排列 -> 停顿 -> 原地起飞翻滚
    // =========================================================
    private IEnumerator CinematicThrowSequence(List<BattleDiceEntry> diceEntries)
    {
        int count = diceEntries.Count;
        float totalWidth = (count - 1) * diceSpacing;
        float startX = -totalWidth / 2f;
        Vector3 centerPos = layoutCenter != null ? layoutCenter.position : spawnPoint.position;

        // ---------------------------------------------------------
        // 阶段 1：从生成点飞入，在半空中或地上排成一排
        // ---------------------------------------------------------
        for (int i = 0; i < count; i++)
        {
            var entry = diceEntries[i];
            Vector3 targetPos = centerPos + Vector3.right * (startX + i * diceSpacing);
            
            GameObject newDiceObj = Instantiate(dicePrefab, spawnPoint.position, Quaternion.identity);
            newDiceObj.transform.SetParent(_container);
        
            PhysicsDice pDice = newDiceObj.GetComponent<PhysicsDice>();
            if (pDice != null)
            {
                pDice.Initialize(entry.combatData, entry.sourceRef); 
                activeDiceList.Add(pDice);
                pDice.OnDiceSettled += HandleSingleDiceSettled;
                
                Rigidbody rb = pDice.GetComponent<Rigidbody>();
                if (rb != null) rb.isKinematic = true; 

                // 飞入动画 (改用 OutQuad 会让减速更平滑，接下来的起飞更自然)
                newDiceObj.transform.DOMove(targetPos, flyInDuration).SetEase(Ease.OutQuad);
            }

            // 发牌间隔
            yield return new WaitForSeconds(flyInDelay);
        }

        // ---------------------------------------------------------
        // 【关键修复】精确计算最后一次发牌的剩余飞行时间，去掉死板的停顿！
        // ---------------------------------------------------------
        float remainingTime = flyInDuration - flyInDelay;
        if (remainingTime > 0)
        {
            yield return new WaitForSeconds(remainingTime);
        }

        // ---------------------------------------------------------
        // 阶段 2：无缝衔接，原地“砰”地爆开起飞翻滚！
        // ---------------------------------------------------------
        for (int i = 0; i < activeDiceList.Count; i++)
        {
            PhysicsDice pDice = activeDiceList[i];
            if (pDice == null) continue;

            Vector3 randomScatter = new Vector3(Random.Range(-1f, 1f), 0, Random.Range(-1f, 1f)) * horizontalScatter;
            Vector3 force = Vector3.up * upwardForce + randomScatter;
            Vector3 torque = Random.insideUnitSphere * torqueForce;
            
            pDice.Roll(force, torque);
            
            // 极短的连发起飞间隔，形成波浪感
            yield return new WaitForSeconds(popDelay);
        }
    }

    public PhysicsDice SpawnSingleDice(RuntimeDiceData data, PlayerDice sourceRef = null)
    {
        // 幽灵骰子/单体生成也可以复用原地弹起逻辑
        Vector3 targetPos = layoutCenter != null ? layoutCenter.position : spawnPoint.position;
        GameObject newDiceObj = Instantiate(dicePrefab, targetPos,Quaternion.identity);
        if (_container != null) newDiceObj.transform.SetParent(_container);

        PhysicsDice pDice = newDiceObj.GetComponent<PhysicsDice>();
        if (pDice != null)
        {
            pDice.Initialize(data, sourceRef);
            activeDiceList.Add(pDice);
            pDice.OnDiceSettled += HandleSingleDiceSettled;
            _totalDiceExpected++;

            Vector3 force = Vector3.up * upwardForce + new Vector3(Random.Range(-1f,1f), 0, Random.Range(-1f,1f)) * horizontalScatter;
            Vector3 torque = Random.insideUnitSphere * torqueForce;
            pDice.Roll(force, torque);

            return pDice;
        }
        return null;
    }

    // ---------------------------------------------------------
    // 阶段 3：落地静止后，重新吸附排队
    // ---------------------------------------------------------
    private void HandleSingleDiceSettled(int value)
    {
        _settledDiceCount++;
        
        if (_settledDiceCount >= _totalDiceExpected && _totalDiceExpected > 0)
        {
            OrganizeDiceLayout();
        }
    }

    private void OrganizeDiceLayout()
    {
        // 清理已被外部销毁的骰子引用
        activeDiceList.RemoveAll(d => d == null);

        int count = activeDiceList.Count;
        if (count == 0) return;

        float totalWidth = (count - 1) * diceSpacing;
        float startX = -totalWidth / 2f;
        Vector3 centerPos = layoutCenter != null ? layoutCenter.position : spawnPoint.position - Vector3.up * 2f;

        Camera cam = Camera.main;

        for (int i = 0; i < count; i++)
        {
            PhysicsDice dice = activeDiceList[i];
            if (dice == null) continue;

            // 再次冻结物理，让动画接管整理
            Rigidbody rb = dice.GetComponent<Rigidbody>();
            if (rb != null) rb.isKinematic = true;

            Vector3 targetPos = centerPos + Vector3.right * (startX + i * diceSpacing);

            // 小幅度跳跃回到完美阵型
            dice.transform.DOJump(targetPos, 0.1f, 1, layoutTweenDuration).SetEase(Ease.OutQuad);

            // 角度取整放平 (将倾斜的骰子”咔哒”一声拉正)
            Vector3 euler = dice.transform.eulerAngles;
            euler.x = Mathf.Round(euler.x / 90f) * 90f;
            euler.y = Mathf.Round(euler.y / 90f) * 90f;
            euler.z = Mathf.Round(euler.z / 90f) * 90f;

            euler = OrientUpFaceToCamera(dice, euler, cam);

            dice.transform.DORotate(euler, layoutTweenDuration).SetEase(Ease.OutQuad);
        }
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
                dice.transform.DOKill();
                _originalScale = dice.transform.localScale;
                dice.transform.DOScale(dice.transform.localScale * 1.3f, 0.2f).SetLoops(-1, LoopType.Yoyo).SetLink(dice.gameObject);
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
                    dice.transform.DOKill();
                    dice.transform.localScale = _originalScale;
                }
            }
            _highlightedDiceList.Clear();
        }
    }
}