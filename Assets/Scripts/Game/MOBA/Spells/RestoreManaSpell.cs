using NGS.Unity.Core.SpellSystem.Spells;
using UnityEngine;

namespace Assets.Scripts.Game.MOBA.Spells
{
    [CreateAssetMenu(fileName = "RestoreManaSpell", menuName = "Game/Spells/RestoreManaSpell")]
    public class RestoreManaSpell : Spell
    {
        [Header("Restore Mana")]
        [SerializeField] private float amount = 100f;

        public override void Execute(SpellCastContext context) => context.Caster.RestoreMana(amount);
    }
}
