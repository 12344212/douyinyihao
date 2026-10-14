using System;
using UnityEngine;

/// <summary>步骤推进方式。</summary>
public enum TutorialAdvance
{
    /// <summary>自动：无目标 → 点任意处；目标有 Button → 点按钮；其它目标 → 点镂空区域。</summary>
    Auto = 0,
    /// <summary>点遮罩或镂空任意处继续（纯说明步骤）。</summary>
    Tap = 1,
    /// <summary>等待游戏事件（eventKey，例如 battle.enemySelected）。</summary>
    Event = 2
}

/// <summary>
/// 单个教程步骤（在 TutorialManager 的 Inspector 列表里配置，每个场景一套）。
/// 目标二选一：target（场景里拖引用）或 targetKey（运行时由 TutorialTargetProvider 解析，如敌人、骰子、列表项）。
/// 两者都为空 = 纯文本步骤（全屏遮罩）。
/// </summary>
[Serializable]
public class TutorialStep
{
    [Tooltip("备注，仅方便在 Inspector 里区分步骤")]
    public string note;

    [Tooltip("场景里的高亮目标（UI）；为空时看 targetKey")]
    public RectTransform target;

    [Tooltip("运行时解析的目标 key（UI 或世界物体），见场景里的 TutorialTargetProvider")]
    public string targetKey;

    [Tooltip("对话框英文文案")]
    [TextArea(2, 4)]
    public string text;

    [Tooltip("对话框角色立绘；为空时保留场景里的占位图")]
    public Sprite portrait;

    [Tooltip("推进方式")]
    public TutorialAdvance advance = TutorialAdvance.Auto;

    [Tooltip("advance = Event 时等待的事件 key")]
    public string eventKey;

    [Tooltip("显示前等待的条件 key（如 battle.idle）；为空不等")]
    public string waitFor;

    [Tooltip("显示前额外等待的秒数（真实时间）")]
    public float showDelay;

    [Tooltip("中途退出后重放时跳过此步（例如已经升过级）")]
    public bool skipOnReplay;

    [Tooltip("条件 key 成立时直接跳过此步（开始时和显示期间都检查），如 battle.chartOpen / role.weaponEquipped")]
    public string skipWhen;

    [Tooltip("目标在开始时不可见、或显示期间消失（面板被提前关闭）时跳到下一步，而不是一直等待")]
    public bool skipIfTargetGone;

    [Tooltip("自由操作步骤：关闭全屏拦截与遮罩，玩家正常游玩；收到 conditionKey 通知后完成，恢复拦截并进入下一步")]
    public bool freePlay;

    [Tooltip("freePlay 完成条件：TutorialManager.NotifyCondition(key) 的 key，多个用 | 分隔（如 boss_defeated|battle_lost）")]
    public string conditionKey;

    [Tooltip("挖洞在目标四周额外留出的像素（教程画布坐标）")]
    public float holePadding = 16f;

    /// <summary>是否配置了目标（场景引用或运行时 key）。</summary>
    public bool HasTarget
    {
        get { return target != null || !string.IsNullOrEmpty(targetKey); }
    }
}
