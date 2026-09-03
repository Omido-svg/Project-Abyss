using System.Collections.Generic;

namespace ProjectAbyss.WeaponSystem
{
    public sealed class WeaponEquipHandle
    {
        private readonly List<ResolvedWeaponGripBinding> bindings = new();

        public string InstanceKey { get; internal set; }
        public WeaponDefinition Definition { get; internal set; }
        public WeaponInstance Instance { get; internal set; }
        public IReadOnlyList<ResolvedWeaponGripBinding> Bindings => bindings;
        public bool IsValid => Instance != null;
        public WeaponRuntimeContext RuntimeContext { get; internal set; }

        public ResolvedWeaponGripBinding MountBinding
        {
            get
            {
                for (int index = 0; index < bindings.Count; index++)
                {
                    if (bindings[index].IsMountBinding)
                        return bindings[index];
                }

                return bindings.Count > 0 ? bindings[0] : null;
            }
        }

        internal List<ResolvedWeaponGripBinding> MutableBindings => bindings;
    }
}
