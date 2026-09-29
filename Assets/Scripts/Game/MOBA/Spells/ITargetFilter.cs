using Assets.Scripts.Game.MOBA.Entities;

namespace Assets.Scripts.Game.MOBA.Spells
{
    public interface ITargetFilter
    {
        bool CanTarget(MobaCharacter caster, MobaCharacter target, out string hintKey);
    }
}
