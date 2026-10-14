using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 暂停按钮打开 PausePanel。继续关闭并恢复游戏，主界面按钮离开战斗。
/// </summary>
public class BattlePausePanel : MonoBehaviour
{
    [SerializeField] private BattleDirector director;
    [SerializeField] private Button pauseButton;
    [Tooltip("PausePanel 根节点。")]
    [SerializeField] private GameObject panel;
    [Tooltip("继续，场景里的 conten。")]
    [SerializeField] private GameObject resumeObject;
    [Tooltip("回主界面，场景里的 Home。")]
    [SerializeField] private GameObject homeObject;

    private bool _held;

    private void Awake()
    {
        if (pauseButton != null)
        {
            pauseButton.onClick.AddListener(Open);
        }

        Bind(resumeObject, Resume);
        Bind(homeObject, GoHome);
        if (panel != null)
        {
            panel.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        if (pauseButton != null)
        {
            pauseButton.onClick.RemoveListener(Open);
        }

        if (_held)
        {
            BattleTime.Release();
            _held = false;
        }
    }

    public void Open()
    {
        if (panel == null || panel.activeSelf)
        {
            return;
        }

        panel.SetActive(true);
        if (!_held)
        {
            BattleTime.Hold();
            _held = true;
        }
    }

    public void Resume()
    {
        if (panel != null)
        {
            panel.SetActive(false);
        }

        if (_held)
        {
            BattleTime.Release();
            _held = false;
        }
    }

    public void GoHome()
    {
        BattleTime.Reset();
        _held = false;
        if (director != null)
        {
            director.OnBack();
        }
    }

    private static void Bind(GameObject target, UnityEngine.Events.UnityAction action)
    {
        if (target == null)
        {
            return;
        }

        Button button = target.GetComponent<Button>();
        if (button == null)
        {
            button = target.AddComponent<Button>();
            button.targetGraphic = target.GetComponent<Image>();
            Navigation navigation = button.navigation;
            navigation.mode = Navigation.Mode.None;
            button.navigation = navigation;
        }

        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
    }
}
