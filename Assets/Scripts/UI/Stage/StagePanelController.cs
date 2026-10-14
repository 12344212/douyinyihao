using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Stage select: 5 slots per page. L/R pages -5/+5 across chapters in order:
/// 1-1..1-5, 1-6..1-10, 2-1..2-5, 2-6..2-10, 3-1..3-5, 3-6..3-10 (max ch3 st10).
/// Only refreshes slot contents; does not recreate the panel.
/// Unlock/clear from LocalSaveManager. TZ badge via StageSlot when stage is the current
/// max challengeable and not cleared. StageWaterFloat for slots; StageCloudFly for yun.
/// </summary>
public class StagePanelController : MonoBehaviour
{
    [Header("Slots")]
    [SerializeField] private StageSlot[] slots = new StageSlot[StageDefs.SlotsPerPage];

    [Header("Chapter (optional)")]
    [SerializeField] private TMP_Text chapterTitle;
    [SerializeField] private Button[] chapterButtons = new Button[StageDefs.ChapterCount];
    [SerializeField] private Color chapterSelectedColor = new Color(0.85f, 0.55f, 0.20f, 1f);
    [SerializeField] private Color chapterNormalColor = new Color(0.30f, 0.34f, 0.40f, 1f);

    [Header("Paging")]
    [SerializeField] private Button prevButton;
    [SerializeField] private Button nextButton;
    [SerializeField] private TMP_Text pageLabel;

    [Header("Motion (optional)")]
    [SerializeField] private StageWaterFloat waterFloat;
    [SerializeField] private StageCloudFly cloudFly;

    [Header("Flow")]
    [SerializeField] private string battleSceneName = "BattleScene";

    private int _chapter = 1;
    private int _page;
    private bool _loading;
    private bool _warnedMissingSave;

    private void Awake()
    {
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] != null)
            {
                slots[i].SetClickHandler(OnSlotClicked);
            }
        }

        for (int i = 0; i < chapterButtons.Length; i++)
        {
            int chapter = i + 1;
            if (chapterButtons[i] != null)
            {
                chapterButtons[i].onClick.AddListener(() => SelectChapter(chapter));
            }
        }

        if (prevButton != null)
        {
            prevButton.onClick.AddListener(OnPrev);
        }

        if (nextButton != null)
        {
            nextButton.onClick.AddListener(OnNext);
        }
    }

    private void OnEnable()
    {
        _loading = false;
        Refresh();
        if (waterFloat != null)
        {
            waterFloat.StartMotion();
        }

        if (cloudFly != null)
        {
            cloudFly.StartMotion();
        }
    }

    private void OnDestroy()
    {
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] != null)
            {
                slots[i].SetClickHandler(null);
            }
        }

        if (prevButton != null)
        {
            prevButton.onClick.RemoveListener(OnPrev);
        }

        if (nextButton != null)
        {
            nextButton.onClick.RemoveListener(OnNext);
        }
    }

    public void SelectChapter(int chapter)
    {
        if (chapter < 1 || chapter > StageDefs.ChapterCount || chapter == _chapter)
        {
            return;
        }

        _chapter = chapter;
        _page = 0;
        Refresh();
    }

    private void OnPrev()
    {
        if (_page > 0)
        {
            _page--;
            Refresh();
            return;
        }

        if (_chapter <= 1)
        {
            return;
        }

        _chapter--;
        _page = StageDefs.PagesPerChapter - 1;
        Refresh();
    }

    private void OnNext()
    {
        if (_page < StageDefs.PagesPerChapter - 1)
        {
            _page++;
            Refresh();
            return;
        }

        if (_chapter >= StageDefs.ChapterCount)
        {
            return;
        }

        _chapter++;
        _page = 0;
        Refresh();
    }

    private void OnSlotClicked(StageSlot slot)
    {
        if (slot == null || _loading)
        {
            return;
        }

        if (!slot.IsUnlocked)
        {
            TipPanelUI.ShowTip("Locked");
            return;
        }

        EnterStage(slot.Chapter, slot.Stage);
    }

    private void EnterStage(int chapter, int stage)
    {
        LocalSaveManager save = LocalSaveManager.Instance;
        if (save == null || !save.IsStageUnlocked(chapter, stage))
        {
            TipPanelUI.ShowTip("Locked");
            return;
        }

        if (string.IsNullOrEmpty(battleSceneName) || !Application.CanStreamedLevelBeLoaded(battleSceneName))
        {
            Debug.LogError("[StagePanelController] Scene '" + battleSceneName + "' is not in Build Settings.");
            return;
        }

        StageSelection.Set(chapter, stage);
        _loading = true;
        StartCoroutine(LoadBattle());
    }

    private IEnumerator LoadBattle()
    {
        AsyncOperation op = SceneManager.LoadSceneAsync(battleSceneName);
        while (op != null && !op.isDone)
        {
            yield return null;
        }
    }

    private void Refresh()
    {
        LocalSaveManager save = LocalSaveManager.Instance;
        if (save == null && !_warnedMissingSave)
        {
            _warnedMissingSave = true;
            Debug.LogWarning("[StagePanelController] LocalSaveManager not found. Start from LoadScene. All stages show as locked.");
        }

        if (chapterTitle != null)
        {
            chapterTitle.text = StageDefs.ChapterName(_chapter);
        }

        for (int i = 0; i < chapterButtons.Length; i++)
        {
            if (chapterButtons[i] == null)
            {
                continue;
            }

            Image image = chapterButtons[i].targetGraphic as Image;
            if (image != null)
            {
                image.color = (i + 1) == _chapter ? chapterSelectedColor : chapterNormalColor;
            }
        }

        int frontierChapter = save != null ? save.HighestUnlockedChapter : 1;
        int frontierStage = save != null ? save.HighestUnlockedStage : 1;
        bool allCleared = save != null && save.AllStagesCleared;

        int startStage = _page * StageDefs.SlotsPerPage + 1;
        for (int i = 0; i < StageDefs.SlotsPerPage; i++)
        {
            if (slots == null || i >= slots.Length || slots[i] == null)
            {
                continue;
            }

            int stage = startStage + i;
            bool unlocked = save != null && save.IsStageUnlocked(_chapter, stage);
            bool cleared = save != null && save.IsStageCleared(_chapter, stage);
            // TZ: current max challengeable, not cleared yet.
            bool showChallenge = unlocked && !cleared && !allCleared
                && _chapter == frontierChapter && stage == frontierStage;
            slots[i].Bind(_chapter, stage, unlocked, cleared, showChallenge);
        }

        if (pageLabel != null)
        {
            int globalPage = (_chapter - 1) * StageDefs.PagesPerChapter + _page;
            pageLabel.text = (globalPage + 1) + " / " + StageDefs.TotalPages;
        }

        if (prevButton != null)
        {
            prevButton.interactable = _page > 0 || _chapter > 1;
        }

        if (nextButton != null)
        {
            nextButton.interactable = _page < StageDefs.PagesPerChapter - 1
                || _chapter < StageDefs.ChapterCount;
        }
    }
}