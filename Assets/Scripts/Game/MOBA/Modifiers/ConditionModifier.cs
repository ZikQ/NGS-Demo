using NGS.Unity.Core.ModifierSystem;
using NGS.Unity.Core.StateSystem;
using UnityEngine;

namespace Assets.Scripts.Game.MOBA.Modifiers
{
    [CreateAssetMenu(fileName = "ConditionModifier", menuName = "Game/Modifiers/ConditionModifier")]
    public class ConditionModifier : Modifier
    {
        [SerializeField] private CharacterCondition condition;

        public override void OnCreated(ModifierInstance instance) =>
            instance.Owner.Get<CharacterStateMachine>()?.AddCondition(condition);

        public override void OnRemove(ModifierInstance instance, bool purge)
        {
            if (purge)
                instance.Owner.Get<CharacterStateMachine>()?.RemoveCondition(condition);
        }
    }
}
