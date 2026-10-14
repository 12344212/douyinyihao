using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 单个藏品格子 CP1–CP15：未拥有显示剪影；拥有后显示彩图并呼吸缩放；
/// 拥有且未领奖时可点一次领取 500 金币，弹出 GetPanel，之后锁死点击并停止呼吸。
/// </summary>
[DisallowMultipleComponent]
public class CollectionCpSlot : MonoBehaviour
{
    private const float BreathScale = 1.08f;
    private const float BreathDuration = 0.85f;

    [SerializeField] private int index;
    [SerializeField] private Image iconImage;
    [SerializeField] private Button button;
    [SerializeField] private Sprite silhouetteSprite;
    [SerializeField] private Sprite ownedSprite;
    [SerializeField] private string silhouetteAddress;
    [SerializeField] private string ownedAddress;

    private Vector3 _baseScale = Vector3.one;
    private Tween _breathTween;
    private Coroutine _loadRoutine;
    private bool _owned;
    private bool _rewardClaimed;
    private bool _interactableForReward;

    public int Index => index;

    private void Awake()
    {
        _baseScale = transform.localScale;
        if (button != null)
        {
            button.onClick.AddListener(OnClicked);
        }
    }

    private void OnDestroy()
    {
        StopBreath();
        if (button != null)
        {
            button.onClick.RemoveListener(OnClicked);
        }
    }

    private void OnDisable()
    {
        StopBreath();
        if (_loadRoutine != null)
        {
            StopCoroutine(_loadRoutine);
            _loadRoutine = null;
        }
    }

    /// <summary>由 Setup 写入序号与资源引用。</summary>
    public void EditorBind(int slotIndex, Image image, Button btn, Sprite silhouette, Sprite owned, string silAddr, string ownedAddr)
    {
        index = slotIndex;
        iconImage = image;
        button = btn;
        silhouetteSprite = silhouette;
        ownedSprite = owned;
        silhouetteAddress = silAddr;
        ownedAddress = ownedAddr;
    }

    public void ApplyState(bool owned, bool rewardClaimed)
    {
        _owned = owned;
        _rewardClaimed = rewardClaimed;
        _interactableForReward = owned && !rewardClaimed;

        if (button != null)
        {
            button.interactable = _interactableForReward;
        }

        StopBreath();
        transform.localScale = _baseScale;

        if (_loadRoutine != null)
        {
            StopCoroutine(_loadRoutine);
            _loadRoutine = null;
        }

        if (isActiveAndEnabled)
        {
            _loadRoutine = StartCoroutine(LoadAndShow());
        }
        else
        {
            ApplySpritesImmediate();
        }
    }

    private IEnumerator LoadAndShow()
    {
        ApplySpritesImmediate();

        // 若 Inspector 未拖图，走 YooAsset 按地址加载
        if (iconImage != null && iconImage.sprite == null)
        {
            string location = _owned
                ? (string.IsNullOrEmpty(ownedAddress) ? CollectionDefs.OwnedAddress(index) : ownedAddress)
                : (string.IsNullOrEmpty(silhouetteAddress) ? CollectionDefs.SilhouetteAddress(index) : silhouetteAddress);

            YooAssetManager yoo = YooAssetManager.Instance;
            if (yoo != null && yoo.IsReady && yoo.HasLocation(location))
            {
                Sprite sprite = null;
                yield return yoo.LoadSpriteAsync(location, s => sprite = s);
                if (sprite != null && iconImage != null)
                {
                    iconImage.sprite = sprite;
                    iconImage.color = Color.white;
                }
            }
        }

        if (_owned && !_rewardClaimed)
        {
            StartBreath();
        }

        _loadRoutine = null;
    }

    private void ApplySpritesImmediate()
    {
        if (iconImage == null)
        {
            return;
        }

        Sprite sprite = _owned ? ownedSprite : silhouetteSprite;
        if (sprite != null)
        {
            iconImage.sprite = sprite;
        }

        iconImage.color = Color.white;
        iconImage.preserveAspect = true;
    }

    private void StartBreath()
    {
        StopBreath();
        transform.localScale = _baseScale;
        _breathTween = transform
            .DOScale(_baseScale * BreathScale, BreathDuration)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo)
            .SetUpdate(true)
            .SetLink(gameObject);
    }

    private void StopBreath()
    {
        if (_breathTween != null && _breathTween.IsActive())
        {
            _breathTween.Kill();
        }

        _breathTween = null;
        transform.localScale = _baseScale;
    }

    private void OnClicked()
    {
        if (!_interactableForReward)
        {
            return;
        }

        LocalSaveManager save = LocalSaveManager.Instance;
        if (save == null)
        {
            Debug.LogWarning("[CollectionCpSlot] LocalSaveManager missing; start from LoadScene.");
            return;
        }

        if (!save.TryClaimCollectibleReward(index, out int gold))
        {
            return;
        }

        _rewardClaimed = true;
        _interactableForReward = false;
        if (button != null)
        {
            button.interactable = false;
        }

        StopBreath();
        GetPanelController.Show(new GetPanelController.Reward
        {
            useCurrency = true,
            currency = CurrencyType.Gold,
            displayName = "Gold",
            count = gold
        });
    }
}
