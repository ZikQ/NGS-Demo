using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Game.MOBA.Entities;
using NGS.Unity.Core.EffectSystem;
using NGS.Unity.Core.ModifierSystem;
using NGS.Unity.Core.ModifierSystem.Aura;
using NGS.Unity.Core.SpellSystem;
using UnityEngine;

namespace Assets.Scripts.Game.MOBA.Modifiers
{
    [CreateAssetMenu(fileName = "BurnAuraModifier", menuName = "Game/Modifiers/BurnAuraModifier")]
    public class BurnAuraModifier : AuraModifier
    {
        private const string DurationSpecial = "duration";

        [SerializeField] private EffectDefinition effect;

        public override void OnCreated(ModifierInstance instance)
        {
            var aura = (AuraInstance)instance;
            var spell = instance.GetSourceSpell();

            if (spell != null)
                aura.RadiusOverride = spell.BaseSpell.GetRadius(spell);

            instance.AttachEffect(effect)?.SetRadius(aura.Radius).Play();
        }

        public override void OnRemove(ModifierInstance instance, bool purge)
        {
            if (purge)
                instance.ReleaseEffect();
        }

        public override IEnumerable<ModifierController> FilterTargets(ModifierController caster, IEnumerable<ModifierController> candidates)
        {
            var owner = caster.Entity as MobaCharacter;
            return candidates.Where(target => owner != null && owner.IsEnemy(target.Entity as MobaCharacter));
        }

        public override void OnAuraModifierAdded(ModifierController target, AuraInstance aura, ModifierInstance mod) =>
            mod.SetSourceSpell(aura.GetSourceSpell());

        public override float GetLingerDuration(AuraInstance aura, ModifierInstance mod)
        {
            var spell = aura.GetSourceSpell();
            return spell != null ? spell.CurrentSpecialFloat(DurationSpecial) : base.GetLingerDuration(aura, mod);
        }
    }
}
