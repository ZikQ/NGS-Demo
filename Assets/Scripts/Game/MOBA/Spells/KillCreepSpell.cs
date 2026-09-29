using Assets.Scripts.Game.MOBA.Entities;
using NGS.Unity.Core.Interfaces;
using NGS.Unity.Core.SpellSystem.Spells;
using UnityEngine;

namespace Assets.Scripts.Game.MOBA.Spells
{
    [CreateAssetMenu(fileName = "KillCreepSpell", menuName = "Game/Spells/KillCreepSpell")]
    public class KillCreepSpell : Spell, ITargetFilter
    {
        private const string CreepsOnlyHint = "hint.creeps_only";

        public bool CanTarget(MobaCharacter caster, MobaCharacter target, out string hintKey)
        {
            hintKey = CreepsOnlyHint;
            return target != null && target.IsCreep();
        }

        public override void Execute(SpellCastContext context)
        {
            if (context.Target is not MobaCharacter creep || !creep.IsCreep() || !creep.IsTargetable)
                return;

            creep.TakeDamage(new HitData(creep.Health, null, context.Caster.Entity as MobaCharacter));
        }
    }
}
