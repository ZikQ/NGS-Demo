using NGS.Unity.Core.EntitySystem;
using NGS.Unity.Core.SpellSystem.Spells;
using UnityEngine;

namespace Assets.Scripts.Game.MOBA.Spells
{
    [CreateAssetMenu(fileName = "BlinkSpell", menuName = "Game/Spells/BlinkSpell")]
    public class BlinkSpell : Spell
    {
        public override void Execute(SpellCastContext context)
        {
            if (context.Caster.Entity is Entity entity)
                entity.Body.Teleport(context.Point);
        }
    }
}
