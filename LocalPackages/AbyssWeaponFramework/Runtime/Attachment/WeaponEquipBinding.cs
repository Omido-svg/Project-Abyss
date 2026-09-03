using System;
using UnityEngine;

namespace ProjectAbyss.WeaponSystem
{
    [Serializable]
    public sealed class WeaponGripSocketBinding
    {
        [SerializeField] private string gripId;
        [SerializeField] private string socketId;

        public string GripId => gripId;
        public string SocketId => socketId;

        public WeaponGripSocketBinding()
        {
        }

        public WeaponGripSocketBinding(string gripId, string socketId)
        {
            this.gripId = gripId;
            this.socketId = socketId;
        }
    }

    public sealed class ResolvedWeaponGripBinding
    {
        public WeaponGripPoint Grip { get; }
        public WeaponSocket Socket { get; }
        public bool IsMountBinding { get; }

        internal ResolvedWeaponGripBinding(
            WeaponGripPoint grip,
            WeaponSocket socket,
            bool isMountBinding)
        {
            Grip = grip;
            Socket = socket;
            IsMountBinding = isMountBinding;
        }
    }
}
