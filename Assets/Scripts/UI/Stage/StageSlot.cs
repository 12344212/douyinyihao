using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One stage cell: number label, Lock (suo), Clear checkmark, TZ challenge badge.
/// TZ shows when this stage is the current max challengeable and not cleared yet.
/// Locked clicks notify the panel; they do not enter battle. Refs are assigned in the editor.
/// </summary>
public class StageSlot : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private Image background;
    [SerializeField] private TMP_Text numberLabel;
    [SerializeField] private GameObject lockMarker;
    [SerializeField] private TMP_Text lockLabel;
    [SerializeField] private GameObject clearMarker;
    [SerializeField] private TMP_Text clearLabel;
    [SerializeField] private GameObject challengeMarker;

    [Tooltip("When false, leave background sprite colors alone (art-driven slots).")]
    [SerializeField] private bool tintBackground = false;
    [SerializeField] private Color unlockedColor = Color.white;
    [SerializeField] private Color clearedColor = Color.white;
    [SerializeField] private Color lockedColor = new Color(0.65f, 0.65f, 0.68f, 1f);

    private int _chapter = -1;
    private int _stage = -1;
    private bool _unlocked;
    private Action<StageSlot> _onClick;

    public int Chapter => _chapter;
    public int Stage => _stage;
    public bool IsUnlocked => _unlocked;

    private void Awake()
    {
        if (button != null)
        {
            button.onClick.AddListener(HandleClick);
        }

        if (lockLabel != null)
        {
            lockLabel.text = "Lock";
        }

        if (clearLabel != null)
        {
            clearLabel.text = "Clear";
        }
    }

    private void OnDestroy()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(HandleClick);
        }
    }

    public void SetClickHandler(Action<StageSlot> onClick)
    {
        _onClick = onClick;
    }

    /// <summary>
    /// Refresh labels and markers only. Does not create or destroy UI nodes.
    /// showChallenge: current max challengeable stage that is not cleared yet.
    /// </summary>
    public void Bind(int chapter, int stage, bool unlocked, bool cleared, bool showChallenge)
    {
        _chapter = chapter;
        _stage = stage;
        _unlocked = unlocked;

        if (numberLabel != null)
        {
            numberLabel.text = StageDefs.FormatLabel(chapter, stage);
        }

        if (lockMarker != null)
        {
            lockMarker.SetActive(!unlocked);
        }

        if (clearMarker != null)
        {
            clearMarker.SetActive(unlocked && cleared);
        }

        if (challengeMarker != null)
        {
            challengeMarker.SetActive(showChallenge);
        }

        if (tintBackground && background != null)
        {
            if (!unlocked)
            {
                background.color = lockedColor;
            }
            else
            {
                background.color = cleared ? clearedColor : unlockedColor;
            }
        }
    }

    private void HandleClick()
    {
        _onClick?.Invoke(this);
    }
}
