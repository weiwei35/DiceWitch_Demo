using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 弱引导的全局状态与持久化服务。
/// 负责保存已完成引导、仲裁最上层界面，以及启动或停止当前目标的视觉效果。
/// </summary>
[DefaultExecutionOrder(-450)]
public sealed class WeakGuideService : MonoBehaviour
{
    private const string ProgressKey = "DiceWitch.WeakGuideProgress.v1";
    private const int ProgressVersion = 1;

    public static WeakGuideService Instance { get; private set; }

    [Header("Default Visual")]
    [Min(1f)] public float pulseScale = 1.1f;
    [Min(0.1f)] public float pulseDuration = 0.9f;
    public Color glowColor = new Color(1f, 0.96f, 0.8f, 1f);
    [Range(0f, 1f)] public float glowMinAlpha = 0.2f;
    [Range(0f, 1f)] public float glowMaxAlpha = 0.72f;
    [Min(0f)] public float haloPadding = 8f;
    [Min(1f)] public float haloTextureRadius = 4f;

    [Header("Hold Guide")]
    [Min(1f)] public float holdChargeStartScale = 1.35f;
    [Min(0.1f)] public float holdChargeDuration = 0.9f;
    [Min(0.01f)] public float holdChargeFadeDuration = 0.12f;

    private readonly HashSet<string> _completedGuideIds = new HashSet<string>();
    private readonly List<ScreenEntry> _screenStack = new List<ScreenEntry>();
    private ScreenEntry _presentedEntry;

    [Serializable]
    private sealed class ProgressData
    {
        public int version = ProgressVersion;
        public List<string> completedGuideIds = new List<string>();
    }

    private sealed class ScreenEntry
    {
        public UnityEngine.Object owner;
        public bool suspended;
        public string guideId;
        public readonly List<WeakGuideEffect> effects = new List<WeakGuideEffect>();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;

        GameObject serviceObject = new GameObject(nameof(WeakGuideService));
        serviceObject.AddComponent<WeakGuideService>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        LoadProgress();
    }

    /// <summary>
    /// 将界面放到弱引导界面栈顶。
    /// </summary>
    public void ActivateScreen(UnityEngine.Object owner)
    {
        if (owner == null) return;

        CleanupDestroyedOwners();
        ScreenEntry entry = FindEntry(owner);
        if (entry == null)
        {
            entry = new ScreenEntry { owner = owner };
            _screenStack.Add(entry);
        }
        else
        {
            _screenStack.Remove(entry);
            _screenStack.Add(entry);
        }

        entry.suspended = false;
        RefreshPresentation();
    }

    /// <summary>
    /// 移除界面及其尚未完成的视觉表现，不会写入完成记录。
    /// </summary>
    public void DeactivateScreen(UnityEngine.Object owner)
    {
        ScreenEntry entry = FindEntry(owner);
        if (entry == null) return;

        if (_presentedEntry == entry)
            StopPresentedEffect();

        StopEntryEffects(entry);
        _screenStack.Remove(entry);
        RefreshPresentation();
    }

    /// <summary>
    /// 暂停或恢复界面引导。用于该界面上方打开材料背包等模态弹窗。
    /// </summary>
    public void SetScreenSuspended(UnityEngine.Object owner, bool suspended)
    {
        ScreenEntry entry = FindEntry(owner);
        if (entry == null) return;

        entry.suspended = suspended;
        RefreshPresentation();
    }

    /// <summary>
    /// 请求显示指定引导。已完成的 ID 不会再次播放。
    /// </summary>
    /// <returns>当前引导尚未完成且请求有效时返回 true。</returns>
    public bool ShowGuide(
        UnityEngine.Object owner,
        string guideId,
        RectTransform scaleTarget,
        Graphic glowGraphic,
        bool useGraphicAlpha = true,
        WeakGuideVisualMode visualMode = WeakGuideVisualMode.Pulse)
    {
        if (owner == null || string.IsNullOrWhiteSpace(guideId) || scaleTarget == null)
            return false;

        WeakGuideEffect effect = WeakGuideEffect.GetOrCreate(
            scaleTarget,
            glowGraphic,
            useGraphicAlpha,
            visualMode);
        return ShowGuide(owner, guideId, new[] { effect });
    }

    /// <summary>
    /// 请求同一个引导 ID 同时显示多个视觉目标。
    /// </summary>
    public bool ShowGuide(
        UnityEngine.Object owner,
        string guideId,
        IReadOnlyList<WeakGuideEffect> effects)
    {
        if (owner == null || string.IsNullOrWhiteSpace(guideId) || effects == null)
            return false;

        if (IsCompleted(guideId))
        {
            ClearGuide(owner);
            return false;
        }

        ScreenEntry entry = FindEntry(owner);
        if (entry == null)
        {
            ActivateScreen(owner);
            entry = FindEntry(owner);
        }

        List<WeakGuideEffect> validEffects = new List<WeakGuideEffect>();
        for (int i = 0; i < effects.Count; i++)
        {
            WeakGuideEffect effect = effects[i];
            if (effect != null && !validEffects.Contains(effect))
                validEffects.Add(effect);
        }
        if (validEffects.Count == 0)
            return false;

        if (entry.guideId == guideId && HasSameEffects(entry.effects, validEffects))
        {
            RefreshPresentation();
            return true;
        }

        StopEntryEffects(entry);
        entry.guideId = guideId;
        entry.effects.AddRange(validEffects);
        RefreshPresentation();
        return true;
    }

    /// <summary>
    /// 清除界面当前的引导请求，不会将其标记为完成。
    /// </summary>
    public void ClearGuide(UnityEngine.Object owner)
    {
        ScreenEntry entry = FindEntry(owner);
        if (entry == null) return;

        if (_presentedEntry == entry)
            StopPresentedEffect();

        StopEntryEffects(entry);
        entry.guideId = null;
        RefreshPresentation();
    }

    /// <summary>
    /// 将业务动作成功对应的引导永久标记为完成。
    /// </summary>
    /// <returns>本次首次写入完成记录时返回 true。</returns>
    public bool CompleteGuide(string guideId)
    {
        if (string.IsNullOrWhiteSpace(guideId)) return false;
        if (!_completedGuideIds.Add(guideId)) return false;

        SaveProgress();
        for (int i = 0; i < _screenStack.Count; i++)
        {
            ScreenEntry entry = _screenStack[i];
            if (entry.guideId != guideId) continue;

            if (_presentedEntry == entry)
                StopPresentedEffect();

            StopEntryEffects(entry);
            entry.guideId = null;
        }

        RefreshPresentation();
        return true;
    }

    public bool IsCompleted(string guideId)
    {
        return !string.IsNullOrWhiteSpace(guideId) && _completedGuideIds.Contains(guideId);
    }

    [ContextMenu("开发/清除全部弱引导记录")]
    public void ResetAllProgressForDevelopment()
    {
        _completedGuideIds.Clear();
        PlayerPrefs.DeleteKey(ProgressKey);
        PlayerPrefs.Save();
        RefreshPresentation();
        Debug.Log("已清除全部弱引导完成记录。");
    }

    private void RefreshPresentation()
    {
        CleanupDestroyedOwners();
        ScreenEntry topEntry = _screenStack.Count > 0 ? _screenStack[_screenStack.Count - 1] : null;
        ScreenEntry nextEntry = topEntry != null
            && !topEntry.suspended
            && !string.IsNullOrWhiteSpace(topEntry.guideId)
            && !IsCompleted(topEntry.guideId)
            && topEntry.effects.Count > 0
                ? topEntry
                : null;

        if (_presentedEntry == nextEntry)
        {
            if (_presentedEntry != null)
                PlayEntryEffects(_presentedEntry);
            return;
        }

        StopPresentedEffect();
        _presentedEntry = nextEntry;
        if (_presentedEntry != null)
            PlayEntryEffects(_presentedEntry);
    }

    private void StopPresentedEffect()
    {
        if (_presentedEntry != null)
            StopEntryEffects(_presentedEntry, clear: false);

        _presentedEntry = null;
    }

    private void PlayEntryEffects(ScreenEntry entry)
    {
        for (int i = 0; i < entry.effects.Count; i++)
        {
            if (entry.effects[i] != null)
                entry.effects[i].PlayGuide(this);
        }
    }

    private static void StopEntryEffects(
        ScreenEntry entry,
        bool immediate = false,
        bool clear = true)
    {
        for (int i = 0; i < entry.effects.Count; i++)
        {
            if (entry.effects[i] != null)
                entry.effects[i].StopGuide(immediate);
        }
        if (clear)
            entry.effects.Clear();
    }

    private static bool HasSameEffects(
        IReadOnlyList<WeakGuideEffect> current,
        IReadOnlyList<WeakGuideEffect> next)
    {
        if (current.Count != next.Count) return false;
        for (int i = 0; i < current.Count; i++)
        {
            if (current[i] != next[i])
                return false;
        }
        return true;
    }

    private ScreenEntry FindEntry(UnityEngine.Object owner)
    {
        if (owner == null) return null;
        for (int i = 0; i < _screenStack.Count; i++)
        {
            if (_screenStack[i].owner == owner)
                return _screenStack[i];
        }

        return null;
    }

    private void CleanupDestroyedOwners()
    {
        for (int i = _screenStack.Count - 1; i >= 0; i--)
        {
            ScreenEntry entry = _screenStack[i];
            if (entry.owner != null) continue;

            if (_presentedEntry == entry)
                StopPresentedEffect();

            StopEntryEffects(entry, immediate: true);
            _screenStack.RemoveAt(i);
        }
    }

    private void LoadProgress()
    {
        _completedGuideIds.Clear();
        string json = PlayerPrefs.GetString(ProgressKey, string.Empty);
        if (string.IsNullOrWhiteSpace(json)) return;

        try
        {
            ProgressData data = JsonUtility.FromJson<ProgressData>(json);
            if (data?.completedGuideIds == null) return;

            foreach (string guideId in data.completedGuideIds)
            {
                if (!string.IsNullOrWhiteSpace(guideId))
                    _completedGuideIds.Add(guideId);
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"弱引导记录读取失败，将使用空记录：{exception.Message}");
        }
    }

    private void SaveProgress()
    {
        ProgressData data = new ProgressData
        {
            completedGuideIds = new List<string>(_completedGuideIds)
        };
        PlayerPrefs.SetString(ProgressKey, JsonUtility.ToJson(data));
        PlayerPrefs.Save();
    }
}
