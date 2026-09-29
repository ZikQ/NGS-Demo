using System;
using System.Collections.Generic;
using Assets.Scripts.Game.MOBA.Conditions;
using NGS.Unity.Core.EffectSystem;
using NGS.Unity.Core.EntitySystem;
using NGS.Unity.Core.Interfaces;
using NGS.Unity.Core.ProjectileSystem;
using NGS.Unity.Core.ProjectileSystem.Types;
using NGS.Unity.Core.Rendering;
using NGS.Unity.Core.SkinSystem;
using NGS.Unity.Core.StatsSystem;
using NGS.Unity.Core.Utils;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Assets.Scripts.Game.MOBA.Entities
{
    public enum OrderType
    {
        None,
        Run,
        Cast,
        Attack
    }

    public enum Team
    {
        Light,
        Dark,
        Neutral,
    }

    public enum EntityType
    {
        Hero,
        Creep,
        Tower
    }

    public class MobaCharacter : Character
    {
        private const float SeparationWeight = 2f;
        private const string HitEvent = "hit";

        // Поиск цели перебирает всех юнитов, поэтому не чаще раза в интервал
        private const float AcquireInterval = 0.2f;

        private static readonly int AttackHash = Animator.StringToHash("attack");
        private static readonly List<MobaCharacter> ActiveUnits = new();
        private static readonly Dictionary<Collider, MobaCharacter> UnitsByHitbox = new();
        private static readonly Collider[] NeighbourBuffer = new Collider[32];
        private static float _maxColliderRadius;

        [SerializeField] private Team team;
        [SerializeField] private EntityType entityType;
        [SerializeField] private float stopDistance = 0.1f;

        [Tooltip("Слои хитбоксов юнитов: среди них ищутся соседи, от которых юнит отходит")]
        [SerializeField] private LayerMask unitMask = ~0;

        [Header("Attack")]
        [SerializeField] private float attackDamage = 20f;

        [Tooltip("Разброс урона (+-N)")]
        [SerializeField] private float attackDamageSpread = 3f;
        [SerializeField] private float attackRange = 1.5f;
        [SerializeField] private float attackInterval = 1.2f;
        [SerializeField] private DamageType attackDamageType;

        [Header("Range attack")]
        [SerializeField] private bool isAttackRange;
        [SerializeField] private Transform projectileSpawnPosition;
        [SerializeField] private EffectDefinition projectileEffect;
        [SerializeField] private int projectileSpeed;
        
        
        [Header("Gizmos")]
        [SerializeField] private MobaGizmoSettings gizmoSettings = new();

        private Collider _hitbox;
        private float _colliderRadius;
        private float _nextAcquireTime;
        private Vector3 _movePoint;
        private Vector3 _lookPoint;
        private bool _hasLookPoint;
        private MobaCharacter _attackTarget;
        private MobaCharacter _swingTarget;
        private AnimationTriggerRelay _attackRelay;
        private FlashTarget _flashTarget;
        private float _nextAttackTime;
        
        public static IReadOnlyList<MobaCharacter> All => ActiveUnits;

        public Team Team => team;

        public Collider Hitbox => _hitbox;

        public OrderType CurrentOrder { get; protected set; }
        public MobaCharacter AttackTarget => _attackTarget;
        public Transform ProjectileSpawnPosition => projectileSpawnPosition;
        public Stat AttackDamageStat { get; private set; }
        public float AttackDamage => AttackDamageStat.Value;
        public float AttackRange => attackRange;

        public bool IsTargetable => isActiveAndEnabled && !IsDeath();

        public bool IsStunned { get; private set; }
        public bool IsSilenced { get; private set; }
        public bool CanCast => !IsStunned && !IsSilenced;

        private int TargetPriority => entityType switch
        {
            EntityType.Creep => 0,
            EntityType.Hero => 1,
            _ => 2,
        };

        protected virtual bool IsBusy => false;
        protected virtual float LeashRange => float.PositiveInfinity;

        protected EffectDefinition ProjectileEffect => projectileEffect;
        protected Vector3 ProjectileOrigin => projectileSpawnPosition != null ? projectileSpawnPosition.position : transform.position;
    
        protected MobaGizmoSettings GizmoSettings => gizmoSettings;
        
        protected SkinController SkinController { get; private set; }
        
        protected override Vector3 LookDirection =>
            _hasLookPoint ? _lookPoint - transform.position : base.LookDirection;

        public event Action<Team> TeamChanged;

        public void SetTeam(Team value)
        {
            if (team == value)
                return;

            team = value;
            TeamChanged?.Invoke(team);
        }

        public bool IsEnemy(MobaCharacter other) => other != null && other.team != team;

        public bool IsAlly(MobaCharacter other) => other != null && other.team == team;

        public bool IsCreep() => entityType == EntityType.Creep;
        public bool IsHero() => entityType == EntityType.Hero;
        public bool IsTower() => entityType == EntityType.Tower;

        public bool CanAttack(MobaCharacter target) =>
            target != null && target != this && target.IsTargetable && IsEnemy(target);

        public float RollAttackDamage() =>
            Mathf.Max(0f, Random.Range(AttackDamage - attackDamageSpread, AttackDamage + attackDamageSpread));

        public void OrderRun(Vector3 point)
        {
            OrderStop();
            _movePoint = point;
            CurrentOrder = OrderType.Run;
        }

        public bool OrderAttack(MobaCharacter target)
        {
            if (!CanAttack(target))
                return false;

            _attackTarget = target;
            CurrentOrder = OrderType.Attack;
            return true;
        }

        public void OrderStop()
        {
            _attackTarget = null;
            CurrentOrder = OrderType.None;
            SetControls(Vector3.zero, false, false);
        }

        public override void OnSpawn()
        {
            base.OnSpawn();

            OrderStop();
            _hasLookPoint = false;
            _swingTarget = null;
            _nextAttackTime = 0f;

            // Волна появляется разом, поиск целей её крипов разносим по разным кадрам
            _nextAcquireTime = Time.time + Random.Range(0f, AcquireInterval);
        }

        protected override void Awake()
        {
            base.Awake();

            _hitbox = GetComponentInChildren<Collider>();
            _colliderRadius = GetColliderRadius();
            _maxColliderRadius = Mathf.Max(_maxColliderRadius, _colliderRadius);
            _flashTarget = GetComponent<FlashTarget>();
            SkinController = GetComponent<SkinController>();

            AttackDamageStat = new Stat("attack_damage", attackDamage);
            Stats.AddStat(AttackDamageStat);

            if (StateMachine != null)
                StateMachine.OnConditionsChanged += RefreshConditions;
        }

        protected virtual void Start()
        {
            if (SkinController != null)
                _attackRelay = SkinController.AnimationEvents;
            else if (Animator != null)
                _attackRelay = AnimationTriggerRelay.For(Animator);

            if (_attackRelay != null)
                _attackRelay.Subscribe(HitEvent, OnAttackHit);
        }

        protected virtual void OnEnable()
        {
            ActiveUnits.Add(this);

            if (_hitbox != null)
                UnitsByHitbox[_hitbox] = this;
        }

        protected virtual void OnDisable()
        {
            ActiveUnits.Remove(this);

            if (_hitbox != null)
                UnitsByHitbox.Remove(_hitbox);
        }

        private void OnDrawGizmos()
        {
            if (gizmoSettings.alwaysShow)
                DrawRadii();
        }

        private void OnDrawGizmosSelected()
        {
            if (!gizmoSettings.alwaysShow)
                DrawRadii();
        }

        protected virtual void DrawRadii()
        {
            DrawRadius(gizmoSettings.stopDistance, stopDistance);
            DrawRadius(gizmoSettings.attack, attackRange);
        }

        protected void DrawRadius(GizmoRadius radius, float value)
        {
            if (!radius.show || value <= 0f)
                return;

            const int segments = 48;

            Gizmos.color = radius.color;

            var center = transform.position;
            var previous = center + new Vector3(value, 0f, 0f);

            for (int i = 1; i <= segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                var next = center + new Vector3(Mathf.Cos(angle) * value, 0f, Mathf.Sin(angle) * value);
                Gizmos.DrawLine(previous, next);
                previous = next;
            }
        }

        protected override void OnDamageTaken(HitData hitData)
        {
            _flashTarget.Play();
        }

        /// <summary>Набор условий поменялся, IsStunned и IsSilenced уже пересчитаны.</summary>
        protected virtual void OnConditionsChanged()
        {
        }

        protected override void Update()
        {
            if (IsDeath() || IsStunned)
            {
                _hasLookPoint = false;
                SetControls(Vector3.zero, false, false);
            }
            else if (IsBusy)
            {
                SetControls(Vector3.zero, false, false);
            }
            else
            {
                UpdateOrder();
                ApplySeparation();
            }

            base.Update();
        }

        protected virtual void UpdateOrder()
        {
            switch (CurrentOrder)
            {
                case OrderType.None:
                    UpdateIdle();
                    break;
                case OrderType.Run:
                    UpdateRun();
                    break;
                case OrderType.Attack:
                    UpdateAttack();
                    break;
            }
        }

        protected virtual void UpdateIdle()
        {
        }

        /// <summary>Запускает анимацию. Без аниматора урон мгновенный/summary>
        protected virtual void PerformAttack(MobaCharacter target)
        {
            if (_attackRelay == null || Animator == null)
            {
                Strike(target);
                return;
            }

            _swingTarget = target;
            Animator.SetTrigger(AttackHash);
        }

        protected void DealAttackDamage(MobaCharacter target)
        {
            target.TakeDamage(new HitData(RollAttackDamage(), attackDamageType, this));
        }

        /// <summary>крипы -> герои -> башни. Внутри группы ближайший.</summary>
        protected MobaCharacter FindTarget(float range)
        {
            MobaCharacter best = null;
            float bestDistance = 0f;

            foreach (var unit in ActiveUnits)
            {
                if (!CanAttack(unit))
                    continue;

                float distance = DistanceTo(unit);
                if (distance > range)
                    continue;

                if (best == null || unit.TargetPriority < best.TargetPriority
                    || (unit.TargetPriority == best.TargetPriority && distance < bestDistance))
                {
                    best = unit;
                    bestDistance = distance;
                }
            }

            return best;
        }

        /// <summary>Выбор цели для робота, не чаще раза в AcquireInterval</summary>
        protected void AcquireTarget(float range)
        {
            if (Time.time < _nextAcquireTime)
                return;

            _nextAcquireTime = Time.time + AcquireInterval;

            var best = FindTarget(range);
            if (best == null)
                return;

            bool keepCurrent = CurrentOrder == OrderType.Attack && CanAttack(_attackTarget)
                && DistanceTo(_attackTarget) <= range && _attackTarget.TargetPriority <= best.TargetPriority;

            if (!keepCurrent)
                OrderAttack(best);
        }

        protected float DistanceTo(MobaCharacter other) =>
            FlatDistance(other.transform.position) - other._colliderRadius;

        protected float FlatDistance(Vector3 point)
        {
            var offset = point - transform.position;
            offset.y = 0f;
            return offset.magnitude;
        }

        protected void RunTowards(Vector3 point)
        {
            var dir = point - transform.position;
            dir.y = 0f;

            _hasLookPoint = false;
            SetControls(dir.normalized, false, true);
        }

        protected void LookAt(Vector3 point)
        {
            _lookPoint = point;
            _hasLookPoint = true;
        }

        private void RefreshConditions()
        {
            bool stunned = false;
            bool silenced = false;

            foreach (var condition in StateMachine.ActiveConditions)
            {
                stunned |= condition is StunCondition;
                silenced |= condition is SilenceCondition;
            }

            IsStunned = stunned;
            IsSilenced = silenced;

            // Замах, начатый до стана, не должен долететь уроном
            if (stunned)
                _swingTarget = null;

            OnConditionsChanged();
        }

        private void OnAttackHit(AnimationEvent animationEvent)
        {
            // Пока шёл замах, цель могла умереть или уйти в пул
            if (IsDeath() || !CanAttack(_swingTarget))
                return;

            Strike(_swingTarget);

            _swingTarget = null;
        }

        private void Strike(MobaCharacter target)
        {
            if (isAttackRange)
                LaunchAttackProjectile(target);
            else
                DealAttackDamage(target);
        }

        protected virtual void LaunchAttackProjectile(MobaCharacter target)
        {
            var targetCollider = target._hitbox;

            var projectile = ProjectileManager.Instance.Create<TrackingProjectile>(
                projectileEffect.Id,
                transform,
                ProjectileOrigin,
                configure: created =>
                {
                    created.Target = target.transform;
                    created.Speed = projectileSpeed;

                    if (targetCollider != null)
                        created.Offset = targetCollider.bounds.center - target.transform.position;
                });

            projectile.OnTargetLost = lost => ProjectileManager.Instance.Release(lost);
            projectile.OnHit = hitInfo => HitAttackTarget(hitInfo, target);
        }

        // Снаряд атаки проходит сквозь всех, кроме своей цели, и гаснет на ней
        protected bool HitAttackTarget(ProjectileHitInfo hitInfo, MobaCharacter target)
        {
            if (hitInfo.Target == null)
                return true;

            if (hitInfo.Target.GetComponentInParent<MobaCharacter>() != target)
                return false;

            if (CanAttack(target))
                DealAttackDamage(target);

            return true;
        }

        private void UpdateRun()
        {
            if (FlatDistance(_movePoint) <= Mathf.Max(stopDistance, RunSpeed * Time.deltaTime))
            {
                OrderStop();
                return;
            }

            RunTowards(_movePoint);
        }

        private void UpdateAttack()
        {
            if (!CanAttack(_attackTarget) || DistanceTo(_attackTarget) > LeashRange)
            {
                OrderStop();
                return;
            }

            var targetPosition = _attackTarget.transform.position;

            if (DistanceTo(_attackTarget) > attackRange)
            {
                RunTowards(targetPosition);
                return;
            }

            SetControls(Vector3.zero, false, false);

            if (!IsTower())
                LookAt(targetPosition);

            if (Time.time < _nextAttackTime)
                return;

            _nextAttackTime = Time.time + attackInterval;
            PerformAttack(_attackTarget);
        }

        private void ApplySeparation()
        {
            if (IsTower())
                return;

            var push = GetSeparation();
            if (push == Vector3.zero)
                return;

            if (IsMoving)
                SetControls((MoveDir + push * SeparationWeight).normalized, false, true);
            else
                SetControls(push.normalized, true, false);
        }

        private Vector3 GetSeparation()
        {
            var push = Vector3.zero;
            bool moving = IsMoving;

            var center = _hitbox != null ? _hitbox.bounds.center : transform.position;
            int count = Physics.OverlapSphereNonAlloc(center, _colliderRadius + _maxColliderRadius, NeighbourBuffer,
                unitMask, QueryTriggerInteraction.Ignore);

            for (int i = 0; i < count; i++)
            {
                if (!UnitsByHitbox.TryGetValue(NeighbourBuffer[i], out var unit) || unit == this || !unit.IsTargetable)
                    continue;

                // Стоящий не уступает бегущему: обходить должен тот, кто движется
                if (!moving && unit.IsMoving)
                    continue;

                var offset = transform.position - unit.transform.position;
                offset.y = 0f;

                float minDistance = _colliderRadius + unit._colliderRadius;
                float distance = offset.magnitude;
                if (distance >= minDistance)
                    continue;

                var away = distance > 0.001f ? offset / distance : RandomFlatDirection();

                var side = Vector3.Cross(Vector3.up, away) * 0.5f;
                push += (away + side) * (1f - distance / minDistance);
            }

            return push;
        }

        private float GetColliderRadius()
        {
            if (_hitbox == null)
                return 0f;

            var extents = _hitbox.bounds.extents;
            return Mathf.Max(extents.x, extents.z);
        }

        private static Vector3 RandomFlatDirection()
        {
            var circle = Random.insideUnitCircle.normalized;
            return new Vector3(circle.x, 0f, circle.y);
        }

    }
}
