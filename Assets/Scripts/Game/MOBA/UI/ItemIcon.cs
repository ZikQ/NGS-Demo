using System;
using NGS.Unity.Core.InventorySystem;
using NGS.Unity.Core.SpellSystem.Spells;
using NGS.Unity.Core.Utils;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Assets.Scripts.Game.MOBA.UI
{ 
    public class ItemIcon : HoverIcon, IPointerClickHandler
    {
        [SerializeField] private TextMeshProUGUI stackCount;
        [SerializeField] private Slider cooldown;
        [SerializeField] private Image icon;
        [SerializeField] private Button button;
        [SerializeField] private TextMeshProUGUI manaCost;

        private SpellInstance _useSpell;
        private float _cooldownTotal;
        private float _shownStacks = float.NaN;
        private float _shownManaCost = float.NaN;

        public Item Item { get; private set; }


        public ItemInstance Instance { get; private set; }

        public event Action<ItemIcon, PointerEventData.InputButton> Clicked;

        protected override DisplayData Display => Item;


        public void Bind(ItemInstance instance, SpellInstance useSpell)
        {
            if (instance != null && instance == Instance)
                return;

            Instance = instance;
            Show(instance?.BaseItem, useSpell);
        }

        public void Refresh(InventoryController inventory, bool pending)
        {
            if (Instance == null)
                return;

            // Полную длительность кулдауна инвентарь не отдаёт, запоминаем её на старте
            float remaining = inventory.GetItemCooldownRemaining(Instance.BaseItem);
            _cooldownTotal = remaining > 0f ? Mathf.Max(_cooldownTotal, remaining) : 0f;

            cooldown.value = pending ? 1f : _cooldownTotal > 0f ? remaining / _cooldownTotal : 0f;

            if (Instance.BaseItem.MaxStack > 1)
                stackCount.SetTextIfChanged("{0}", Instance.Stacks, ref _shownStacks);

            RefreshManaCost();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (Item != null)
                Clicked?.Invoke(this, eventData.button);
        }

        protected void Show(Item item, SpellInstance useSpell)
        {
            Item = item;
            _useSpell = useSpell;
            _cooldownTotal = 0f;

            icon.enabled = item != null;
            icon.sprite = item != null ? item.Icon : null;
            button.interactable = item != null;

            stackCount.gameObject.SetActive(Instance != null && Instance.BaseItem.MaxStack > 1);

            cooldown.minValue = 0f;
            cooldown.maxValue = 1f;
            cooldown.value = 0f;

            RefreshManaCost();
            RefreshHover();
        }

        private void RefreshManaCost()
        {
            float cost = _useSpell != null ? Mathf.Round(_useSpell.BaseSpell.GetManaCost(_useSpell)) : 0f;
            manaCost.gameObject.SetActive(cost > 0f);

            if (cost > 0f)
                manaCost.SetTextIfChanged("{0}", cost, ref _shownManaCost);
        }
    }
}
