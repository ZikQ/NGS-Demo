using UnityEngine;
using UnityEngine.InputSystem;

namespace Assets.Scripts.Game.MOBA.Player
{
    public class CameraController : MonoBehaviour
    {
        [SerializeField] private Camera camera;
        [SerializeField] private float speed = 20f;
        [SerializeField] private float edgeSize = 20f;
        [SerializeField] private float dragSpeed = 0.1f;

        private void Update()
        {
            var mouse = Mouse.current;
            if (mouse == null || !Application.isFocused)
                return;

            var move = mouse.middleButton.isPressed
                ? mouse.delta.ReadValue() * -dragSpeed
                : GetEdgeDirection(mouse.position.ReadValue()) * (speed * Time.deltaTime);

            if (move == Vector2.zero)
                return;

            var right = camera.transform.right;
            right.y = 0f;
            right.Normalize();

            var forward = Vector3.Cross(right, Vector3.up);

            camera.transform.position += right * move.x + forward * move.y;
        }

        private Vector2 GetEdgeDirection(Vector2 cursor)
        {
            if (cursor.x < 0f || cursor.y < 0f || cursor.x > Screen.width || cursor.y > Screen.height)
                return Vector2.zero;

            var direction = Vector2.zero;

            if (cursor.x <= edgeSize)
                direction.x = -1f;
            else if (cursor.x >= Screen.width - edgeSize)
                direction.x = 1f;

            if (cursor.y <= edgeSize)
                direction.y = -1f;
            else if (cursor.y >= Screen.height - edgeSize)
                direction.y = 1f;

            return direction.normalized;
        }
    }
}
