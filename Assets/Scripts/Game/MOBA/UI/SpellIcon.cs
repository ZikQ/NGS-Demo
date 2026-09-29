using System;
using NGS.Unity.Core.SpellSystem;
using NGS.Unity.Core.SpellSystem.Spells;
using NGS.Unity.Core.Utils;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Assets.Scripts.Game.MOBA.UI
{
    public class SpellIcon : HoverIcon, IPointerClickHandler
    {
        [SerializeField] private TextMeshProUGUI manaCostText;
        [SerializeField] private Slider cooldownSlider;
        [SerializeField] private TextMeshProUGUI spellkey;
        [SerializeField] private Image Icon;

        private SpellInstance _spell;
        private int _slot;
        private float _shownManaCost = float.NaN;

        public event Action<int> Clicked;

        protected override DisplayData Display => _spell?.BaseSpell;

        public void Bind(SpellInstance spell, string key, int slot)
        {
            _spell = spell;
            _slot = slot;

            Icon.sprite = spell.BaseSpell.Icon;
            spellkey.text = key;

            cooldownSlider.minValue = 0f;
            cooldownSlider.maxValue = 1f;
        }

        public void Refresh(Caster caster, bool pending)
        {
            cooldownSlider.value = pending ? 1f : 1f - caster.GetCooldownProgress(_spell);

            float manaCost = Mathf.Round(_spell.BaseSpell.GetManaCost(_spell));
            manaCostText.gameObject.SetActive(manaCost > 0f);
            manaCostText.SetTextIfChanged("{0}", manaCost, ref _shownManaCost);
        }

        public void OnPointerClick(PointerEventData eventData) => Clicked?.Invoke(_slot);
    }
}
