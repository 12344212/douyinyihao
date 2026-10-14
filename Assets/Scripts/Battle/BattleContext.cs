using UnityEngine;

/// <summary>
/// Current battle stage index (0 = stage 1-1) for tutorial gates.
/// </summary>
public static class BattleContext
{
    public static int CurrentStage
    {
        get
        {
            if (!StageSelection.HasSelection)
            {
                return 0;
            }

            StageSelection.Pick pick = StageSelection.Current;
            return BattleTuning.StageIndex(pick.chapter, pick.stage);
        }
    }

    public static int CurrentChapter
    {
        get { return StageSelection.HasSelection ? StageSelection.Current.chapter : 1; }
    }

    public static int CurrentStageNumber
    {
        get { return StageSelection.HasSelection ? StageSelection.Current.stage : 1; }
    }
}
