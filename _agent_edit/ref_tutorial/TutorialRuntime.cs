using System;

/// <summary>
/// 本次进程内的新手教程步骤。完成标记在存档里；未完成时冷启动由 LocalSaveManager 回滚。
/// </summary>
public static class TutorialRuntime
{
    public const int OpeningCardId = 1;

    public enum Step
    {
        None = 0,
        OpenDeck,
        UpgradeCard,
        CloseDeck,
        EnterBattle,
        SelectStage,
        ExplainLevel,
        ExplainSilver,
        ExplainEnergy,
        DragCard,
        EndTurn,
        WaitDealForRefresh,
        RefreshCards
    }

    public static Step Current { get; private set; }

    public static bool IsRunning =>
        LocalSaveManager.Instance != null && !LocalSaveManager.Instance.TutorialCompleted;

    /// <summary>未完成教程时，本局第一手必出指定卡。</summary>
    public static bool ShouldForceOpeningCard => IsRunning;

    public static bool RequiresEnemyDrop => IsRunning && Current == Step.DragCard;

    public static event Action<int> CardPlayed;

    public static void BeginMain()
    {
        Current = IsRunning ? Step.OpenDeck : Step.None;
    }

    public static void BeginBattle()
    {
        Current = IsRunning ? Step.ExplainLevel : Step.None;
    }

    public static void Set(Step step)
    {
        Current = step;
    }

    public static void Complete()
    {
        Current = Step.None;
        if (LocalSaveManager.Instance != null)
        {
            LocalSaveManager.Instance.CompleteTutorial();
        }
    }

    public static void NotifyCardPlayed(int cardId)
    {
        CardPlayed?.Invoke(cardId);
    }

    /// <summary>0 表示不限制；正数只允许该 cardId；教程其它步骤禁止拖牌。</summary>
    public static bool AllowsCardDrag(int cardId)
    {
        if (!IsRunning)
        {
            return true;
        }

        if (Current != Step.DragCard)
        {
            return false;
        }

        return cardId == OpeningCardId;
    }
}
