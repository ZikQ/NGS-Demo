using Assets.Scripts.Game.MOBA.Entities;
using NGS.Unity.Core.EffectSystem;
using NGS.Unity.Core.Interfaces;
using NGS.Unity.Core.ModifierSystem;
using NGS.Unity.Core.SpellSystem;
using UnityEngine;

namespace Assets.Scripts.Game.MOBA.Modifiers
{
    [CreateAssetMenu(fileName = "TickDamage_Debuff", menuName = "Game/Modifiers/TickDamage_Debuff")]
    public class TickDamage_Debuff : Modifier
    {
        private const string DamageSpecial = "damage";

        [SerializeField] private EffectDefinition effect;

        public override void OnCreated(ModifierInstance instance) =>
            instance.AttachEffect(effect)?.Play();

        public override void OnIntervalThink(ModifierInstance instance)
        {
            var spell = instance.GetSourceSpell();
            if (spell == null || instance.Owner is not IDamageable victim || victim.IsDeath())
                return;

            var attacker = instance.Caster.IsAlive() ? instance.Caster as MobaCharacter : null;
            float damage = spell.CurrentSpecialFloat(DamageSpecial) * instance.Interval;

            if (attacker is HeroCharacter hero)
                damage = hero.ScaleSpellDamage(damage);

            victim.TakeDamage(new HitData(damage, null, attacker));
        }

        public override void OnRemove(ModifierInstance instance, bool purge)
        {
            if (purge)
                instance.ReleaseEffect();
        }
    }
}
