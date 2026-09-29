using System;
using System.Collections.Generic;
using NGS.Unity.Core.ModifierSystem;
using NGS.Unity.Core.SpellSystem;
using NGS.Unity.Core.StatsSystem;
using UnityEngine;

namespace Assets.Scripts.Game.MOBA.Modifiers
{
    [CreateAssetMenu(fileName = "StatBuffModifier", menuName = "Game/Modifiers/StatBuffModifier")]
    public class StatBuffModifier : Modifier
    {
        [Serializable]
        public class SpecialStat
        {
            [Tooltip("Стат, например spell_amp.")]
            public string stat;

            public StatModifierType type;

            [Tooltip("Спец-параметр спелла-источника со значением за один стак.")]
            public string special;

            [Tooltip("В спелле проценты, а стат хранится долей, как spell_amp: 5 → 0.05.")]
            public bool percent;
        }

        [SerializeField] private List<SpecialStat> specialStats = new();

        public override IEnumerable<StatModifier> GetStatModifiers(ModifierInstance instance)
        {
            foreach (var stat in base.GetStatModifiers(instance))
                yield return new StatModifier(stat.Name, stat.Value * instance.Stacks, stat.Type);

            var spell = instance.GetSourceSpell();
            if (spell == null)
                yield break;

            foreach (var stat in specialStats)
            {
                float value = spell.CurrentSpecialFloat(stat.special);
                if (stat.percent)
                    value /= 100f;

                yield return new StatModifier(stat.stat, value * instance.Stacks, stat.type);
            }
        }
    }
}
