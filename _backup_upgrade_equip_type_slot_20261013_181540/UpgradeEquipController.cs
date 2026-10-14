using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UpgradePanel equipment UI: EquipSlots open Equit list (all owned equipment);
/// EquitInfo equip/unequip with slot-type validation; slot bg shows equipped icon.
/// Inspector drag refs. English player text.
/// Slot map: 1=Shoes(ZB-XZ), 2=Armor(ZB-YF), 3=Spear(ZB-BQ), 4=Hood(ZB-MZ).
/// </summary>
[DisallowMultipleComponent]
public class UpgradeEquipController : MonoBehaviour
{
    [System.Serializable]
    public class SlotRefs
    {
        public Button slotButton;
        public GameObject bgRoot;
        public Image iconImage;
    }

    [Header("Panel")]
    [SerializeField] private UIPanel panel;

    [Header("Equip slots (1=Shoes, 2=Armor, 3=Spear, 4=Hood)")]
    [SerializeField] private SlotRefs slot1;
    [SerializeField] private SlotRefs slot2;
    [SerializeField] private SlotRefs slot3;
    [SerializeField] private SlotRefs slot4;

    [Header("Equit list")]
    [SerializeField] private GameObject equitRoot;
    [SerializeField] private RectTransform listRoot;
    [SerializeField] private BagItemSlot bagPrefab;
    [SerializeField] private TMP_Text emptyHint;
    [SerializeField] private ItemConfig itemConfig;

    [Header("EquitInfo")]
    [SerializeField] private GameObject equitInfoRoot;
    [SerializeField] private Image infoIcon;
    [SerializeField] private TMP_Text infoName;
    [SerializeField] private TMP_Text infoDescription;
    [SerializeField] private Button equipButton;
    [SerializeField] private Button unequipButton;
    [SerializeField] private Button infoCloseButton;

    [Header("Icon")]
    [SerializeField] private Color placeholderColor = new Color(0.55f, 0.40f, 0.85f, 1f);

    private readonly List<BagItemSlot> _slots = new List<BagItemSlot>(8);
    private EquipItemType _openType = EquipItemType.None;
    private string _infoItemId;
    private Coroutine _infoIconRoutine;

    private void Awake()
    {
        if (panel == null)
        {
            panel = GetComponent<UIPanel>();
        }

        HideEquitInfo();
        HideEquit();
    }

    private void OnEnable()
    {
        WireSlot(slot1, 1);
        WireSlot(slot2, 2);
        WireSlot(slot3, 3);
        WireSlot(slot4, 4);

        if (equipButton != null)
        {
            equipButton.onClick.AddListener(OnEquipClicked);
        }

        if (unequipButton != null)
        {
            unequipButton.onClick.AddListener(OnUnequipClicked);
        }

        if (infoCloseButton != null)
        {
            infoCloseButton.onClick.AddListener(HideEquitInfo);
        }

        LocalSaveManager.OnItemsChanged += OnItemsOrEquipChanged;
        LocalSaveManager.OnEquipmentChanged += OnItemsOrEquipChanged;
        if (panel != null)
        {
            panel.Shown += OnPanelShown;
        }

        RefreshSlotVisuals();
    }

    private void OnDisable()
    {
        UnwireSlot(slot1);
        UnwireSlot(slot2);
        UnwireSlot(slot3);
        UnwireSlot(slot4);

        if (equipButton != null)
        {
            equipButton.onClick.RemoveListener(OnEquipClicked);
        }

        if (unequipButton != null)
        {
            unequipButton.onClick.RemoveListener(OnUnequipClicked);
        }

        if (infoCloseButton != null)
        {
            infoCloseButton.onClick.RemoveListener(HideEquitInfo);
        }

        LocalSaveManager.OnItemsChanged -= OnItemsOrEquipChanged;
        LocalSaveManager.OnEquipmentChanged -= OnItemsOrEquipChanged;
        if (panel != null)
        {
            panel.Shown -= OnPanelShown;
        }
    }

    private void OnPanelShown(UIPanel _)
    {
        HideEquitInfo();
        HideEquit();
        RefreshSlotVisuals();
    }

    private void OnItemsOrEquipChanged()
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        RefreshSlotVisuals();
        if (equitRoot != null && equitRoot.activeSelf && _openType != EquipItemType.None)
        {
            RebuildEquitList();
        }

        if (equitInfoRoot != null && equitInfoRoot.activeSelf && !string.IsNullOrEmpty(_infoItemId))
        {
            ShowEquitInfo(_infoItemId);
        }
    }

    private void WireSlot(SlotRefs slot, int index)
    {
        if (slot == null || slot.slotButton == null)
        {
            return;
        }

        int captured = index;
        slot.slotButton.onClick.AddListener(() => OnEquipSlotClicked(captured));
    }

    private void UnwireSlot(SlotRefs slot)
    {
        if (slot == null || slot.slotButton == null)
        {
            return;
        }

        slot.slotButton.onClick.RemoveAllListeners();
    }

    private void OnEquipSlotClicked(int slotIndex)
    {
        EquipItemType type = LocalSaveManager.EquipTypeForSlotIndex(slotIndex);
        if (type == EquipItemType.None)
        {
            return;
        }

        HideEquitInfo();
        OpenEquit(type);
    }

    private void OpenEquit(EquipItemType type)
    {
        _openType = type;
        if (equitRoot != null)
        {
            equitRoot.SetActive(true);
        }

        RebuildEquitList();
    }

    private void HideEquit()
    {
        _openType = EquipItemType.None;
        ClearListSlots();
        if (equitRoot != null)
        {
            equitRoot.SetActive(false);
        }
    }

    private void RebuildEquitList()
    {
        ClearListSlots();

        if (emptyHint != null)
        {
            emptyHint.text = "No equipment yet. Head to the shop to buy some.";
        }

        LocalSaveManager save = LocalSaveManager.Instance;
        if (save == null)
        {
            SetEmpty(true);
            return;
        }

        if (GameItemDefs.Current == null && itemConfig != null)
        {
            GameItemDefs.Bind(itemConfig);
        }

        if (listRoot == null || bagPrefab == null)
        {
            Debug.LogWarning("[UpgradeEquipController] List root or BagPrefab is not assigned.");
            SetEmpty(true);
            return;
        }

        // Activate before Instantiate/Bind so BagItemSlot.isActiveAndEnabled is true on first open.
        // Otherwise Bind skips StartCoroutine(LoadIcon) under an inactive listRoot (empty Icon until reopen).
        listRoot.gameObject.SetActive(true);

        List<ItemStackEntry> items = save.GetAllItems();
        int shown = 0;
        for (int i = 0; i < items.Count; i++)
        {
            ItemStackEntry stack = items[i];
            if (stack == null || stack.count <= 0 || string.IsNullOrEmpty(stack.itemId))
            {
                continue;
            }

            GameItemDefs.Def def = GameItemDefs.Get(stack.itemId);
            if (def == null || !def.IsEquipment)
            {
                continue;
            }

            BagItemSlot slot = Instantiate(bagPrefab, listRoot);
            slot.gameObject.SetActive(true);
            slot.Bind(
                stack.itemId,
                def.displayName,
                stack.count,
                def.iconLocation,
                OnBagItemClicked,
                showCount: false,
                setNativeSize: true);
            slot.SetSelected(save.IsItemEquipped(stack.itemId));
            _slots.Add(slot);
            shown++;
        }

        SetEmpty(shown == 0);
        if (listRoot != null)
        {
            listRoot.gameObject.SetActive(shown > 0);
        }
    }

    private void OnBagItemClicked(string itemId)
    {
        ShowEquitInfo(itemId);
    }

    private void ShowEquitInfo(string itemId)
    {
        _infoItemId = itemId;
        GameItemDefs.Def def = GameItemDefs.Get(itemId);
        if (def == null)
        {
            HideEquitInfo();
            return;
        }

        if (equitInfoRoot != null)
        {
            equitInfoRoot.SetActive(true);
        }

        if (infoName != null)
        {
            infoName.text = string.IsNullOrEmpty(def.displayName) ? itemId : def.displayName;
        }

        if (infoDescription != null)
        {
            infoDescription.text = def.FormatDetailText();
        }

        if (infoIcon != null)
        {
            infoIcon.sprite = null;
            infoIcon.color = placeholderColor;
            if (_infoIconRoutine != null)
            {
                StopCoroutine(_infoIconRoutine);
            }

            _infoIconRoutine = StartCoroutine(LoadIcon(infoIcon, def.iconLocation));
        }

        LocalSaveManager save = LocalSaveManager.Instance;
        bool equipped = save != null && save.IsItemEquipped(itemId);
        if (equipButton != null)
        {
            equipButton.gameObject.SetActive(!equipped);
        }

        if (unequipButton != null)
        {
            unequipButton.gameObject.SetActive(equipped);
        }
    }

    private void HideEquitInfo()
    {
        _infoItemId = null;
        if (_infoIconRoutine != null)
        {
            StopCoroutine(_infoIconRoutine);
            _infoIconRoutine = null;
        }

        if (equitInfoRoot != null)
        {
            equitInfoRoot.SetActive(false);
        }
    }

    private void OnEquipClicked()
    {
        if (string.IsNullOrEmpty(_infoItemId))
        {
            return;
        }

        LocalSaveManager save = LocalSaveManager.Instance;
        if (save == null)
        {
            TipPanelUI.ShowTip("Cannot equip right now.");
            return;
        }

        GameItemDefs.Def def = GameItemDefs.Get(_infoItemId);
        if (def == null || !def.IsEquipment)
        {
            TipPanelUI.ShowTip("Cannot equip.");
            return;
        }

        if (_openType != EquipItemType.None && def.equipType != _openType)
        {
            TipPanelUI.ShowTip("This equipment does not match the selected slot.");
            return;
        }

        if (!save.TryEquipItem(_infoItemId))
        {
            TipPanelUI.ShowTip("Cannot equip.");
            return;
        }

        TipPanelUI.ShowTip("Equipped!");
        HideEquitInfo();
        RefreshSlotVisuals();
        if (_openType != EquipItemType.None)
        {
            RebuildEquitList();
        }
    }

    private void OnUnequipClicked()
    {
        if (string.IsNullOrEmpty(_infoItemId))
        {
            return;
        }

        LocalSaveManager save = LocalSaveManager.Instance;
        if (save == null)
        {
            TipPanelUI.ShowTip("Cannot unequip right now.");
            return;
        }

        if (!save.UnequipItem(_infoItemId))
        {
            TipPanelUI.ShowTip("Cannot unequip.");
            return;
        }

        TipPanelUI.ShowTip("Unequipped.");
        HideEquitInfo();
        RefreshSlotVisuals();
        if (_openType != EquipItemType.None)
        {
            RebuildEquitList();
        }
    }

    public void RefreshSlotVisuals()
    {
        RefreshOneSlot(slot1, 1);
        RefreshOneSlot(slot2, 2);
        RefreshOneSlot(slot3, 3);
        RefreshOneSlot(slot4, 4);
    }

    private void RefreshOneSlot(SlotRefs slot, int index)
    {
        if (slot == null)
        {
            return;
        }

        LocalSaveManager save = LocalSaveManager.Instance;
        string itemId = save != null ? save.GetEquippedItemIdForSlot(index) : string.Empty;
        bool has = !string.IsNullOrEmpty(itemId);
        if (slot.bgRoot != null)
        {
            slot.bgRoot.SetActive(has);
        }

        if (!has || slot.iconImage == null)
        {
            if (slot.iconImage != null)
            {
                slot.iconImage.sprite = null;
            }

            return;
        }

        GameItemDefs.Def def = GameItemDefs.Get(itemId);
        string location = def != null ? def.iconLocation : null;
        slot.iconImage.sprite = null;
        slot.iconImage.color = placeholderColor;
        StartCoroutine(LoadIcon(slot.iconImage, location));
    }

    private IEnumerator LoadIcon(Image target, string location)
    {
        YooAssetManager yoo = YooAssetManager.Instance;
        if (target == null || yoo == null || !yoo.IsReady || string.IsNullOrEmpty(location) || !yoo.HasLocation(location))
        {
            yield break;
        }

        Sprite sprite = null;
        yield return yoo.LoadSpriteAsync(location, s => sprite = s);
        if (sprite != null && target != null)
        {
            target.sprite = sprite;
            target.color = Color.white;
        }
    }

    private void SetEmpty(bool empty)
    {
        if (emptyHint != null)
        {
            emptyHint.gameObject.SetActive(empty);
        }

        if (listRoot != null && empty)
        {
            listRoot.gameObject.SetActive(false);
        }
    }

    private void ClearListSlots()
    {
        for (int i = 0; i < _slots.Count; i++)
        {
            if (_slots[i] != null)
            {
                Destroy(_slots[i].gameObject);
            }
        }

        _slots.Clear();

        if (listRoot == null)
        {
            return;
        }

        for (int i = listRoot.childCount - 1; i >= 0; i--)
        {
            Transform child = listRoot.GetChild(i);
            if (child != null)
            {
                Destroy(child.gameObject);
            }
        }
    }
}