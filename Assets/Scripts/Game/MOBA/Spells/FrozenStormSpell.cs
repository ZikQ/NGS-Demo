using System;
using Assets.Scripts.Game.MOBA.Entities;
using NGS.Unity.Core.EffectSystem;
using NGS.Unity.Core.Interfaces;
using NGS.Unity.Core.ProjectileSystem;
using NGS.Unity.Core.ProjectileSystem.Types;
using NGS.Unity.Core.SpellSystem.Spells;
using UnityEngine;

namespace Assets.Scripts.Game.MOBA.Spells
{
    [CreateAssetMenu(fileName = "FrozenStormSpell", menuName = "Game/Spells/FrozenStormSpell")]
    public class FrozenStormSpell : Spell
    {
        private const int PointCount = 4;

        [Header("Frozen Storm")]
        [SerializeField] private EffectDefinition projectileEffect;
        [SerializeField] private float projectileSpeed = 10f;

        [Tooltip("Сколько пролетает снаряд, если ни в кого не попал.")]
        [SerializeField] private float projectileRange = 10f;

        [SerializeField] private float damage;

        [Tooltip("Скорость вращения точек по орбите, градусов в секунду.")]
        [SerializeField] private float rotationSpeed = 90f;

        [Tooltip("Высота точек спавна над полом, от пивота кастера.")]
        [SerializeField] private float height;

        private Func<ProjectileHitInfo, bool> _onHit;

        public override void OnChannelTick(SpellCastContext context)
        {
            var owner = context.Caster.transform;
            var center = owner.position + Vector3.up * height;
            float radius = GetRadius(context.Instance);

            // Фаза от игрового времени: точки крутятся непрерывно, хранить состояние каста не нужно
            float phase = Time.time * rotationSpeed;

            _onHit ??= OnProjectileHit;

            for (int i = 0; i < PointCount; i++)
            {
                var direction = Quaternion.Euler(0f, phase + i * 360f / PointCount, 0f) * Vector3.forward;

                var projectile = ProjectileManager.Instance.Create<LinearProjectile>(projectileEffect.Id, owner,
                    center + direction * radius);
                if (projectile == null)
                    continue;

                projectile.SetVelocity(direction * projectileSpeed);
                projectile.ExpiredTime = projectileRange / projectileSpeed;
                projectile.OnHit = _onHit;
            }
        }

        private bool OnProjectileHit(ProjectileHitInfo hitInfo)
        {
            if (hitInfo.Target == null)
                return true;

            var caster = hitInfo.Projectile.Owner != null ? hitInfo.Projectile.Owner.GetComponent<MobaCharacter>() : null;
            var unit = hitInfo.Target.GetComponentInParent<MobaCharacter>();
            if (unit == null || caster == null || !caster.CanAttack(unit))
                return false;

            float amount = caster is HeroCharacter hero ? hero.ScaleSpellDamage(damage) : damage;
            unit.TakeDamage(new HitData(amount, null, caster));
            return true;
        }
    }
}
