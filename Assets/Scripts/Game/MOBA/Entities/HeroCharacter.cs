using System.Collections;
using NGS.Unity.Core.EntitySystem;
using NGS.Unity.Core.InventorySystem;
using NGS.Unity.Core.SkinSystem;
using NGS.Unity.Core.SpellSystem;
using NGS.Unity.Core.SpellSystem.Spells;
using NGS.Unity.Core.StatsSystem;
using UnityEngine;

namespace Assets.Scripts.Game.MOBA.Entities
{
    [RequireComponent(typeof(Caster))]
    public class HeroCharacter : MobaCharacter
    {
        public const float HealthPerStrength = 20f;
        public const float HealthRegenPerStrength = 0.1f;
        public const float AttackDamagePerStrength = 1f;

        public const float ManaPerIntelligence = 12f;
        public const float ManaRegenPerIntelligence = 0.05f;
        public const float SpellAmpPerIntelligence = 0.01f;

        [SerializeField] private GameObject staff;
        [SerializeField] private AttachmentSlot rightHand;

        [Header("Attributes")]
        [SerializeField] private float strength = 20f;
        [SerializeField] private float intelligence = 20f;
        [SerializeField] private float healthRegenBase = 0.5f;

        protected AttachmentController AttachmentController;

        private Vector3 _spawnPosition;
        private Quaternion _spawnRotation;

        private int _castSpellIndex;
        private ItemInstance _castItem;
        private Vector3 _castPoint;
        private MobaCharacter _castTarget;

        public Caster Caster { get; private set; }
        public InventoryController Inventory { get; private set; }

        public Stat StrengthStat { get; private set; }
        public Stat IntelligenceStat { get; private set; }
        public Stat HealthRegenStat { get; private set; }

        /// <summary>Реген от максимума здоровья в секунду: 0.01 = 1%.</summary>
        public Stat HealthRegenPercentStat { get; private set; }

        /// <summary>Усиление урона заклинаний: 0.2 = +20%.</summary>
        public Stat SpellAmpStat { get; private set; }

        public float Strength => StrengthStat.Value;
        public float Intelligence => IntelligenceStat.Value;

        protected override bool IsBusy => Caster.IsCasting;

        public float ScaleSpellDamage(float damage) => damage * (1f + SpellAmpStat.Value);

        public bool OrderCast(int spellIndex, Vector3 point, MobaCharacter target = null)
        {
            var spell = Caster.GetSpell(spellIndex);
            if (spell == null || !CanCast || Caster.IsOnCooldown(spell) || !Caster.HasEnoughMana(spell))
                return false;

            OrderStop();
            _castSpellIndex = spellIndex;
            _castItem = null;
            _castPoint = point;
            _castTarget = target;
            CurrentOrder = OrderType.Cast;
            return true;
        }

        public bool OrderUseItem(ItemInstance item, Vector3 point, MobaCharacter target = null)
        {
            if (Inventory == null || IsStunned || !Inventory.CanUseItem(item))
                return false;

            OrderStop();
            _castItem = item;
            _castPoint = point;
            _castTarget = target;
            CurrentOrder = OrderType.Cast;
            return true;
        }

        protected override void Awake()
        {
            AttachmentController = GetComponent<AttachmentController>();
            base.Awake();

            Caster = GetComponent<Caster>();
            Inventory = GetComponent<InventoryController>();

            StrengthStat = new Stat("strength", strength);
            IntelligenceStat = new Stat("intelligence", intelligence);
            HealthRegenStat = new Stat("health_regen", healthRegenBase);
            HealthRegenPercentStat = new Stat("health_regen_percent");
            SpellAmpStat = new Stat("spell_amp");

            Stats.AddStat(StrengthStat);
            Stats.AddStat(IntelligenceStat);
            Stats.AddStat(HealthRegenStat);
            Stats.AddStat(HealthRegenPercentStat);
            Stats.AddStat(SpellAmpStat);
        }

        protected override void Start()
        {
            base.Start();

            _spawnPosition = transform.position;
            _spawnRotation = transform.rotation;

            Stats.AddBonus(HealthStat, () => Strength * HealthPerStrength);
            Stats.AddBonus(HealthRegenStat, () => Strength * HealthRegenPerStrength);
            Stats.AddBonus(HealthRegenStat, () => MaxHealth * HealthRegenPercentStat.Value);
            Stats.AddBonus(AttackDamageStat, () => Strength * AttackDamagePerStrength);

            Stats.AddBonus(Caster.Mana, () => Intelligence * ManaPerIntelligence);
            Stats.AddBonus(Caster.ManaRegen, () => Intelligence * ManaRegenPerIntelligence);
            Stats.AddBonus(SpellAmpStat, () => Intelligence * SpellAmpPerIntelligence);
        }

        protected override void Update()
        {
            base.Update();

            if (!IsDeath() && Health < MaxHealth)
                HealthStat.Value += HealthRegenStat.Value * Time.deltaTime;
        }

        protected override void Death()
        {
            base.Death();
            StartCoroutine(Respawn());
        }

        protected override void UpdateOrder()
        {
            if (CurrentOrder == OrderType.Cast)
                UpdateCast();
            else
                base.UpdateOrder();
        }

        protected override void OnConditionsChanged()
        {
            base.OnConditionsChanged();

            // Стан сбивает любой каст, сайленс — только спелл: предметы под сайленсом работают
            var cast = Caster.CurrentCast;
            if (cast != null && (IsStunned || (IsSilenced && !cast.IsItem)))
                Caster.CancelCast();
        }

        private void UpdateCast()
        {
            var spell = _castItem != null ? Inventory.GetItemSpell(_castItem.BaseItem) : Caster.GetSpell(_castSpellIndex);
            bool targetLost = _castTarget != null && !_castTarget.IsTargetable;

            // Предмет могли выкинуть по дороге, а сайленс запрещает только спеллы
            bool blocked = _castItem != null ? !Inventory.Contains(_castItem) : !CanCast;

            if (spell == null || targetLost || blocked)
            {
                OrderStop();
                return;
            }

            var point = _castTarget != null ? _castTarget.transform.position : _castPoint;
            float range = spell.BaseSpell.GetRange(spell);

            if (range > 0f && Vector3.Distance(transform.position, point) > range)
            {
                RunTowards(point);
                return;
            }

            OrderStop();
            LookAt(point);

            if (_castItem != null)
                Inventory.UseItem(_castItem, point, _castTarget);
            else
                Caster.CastSpell(new SpellCastContext(spell, Caster, point, _castTarget));
        }

        private IEnumerator Respawn()
        {
            yield return null;

            Body.Teleport(_spawnPosition);
            Body.SetRotation(_spawnRotation);
            Body.Stop();

            OnSpawn();
            Caster.Mana.Value = Caster.Mana.MaxValue;

            // Пережившие смерть модификаторы восстанавливают свои эффекты
            ModifierController.NotifyOwnerEnabled();
        }
    }
}
