using NGS.Unity.Core.ModifierSystem;
using NGS.Unity.Core.SpellSystem;
using NGS.Unity.Core.SpellSystem.Spells;
using UnityEngine;

namespace Assets.Scripts.Game.MOBA.Modifiers
{
    [CreateAssetMenu(fileName = "SadistBuff", menuName = "Game/Modifiers/SadistBuff")]
    public class SadistBuff : Modifier
    {
        private const string DurationSpecial = "duration";
        private const string MaxStacksSpecial = "max_stacks";
        private const string StacksPerCastSpecial = "stacks_per_cast";

        [SerializeField] private StatBuffModifier buff;

        public override void OnSpellCast(ModifierInstance instance, SpellCastContext ctx)
        {
            var spell = instance.GetSourceSpell();
            if (buff == null || spell == null || ctx.IsItem || ctx.Caster.Entity != instance.Owner)
                return;

            var controller = instance.Owner.Get<ModifierController>();
            if (controller == null)
                return;

            float duration = spell.CurrentSpecialFloat(DurationSpecial);
            int maxStacks = spell.CurrentSpecialInt(MaxStacksSpecial);

            var mod = controller.CreateModifier(buff,
                stacks: Mathf.Max(1, spell.CurrentSpecialInt(StacksPerCastSpecial)),
                duration: duration > 0f ? duration : -1f,
                caster: instance.Owner);

            mod.SetSourceSpell(spell);
            if (maxStacks > 0)
                mod.MaxStacks = maxStacks;

            mod = controller.AddModifier(mod);

            if (mod != null)
                mod.Duration = duration > 0f ? duration : -1f;
        }
    }
}
