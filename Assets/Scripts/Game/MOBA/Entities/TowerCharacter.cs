using System.Collections.Generic;
using NGS.Unity.Core.EntitySystem;
using NGS.Unity.Core.ProjectileSystem;
using NGS.Unity.Core.ProjectileSystem.Types;
using UnityEngine;

namespace Assets.Scripts.Game.MOBA.Entities
{
    public class TowerCharacter : MobaCharacter
    {
        private static readonly List<TowerCharacter> ActiveTowers = new();

        [Header("Creep waves")]
        [SerializeField] private EntityDefinition creepDefinition;
        [SerializeField] private int creepsPerWave = 4;
        [SerializeField] private float waveInterval = 30f;
        [SerializeField] private Transform creepSpawnPoint;
        [Tooltip("Расстояние между крипами в колонне, колонна строится от точки спавна назад от вражеской башни")]
        [SerializeField] private float creepSpacing = 1.5f;
        [SerializeField] private TowerCharacter enemyTower;

        private float _nextWaveTime;

        protected override float LeashRange => AttackRange;

        /// <summary>Внеочередная волна от каждой живой башни. Старые крипы остаются, таймер обычных волн не сбивается.</summary>
        public static void SpawnWaves()
        {
            foreach (var tower in ActiveTowers)
            {
                if (!tower.IsDeath())
                    tower.SpawnWave();
            }
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            ActiveTowers.Add(this);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            ActiveTowers.Remove(this);
        }

        protected override void Start()
        {
            base.Start();

            // Регистрируем сами, чтобы первая волна не зависела от асинхронного прогрева CoreBootstrap
            if (creepDefinition != null)
                EntityManager.Register(creepDefinition);
        }

        protected override void Update()
        {
            base.Update();

            if (IsDeath() || Time.time < _nextWaveTime)
                return;

            _nextWaveTime = Time.time + waveInterval;
            SpawnWave();
        }

        protected override void UpdateOrder()
        {
            AcquireTarget(AttackRange);
            base.UpdateOrder();
        }

        protected override void LaunchAttackProjectile(MobaCharacter target)
        {
            var end = target.Hitbox != null ? target.Hitbox.bounds.center : target.transform.position;

            var beam = ProjectileManager.Instance.Create<BeamProjectile>(
                ProjectileEffect.Id,
                transform,
                ProjectileOrigin,
                configure: created => created.SetEnd(end));

            beam.OnHit = hitInfo => HitAttackTarget(hitInfo, target);
        }

        private void SpawnWave()
        {
            var origin = creepSpawnPoint != null ? creepSpawnPoint : transform;

            var laneDirection = enemyTower != null ? enemyTower.transform.position - origin.position : origin.forward;
            laneDirection.y = 0f;
            laneDirection.Normalize();

            var rotation = Quaternion.LookRotation(laneDirection);

            for (int i = 0; i < creepsPerWave; i++)
            {
                var position = origin.position - laneDirection * (i * creepSpacing);
                var creep = EntityManager.Spawn<CreepCharacter>(creepDefinition, position);
                if (creep == null)
                    return;

                creep.Body.SetRotation(rotation);
                creep.SetTeam(Team);
                creep.SetLaneTarget(enemyTower);
            }
        }
    }
}
