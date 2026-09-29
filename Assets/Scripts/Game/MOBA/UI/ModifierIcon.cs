using NGS.Unity.Core.ModifierSystem;
using NGS.Unity.Core.Utils;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.Game.MOBA.UI
{
    public class ModifierIcon : HoverIcon
    {
        [SerializeField] private Image icon;
        [SerializeField] private Slider duration;
        [SerializeField] private Image debuffImage;
        [SerializeField] private TextMeshProUGUI stackCount;
        private ModifierInstance _modifier;
        private bool _stackable;
        private float _shownStacks = float.NaN;

        protected override DisplayData Display => _modifier?.BaseMod;

        public void Bind(ModifierInstance modifier)
        {
            _modifier = modifier;
            icon.sprite = modifier.BaseMod.Icon;
            debuffImage.gameObject.SetActive(modifier.BaseMod.IsDebuff);

            // Счётчик только у тех, кто вообще умеет стакаться
            _stackable = modifier.MaxStacks > 1;
            stackCount.gameObject.SetActive(_stackable);

            duration.minValue = 0f;
            duration.maxValue = 1f;
        }

        public void Refresh()
        {
            bool timed = !_modifier.IsInfinity && _modifier.Duration > 0f;
            duration.value = timed ? _modifier.LifeTime / _modifier.Duration : 0f;

            if (_stackable)
                stackCount.SetTextIfChanged("{0}", _modifier.Stacks, ref _shownStacks);
        }
    }
}
