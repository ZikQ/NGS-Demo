using System;
using Cysharp.Threading.Tasks;
using NGS.Unity.Core.ModifierSystem;
using NGS.Unity.Core.SpellSystem;
using NGS.Unity.Core.SpellSystem.Spells;
using UnityEngine;

namespace Assets.Scripts.Game.MOBA.Spells
{
    [CreateAssetMenu(fileName = "SwitchableSpell", menuName = "Game/Spells/SwitchableSpell")]
    public class SwitchableSpell : Spell
    {
        [Header("Switchable")]
        [SerializeField] private Modifier modifier;

        [Tooltip("Тратить ману, пока спелл включён. Не хватает на очередное списание — спелл выключается.")]
        [SerializeField] private bool drainMana;

        [Tooltip("Сколько маны списывать за раз.")]
        [SerializeField, Min(0f)] private float drainAmount = 5f;

        [Tooltip("Раз во сколько секунд списывать.")]
        [SerializeField, Min(0.1f)] private float drainInterval = 1f;

        public bool IsActive(SpellInstance instance)
        {
            var controller = GetController(instance);
            return controller != null && FindActive(controller, instance) != null;
        }

        public override float GetManaCost(SpellInstance instance) =>
            IsActive(instance) ? 0f : base.GetManaCost(instance);

        public override float GetCooldown(SpellInstance instance) =>
            IsActive(instance) ? 0f : base.GetCooldown(instance);

        public override void Execute(SpellCastContext context)
        {
            var instance = context.Instance;
            var controller = GetController(instance);
            if (controller == null || modifier == null)
                return;

            var active = FindActive(controller, instance);
            if (active != null)
            {
                controller.RemoveModifier(active, purge: true);
                return;
            }

            var mod = controller.CreateModifier(modifier, caster: controller.Entity);
            // Спелл нужен модификатору уже в OnCreated, а тот вызывается внутри AddModifier
            mod.SetSourceSpell(instance);

            mod = controller.AddModifier(mod);
            if (mod == null)
                return;

            StartCooldownOnRemove(instance, controller, mod);

            if (drainMana && drainAmount > 0f)
                DrainMana(instance, controller, mod).Forget();
        }

        // Выключить могут не только кастом: кончилась мана, умер кастер, развеяли.
        // При касте CastProcessor следом поставит тот же кулдаун ещё раз, это не мешает
        private void StartCooldownOnRemove(SpellInstance instance, ModifierController controller, ModifierInstance mod)
        {
            void OnRemoved(ModifierInstance removed, bool purged)
            {
                if (removed != mod || !purged)
                    return;

                controller.OnModifierRemoved -= OnRemoved;
                StartCooldown(instance);
            }

            controller.OnModifierRemoved += OnRemoved;
        }

        private void StartCooldown(SpellInstance instance)
        {
            var caster = instance.Owner;
            float reduction = caster.CooldownReduction?.Value ?? 0f;

            caster.SetCooldown(instance, base.GetCooldown(instance) * Mathf.Clamp01(1f - reduction));
        }

        private async UniTaskVoid DrainMana(SpellInstance instance, ModifierController controller, ModifierInstance mod)
        {
            var caster = instance.Owner;
            var token = caster.destroyCancellationToken;
            var interval = TimeSpan.FromSeconds(drainInterval);

            while (!await UniTask.Delay(interval, cancellationToken: token).SuppressCancellationThrow())
            {
                // Спелл уже выключили, а может и включили заново со своим списанием
                if (FindActive(controller, instance) != mod)
                    return;

                if (caster.Mana.Value < drainAmount)
                {
                    controller.RemoveModifier(mod, purge: true);
                    return;
                }

                caster.Mana.Value -= drainAmount;
            }
        }

        private ModifierInstance FindActive(ModifierController controller, SpellInstance instance)
        {
            var modifiers = controller.Modifiers;
            for (int i = 0; i < modifiers.Count; i++)
            {
                var mod = modifiers[i];
                if (mod != null && mod.BaseMod == modifier && mod.GetSourceSpell() == instance)
                    return mod;
            }

            return null;
        }

        private static ModifierController GetController(SpellInstance instance) =>
            instance.Owner.Entity?.Get<ModifierController>();
    }
}
