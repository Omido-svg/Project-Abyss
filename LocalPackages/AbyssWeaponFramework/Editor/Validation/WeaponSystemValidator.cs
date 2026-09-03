#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace ProjectAbyss.WeaponSystem.Editor
{
    internal static class WeaponSystemValidator
    {
        public static bool ValidateDefinition(WeaponDefinition definition, out string report)
        {
            StringBuilder b = new(); bool valid = true;
            if (definition == null) { report = "WeaponDefinition is null."; return false; }
            if (string.IsNullOrWhiteSpace(definition.WeaponId)) { valid = false; b.AppendLine("- Weapon Id is empty."); }
            if (definition.Prefab == null) { valid = false; b.AppendLine("- Prefab is missing."); }
            else if (!ValidateWeaponPrefab(definition.Prefab, out string prefab)) { valid = false; b.Append(prefab); }
            if (valid) b.AppendLine("PASS: WeaponDefinition is valid.");
            report = b.ToString(); return valid;
        }

        public static bool ValidateWeaponPrefab(GameObject prefabRoot, out string report)
        {
            StringBuilder b = new(); bool valid = true;
            if (prefabRoot == null) { report = "Weapon prefab is null."; return false; }
            WeaponInstance instance = prefabRoot.GetComponent<WeaponInstance>();
            if (instance == null) { report = "- WeaponInstance must be on the prefab root."; return false; }
            instance.RefreshCache();

            HashSet<string> gripIds = new(StringComparer.Ordinal);
            int autoGripCount = 0, primaryCount = 0;
            for (int i = 0; i < instance.Grips.Count; i++)
            {
                WeaponGripPoint grip = instance.Grips[i]; if (grip == null) continue;
                if (string.IsNullOrWhiteSpace(grip.GripId)) { valid = false; b.AppendLine($"- Grip #{i} has an empty id."); }
                else if (!gripIds.Add(grip.GripId)) { valid = false; b.AppendLine($"- Duplicate grip id: {grip.GripId}"); }
                if (grip.RequiredForAutoEquip) autoGripCount++;
                if (grip.Role == WeaponGripRole.Primary) primaryCount++;
            }
            if (instance.Grips.Count == 0) { valid = false; b.AppendLine("- No WeaponGripPoint exists."); }
            if (autoGripCount == 0) { valid = false; b.AppendLine("- No grip is marked Required For Auto Equip."); }
            if (primaryCount == 0) b.AppendLine("- Warning: no Primary grip; first planned grip becomes mount binding.");

            HashSet<string> pointIds = new(StringComparer.Ordinal);
            for (int i = 0; i < instance.Points.Count; i++)
            {
                WeaponPoint point = instance.Points[i]; if (point == null) continue;
                if (string.IsNullOrWhiteSpace(point.PointId)) { valid = false; b.AppendLine($"- Point #{i} has an empty id."); }
                else if (!pointIds.Add(point.PointId)) { valid = false; b.AppendLine($"- Duplicate point id: {point.PointId}"); }
            }

            WeaponAttackModule[] attacks = prefabRoot.GetComponentsInChildren<WeaponAttackModule>(true);
            HashSet<string> attackIds = new(StringComparer.Ordinal);
            for (int i = 0; i < attacks.Length; i++)
            {
                WeaponAttackModule attack = attacks[i];
                if (attack.Profile == null) { valid = false; b.AppendLine($"- {attack.GetType().Name} has no Attack Profile."); continue; }
                if (attack.Kind != attack.Profile.Kind) { valid = false; b.AppendLine($"- {attack.GetType().Name} profile kind mismatch ({attack.Profile.Kind})."); }
                if (string.IsNullOrWhiteSpace(attack.AttackId)) { valid = false; b.AppendLine($"- {attack.GetType().Name} has an empty Attack Id."); }
                else if (!attackIds.Add(attack.AttackId)) b.AppendLine($"- Warning: duplicate Attack Id '{attack.AttackId}' on one weapon; WeaponAttackController resolves the first one.");

                if (attack is MeleeWeaponModule && (instance.FindPoint(WeaponPointType.BladeStart) == null || instance.FindPoint(WeaponPointType.BladeEnd) == null))
                { valid = false; b.AppendLine("- MeleeWeaponModule requires BladeStart and BladeEnd points."); }
                if (attack is HitscanWeaponModule && instance.FindPoint(WeaponPointType.Muzzle) == null)
                    b.AppendLine("- Warning: Hitscan weapon has no Muzzle point; module Transform will be used.");
                if (attack is ProjectileWeaponModule && instance.FindPoint(WeaponPointType.ProjectileOrigin) == null && instance.FindPoint(WeaponPointType.Muzzle) == null)
                    b.AppendLine("- Warning: Projectile weapon has no ProjectileOrigin/Muzzle point; module Transform will be used.");
            }

            WeaponHandPoseModule poseModule = prefabRoot.GetComponentInChildren<WeaponHandPoseModule>(true);
            if (poseModule != null && instance.Grips.Count == 0) { valid = false; b.AppendLine("- Hand pose module exists without grips."); }
            ChainWeaponPhysicsModule chain = prefabRoot.GetComponentInChildren<ChainWeaponPhysicsModule>(true);
            if (chain != null && prefabRoot.GetComponentsInChildren<Rigidbody>(true).Length == 0)
                b.AppendLine("- Warning: Chain physics module found but no Rigidbody links exist yet.");

            if (valid) b.AppendLine("PASS: Weapon prefab structure is valid.");
            report = b.ToString(); return valid;
        }

        public static bool ValidateController(WeaponMountController controller, out string report)
        {
            StringBuilder b = new(); bool valid = true;
            if (controller == null) { report = "WeaponMountController is missing."; return false; }
            controller.RefreshSockets();
            if (controller.Sockets.Count == 0) { valid = false; b.AppendLine("- No WeaponSocket found under controller."); }
            HashSet<string> ids = new(StringComparer.Ordinal);
            for (int i = 0; i < controller.Sockets.Count; i++)
            {
                WeaponSocket socket = controller.Sockets[i]; if (socket == null) continue;
                if (string.IsNullOrWhiteSpace(socket.SocketId)) { valid = false; b.AppendLine($"- Socket #{i} has an empty id."); }
                else if (!ids.Add(socket.SocketId)) { valid = false; b.AppendLine($"- Duplicate socket id: {socket.SocketId}"); }
            }
            Animator animator = controller.GetComponent<Animator>();
            WeaponSkeletonMap map = controller.GetComponent<WeaponSkeletonMap>();
            if (animator != null && map == null) b.AppendLine("- Warning: Animator exists but WeaponSkeletonMap is missing (hand pose/custom IK unavailable).");
            if (animator != null && animator.isHuman && controller.GetComponent<HumanoidWeaponIK>() == null) b.AppendLine("- Warning: HumanoidWeaponIK is missing; support-hand grips will not drive Humanoid IK.");
            if (animator != null && !animator.isHuman && controller.GetComponent<MappedWeaponIK>() == null) b.AppendLine("- Warning: Generic animator has no MappedWeaponIK.");
            if (valid) b.AppendLine("PASS: Character weapon setup is structurally valid.");
            report = b.ToString(); return valid;
        }
    }
}
#endif
