using System;
using System.Collections.Generic;
using Assets.Scripts.Game.MOBA.Entities;
using Assets.Scripts.Game.MOBA.Spells;
using NGS.Unity.Core.InventorySystem;
using NGS.Unity.Core.Rendering;
using NGS.Unity.Core.SpellSystem.Spells;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Assets.Scripts.Game.MOBA.Player
{
    public class PlayerOrderInput : MonoBehaviour
    {
        /// <summary>Клавиши слотов спеллов, индекс — слот в Caster.</summary>
        public static readonly IReadOnlyList<Key> SpellKeys = new[] { Key.Q, Key.W, Key.E, Key.R };

        private const string StunHint = "hint.stun";
        private const string SilenceHint = "hint.silence";

        [SerializeField] private Camera viewCamera;
        [SerializeField] private HeroCharacter character;

        [Tooltip("Кем ещё, кроме Character, может управлять игрок: ЛКМ по такому герою берёт управление")]
        [SerializeField] private List<HeroCharacter> controllable = new();

        [SerializeField] private LayerMask groundMask = ~0;
        [SerializeField] private LayerMask unitMask = ~0;

        [Tooltip("Вспышка на юните, по которому кликнули или на которого скастовали")]
        [SerializeField] private FlashSettings clickFlash = new()
        {
            color = new Color(0.55f, 1f, 0.2f),
            secondColor = new Color(0.55f, 1f, 0.2f),
            intensity = 0.7f,
            additive = 0.5f,
            duration = 0.35f,
        };

        [Tooltip("Материал кругов дальности и AOE. Цвет задаётся вертекс-цветом линии, " +
                 "поэтому шейдер должен его учитывать, например Sprites/Default")]
        [SerializeField] private Material circleMaterial;

        [Header("AOE radius")]
        [SerializeField] private Color aoeRadiusColor = new(0.2f, 0.5f, 1f);
        [SerializeField] private float aoeRadiusWidth = 0.08f;

        [Header("Cast range")]
        [SerializeField] private Color castRangeColor = new(1f, 1f, 1f, 0.5f);
        [SerializeField] private float castRangeWidth = 0.05f;

        // Слот спелла или предмет, для которого ждём клик по цели. Ждём что-то одно
        private int _pendingSpell = -1;
        private ItemInstance _pendingItem;

        private GroundCircle _aoeCircle;
        private GroundCircle _rangeCircle;

        private HeroCharacter _playerHero;
        public HeroCharacter Hero => character;
        public Team PlayerTeam => _playerHero.Team;
        public int PendingSpell => _pendingSpell;
        public ItemInstance PendingItem => _pendingItem;
        public MobaCharacter Selected { get; private set; }

        public event Action<MobaCharacter> SelectionChanged;
        public event Action<string> HintRequested;

        private bool HasPending => _pendingSpell >= 0 || _pendingItem != null;

        private void Awake() => _playerHero = character;

        private void Start()
        {
            if (!viewCamera)
            {
                viewCamera = Camera.main;
            }

            _aoeCircle = new GroundCircle("AoeRadius", circleMaterial, aoeRadiusColor, aoeRadiusWidth);
            _rangeCircle = new GroundCircle("CastRange", circleMaterial, castRangeColor, castRangeWidth);

            Select(character);
        }

        private void Update()
        {
            if (Selected != character && (Selected == null || !Selected.IsTargetable))
                Select(character);

            // Пока выбирали цель, спелл или предмет мог уйти на кулдаун, кончилась мана или предмет выкинули
            if (_pendingSpell >= 0 && !CanStartCast(_pendingSpell))
                _pendingSpell = -1;

            if (_pendingItem != null && !CanStartUse(_pendingItem))
                _pendingItem = null;

            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                for (int i = 0; i < SpellKeys.Count; i++)
                {
                    if (keyboard[SpellKeys[i]].wasPressedThisFrame)
                        SelectSpell(i);
                }

                if (keyboard.escapeKey.wasPressedThisFrame)
                    CancelPending();
            }

            var mouse = Mouse.current;
            if (mouse == null)
                return;

            var ray = viewCamera.ScreenPointToRay(mouse.position.ReadValue());

            UpdateSpellCircles(ray);

            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;

            if (mouse.leftButton.wasPressedThisFrame)
            {
                if (HasPending)
                    CastPending(ray);
                else
                    SelectUnderCursor(ray);
            }

            if (mouse.rightButton.wasPressedThisFrame)
            {
                CancelPending();

                if (ReportBlocked(bySilence: false))
                    return;

                // Новый приказ прерывает каст и каналинг, иначе он ждал бы их конца
                if (character.Caster.IsCasting)
                    character.Caster.CancelCast();

                IssueOrder(ray);
            }
        }

        public void SelectSpell(int slot)
        {
            Select(character);

            if (ReportBlocked(bySilence: true) || !CanStartCast(slot))
                return;

            _pendingItem = null;

            var type = character.Caster.GetSpell(slot).BaseSpell.SpellType;

            if ((type & (SpellType.Point | SpellType.Unit)) == 0)
            {
                _pendingSpell = -1;
                character.OrderCast(slot, character.transform.position, (type & SpellType.Self) != 0 ? character : null);
                return;
            }

            _pendingSpell = slot;
        }

        public void SelectItem(ItemInstance item)
        {
            Select(character);

            if (ReportBlocked(bySilence: false) || !CanStartUse(item))
                return;

            _pendingSpell = -1;

            var type = item.BaseItem.UseSpell.SpellType;

            if ((type & (SpellType.Point | SpellType.Unit)) == 0)
            {
                _pendingItem = null;
                character.OrderUseItem(item, character.transform.position, (type & SpellType.Self) != 0 ? character : null);
                return;
            }

            _pendingItem = item;
        }

        private void Select(MobaCharacter unit)
        {
            if (Selected == unit)
                return;

            Selected = unit;
            SelectionChanged?.Invoke(Selected);
        }

        private void IssueOrder(Ray ray)
        {
            Select(character);

            var unit = RaycastUnit(ray);
            if (unit != null)
                Highlight(unit);

            if (character.OrderAttack(unit))
                return;

            if (RaycastGround(ray, out var point))
                character.OrderRun(point);
        }

        private void SelectUnderCursor(Ray ray)
        {
            var unit = RaycastUnit(ray);
            if (unit == null)
                return;

            if (unit is HeroCharacter hero && CanControl(hero))
                TakeControl(hero);

            Select(unit);
            Highlight(unit);
        }

        private bool CanControl(HeroCharacter hero) =>
            hero != character && hero.IsTargetable && (hero == _playerHero || controllable.Contains(hero));

        private void TakeControl(HeroCharacter hero)
        {
            CancelPending();
            character = hero;
        }

        private bool ReportBlocked(bool bySilence)
        {
            string hint = character.IsStunned ? StunHint
                : bySilence && character.IsSilenced ? SilenceHint
                : null;

            if (hint == null)
                return false;

            HintRequested?.Invoke(hint);
            return true;
        }

        private bool CanStartCast(int slot)
        {
            var spell = character.Caster.GetSpell(slot);

            return spell != null && !spell.BaseSpell.IsPassive && !character.IsDeath() && character.CanCast
                   && !character.Caster.IsOnCooldown(spell) && character.Caster.HasEnoughMana(spell);
        }

        private bool CanStartUse(ItemInstance item) =>
            character.Inventory != null && !character.IsDeath() && !character.IsStunned
            && character.Inventory.CanUseItem(item);

        private SpellInstance GetPendingSpell()
        {
            if (_pendingItem != null)
                return character.Inventory.GetItemSpell(_pendingItem.BaseItem);

            return _pendingSpell >= 0 ? character.Caster.GetSpell(_pendingSpell) : null;
        }

        private void CastPending(Ray ray)
        {
            var spell = GetPendingSpell().BaseSpell;
            var type = spell.SpellType;
            var unit = RaycastUnit(ray);

            if ((type & SpellType.Unit) != 0 && unit != null)
            {
                // Цель не подходит: подсказываем и ждём следующий клик
                if (spell is ITargetFilter filter && !filter.CanTarget(character, unit, out string hint))
                {
                    HintRequested?.Invoke(hint);
                    return;
                }

                if (OrderPending(unit.transform.position, unit))
                    Highlight(unit);
            }
            else if ((type & SpellType.Point) != 0 && RaycastGround(ray, out var point))
            {
                OrderPending(point, null);
            }
            else
            {
                // Спелл только на юнита, а кликнули в пустое место: ждём следующий клик
                return;
            }

            CancelPending();
        }

        private bool OrderPending(Vector3 point, MobaCharacter target) =>
            _pendingItem != null
                ? character.OrderUseItem(_pendingItem, point, target)
                : character.OrderCast(_pendingSpell, point, target);

        private void CancelPending()
        {
            _pendingSpell = -1;
            _pendingItem = null;
        }

        // Пока выбирается цель: дальность каста вокруг героя и радиус AOE под курсором
        private void UpdateSpellCircles(Ray ray)
        {
            var spell = GetPendingSpell();

            if (spell == null)
            {
                _rangeCircle.Hide();
                _aoeCircle.Hide();
                return;
            }

            float range = spell.BaseSpell.GetRange(spell);

            if (range > 0f)
                _rangeCircle.Show(character.transform.position, range);
            else
                _rangeCircle.Hide();

            var type = spell.BaseSpell.SpellType;
            float radius = (type & SpellType.AOE) != 0 ? spell.BaseSpell.GetRadius(spell) : 0f;

            // Спелл на юнита взорвётся на юните, а не на земле за ним
            var unit = (type & SpellType.Unit) != 0 ? RaycastUnit(ray) : null;

            if (radius > 0f && unit != null)
                _aoeCircle.Show(unit.transform.position, radius);
            else if (radius > 0f && RaycastGround(ray, out var point))
                _aoeCircle.Show(point, radius);
            else
                _aoeCircle.Hide();
        }

        private void Highlight(MobaCharacter unit)
        {
            var flash = unit.Get<FlashTarget>();
            if (flash != null)
                flash.Play(clickFlash);
        }

        private MobaCharacter RaycastUnit(Ray ray)
        {
            if (!Physics.Raycast(ray, out var hit, Mathf.Infinity, unitMask, QueryTriggerInteraction.Ignore))
                return null;

            return hit.collider.GetComponentInParent<MobaCharacter>();
        }

        private bool RaycastGround(Ray ray, out Vector3 point)
        {
            bool hasHit = Physics.Raycast(ray, out var hit, Mathf.Infinity, groundMask, QueryTriggerInteraction.Ignore);
            point = hit.point;
            return hasHit;
        }

        private sealed class GroundCircle
        {
            private const int Segments = 64;

            private readonly LineRenderer _line;
            private float _radius;

            public GroundCircle(string name, Material material, Color color, float width)
            {
                // В корне сцены: под повёрнутым родителем, например камерой, круг бы наклонился
                _line = new GameObject(name).AddComponent<LineRenderer>();
                _line.useWorldSpace = false;
                _line.loop = true;
                _line.widthMultiplier = width;
                _line.sharedMaterial = material;
                _line.startColor = color;
                _line.endColor = color;
                _line.enabled = false;
            }

            public void Show(Vector3 center, float radius)
            {
                _line.enabled = true;

                // Чуть над землёй, иначе линия мерцает, наполовину уходя в неё
                _line.transform.position = center + Vector3.up * 0.05f;

                if (Mathf.Approximately(radius, _radius))
                    return;

                _radius = radius;
                _line.positionCount = Segments;

                for (int i = 0; i < Segments; i++)
                {
                    float angle = i * Mathf.PI * 2f / Segments;
                    _line.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
                }
            }

            public void Hide() => _line.enabled = false;
        }
    }
}
