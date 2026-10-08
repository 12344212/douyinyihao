/// <summary>
/// 玩家在关卡面板点下的关卡。战斗场景以后读取 StageSelection.Current。
/// 章节和关卡都从 1 开始。
/// </summary>
public static class StageSelection
{
    public struct Pick
    {
        public int chapter;
        public int stage;
    }

    public static Pick Current { get; private set; }
    public static bool HasSelection { get; private set; }

    public static void Set(int chapter, int stage)
    {
        Current = new Pick { chapter = chapter, stage = stage };
        HasSelection = true;
    }
}