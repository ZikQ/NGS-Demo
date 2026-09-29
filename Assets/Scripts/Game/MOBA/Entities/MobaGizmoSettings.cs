using System;
using UnityEngine;

namespace Assets.Scripts.Game.MOBA.Entities
{
    [Serializable]
    public struct GizmoRadius
    {
        public bool show;
        public Color color;

        public GizmoRadius(bool show, Color color)
        {
            this.show = show;
            this.color = color;
        }
    }

    [Serializable]
    public class MobaGizmoSettings
    {
        public bool alwaysShow;

        public GizmoRadius stopDistance = new(true, Color.white);
        public GizmoRadius attack = new(true, Color.red);

        [Tooltip("Радиус поиска целей, есть только у крипов")]
        public GizmoRadius acquire = new(true, Color.green);
    }
}
