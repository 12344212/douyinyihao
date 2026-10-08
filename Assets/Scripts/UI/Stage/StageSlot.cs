using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 一个关卡格子：编号、锁定遮罩、通关标记。
/// 未解锁时点击不进战斗，只通知外面弹提示。引用都由编辑器赋值。
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

    [SerializeField] private Color unlockedColor = new Color(0.25f, 0.45f, 0.72f, 1f);
    [SerializeField] private Color clearedColor = new Color(0.22f, 0.55f, 0.38f, 1f);
    [SerializeField] private Color lockedColor = new Color(0.32f, 0.33f, 0.36f, 1f);

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

    public void Bind(int chapter, int stage, bool unlocked, bool cleared)
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

        if (background != null)
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