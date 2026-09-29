using System.Collections;
using NGS.Unity.Core.EntitySystem;
using UnityEngine;

namespace Assets.Scripts.Game.MOBA.Entities
{
    public class CreepCharacter : MobaCharacter
    {
        [Tooltip("Радиус поиска целей. Дальше него цель бросается")]
        [SerializeField] private float acquireRange = 6f;
        [SerializeField] private float despawnDelay = 2f;

        private MobaCharacter _laneTarget;

        protected override float LeashRange => acquireRange;

        public void SetLaneTarget(MobaCharacter target) => _laneTarget = target;

        public override void OnSpawn()
        {
            base.OnSpawn();
            _laneTarget = null;
        }

        protected override void UpdateOrder()
        {
            AcquireTarget(acquireRange);
            base.UpdateOrder();
        }

        protected override void UpdateIdle()
        {
            if (_laneTarget != null && _laneTarget.IsTargetable)
                RunTowards(_laneTarget.transform.position);
            else
                SetControls(Vector3.zero, false, false);
        }

        protected override void DrawRadii()
        {
            base.DrawRadii();
            DrawRadius(GizmoSettings.acquire, acquireRange);
        }

        protected override void Death()
        {
            base.Death();
            StartCoroutine(Despawn());
        }

        private IEnumerator Despawn()
        {
            yield return new WaitForSeconds(despawnDelay);

            if (!EntityManager.Release(this))
                gameObject.SetActive(false);
        }
    }
}
