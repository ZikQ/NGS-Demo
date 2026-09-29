using System.Collections.Generic;
using Assets.Scripts.Game.MOBA.Entities;
using NGS.Unity.Core.EffectSystem;
using NGS.Unity.Core.Interfaces;
using NGS.Unity.Core.ModifierSystem;
using NGS.Unity.Core.ProjectileSystem;
using NGS.Unity.Core.ProjectileSystem.Types;
using NGS.Unity.Core.SpellSystem;
using NGS.Unity.Core.SpellSystem.Spells;
using UnityEngine;

namespace Assets.Scripts.Game.MOBA.Spells
{
    [CreateAssetMenu(fileName ="ProjectileSpell", menuName ="Game/Spells/ProjectileSpell")]
    public class ProjectileSpell : Spell
    {
        [Header("Projetile Settings")]
        [SerializeField] private EffectDefinition projectileEffect;
        [SerializeField] private float ProjectileSpeed;
        [SerializeField] private float damage;

        [Header("On Hit")]
        [SerializeField] private Modifier hitModifier;
        [SerializeField] private float hitModifierDuration = 1f;

        private static readonly List<MobaCharacter> AreaTargets = new();

        public override void Execute(SpellCastContext context)
        {
            var caster = context.Caster.Entity as MobaCharacter;
            var spawn = caster != null ? caster.ProjectileSpawnPosition : null;
            var origin = spawn != null ? spawn.position : context.Caster.transform.position;

            var target = context.Target as MobaCharacter;

            if ((SpellType & SpellType.Unit) != 0 && target != null)
                Launch(context, caster, origin, GetCenter(target));
            else if ((SpellType & SpellType.Point) != 0)
                Launch(context, caster, origin, new Vector3(context.Point.x, origin.y, context.Point.z));
        }

        private static Vector3 GetCenter(MobaCharacter unit)
        {
            var unitCollider = unit.GetComponentInChildren<Collider>();
            return unitCollider != null ? unitCollider.bounds.center : unit.transform.position;
        }

        private void Launch(SpellCastContext context, MobaCharacter caster, Vector3 origin, Vector3 destination)
        {
            var toDestination = destination - origin;
            float distance = toDestination.magnitude;
            if (distance < 0.01f)
                return;

            var projectile = ProjectileManager.Instance.Create<LinearProjectile>(
                projectileEffect.Id,
                context.Caster.transform,
                origin,
                configure: created =>
                {
                    created.SetVelocity(toDestination / distance * ProjectileSpeed);
                    created.ExpiredTime = distance / ProjectileSpeed;
                });

            float amount = caster is HeroCharacter hero ? hero.ScaleSpellDamage(damage) : damage;
            float radius = (SpellType & SpellType.AOE) != 0 ? GetRadius(context.Instance) : 0f;

            // Target == null — снаряд долетел до точки
            projectile.OnHit = hitInfo =>
            {
                // AOE летит сквозь всех и взрывается только в точке
                if (radius > 0f)
                {
                    if (hitInfo.Target != null)
                        return false;

                    HitArea(context, caster, hitInfo.ContactPoint, radius, amount);
                    return true;
                }

                if (hitInfo.Target == null)
                    return true;

                // Обычный пролетает сквозь землю, союзников и кастера, бьёт первого врага
                var unit = hitInfo.Target.GetComponentInParent<MobaCharacter>();
                if (unit == null || caster == null || !caster.CanAttack(unit))
                    return false;

                Hit(context, caster, unit, amount);
                return true;
            };
        }

        private void HitArea(SpellCastContext context, MobaCharacter caster, Vector3 center, float radius, float amount)
        {
            if (caster == null)
                return;

            // Сначала собираем: урон может убить юнита и выкинуть его из All посреди перебора
            AreaTargets.Clear();

            foreach (var unit in MobaCharacter.All)
            {
                var offset = unit.transform.position - center;
                offset.y = 0f;

                if (offset.sqrMagnitude <= radius * radius && caster.CanAttack(unit))
                    AreaTargets.Add(unit);
            }

            foreach (var unit in AreaTargets)
                Hit(context, caster, unit, amount);
        }

        private void Hit(SpellCastContext context, MobaCharacter caster, MobaCharacter unit, float amount)
        {
            unit.TakeDamage(new HitData(amount, null, caster));

            if (hitModifier == null || unit.IsDeath())
                return;

            var controller = unit.ModifierController;
            var mod = controller.CreateModifier(hitModifier, duration: hitModifierDuration, caster: caster);
            mod.SetSourceSpell(context.Instance);

            // Уже висящему AddModifier только сбрасывает время, длительность остаётся от прошлого наложения
            mod = controller.AddModifier(mod);
            if (mod != null)
                mod.Duration = hitModifierDuration;
        }
    }
}
