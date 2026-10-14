from pathlib import Path
path = Path(r"D:\UnitProject\26-10-8\Assets\Scripts\Tutorial\TutorialManager.cs")
text = path.read_text(encoding="utf-8")

needle = "[SerializeField] private List<string> requiredDoneKeys = new List<string>();"
insert = needle + """
    [Tooltip("Only run during tutorial battle (TutorialBattleHooks.Active)")]
    [SerializeField] private bool battleTutorialOnly;"""
if "battleTutorialOnly" not in text:
    assert needle in text, "requiredDoneKeys not found"
    text = text.replace(needle, insert, 1)

needle = "public string TutorialKey => tutorialKey;"
insert = needle + "\n    public int CurrentIndex => _index;"
if "CurrentIndex" not in text:
    assert needle in text
    text = text.replace(needle, insert, 1)

old = """    private void Start()
    {
        if (overlay != null)
        {
            overlay.MaskTapped += OnMaskTapped;
        }

        if (autoStart)
        {
            StartTutorial();
        }
    }"""
new = """    private void Start()
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
    }"""
if "TutorialBattleHooks.Raised +=" not in text:
    assert old in text, "Start not found"
    text = text.replace(old, new, 1)

old = """    private void OnDestroy()
    {
        if (overlay != null)
        {
            overlay.MaskTapped -= OnMaskTapped;
        }

        StopListening();
        Instances.Remove(this);
        SetBlock(false);
    }"""
new = """    private void OnDestroy()
    {
        if (overlay != null)
        {
            overlay.MaskTapped -= OnMaskTapped;
        }

        TutorialBattleHooks.Raised -= OnBattleEvent;

        StopListening();
        Instances.Remove(this);
        SetBlock(false);
    }"""
if "TutorialBattleHooks.Raised -=" not in text:
    assert old in text, "OnDestroy not found"
    text = text.replace(old, new, 1)

# WillRun: insert before final return true of WillRun
marker = "private bool WillRun(bool early)"
wi = text.find(marker)
assert wi >= 0
# find the return true; that closes WillRun (first return true after marker that is followed by closing brace of method)
chunk = text[wi:]
# locate the for-loop end then return true
key = "requiredDoneKeys.Count"
ki = chunk.find(key)
assert ki >= 0
# from here find "\n        return true;\n    }"
rt = chunk.find("\n        return true;\n    }", ki)
assert rt >= 0, "return true in WillRun not found"
# check battleTutorialOnly not already there in this method
method_end = wi + rt
method = text[wi:method_end]
if "battleTutorialOnly" not in method:
    insert = """
        if (battleTutorialOnly)
        {
            return early ? TutorialBattleHooks.ShouldBeActive(BattleContext.CurrentStage) : TutorialBattleHooks.Active;
        }
"""
    text = text[: wi + rt] + insert + text[wi + rt :]

old = """    private void OnMaskTapped()
    {
        if (_showing && _tapMode)
        {
            Advance();
        }
    }


    private void Advance()"""
new = """    private void OnMaskTapped()
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

    private void Advance()"""
if "OnBattleEvent" not in text:
    assert old in text, "OnMaskTapped block not found: " + repr(text[text.find("OnMaskTapped"):text.find("OnMaskTapped")+200])
    text = text.replace(old, new, 1)

# Release EndScripted: find Release method's _hidden.Clear before HideOverlay
hi = text.find("private void HideOverlay()")
assert hi >= 0
chunk = text[:hi]
ri = chunk.rfind("private void Release()")
assert ri >= 0
release = text[ri:hi]
if "TutorialBattleHooks.EndScripted" not in release:
    ci = release.rfind("_hidden.Clear();")
    assert ci >= 0
    abs_ci = ri + ci
    text = (
        text[:abs_ci]
        + """_hidden.Clear();
        if (battleTutorialOnly)
        {
            TutorialBattleHooks.EndScripted();
        }"""
        + text[abs_ci + len("_hidden.Clear();") :]
    )

path.write_text(text, encoding="utf-8")
print("OK", len(text.splitlines()))
for s in ["battleTutorialOnly", "CurrentIndex", "Raised +=", "Raised -=", "OnBattleEvent", "EndScripted", "ShouldBeActive"]:
    print(s, text.count(s))
