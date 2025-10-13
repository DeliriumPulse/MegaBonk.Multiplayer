// File: CameraFollower.cs
using UnityEngine;

namespace Megabonk.Multiplayer
{
    public class CameraFollower : MonoBehaviour
    {
        public Transform Target;
        public Vector3 Offset = new Vector3(0, 5, -8);
        public float Lerp = 6f;

        private void LateUpdate()
        {
            if (!Target) return;
            var desired = Target.position + Offset;
            transform.position = Vector3.Lerp(transform.position, desired, Time.deltaTime * Lerp);
            transform.LookAt(Target);
        }
    }
}