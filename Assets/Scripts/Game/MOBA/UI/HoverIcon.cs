using System;
using NGS.Unity.Core.Utils;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Assets.Scripts.Game.MOBA.UI
{
    /// <summary>Иконка худа, при наведении на которую показывается панель с названием и описанием.</summary>
    public abstract class HoverIcon : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private bool _hovered;

        public event Action<DisplayData> Hovered;
        public event Action Unhovered;

        protected abstract DisplayData Display { get; }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (Display == null)
                return;

            _hovered = true;
            Hovered?.Invoke(Display);
        }

        public void OnPointerExit(PointerEventData eventData) => EndHover();

        // Выключенная под курсором иконка OnPointerExit уже не получит
        protected virtual void OnDisable() => EndHover();

        /// <summary>Под курсором сменилось содержимое иконки.</summary>
        protected void RefreshHover()
        {
            if (!_hovered)
                return;

            if (Display != null)
                Hovered?.Invoke(Display);
            else
                EndHover();
        }

        private void EndHover()
        {
            if (!_hovered)
                return;

            _hovered = false;
            Unhovered?.Invoke();
        }
    }
}
