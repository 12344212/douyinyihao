using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 新手教程流程（挂在 TutorialPanel 上；一个场景可挂多个，按 tutorialKey 区分，如 mainScenes 的 main / role）。
/// 按步骤推进，每步进度写入存档；该段完成后不再触发。
/// 步骤目标：场景引用 target，或运行时 targetKey（由 targetProvider 解析，如敌人、骰子、装备格子）。
/// 推进：点按钮 / 点镂空 / 点任意处 / 等待战斗事件（TutorialBattleHooks.Raised）。
/// 全屏拦截：该段会运行时，从场景 Awake 起就请求 TutorialOverlay 打开透明拦截层，步骤之间 / 等待目标 / 面板动画 /
/// 切场景期间都不留空档；只有当前步骤的镂空可点。freePlay 步骤关闭拦截，等 NotifyCondition 后恢复。
/// </summary>
public class TutorialManager : MonoBehaviour
{
    [Tooltip("存档键：main / battle / role；不同键互不影响")]
    [SerializeField] private string tutorialKey = "main";
    [SerializeField] private TutorialOverlay overlay;
    [Tooltip("运行时目标 / 条件解析（BattleTutorialTargets / MainTutorialTargets）")]
    [SerializeField] private TutorialTargetProvider targetProvider;
    [SerializeField] private List<TutorialStep> steps = new List<TutorialStep>();

    [Header("启动条件")]
    [Tooltip("进场景后延迟多久开始（真实时间秒）")]
    [SerializeField] private float startDelay = 0.5f;
    [SerializeField] private bool autoStart = true;
    [Tooltip("这些教程段全部完成后才开始（例如 role 需要 main、battle 完成）")]
    [SerializeField] private List<string> requiredDoneKeys = new List<string>();
    [Tooltip("Only run during tutorial battle (TutorialBattleHooks.Active)")]
    [SerializeField] private bool battleTutorialOnly;
    [Tooltip("中途退出后从存档步骤继续；关闭则每次从第 0 步开始（战斗状态无法恢复）")]
    [SerializeField] private bool resumeFromSavedStep = true;

    [Header("运行期间")]
    [Tooltip("教程运行期间隐藏的节点（例如战斗暂停按钮），释放时恢复")]
    [SerializeField] private List<GameObject> hideWhileRunning = new List<GameObject>();
    [Tooltip("走到该步骤时提前释放：恢复隐藏节点并结束战斗脚本（固定骰子/必杀）；-1 = 教程结束时")]
    [SerializeField] private int releaseAtStep = -1;
    [Tooltip("本段完成后继续保持全屏拦截（最后一步会切场景时用，避免切换前的空档）；超时后自动解除")]
    [SerializeField] private bool keepBlockingAfterFinish;
    [SerializeField] private float keepBlockingTimeout = 5f;

    [SerializeField, HideInInspector] private int wiredStepsVersion;

    private int _index = -1;
    private int _savedFloor;
    private int _replayUntil;
    private bool _released = true;
    private Button _listening;
    private Transform _current;
    private bool _currentRequired;
    private bool _showing;
    private bool _holeTapMode;
    private bool _tapMode;
    private string _eventKey;
    private Coroutine _flow;
    private readonly List<GameObject> _hidden = new List<GameObject>(2);
    private bool _freePlay;
    private string[] _freePlayKeys;
    private bool _battleHeld;

    // NotifyCondition 广播用：当前场景里所有 TutorialManager；已通知的 key 在本场景内锁存（防止先通知后进入步骤）
    private static readonly List<TutorialManager> Instances = new List<TutorialManager>(4);
    private static readonly HashSet<string> Notified = new HashSet<string>();

    public bool IsRunning => _index >= 0;
    public string TutorialKey => tutorialKey;
    public int CurrentIndex => _index;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instances.Clear();
        Notified.Clear();
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        SceneManager.activeSceneChanged += OnActiveSceneChanged;
    }

    private static void OnActiveSceneChanged(Scene from, Scene to)
    {
        Notified.Clear();
    }

    /// <summary>
    /// 通知教程条件达成（静态，战斗 / 外壳代码直接调用）。当前处于 freePlay 且 conditionKey 匹配的步骤会完成并进入下一步；
    /// 没有教程在等时只记录（本场景内有效），无其它副作用。
    /// 已接入：BattleFlow.Finish → boss_defeated（胜利）/ battle_lost（失败）。
    /// </summary>
    public static void NotifyCondition(string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            return;
        }

        Notified.Add(key);
        for (int i = Instances.Count - 1; i >= 0; i--)
        {
            if (Instances[i] != null)
            {
                Instances[i].OnCondition(key);
            }
        }
    }

    private void Awake()
    {
        if (!Instances.Contains(this))
        {
            Instances.Add(this);
        }

        // 该段本场景会运行：从场景一开始就拦截（覆盖 startDelay / 开场转场 / 切场景后的空档），并提前隐藏暂停按钮等
        if (autoStart && WillRun(true))
        {
            SetBlock(true);
            HideObjects();
        }
    }

    private void Start()
    {
        if (overlay != null)
        {
            overlay.MaskTapped += OnMaskTapped;
        }

        TutorialBattleHooks.Raised += OnBattleEvent;

        if (autoStart)
        {
            StartTutorial();
        }
    }

    private void OnDestroy()
    {
        if (overlay != null)
        {
            overlay.MaskTapped -= OnMaskTapped;
        }

        TutorialBattleHooks.Raised -= OnBattleEvent;

        StopListening();
        Instances.Remove(this);
        SetBlock(false);
        SyncBattlePause(true);
    }

    /// <summary>若未完成且满足条件则开始 / 继续教程。</summary>
    public void StartTutorial()
    {
        if (_flow != null)
        {
            StopCoroutine(_flow);
        }

        _flow = StartCoroutine(BeginRoutine());
    }

    /// <summary>跳过：直接记为完成。</summary>
    public void Skip()
    {
        Finish();
    }

    private IEnumerator BeginRoutine()
    {
        if (startDelay > 0f)
        {
            yield return new WaitForSecondsRealtime(startDelay);
        }

        _flow = null;
        LocalSaveManager save = LocalSaveManager.Instance;
        if (!WillRun(false))
        {
            // 不运行：撤掉 Awake 里可能提前打开的拦截与隐藏
            SetBlock(false);
            Release();
            yield break;
        }

        int saved = resumeFromSavedStep ? Mathf.Max(0, save.GetTutorialStep(tutorialKey)) : 0;
        if (saved >= steps.Count)
        {
            Finish();
            yield break;
        }

        _savedFloor = saved;
        int start = saved;
        _replayUntil = 0;
        // 中途退出：若保存的那步目标当前不可见（面板没打开等），从头重放，已做过且标记 skipOnReplay 的步骤跳过
        if (saved > 0 && !IsReadyNow(steps[saved]))
        {
            start = 0;
            _replayUntil = saved;
        }

        Debug.Log("[TutorialManager] start key=" + tutorialKey + " step=" + start + "/" + steps.Count);
        SetBlock(true);
        if (_released)
        {
            HideObjects();
        }

        _index = start;
        _flow = StartCoroutine(ShowStepRoutine());
    }

    private IEnumerator ShowStepRoutine()
    {
        StopListening();
        ClearMode();
        TutorialStep step = steps[_index];

        if (_index == releaseAtStep)
        {
            Release();
        }

        if (_index < _replayUntil && step.skipOnReplay)
        {
            _flow = null;
            Advance();
            yield break;
        }

        // 自由操作：关掉拦截和遮罩，等 NotifyCondition(conditionKey)
        // freePlay still honors waitFor (e.g. spawn guide enemy before releasing control)
        if (!string.IsNullOrEmpty(step.waitFor))
        {
            while (!CheckCondition(step.waitFor))
            {
                if (ShouldSkipNow(step))
                {
                    SkipFromRoutine("skipWhen");
                    yield break;
                }

                HideOverlay();
                yield return null;
            }
        }

        if (step.freePlay)
        {
            HideOverlay();
            _flow = null;
            BeginFreePlay(step);
            yield break;
        }

        if (step.showDelay > 0f)
        {
            HideOverlay();
            yield return new WaitForSecondsRealtime(step.showDelay);
        }

        if (!string.IsNullOrEmpty(step.waitFor))
        {
            while (!CheckCondition(step.waitFor))
            {
                if (ShouldSkipNow(step))
                {
                    SkipFromRoutine("skipWhen");
                    yield break;
                }

                HideOverlay();
                yield return null;
            }
        }

        if (ShouldSkipNow(step))
        {
            SkipFromRoutine("skipWhen");
            yield break;
        }

        // 目标还没生成 / 显示（面板打开中、敌人未入场）时隐藏遮罩等待；
        // skipIfTargetGone 的步骤不等（例如图鉴已被提前关闭），直接进入下一步
        Transform target = ResolveTarget(step);
        while (step.HasTarget && (target == null || !target.gameObject.activeInHierarchy))
        {
            if (step.skipIfTargetGone)
            {
                SkipFromRoutine("target gone");
                yield break;
            }

            if (ShouldSkipNow(step))
            {
                SkipFromRoutine("skipWhen");
                yield break;
            }

            HideOverlay();
            yield return null;
            target = ResolveTarget(step);
        }

        // 等一帧让布局 / 动画开始，位置由 Overlay 每帧跟随
        yield return null;

        _current = target;
        _currentRequired = step.HasTarget;
        Button button = target != null ? target.GetComponent<Button>() : null;
        switch (step.advance)
        {
            case TutorialAdvance.Tap:
                _tapMode = true;
                break;
            case TutorialAdvance.Event:
                _eventKey = step.eventKey;
                break;
            default:
                if (target == null)
                {
                    _tapMode = true;
                }
                else if (button != null)
                {
                    _listening = button;
                    _listening.onClick.AddListener(OnTargetClicked);
                }
                else
                {
                    _holeTapMode = true;
                }

                break;
        }

        _showing = true;
        if (overlay != null)
        {
            overlay.Show(target, step.holePadding, step.text, step.portrait, _tapMode);
        }

        SyncBattlePause();
        _flow = null;
    }

    private void Update()
    {
        if (!_showing || _index < 0)
        {
            return;
        }

        TutorialStep step = steps[_index];
        if (ShouldSkipNow(step))
        {
            Debug.Log("[TutorialManager] skip step " + _index + " (skipWhen " + step.skipWhen + ") key=" + tutorialKey);
            Advance();
            return;
        }

        // 目标在显示期间消失（被销毁 / 隐藏，例如图鉴被提前关闭）：
        // skipIfTargetGone → 进入下一步；否则回到等待，重新解析
        if (_currentRequired && (_current == null || !_current.gameObject.activeInHierarchy))
        {
            if (step.skipIfTargetGone)
            {
                Debug.Log("[TutorialManager] skip step " + _index + " (target gone) key=" + tutorialKey);
                Advance();
            }
            else
            {
                RestartStep();
            }

            return;
        }

        // 无 Button 的目标或说明步骤：点击抬起落在镂空内即推进（目标自身的点击逻辑照常执行）
        if ((_holeTapMode || _tapMode) && _current != null && overlay != null && Input.GetMouseButtonUp(0)
            && overlay.IsScreenPointInHole(Input.mousePosition))
        {
            Advance();
        }
    }

    private void BeginFreePlay(TutorialStep step)
    {
        _freePlayKeys = string.IsNullOrEmpty(step.conditionKey)
            ? new string[0]
            : step.conditionKey.Split(new[] { '|' }, System.StringSplitOptions.RemoveEmptyEntries);
        if (_freePlayKeys.Length == 0)
        {
            Debug.LogWarning("[TutorialManager] freePlay step " + _index + " has no conditionKey; it can never complete. key=" + tutorialKey);
        }

        _freePlay = true;
        SetBlock(false);
        SyncBattlePause();
        Debug.Log("[TutorialManager] free play step " + _index + " waiting for [" + step.conditionKey + "] key=" + tutorialKey);
        for (int i = 0; i < _freePlayKeys.Length; i++)
        {
            if (Notified.Contains(_freePlayKeys[i].Trim()))
            {
                CompleteFreePlay(_freePlayKeys[i]);
                return;
            }
        }
    }

    private void OnCondition(string key)
    {
        if (!_freePlay || _freePlayKeys == null)
        {
            return;
        }

        for (int i = 0; i < _freePlayKeys.Length; i++)
        {
            if (_freePlayKeys[i].Trim() == key)
            {
                CompleteFreePlay(key);
                return;
            }
        }
    }

    private void CompleteFreePlay(string key)
    {
        Debug.Log("[TutorialManager] free play step " + _index + " done by '" + key + "' key=" + tutorialKey);
        _freePlay = false;
        _freePlayKeys = null;
        // 先恢复拦截，下一步（例如等结算面板出现）期间不留空档
        SetBlock(true);
        SyncBattlePause();
        Advance();
    }

    private void SetBlock(bool on)
    {
        if (overlay != null)
        {
            overlay.RequestBlock(this, on);
        }
    }

    /// <summary>该段是否会运行。early = true 时在 Awake 使用（战斗用 ShouldBeActive 预判，因为 BattleController.Awake 可能还没执行）。</summary>
    private bool WillRun(bool early)
    {
        LocalSaveManager save = LocalSaveManager.Instance;
        if (save == null)
        {
            if (!early)
            {
                Debug.LogWarning("[TutorialManager] LocalSaveManager missing (start from LoadScenes). key=" + tutorialKey);
            }

            return false;
        }

        // 每次启动游戏：整套教程未完成则全部从头开始（本次会话内只执行一次）
        TutorialSession.EnsureSessionStart();
        if (save.IsTutorialDone(tutorialKey) || steps == null || steps.Count == 0)
        {
            return false;
        }

        for (int i = 0; i < requiredDoneKeys.Count; i++)
        {
            if (!string.IsNullOrEmpty(requiredDoneKeys[i]) && !save.IsTutorialDone(requiredDoneKeys[i]))
            {
                return false;
            }
        }


        if (battleTutorialOnly)
        {
            return early ? TutorialBattleHooks.ShouldBeActive(BattleContext.CurrentStage) : TutorialBattleHooks.Active;
        }
        return true;
    }

    private IEnumerator ReleaseBlockLater(float seconds)
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(0.1f, seconds));
        SetBlock(false);
    }

    private void OnTargetClicked()
    {
        Advance();
    }

    private void OnMaskTapped()
    {
        if (_showing && _tapMode)
        {
            Advance();
        }
    }

    private void OnBattleEvent(string key)
    {
        if (_showing && !string.IsNullOrEmpty(_eventKey) && key == _eventKey)
        {
            Advance();
        }
    }

    private void Advance()
    {
        if (_index < 0)
        {
            return;
        }

        StopListening();
        ClearMode();
        _index++;
        if (_index >= steps.Count)
        {
            Finish();
            return;
        }

        // 只往前记，不因重放把进度写小
        LocalSaveManager save = LocalSaveManager.Instance;
        if (save != null && _index > _savedFloor)
        {
            _savedFloor = _index;
            save.SetTutorialStep(tutorialKey, _index);
        }

        if (_flow != null)
        {
            StopCoroutine(_flow);
        }

        _flow = StartCoroutine(ShowStepRoutine());
    }

    private void RestartStep()
    {
        StopListening();
        ClearMode();
        if (_flow != null)
        {
            StopCoroutine(_flow);
        }

        _flow = StartCoroutine(ShowStepRoutine());
    }

    private void Finish()
    {
        StopListening();
        ClearMode();
        if (_flow != null)
        {
            StopCoroutine(_flow);
            _flow = null;
        }

        bool wasRunning = _index >= 0;
        _index = -1;
        _freePlay = false;
        Release();
        if (overlay != null && wasRunning)
        {
            overlay.Hide();
        }

        SyncBattlePause(true);

        // 最后一步会切场景（Stage1 → 战斗，Main → 主界面）：保持拦截直到场景切走；超时兜底解除
        if (keepBlockingAfterFinish && wasRunning && isActiveAndEnabled)
        {
            StartCoroutine(ReleaseBlockLater(keepBlockingTimeout));
        }
        else
        {
            SetBlock(false);
        }

        if (LocalSaveManager.Instance != null)
        {
            LocalSaveManager.Instance.CompleteTutorial(tutorialKey);
        }

        Debug.Log("[TutorialManager] completed key=" + tutorialKey);
    }

    private void HideObjects()
    {
        _released = false;
        _hidden.Clear();
        for (int i = 0; i < hideWhileRunning.Count; i++)
        {
            GameObject go = hideWhileRunning[i];
            if (go != null && go.activeSelf)
            {
                go.SetActive(false);
                _hidden.Add(go);
            }
        }
    }

    /// <summary>恢复隐藏节点；战斗教程同时结束脚本阶段。</summary>
    private void Release()
    {
        if (_released)
        {
            return;
        }

        _released = true;
        for (int i = 0; i < _hidden.Count; i++)
        {
            if (_hidden[i] != null)
            {
                _hidden[i].SetActive(true);
            }
        }

        _hidden.Clear();
        if (battleTutorialOnly)
        {
            TutorialBattleHooks.EndScripted();
        }
    }

    private void HideOverlay()
    {
        if (_showing || (overlay != null && overlay.IsShowing))
        {
            _showing = false;
            if (overlay != null)
            {
                overlay.Hide();
            }
        }

        SyncBattlePause();
    }

    private void ClearMode()
    {
        _holeTapMode = false;
        _tapMode = false;
        _eventKey = null;
        _freePlay = false;
        _freePlayKeys = null;
        _showing = false;
        _current = null;
        _currentRequired = false;
        SyncBattlePause();
    }

    private void StopListening()
    {
        if (_listening != null)
        {
            _listening.onClick.RemoveListener(OnTargetClicked);
            _listening = null;
        }
    }



    /// <summary>
    /// Battle tutorial only: freeze fight (BattleTime / timeScale) while a non-Event overlay step is showing.
    /// Resume on freePlay, Event (player must act on world), while waiting (overlay hidden), and when tutorial ends.
    /// </summary>
    private void SyncBattlePause(bool forceRelease = false)
    {
        bool wantHold = !forceRelease && ShouldHoldBattle();
        if (wantHold == _battleHeld)
        {
            return;
        }

        if (wantHold)
        {
            BattleTime.Hold();
            _battleHeld = true;
        }
        else
        {
            BattleTime.Release();
            _battleHeld = false;
        }
    }

    private bool ShouldHoldBattle()
    {
        if (!battleTutorialOnly || _index < 0 || _freePlay)
        {
            return false;
        }

        // Event steps need nets/enemies/towers to tick (and player world input).
        if (!string.IsNullOrEmpty(_eventKey))
        {
            return false;
        }

        return _showing;
    }

    private Transform ResolveTarget(TutorialStep step)
    {
        if (step.target != null)
        {
            return step.target;
        }

        if (!string.IsNullOrEmpty(step.targetKey) && targetProvider != null)
        {
            return targetProvider.Resolve(step.targetKey);
        }

        return null;
    }

    private bool ShouldSkipNow(TutorialStep step)
    {
        return !string.IsNullOrEmpty(step.skipWhen) && targetProvider != null && targetProvider.CheckCondition(step.skipWhen);
    }

    /// <summary>在步骤协程内部跳过：先清掉协程引用，避免 Advance 停掉新协程。</summary>
    private void SkipFromRoutine(string reason)
    {
        Debug.Log("[TutorialManager] skip step " + _index + " (" + reason + ") key=" + tutorialKey);
        _flow = null;
        Advance();
    }

    private bool CheckCondition(string key)
    {
        return targetProvider == null || targetProvider.CheckCondition(key);
    }

    private bool IsReadyNow(TutorialStep step)
    {
        if (!step.HasTarget)
        {
            return true;
        }

        Transform t = ResolveTarget(step);
        return t != null && t.gameObject.activeInHierarchy;
    }
}
