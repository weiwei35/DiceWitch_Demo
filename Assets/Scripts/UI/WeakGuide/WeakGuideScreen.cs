using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 普通界面的弱引导入口。
/// 按列表顺序选择第一个可用且尚未完成的目标，同一时间只显示一个。
/// </summary>
public sealed class WeakGuideScreen : MonoBehaviour
{
    [Tooltip("按优先级从高到低排列。大多数普通界面只需配置一个。")]
    public List<WeakGuideTarget> targets = new List<WeakGuideTarget>();

    private void OnEnable()
    {
        WeakGuideService.Instance?.ActivateScreen(this);
        RefreshGuide();
    }

    private void OnDisable()
    {
        WeakGuideService.Instance?.DeactivateScreen(this);
    }

    public void RefreshGuide()
    {
        WeakGuideService service = WeakGuideService.Instance;
        if (service == null) return;

        foreach (WeakGuideTarget target in targets)
        {
            if (target == null
                || string.IsNullOrWhiteSpace(target.guideId)
                || service.IsCompleted(target.guideId)
                || !target.IsAvailable)
                continue;

            if (target.Show(this))
                return;
        }

        service.ClearGuide(this);
    }

    public void SetModalBlocked(bool blocked)
    {
        WeakGuideService.Instance?.SetScreenSuspended(this, blocked);
        if (!blocked)
            RefreshGuide();
    }
}
