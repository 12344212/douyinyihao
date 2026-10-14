using UnityEngine;

/// <summary>
/// 教程目标解析基类：给运行时才生成的目标（敌人、骰子、列表项）按 key 返回 Transform，
/// 以及按 key 检查“可以显示下一步”的条件。每个场景挂一个子类，在 TutorialManager 上引用。
/// </summary>
public class TutorialTargetProvider : MonoBehaviour
{
    /// <summary>按 key 解析目标；找不到（或还没生成）返回 null，TutorialManager 会每帧重试。</summary>
    public virtual Transform Resolve(string key)
    {
        return null;
    }

    /// <summary>按 key 检查条件；空 key 或未知 key 视为满足。</summary>
    public virtual bool CheckCondition(string key)
    {
        return true;
    }
}
