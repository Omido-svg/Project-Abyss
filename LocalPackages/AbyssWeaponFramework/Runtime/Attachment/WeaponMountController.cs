using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectAbyss.WeaponSystem
{
    [DisallowMultipleComponent]
    public sealed class WeaponMountController : MonoBehaviour
    {
        [Header("Ownership")]
        [Tooltip("Optional component implementing IWeaponOwner. If empty, this GameObject is the owner.")]
        [SerializeField] private MonoBehaviour ownerProvider;

        [Header("Sockets")]
        [SerializeField] private Transform socketRoot;
        [SerializeField] private bool includeInactiveSockets = true;

        [Header("Starting Loadout")]
        [SerializeField] private bool equipStartingLoadoutOnStart;
        [SerializeField] private WeaponLoadoutDefinition startingLoadout;

        [Header("Unequip")]
        [SerializeField] private bool destroyInstanceOnUnequip = true;

        private readonly List<WeaponSocket> sockets = new();
        private readonly List<WeaponEquipHandle> equipped = new();

        public event Action<WeaponEquipHandle> WeaponEquipped;
        public event Action<WeaponEquipHandle> WeaponUnequipped;
        public event Action<WeaponEquipHandle> WeaponRemounted;

        public IReadOnlyList<WeaponSocket> Sockets => sockets;
        public IReadOnlyList<WeaponEquipHandle> Equipped => equipped;
        public IWeaponOwner OwnerProvider => ownerProvider as IWeaponOwner;
        public GameObject OwnerObject => OwnerProvider?.WeaponOwnerObject ?? gameObject;

        private void Awake()
        {
            RefreshSockets();
        }

        private void Start()
        {
            if (equipStartingLoadoutOnStart && startingLoadout != null)
            {
                if (!TryApplyLoadout(startingLoadout, out string error))
                    Debug.LogError($"[WeaponSystem] Starting loadout failed: {error}", this);
            }
        }

        public void RefreshSockets()
        {
            sockets.Clear();
            Transform root = socketRoot != null ? socketRoot : transform;
            WeaponSocket[] found = root.GetComponentsInChildren<WeaponSocket>(includeInactiveSockets);

            for (int index = 0; index < found.Length; index++)
            {
                if (found[index] != null)
                    sockets.Add(found[index]);
            }
        }

        public WeaponSocket FindSocket(string socketId)
        {
            if (string.IsNullOrWhiteSpace(socketId))
                return null;

            EnsureSockets();
            for (int index = 0; index < sockets.Count; index++)
            {
                WeaponSocket socket = sockets[index];
                if (socket != null && string.Equals(socket.SocketId, socketId, StringComparison.Ordinal))
                    return socket;
            }

            return null;
        }

        public WeaponEquipHandle FindByKey(string instanceKey)
        {
            if (string.IsNullOrWhiteSpace(instanceKey))
                return null;

            for (int index = 0; index < equipped.Count; index++)
            {
                WeaponEquipHandle handle = equipped[index];
                if (handle != null && string.Equals(handle.InstanceKey, instanceKey, StringComparison.Ordinal))
                    return handle;
            }

            return null;
        }

        public WeaponEquipHandle FindByDefinition(WeaponDefinition definition)
        {
            if (definition == null)
                return null;

            for (int index = 0; index < equipped.Count; index++)
            {
                WeaponEquipHandle handle = equipped[index];
                if (handle != null && handle.Definition == definition)
                    return handle;
            }

            return null;
        }

        public bool TryEquip(
            WeaponDefinition definition,
            out WeaponEquipHandle handle,
            string instanceKey = null,
            IReadOnlyList<WeaponGripSocketBinding> explicitBindings = null,
            bool useOnlyExplicitBindings = false)
        {
            handle = null;
            EnsureSockets();

            if (definition == null)
            {
                Debug.LogError("[WeaponSystem] WeaponDefinition is null.", this);
                return false;
            }

            if (definition.Prefab == null)
            {
                Debug.LogError($"[WeaponSystem] '{definition.DisplayName}' has no prefab.", definition);
                return false;
            }

            WeaponInstance prefabInstance = definition.Prefab.GetComponent<WeaponInstance>();
            if (prefabInstance == null)
            {
                Debug.LogError(
                    $"[WeaponSystem] Prefab '{definition.Prefab.name}' must have WeaponInstance on the prefab root.",
                    definition.Prefab);
                return false;
            }

            if (!TryBuildPlan(
                    prefabInstance,
                    explicitBindings,
                    useOnlyExplicitBindings,
                    reservedSockets: null,
                    ignoreOccupancy: false,
                    out List<PlanBinding> plan,
                    out string error))
            {
                Debug.LogError($"[WeaponSystem] Equip failed for '{definition.DisplayName}': {error}", this);
                return false;
            }

            return TryInstantiateFromPlan(
                definition,
                instanceKey,
                plan,
                out handle,
                out _);
        }

        public bool TryUnequip(WeaponEquipHandle handle)
        {
            if (handle == null || !equipped.Contains(handle))
                return false;

            WeaponEquipContext context = new(this, handle);
            handle.Instance?.NotifyUnequipped(context);

            ReleaseSockets(handle);
            equipped.Remove(handle);
            WeaponUnequipped?.Invoke(handle);

            if (handle.Instance != null)
            {
                if (destroyInstanceOnUnequip)
                    Destroy(handle.Instance.gameObject);
                else
                {
                    handle.Instance.transform.SetParent(transform, true);
                    handle.Instance.gameObject.SetActive(false);
                }
            }

            return true;
        }

        public void UnequipAll()
        {
            for (int index = equipped.Count - 1; index >= 0; index--)
                TryUnequip(equipped[index]);
        }

        public bool TryRemount(
            WeaponEquipHandle handle,
            IReadOnlyList<WeaponGripSocketBinding> bindings,
            bool useOnlyExplicitBindings = true)
        {
            if (handle == null || !equipped.Contains(handle) || handle.Instance == null)
                return false;

            List<ResolvedWeaponGripBinding> oldBindings =
                new(handle.MutableBindings);

            ReleaseSockets(handle);

            if (!TryBuildPlan(
                    handle.Instance,
                    bindings,
                    useOnlyExplicitBindings,
                    reservedSockets: null,
                    ignoreOccupancy: false,
                    out List<PlanBinding> plan,
                    out string error))
            {
                RestoreBindings(handle, oldBindings);
                Debug.LogError($"[WeaponSystem] Remount failed: {error}", this);
                return false;
            }

            if (!ApplyPlanToExisting(handle, plan, out error))
            {
                ReleaseSockets(handle);
                RestoreBindings(handle, oldBindings);
                Debug.LogError($"[WeaponSystem] Remount failed: {error}", this);
                return false;
            }

            WeaponEquipContext context = new(this, handle);
            handle.Instance.NotifyRemounted(context);
            WeaponRemounted?.Invoke(handle);
            return true;
        }

        public bool TryApplyLoadout(
            WeaponLoadoutDefinition loadout,
            out string error)
        {
            error = null;
            EnsureSockets();

            if (loadout == null)
            {
                error = "Loadout is null.";
                return false;
            }

            List<PlannedEntry> plannedEntries = new();
            HashSet<WeaponSocket> reserved = new();
            HashSet<string> keys = new(StringComparer.Ordinal);

            IReadOnlyList<WeaponLoadoutEntry> entries = loadout.Entries;
            for (int index = 0; index < entries.Count; index++)
            {
                WeaponLoadoutEntry entry = entries[index];
                if (entry == null || !entry.Enabled)
                    continue;

                if (entry.Weapon == null || entry.Weapon.Prefab == null)
                {
                    error = $"Entry {index} has no valid weapon/prefab.";
                    return false;
                }

                string key = string.IsNullOrWhiteSpace(entry.InstanceKey)
                    ? $"Weapon_{index}"
                    : entry.InstanceKey;

                if (!keys.Add(key))
                {
                    error = $"Duplicate instance key '{key}'.";
                    return false;
                }

                WeaponInstance prefabInstance = entry.Weapon.Prefab.GetComponent<WeaponInstance>();
                if (prefabInstance == null)
                {
                    error = $"Prefab '{entry.Weapon.Prefab.name}' has no root WeaponInstance.";
                    return false;
                }

                if (!TryBuildPlan(
                        prefabInstance,
                        entry.Bindings,
                        entry.UseOnlyExplicitBindings,
                        reserved,
                        ignoreOccupancy: true,
                        out List<PlanBinding> plan,
                        out string planError))
                {
                    error = $"Entry '{key}' failed: {planError}";
                    return false;
                }

                for (int bindingIndex = 0; bindingIndex < plan.Count; bindingIndex++)
                    reserved.Add(plan[bindingIndex].Socket);

                plannedEntries.Add(new PlannedEntry(entry.Weapon, key, plan));
            }

            UnequipAll();

            List<WeaponEquipHandle> created = new();
            for (int index = 0; index < plannedEntries.Count; index++)
            {
                PlannedEntry entry = plannedEntries[index];
                if (!TryInstantiateFromPlan(
                        entry.Definition,
                        entry.InstanceKey,
                        entry.Plan,
                        out WeaponEquipHandle createdHandle,
                        out string instantiateError))
                {
                    for (int rollback = created.Count - 1; rollback >= 0; rollback--)
                        TryUnequip(created[rollback]);

                    error = $"Runtime instantiate failed for '{entry.InstanceKey}': {instantiateError}";
                    return false;
                }

                created.Add(createdHandle);
            }

            return true;
        }

        private bool TryInstantiateFromPlan(
            WeaponDefinition definition,
            string instanceKey,
            List<PlanBinding> plan,
            out WeaponEquipHandle handle,
            out string error)
        {
            handle = null;
            error = null;

            GameObject clone = Instantiate(definition.Prefab, transform);
            clone.name = definition.Prefab.name;

            WeaponInstance instance = clone.GetComponent<WeaponInstance>();
            if (instance == null)
            {
                Destroy(clone);
                error = "Instantiated prefab has no root WeaponInstance.";
                return false;
            }

            instance.BindRuntimeDefinition(definition);
            instance.RefreshCache();

            handle = new WeaponEquipHandle
            {
                Definition = definition,
                Instance = instance,
                InstanceKey = string.IsNullOrWhiteSpace(instanceKey)
                    ? definition.WeaponId
                    : instanceKey
            };

            handle.RuntimeContext = new WeaponRuntimeContext(this, handle, OwnerProvider);
            instance.BindRuntimeContext(handle.RuntimeContext);

            List<PlanBinding> clonePlan = new(plan.Count);
            for (int index = 0; index < plan.Count; index++)
            {
                PlanBinding source = plan[index];
                WeaponGripPoint cloneGrip = instance.FindGrip(source.Grip.GripId);
                if (cloneGrip == null)
                {
                    Destroy(clone);
                    handle = null;
                    error = $"Grip '{source.Grip.GripId}' was not found on instantiated weapon.";
                    return false;
                }

                clonePlan.Add(new PlanBinding(cloneGrip, source.Socket, source.IsMountBinding));
            }

            if (!ApplyPlanToExisting(handle, clonePlan, out error))
            {
                Destroy(clone);
                handle = null;
                return false;
            }

            equipped.Add(handle);
            WeaponEquipContext context = new(this, handle);
            instance.NotifyEquipped(context);
            WeaponEquipped?.Invoke(handle);
            return true;
        }

        private bool ApplyPlanToExisting(
            WeaponEquipHandle handle,
            List<PlanBinding> plan,
            out string error)
        {
            error = null;

            if (handle?.Instance == null || plan == null || plan.Count == 0)
            {
                error = "No valid mount plan.";
                return false;
            }

            for (int index = 0; index < plan.Count; index++)
            {
                WeaponSocket socket = plan[index].Socket;
                if (socket == null || (socket.IsOccupied && socket.Occupant != handle))
                {
                    error = $"Socket '{socket?.SocketId ?? "<null>"}' is unavailable.";
                    return false;
                }
            }

            PlanBinding mount = plan[0];
            for (int index = 0; index < plan.Count; index++)
            {
                if (plan[index].IsMountBinding)
                {
                    mount = plan[index];
                    break;
                }
            }

            Transform root = handle.Instance.transform;
            Transform target = mount.Socket.MountTransform;
            WeaponTransformUtility.AlignGripToTarget(root, mount.Grip.transform, target);
            root.SetParent(target, true);

            handle.MutableBindings.Clear();
            for (int index = 0; index < plan.Count; index++)
            {
                PlanBinding binding = plan[index];
                if (!binding.Socket.TryOccupy(handle))
                {
                    error = $"Could not occupy socket '{binding.Socket.SocketId}'.";
                    ReleaseSockets(handle);
                    handle.MutableBindings.Clear();
                    return false;
                }

                handle.MutableBindings.Add(
                    new ResolvedWeaponGripBinding(
                        binding.Grip,
                        binding.Socket,
                        binding.IsMountBinding));
            }

            return true;
        }

        private bool TryBuildPlan(
            WeaponInstance sourceInstance,
            IReadOnlyList<WeaponGripSocketBinding> explicitBindings,
            bool useOnlyExplicitBindings,
            HashSet<WeaponSocket> reservedSockets,
            bool ignoreOccupancy,
            out List<PlanBinding> plan,
            out string error)
        {
            plan = new List<PlanBinding>();
            error = null;

            if (sourceInstance == null)
            {
                error = "WeaponInstance is null.";
                return false;
            }

            sourceInstance.RefreshCache();
            Dictionary<string, string> explicitMap = BuildExplicitMap(explicitBindings, out error);
            if (explicitMap == null)
                return false;

            List<WeaponGripPoint> selectedGrips = new();
            IReadOnlyList<WeaponGripPoint> allGrips = sourceInstance.Grips;

            for (int index = 0; index < allGrips.Count; index++)
            {
                WeaponGripPoint grip = allGrips[index];
                if (grip == null || string.IsNullOrWhiteSpace(grip.GripId))
                    continue;

                bool explicitlyBound = explicitMap.ContainsKey(grip.GripId);
                bool include = useOnlyExplicitBindings
                    ? explicitlyBound
                    : grip.RequiredForAutoEquip || explicitlyBound;

                if (include)
                    selectedGrips.Add(grip);
            }

            if (selectedGrips.Count == 0)
            {
                error = useOnlyExplicitBindings
                    ? "No explicit grip bindings were resolved."
                    : "Weapon has no auto-equip grips.";
                return false;
            }

            HashSet<string> gripIds = new(StringComparer.Ordinal);
            for (int index = 0; index < selectedGrips.Count; index++)
            {
                if (!gripIds.Add(selectedGrips[index].GripId))
                {
                    error = $"Duplicate grip id '{selectedGrips[index].GripId}'.";
                    return false;
                }
            }

            foreach (KeyValuePair<string, string> pair in explicitMap)
            {
                if (sourceInstance.FindGrip(pair.Key) == null)
                {
                    error = $"Explicit grip '{pair.Key}' does not exist on weapon.";
                    return false;
                }
            }

            HashSet<WeaponSocket> locallyReserved = reservedSockets != null
                ? new HashSet<WeaponSocket>(reservedSockets)
                : new HashSet<WeaponSocket>();

            for (int index = 0; index < selectedGrips.Count; index++)
            {
                WeaponGripPoint grip = selectedGrips[index];
                WeaponSocket socket;

                if (explicitMap.TryGetValue(grip.GripId, out string socketId))
                {
                    socket = FindSocket(socketId);
                    if (socket == null)
                    {
                        error = $"Socket '{socketId}' was not found for grip '{grip.GripId}'.";
                        return false;
                    }

                    if (!grip.IsCompatible(socket))
                    {
                        error = $"Grip '{grip.GripId}' is incompatible with socket '{socket.SocketId}'.";
                        return false;
                    }

                    if (locallyReserved.Contains(socket))
                    {
                        error = $"Socket '{socket.SocketId}' is already reserved by this loadout.";
                        return false;
                    }

                    if (!ignoreOccupancy && socket.IsOccupied)
                    {
                        error = $"Socket '{socket.SocketId}' is occupied.";
                        return false;
                    }
                }
                else
                {
                    socket = FindBestSocket(grip, locallyReserved, ignoreOccupancy);
                    if (socket == null)
                    {
                        error = $"No free compatible socket for grip '{grip.GripId}'.";
                        return false;
                    }
                }

                locallyReserved.Add(socket);
                plan.Add(new PlanBinding(grip, socket, false));
            }

            int mountIndex = -1;
            for (int index = 0; index < plan.Count; index++)
            {
                if (plan[index].Grip.Role == WeaponGripRole.Primary)
                {
                    mountIndex = index;
                    break;
                }
            }

            if (mountIndex < 0)
            {
                for (int index = 0; index < plan.Count; index++)
                {
                    if (plan[index].Grip.Role == WeaponGripRole.Stow)
                    {
                        mountIndex = index;
                        break;
                    }
                }
            }

            if (mountIndex < 0)
                mountIndex = 0;

            PlanBinding selectedMount = plan[mountIndex];
            plan[mountIndex] = new PlanBinding(
                selectedMount.Grip,
                selectedMount.Socket,
                true);

            return true;
        }

        private Dictionary<string, string> BuildExplicitMap(
            IReadOnlyList<WeaponGripSocketBinding> explicitBindings,
            out string error)
        {
            error = null;
            Dictionary<string, string> result = new(StringComparer.Ordinal);

            if (explicitBindings == null)
                return result;

            for (int index = 0; index < explicitBindings.Count; index++)
            {
                WeaponGripSocketBinding binding = explicitBindings[index];
                if (binding == null)
                    continue;

                if (string.IsNullOrWhiteSpace(binding.GripId) ||
                    string.IsNullOrWhiteSpace(binding.SocketId))
                {
                    error = $"Explicit binding {index} has an empty grip/socket id.";
                    return null;
                }

                if (result.ContainsKey(binding.GripId))
                {
                    error = $"Grip '{binding.GripId}' is bound more than once.";
                    return null;
                }

                result.Add(binding.GripId, binding.SocketId);
            }

            return result;
        }

        private WeaponSocket FindBestSocket(
            WeaponGripPoint grip,
            HashSet<WeaponSocket> reserved,
            bool ignoreOccupancy)
        {
            WeaponSocket best = null;
            int bestScore = int.MinValue;

            for (int index = 0; index < sockets.Count; index++)
            {
                WeaponSocket socket = sockets[index];
                if (socket == null || !socket.AllowAutoSelection)
                    continue;

                if (reserved != null && reserved.Contains(socket))
                    continue;

                if (!ignoreOccupancy && socket.IsOccupied)
                    continue;

                int score = grip.GetCompatibilityScore(socket);
                if (score <= bestScore)
                    continue;

                best = socket;
                bestScore = score;
            }

            return best;
        }

        private void ReleaseSockets(WeaponEquipHandle handle)
        {
            if (handle == null)
                return;

            IReadOnlyList<ResolvedWeaponGripBinding> bindings = handle.Bindings;
            for (int index = 0; index < bindings.Count; index++)
                bindings[index]?.Socket?.Release(handle);
        }

        private void RestoreBindings(
            WeaponEquipHandle handle,
            List<ResolvedWeaponGripBinding> oldBindings)
        {
            if (handle == null || oldBindings == null || oldBindings.Count == 0)
                return;

            handle.MutableBindings.Clear();
            for (int index = 0; index < oldBindings.Count; index++)
            {
                ResolvedWeaponGripBinding binding = oldBindings[index];
                if (binding?.Socket == null || binding.Grip == null)
                    continue;

                binding.Socket.TryOccupy(handle);
                handle.MutableBindings.Add(binding);
            }

            ResolvedWeaponGripBinding mount = handle.MountBinding;
            if (mount != null && handle.Instance != null)
            {
                WeaponTransformUtility.AlignGripToTarget(
                    handle.Instance.transform,
                    mount.Grip.transform,
                    mount.Socket.MountTransform);
                handle.Instance.transform.SetParent(mount.Socket.MountTransform, true);
            }
        }

        private void EnsureSockets()
        {
            if (sockets.Count == 0)
                RefreshSockets();
        }

        private readonly struct PlanBinding
        {
            public readonly WeaponGripPoint Grip;
            public readonly WeaponSocket Socket;
            public readonly bool IsMountBinding;

            public PlanBinding(
                WeaponGripPoint grip,
                WeaponSocket socket,
                bool isMountBinding)
            {
                Grip = grip;
                Socket = socket;
                IsMountBinding = isMountBinding;
            }
        }

        private sealed class PlannedEntry
        {
            public WeaponDefinition Definition { get; }
            public string InstanceKey { get; }
            public List<PlanBinding> Plan { get; }

            public PlannedEntry(
                WeaponDefinition definition,
                string instanceKey,
                List<PlanBinding> plan)
            {
                Definition = definition;
                InstanceKey = instanceKey;
                Plan = plan;
            }
        }
    }
}
