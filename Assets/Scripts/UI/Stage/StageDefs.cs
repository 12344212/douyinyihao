/// <summary>
/// 关卡表：3 章 × 每章 10 关，一页 5 个。编号从 1 开始，与 LocalSaveManager 的 ChapterCount / StagesPerChapter 一致。
/// 这里只有展示用的名字，没有战斗内容。
/// </summary>
public static class StageDefs
{
    public const int ChapterCount = 3;
    public const int StagesPerChapter = 10;
    public const int SlotsPerPage = 5;
    public const int PagesPerChapter = StagesPerChapter / SlotsPerPage;

    public struct Info
    {
        public int chapter;
        public int stage;
        public string displayName;
    }

    public static string ChapterName(int chapter)
    {
        return "Chapter " + chapter;
    }

    /// <summary>关卡标签，如 1-1。</summary>
    public static string FormatLabel(int chapter, int stage)
    {
        return chapter + "-" + stage;
    }

    public static bool IsValid(int chapter, int stage)
    {
        return chapter >= 1 && chapter <= ChapterCount && stage >= 1 && stage <= StagesPerChapter;
    }

    public static Info Get(int chapter, int stage)
    {
        return new Info
        {
            chapter = chapter,
            stage = stage,
            displayName = FormatLabel(chapter, stage)
        };
    }
}