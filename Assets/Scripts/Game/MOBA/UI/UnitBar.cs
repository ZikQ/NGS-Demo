using Assets.Scripts.Game.MOBA.Entities;
using NGS.Unity.Core.SpellSystem;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.Game.MOBA.UI
{
    public class UnitBar : MonoBehaviour
    {
        [SerializeField] private Slider health;

        [Tooltip("Белый хвост урона под слайдером здоровья: после удара остаётся на старом месте и догоняет")]
        [SerializeField] private Slider damage;

        [Tooltip("Пусто у полоски без маны")]
        [SerializeField] private Slider mana;

        [Tooltip("Красится в цвет союзника или врага")]
        [SerializeField] private Image background;

        [SerializeField] private float damageDelay = 0.3f;
        [SerializeField] private float damageCatchUpDuration = 0.4f;

        private MobaCharacter _unit;
        private Caster _caster;

        private float _catchUpAt;
        private float _catchUpSpeed;

        public void Bind(MobaCharacter unit)
        {
            _unit = unit;
            _caster = unit.Get<Caster>();

            health.maxValue = unit.MaxHealth;
            damage.maxValue = unit.MaxHealth;
            health.value = unit.MaxHealth;
            damage.value = unit.MaxHealth;

            _catchUpSpeed = 0f;
        }

        public void Refresh(Color color)
        {
            float current = _unit.Health;

            health.maxValue = _unit.MaxHealth;
            damage.maxValue = _unit.MaxHealth;

            if (current < health.value)
            {
                _catchUpAt = Time.time + damageDelay;
                _catchUpSpeed = 0f;
            }

            health.value = current;
            RefreshDamage(current);

            background.color = color;

            if (mana != null && _caster != null)
            {
                mana.maxValue = _caster.Mana.MaxValue;
                mana.value = _caster.Mana.Value;
            }
        }

        private void RefreshDamage(float current)
        {
            // Лечение подтягивает хвост сразу
            if (current >= damage.value)
            {
                damage.value = current;
                return;
            }

            if (Time.time < _catchUpAt)
                return;

            if (_catchUpSpeed <= 0f)
                _catchUpSpeed = (damage.value - current) / damageCatchUpDuration;

            damage.value = Mathf.MoveTowards(damage.value, current, _catchUpSpeed * Time.deltaTime);
        }
    }
}
