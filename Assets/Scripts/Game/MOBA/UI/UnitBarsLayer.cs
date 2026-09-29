using System.Collections.Generic;
using Assets.Scripts.Game.MOBA.Entities;
using Assets.Scripts.Game.MOBA.Player;
using NGS.Unity.Core.ObjectPoolSystem;
using UnityEngine;

namespace Assets.Scripts.Game.MOBA.UI
{
    public class UnitBarsLayer : MonoBehaviour
    {
        [SerializeField] private PlayerOrderInput playerInput;
        [SerializeField] private Camera viewCamera;
        [SerializeField] private UnitBar heroBarTemplate;
        [SerializeField] private UnitBar unitBarTemplate;
        [SerializeField] private Color allyColor = new(0.3f, 0.85f, 0.3f);
        [SerializeField] private Color enemyColor = new(0.9f, 0.25f, 0.2f);

        [Tooltip("Насколько выше верха хитбокса, в пикселях экрана")]
        [SerializeField] private float headOffset = 10f;

        private readonly Dictionary<MobaCharacter, UnitBar> _bars = new();
        private readonly HashSet<MobaCharacter> _shown = new();
        private readonly List<MobaCharacter> _hidden = new();

        private Pool _heroPool;
        private Pool _unitPool;

        private RectTransform _rect;
        private Camera _canvasCamera;

        private void Awake()
        {
            HideTemplate(heroBarTemplate);
            HideTemplate(unitBarTemplate);

            _heroPool = new Pool("hud_hero_bars", heroBarTemplate.gameObject, transform, defaultCapacity: 4, maxSize: 0);
            _unitPool = new Pool("hud_unit_bars", unitBarTemplate.gameObject, transform, defaultCapacity: 32, maxSize: 0);

            _rect = (RectTransform)transform;
            var canvas = GetComponentInParent<Canvas>(true).rootCanvas;
            _canvasCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        }

        private void LateUpdate()
        {
            _shown.Clear();

            var units = MobaCharacter.All;

            for (int i = 0; i < units.Count; i++)
            {
                var unit = units[i];

                if (!IsShown(unit) || !TryGetScreenPoint(unit, out var point))
                    continue;

                if (!_bars.TryGetValue(unit, out var bar))
                {
                    bar = PoolFor(unit).Get().GetComponent<UnitBar>();
                    bar.Bind(unit);
                    _bars[unit] = bar;
                }

                bar.transform.position = point;
                bar.Refresh(unit.Team == playerInput.PlayerTeam ? allyColor : enemyColor);

                _shown.Add(unit);
            }

            ReleaseHidden();
        }

        private static void HideTemplate(Component template)
        {
            if (template.gameObject.scene.IsValid())
                template.gameObject.SetActive(false);
        }

        private static bool IsShown(MobaCharacter unit) =>
            unit.IsTargetable && !(unit.IsCreep() && unit.Health >= unit.MaxHealth);

        private bool TryGetScreenPoint(MobaCharacter unit, out Vector3 point)
        {
            var hitbox = unit.Hitbox;
            var head = hitbox != null
                ? new Vector3(hitbox.bounds.center.x, hitbox.bounds.max.y, hitbox.bounds.center.z)
                : unit.transform.position;

            var screen = viewCamera.WorldToScreenPoint(head);

            if (screen.z <= 0f)
            {
                point = default;
                return false;
            }

            screen.y += headOffset;
            return RectTransformUtility.ScreenPointToWorldPointInRectangle(_rect, screen, _canvasCamera, out point);
        }

        private void ReleaseHidden()
        {
            _hidden.Clear();

            foreach (var pair in _bars)
            {
                if (!_shown.Contains(pair.Key))
                    _hidden.Add(pair.Key);
            }

            foreach (var unit in _hidden)
            {
                PoolFor(unit).Release(_bars[unit].gameObject);
                _bars.Remove(unit);
            }
        }

        private Pool PoolFor(MobaCharacter unit) => unit.IsHero() ? _heroPool : _unitPool;
    }
}
