using System.Collections.Generic;
using Assets.Scripts.Game.MOBA.Entities;
using Assets.Scripts.Game.MOBA.Player;
using DG.Tweening;
using NGS.Unity.Core.AssetLibrarySystem;
using NGS.Unity.Core.InventorySystem;
using NGS.Unity.Core.LocalizationSystem;
using NGS.Unity.Core.ModifierSystem;
using NGS.Unity.Core.ObjectPoolSystem;
using NGS.Unity.Core.SpellSystem;
using NGS.Unity.Core.SpellSystem.Spells;
using NGS.Unity.Core.StatsSystem;
using NGS.Unity.Core.Utils;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Assets.Scripts.Game.MOBA.UI
{
    public class HUD : MonoBehaviour
    {
        private const int InventorySlotCount = 6;
        private const string InventoryFullHint = "hint.inventory_full";
        private const string StrengthLabel = "stats.strength";
        private const string IntelligenceLabel = "stats.intelligence";

        [SerializeField] private PlayerOrderInput playerInput;
        [SerializeField] private Button spawnWaveButton;
        [Header("Language")]
        [SerializeField] private Button switchLanguageButton;
        [SerializeField] private Sprite ru;
        [SerializeField] private Sprite en;
        [SerializeField] private Sprite tr;


        [Header("Health bar")]
        [SerializeField] private Slider healhSlider;
        [SerializeField] private TextMeshProUGUI healthText;
        [SerializeField] private TextMeshProUGUI healthRegenText;

        [Header("Mana bar")]
        [SerializeField] private Slider manaSlider;
        [SerializeField] private TextMeshProUGUI manaText;
        [SerializeField] private TextMeshProUGUI manaRegenText;

        [Header("CastBar")]
        [SerializeField] private GameObject castBar;
        [SerializeField] private Slider castDuration;
        [SerializeField] private TextMeshProUGUI castTime;

        [Header("Spell bar")]
        [SerializeField] private GameObject spellsCollection;
        [SerializeField] private SpellIcon spellIconTemplate;

        [Header("Modifiers bar")]
        [SerializeField] private GameObject modifiersCollection;
        [SerializeField] private ModifierIcon modifierIconTemplate;

        [Header("Hint bar")]
        [SerializeField] private GameObject hintBar;
        [SerializeField] private TextMeshProUGUI hintText;
        [SerializeField] private float hintShowDuration = 0.25f;
        [SerializeField] private float hintHoldTime = 1.5f;
        [SerializeField] private float hintHideDuration = 0.4f;

        [Header("Inventory bar")]
        [SerializeField] private GameObject itemsCollection;
        [SerializeField] private ItemIcon itemIconTemplate;

        [Header("Shop")]
        [SerializeField] private GameObject shop;
        [SerializeField] private Button shopButton;
        [SerializeField] private GameObject shopItemCollection;
        [SerializeField] private ShopItemIcon shopItemTemplate;
        [SerializeField] private float shopShowDuration = 0.25f;
        [SerializeField] private float shopHideDuration = 0.2f;

        [Header("Chars Bar")]
        [SerializeField] private GameObject charsBar;
        [SerializeField] private TextMeshProUGUI strenghtText;
        [SerializeField] private TextMeshProUGUI intellectText;

        [Header("Display Bar")]
        [SerializeField] private GameObject displayBar;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI descriptionText;
        [Tooltip("Отступ от курсора в пикселях экрана")]
        [SerializeField] private Vector2 displayOffset = new(16f, 16f);

        private readonly List<SpellIcon> _spellIcons = new();
        private readonly Dictionary<ModifierInstance, ModifierIcon> _modifierIcons = new();
        private readonly List<ItemIcon> _itemIcons = new();

        // Модификаторы появляются и пропадают постоянно, иконки под них не пересоздаём
        private Pool _modifierIconPool;

        private MobaCharacter _currentCharacter;
        private StatsContainer _stats;
        private Caster _caster;
        private ModifierController _modifiers;
        private InventoryController _inventory;

        private CanvasGroup _hintGroup;
        private Sequence _hintSequence;

        private CanvasGroup _shopGroup;
        private bool _shopOpen;

        private RectTransform _displayRect;
        private Camera _canvasCamera;
        private DisplayData _displayed;

        private bool _hasStrength;
        private bool _hasIntelligence;
        private int _shownStrength;
        private int _shownIntelligence;
        private int _statsVersion = -1;


        private float _shownHealth = float.NaN;
        private float _shownMaxHealth = float.NaN;
        private float _shownHealthRegen = float.NaN;
        private float _shownMana = float.NaN;
        private float _shownMaxMana = float.NaN;
        private float _shownManaRegen = float.NaN;
        private float _shownCastTime = float.NaN;

        public void BindToCharacter(MobaCharacter character)
        {
            Unsubscribe();

            _currentCharacter = character;
            _stats = character != null ? character.Get<StatsContainer>() : null;
            _caster = character != null ? character.Get<Caster>() : null;
            _modifiers = character != null ? character.ModifierController : null;
            _inventory = character != null ? character.Get<InventoryController>() : null;

            if (_caster != null)
            {
                _caster.OnSpellLearned += OnSpellsChanged;
                _caster.OnSpellForgotten += OnSpellsChanged;
            }

            if (_modifiers != null)
            {
                _modifiers.OnModifierCreated += AddModifierIcon;
                _modifiers.OnModifierRemoved += RemoveModifierIcon;
            }

            if (_inventory != null)
            {
                _inventory.OnItemAdded += OnInventoryChanged;
                _inventory.OnItemRemoved += OnInventoryChanged;
            }

            // У крипов и башен нет Caster, а значит и маны
            manaSlider.gameObject.SetActive(_caster != null);
            manaText.gameObject.SetActive(_caster != null);
            manaRegenText.gameObject.SetActive(_caster != null);

            castBar.SetActive(false);

            BindStats();
            RebuildSpells();
            RebuildModifiers();
            RebindItems();
        }

        private void Awake()
        {
            // Скрываем шаблоны тогда
            HideTemplate(spellIconTemplate);
            HideTemplate(modifierIconTemplate);
            HideTemplate(itemIconTemplate);
            HideTemplate(shopItemTemplate);

            _modifierIconPool = new Pool("hud_modifier_icons", modifierIconTemplate.gameObject,
                modifiersCollection.transform, defaultCapacity: 8, maxSize: 0);

            castDuration.minValue = 0f;
            castDuration.maxValue = 1f;

            _hintGroup = hintBar.GetComponent<CanvasGroup>();
            _shopGroup = shop.GetComponent<CanvasGroup>();

            _displayRect = (RectTransform)displayBar.transform;
            var canvas = displayBar.GetComponentInParent<Canvas>(true).rootCanvas;
            _canvasCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;

            hintBar.SetActive(false);
            shop.SetActive(false);
            displayBar.SetActive(false);

            CreateItemSlots();

            spawnWaveButton.onClick.AddListener(TowerCharacter.SpawnWaves);
            switchLanguageButton.onClick.AddListener(SwitchLanguage);
            shopButton.onClick.AddListener(ToggleShop);
        }

        // Не в Awake: прогретое ядро зовёт колбэк сразу, а спеллам предметов нужен уже проснувшийся герой
        private void Start() => AssetLibraryManager.WhenLoaded(FillShop);

        private void OnEnable()
        {
            playerInput.SelectionChanged += BindToCharacter;
            playerInput.HintRequested += ShowHintKey;
            Loc.LanguageChanged += OnLanguageChanged;

            BindToCharacter(playerInput.Selected);
            OnLanguageChanged();
        }

        private void OnDisable()
        {
            playerInput.SelectionChanged -= BindToCharacter;
            playerInput.HintRequested -= ShowHintKey;
            Loc.LanguageChanged -= OnLanguageChanged;

            BindToCharacter(null);
            _hintSequence?.Kill();
        }

        private void Update()
        {
            if (_displayed != null)
                FollowCursor();

            if (_currentCharacter == null)
                return;

            healhSlider.maxValue = _currentCharacter.MaxHealth;
            healhSlider.value = _currentCharacter.Health;
            healthText.SetTextIfChanged("{0} / {1}", Mathf.Round(_currentCharacter.Health),
                Mathf.Round(_currentCharacter.MaxHealth), ref _shownHealth, ref _shownMaxHealth);
            healthRegenText.SetTextIfChanged("+{0:1}", RoundTenths(_stats.GetStatValue("health_regen")), ref _shownHealthRegen);

            if (_caster != null)
            {
                manaSlider.maxValue = _caster.Mana.MaxValue;
                manaSlider.value = _caster.Mana.Value;
                manaText.SetTextIfChanged("{0} / {1}", Mathf.Round(_caster.Mana.Value), Mathf.Round(_caster.Mana.MaxValue),
                    ref _shownMana, ref _shownMaxMana);
                manaRegenText.SetTextIfChanged("+{0:1}", RoundTenths(_caster.ManaRegen.Value), ref _shownManaRegen);

                int pending = _currentCharacter == playerInput.Hero ? playerInput.PendingSpell : -1;

                for (int slot = 0; slot < _spellIcons.Count; slot++)
                    _spellIcons[slot].Refresh(_caster, slot == pending);
            }

            RefreshCastBar();
            RefreshStats();
            RefreshItems();

            foreach (var icon in _modifierIcons.Values)
                icon.Refresh();
        }

        private static void HideTemplate(Component template)
        {
            if (template.gameObject.scene.IsValid())
                template.gameObject.SetActive(false);
        }

        private static float RoundTenths(float value) => Mathf.Round(value * 10f) / 10f;

        private void RefreshCastBar()
        {
            bool channeling = _caster != null && _caster.IsChanneling && _caster.ChannelDuration > 0f;
            castBar.SetActive(channeling);

            if (!channeling)
                return;

            castDuration.value = _caster.ChannelRemaining / _caster.ChannelDuration;
            castTime.SetTextIfChanged("{0:1}", RoundTenths(_caster.ChannelRemaining), ref _shownCastTime);
        }

        private void ShowHintKey(string key) => ShowHint(Loc.Get(key));

        private void ShowHint(string text)
        {
            hintText.text = text;

            _hintSequence?.Kill();
            _hintGroup.alpha = 1f;

            var bar = hintBar.transform;
            bar.localScale = Vector3.zero;
            hintBar.SetActive(true);

            _hintSequence = DOTween.Sequence()
                .Append(bar.DOScale(1f, hintShowDuration).SetEase(Ease.OutBack))
                .AppendInterval(hintHoldTime)
                .Append(_hintGroup.DOFade(0f, hintHideDuration))
                .OnComplete(() => hintBar.SetActive(false));
        }

        // Атрибуты есть только у героев
        private void BindStats()
        {
            _hasStrength = _stats != null && _stats.HasStat("strength");
            _hasIntelligence = _stats != null && _stats.HasStat("intelligence");

            charsBar.SetActive(_hasStrength || _hasIntelligence);
            strenghtText.gameObject.SetActive(_hasStrength);
            intellectText.gameObject.SetActive(_hasIntelligence);

            _statsVersion = -1;
        }

        private void RefreshStats()
        {
            if (!_hasStrength && !_hasIntelligence)
                return;

            int strength = Mathf.RoundToInt(_stats.GetStatValue("strength"));
            int intelligence = Mathf.RoundToInt(_stats.GetStatValue("intelligence"));

            if (strength == _shownStrength && intelligence == _shownIntelligence && _statsVersion == Loc.Version)
                return;

            _shownStrength = strength;
            _shownIntelligence = intelligence;
            _statsVersion = Loc.Version;

            if (_hasStrength)
                strenghtText.text = Loc.Format(StrengthLabel, strength);

            if (_hasIntelligence)
                intellectText.text = Loc.Format(IntelligenceLabel, intelligence);
        }

        private void SwitchLanguage() => Loc.SetLanguage(Loc.NextLanguage());

        private void OnLanguageChanged()
        {
            switchLanguageButton.image.sprite = Loc.Current switch
            {
                "ru" => ru,
                "tr" => tr,
                _ => en,
            };

            if (_displayed != null)
                RefreshDisplayTexts();
        }

        private void Unsubscribe()
        {
            if (_caster != null)
            {
                _caster.OnSpellLearned -= OnSpellsChanged;
                _caster.OnSpellForgotten -= OnSpellsChanged;
            }

            if (_modifiers != null)
            {
                _modifiers.OnModifierCreated -= AddModifierIcon;
                _modifiers.OnModifierRemoved -= RemoveModifierIcon;
            }

            if (_inventory != null)
            {
                _inventory.OnItemAdded -= OnInventoryChanged;
                _inventory.OnItemRemoved -= OnInventoryChanged;
            }
        }

        private void OnSpellsChanged(SpellInstance spell) => RebuildSpells();

        private void RebuildSpells()
        {
            foreach (var icon in _spellIcons)
                Destroy(icon.gameObject);

            _spellIcons.Clear();

            if (_caster == null)
                return;

            var spells = _caster.GetAllSpells();
            int count = Mathf.Min(spells.Count, PlayerOrderInput.SpellKeys.Count);

            for (int slot = 0; slot < count; slot++)
            {
                var icon = Instantiate(spellIconTemplate, spellsCollection.transform);
                icon.gameObject.SetActive(true);
                icon.Bind(spells[slot], PlayerOrderInput.SpellKeys[slot].ToString(), slot);
                icon.Clicked += OnSpellIconClicked;
                SubscribeHover(icon);

                _spellIcons.Add(icon);
            }
        }

        // Кастовать с худа можно только своим героем, у чужого юнита иконки только для просмотра
        private void OnSpellIconClicked(int slot)
        {
            if (_currentCharacter == playerInput.Hero)
                playerInput.SelectSpell(slot);
        }

        private void RebuildModifiers()
        {
            foreach (var icon in _modifierIcons.Values)
                ReleaseModifierIcon(icon);

            _modifierIcons.Clear();

            if (_modifiers == null)
                return;

            foreach (var modifier in _modifiers.Modifiers)
                AddModifierIcon(modifier);
        }

        private void AddModifierIcon(ModifierInstance modifier)
        {
            if (modifier.BaseMod.IsHidden || _modifierIcons.ContainsKey(modifier))
                return;

            var icon = _modifierIconPool.Get().GetComponent<ModifierIcon>();

            // Из пула иконка приходит на своё старое место среди соседей, а новый модификатор встаёт в конец
            icon.transform.SetAsLastSibling();
            icon.Bind(modifier);
            SubscribeHover(icon);

            _modifierIcons[modifier] = icon;
        }

        private void RemoveModifierIcon(ModifierInstance modifier, bool purged)
        {
            if (_modifierIcons.Remove(modifier, out var icon))
                ReleaseModifierIcon(icon);
        }

        // Отписываемся после возврата в пул: выключаясь, иконка ещё успевает убрать панель описания
        private void ReleaseModifierIcon(ModifierIcon icon)
        {
            _modifierIconPool.Release(icon.gameObject);
            UnsubscribeHover(icon);
        }

        private void CreateItemSlots()
        {
            for (int i = 0; i < InventorySlotCount; i++)
            {
                var icon = Instantiate(itemIconTemplate, itemsCollection.transform);
                icon.gameObject.SetActive(true);
                icon.Bind(null, null);
                icon.Clicked += OnItemIconClicked;
                SubscribeHover(icon);

                _itemIcons.Add(icon);
            }
        }

        private void OnInventoryChanged(ItemInstance item) => RebindItems();

        private void RebindItems()
        {
            var items = _inventory != null ? _inventory.Items : null;

            for (int slot = 0; slot < _itemIcons.Count; slot++)
            {
                var item = items != null && slot < items.Count ? items[slot] : null;
                var useSpell = item != null ? _inventory.GetItemSpell(item.BaseItem) : null;

                _itemIcons[slot].Bind(item, useSpell);
            }
        }

        private void RefreshItems()
        {
            if (_inventory == null)
                return;

            var pending = _currentCharacter == playerInput.Hero ? playerInput.PendingItem : null;

            foreach (var icon in _itemIcons)
                icon.Refresh(_inventory, pending != null && icon.Instance == pending);
        }

        // Использовать и выкидывать можно только предметы своего героя, чужой инвентарь только для просмотра
        private void OnItemIconClicked(ItemIcon icon, PointerEventData.InputButton button)
        {
            if (_currentCharacter != playerInput.Hero || icon.Instance == null)
                return;

            if (button == PointerEventData.InputButton.Left)
                playerInput.SelectItem(icon.Instance);
            else if (button == PointerEventData.InputButton.Right)
                _inventory.RemoveItem(icon.Instance);
        }

        private void FillShop()
        {
            // Прогрев мог закончиться, когда худа уже нет
            if (this == null)
                return;

            var library = AssetLibraryManager.Get<ItemLibrary>();
            if (library == null)
                return;

            var inventory = playerInput.Hero.Inventory;

            foreach (var item in library.All)
            {
                var icon = Instantiate(shopItemTemplate, shopItemCollection.transform);
                icon.gameObject.SetActive(true);
                icon.Bind(item, inventory != null ? inventory.GetItemSpell(item) : null);
                icon.Clicked += OnShopItemClicked;
                SubscribeHover(icon);
            }
        }

        private void OnShopItemClicked(ItemIcon icon, PointerEventData.InputButton button)
        {
            var inventory = playerInput.Hero.Inventory;

            if (button != PointerEventData.InputButton.Left || inventory == null)
                return;

            if (inventory.AddItem(icon.Item) == 0)
                ShowHintKey(InventoryFullHint);
        }

        private void ToggleShop()
        {
            _shopOpen = !_shopOpen;

            var panel = shop.transform;
            panel.DOKill();
            _shopGroup.DOKill();

            // Закрывающийся магазин уже пропускает клики сквозь себя
            _shopGroup.blocksRaycasts = _shopOpen;

            if (_shopOpen)
            {
                shop.SetActive(true);
                _shopGroup.alpha = 0f;
                panel.localScale = Vector3.one * 0.9f;

                _shopGroup.DOFade(1f, shopShowDuration);
                panel.DOScale(1f, shopShowDuration).SetEase(Ease.OutBack);
            }
            else
            {
                _shopGroup.DOFade(0f, shopHideDuration).OnComplete(() => shop.SetActive(false));
            }
        }

        private void SubscribeHover(HoverIcon icon)
        {
            icon.Hovered += ShowDisplay;
            icon.Unhovered += HideDisplay;
        }

        private void UnsubscribeHover(HoverIcon icon)
        {
            icon.Hovered -= ShowDisplay;
            icon.Unhovered -= HideDisplay;
        }

        private void ShowDisplay(DisplayData data)
        {
            // У служебных модификаторов без имени показывать нечего
            if (string.IsNullOrEmpty(data.DisplayNameKey))
            {
                HideDisplay();
                return;
            }

            _displayed = data;
            RefreshDisplayTexts();

            displayBar.SetActive(true);
            FollowCursor();
        }

        private void HideDisplay()
        {
            _displayed = null;
            displayBar.SetActive(false);
        }

        private void RefreshDisplayTexts()
        {
            string description = _displayed.DisplayDescription;

            titleText.text = _displayed.DisplayName;
            descriptionText.text = description;
            descriptionText.gameObject.SetActive(!string.IsNullOrEmpty(description));
        }

        private void FollowCursor()
        {
            var mouse = Mouse.current;
            if (mouse == null)
                return;

            var screen = mouse.position.ReadValue();

            bool toLeft = screen.x > Screen.width * 0.5f;
            bool below = screen.y > Screen.height * 0.5f;

            _displayRect.pivot = new Vector2(toLeft ? 1f : 0f, below ? 1f : 0f);
            screen += new Vector2(toLeft ? -displayOffset.x : displayOffset.x, below ? -displayOffset.y : displayOffset.y);

            if (RectTransformUtility.ScreenPointToWorldPointInRectangle(_displayRect, screen, _canvasCamera, out var world))
                _displayRect.position = world;
        }
    }
}
