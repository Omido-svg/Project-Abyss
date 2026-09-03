#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ProjectAbyss.WeaponSystem.Editor
{
    public sealed class WeaponSystemSetupWindow : EditorWindow
    {
        private enum Preset { AttachmentOnly, OneHandMelee, TwoHandMelee, HitscanGun, TwoHandHitscan, ProjectileWeapon, ChainWeapon }

        private Animator characterAnimator;
        private GameObject weaponRoot;
        private Preset preset;
        private string definitionId = "weapon.new";
        private string definitionDisplayName = "New Weapon";
        private WeaponFamily definitionFamily = WeaponFamily.Unspecified;
        private WeaponHandUsage definitionHandUsage = WeaponHandUsage.OneHanded;
        private Vector2 scroll;
        private GameObject previewObject;

        [MenuItem("Tools/Abyss Weapon Framework/Setup Wizard", priority = 1500)]
        [MenuItem("Tools/Project Abyss/Weapon System/Setup Wizard", priority = 1510)]
        public static void Open() => GetWindow<WeaponSystemSetupWindow>("Weapon Framework");

        private void OnDisable() => ClearPreview();

        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField("Abyss Weapon Framework v2", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Framework runtime is game-agnostic. Project-specific damage, skills, inventory and combat rules connect through adapters/interfaces.", MessageType.Info);

            DrawCharacterSetup();
            EditorGUILayout.Space(12f);
            DrawWeaponSetup();
            EditorGUILayout.Space(12f);
            DrawDefinition();
            EditorGUILayout.Space(12f);
            DrawPreviewAndValidation();
            EditorGUILayout.EndScrollView();
        }

        private void DrawCharacterSetup()
        {
            EditorGUILayout.LabelField("1. Character / Skeleton", EditorStyles.boldLabel);
            characterAnimator = (Animator)EditorGUILayout.ObjectField("Animator", characterAnimator, typeof(Animator), true);
            using (new EditorGUI.DisabledScope(characterAnimator == null))
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Ensure Framework Components")) EnsureCharacterComponents(characterAnimator);
                if (GUILayout.Button("Create / Repair Hand Sockets")) CreateHandSockets(characterAnimator);
                EditorGUILayout.EndHorizontal();

                if (GUILayout.Button("Auto Map Humanoid Skeleton"))
                {
                    WeaponSkeletonMap map = EnsureSkeletonMap(characterAnimator);
                    if (characterAnimator.isHuman) { Undo.RecordObject(map, "Auto Map Weapon Skeleton"); map.AutoMapHumanoid(); EditorUtility.SetDirty(map); }
                    else EditorUtility.DisplayDialog("Abyss Weapon Framework", "Animator is not Humanoid. Use WeaponSkeletonMap inspector for manual bone mapping.", "OK");
                }
            }
        }

        private void DrawWeaponSetup()
        {
            EditorGUILayout.LabelField("2. Weapon Prefab", EditorStyles.boldLabel);
            weaponRoot = (GameObject)EditorGUILayout.ObjectField("Weapon Root", weaponRoot, typeof(GameObject), true);
            preset = (Preset)EditorGUILayout.EnumPopup("Preset", preset);

            using (new EditorGUI.DisabledScope(weaponRoot == null))
            {
                if (GUILayout.Button("Apply Preset / Repair Structure")) ApplyPreset();
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Add Primary Grip")) AddGrip(weaponRoot, "Grip_Main", WeaponGripRole.Primary, WeaponHand.Any, false);
                if (GUILayout.Button("Add Support Grip")) AddGrip(weaponRoot, "Grip_Support", WeaponGripRole.Support, WeaponHand.Any, true);
                if (GUILayout.Button("Add Stow Grip")) AddStowGrip(weaponRoot);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Add Muzzle")) AddPoint(weaponRoot, "Muzzle", WeaponPointType.Muzzle);
                if (GUILayout.Button("Add Blade Start")) AddPoint(weaponRoot, "BladeStart", WeaponPointType.BladeStart);
                if (GUILayout.Button("Add Blade End")) AddPoint(weaponRoot, "BladeEnd", WeaponPointType.BladeEnd);
                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawDefinition()
        {
            EditorGUILayout.LabelField("3. Weapon Definition", EditorStyles.boldLabel);
            definitionId = EditorGUILayout.TextField("Weapon Id", definitionId);
            definitionDisplayName = EditorGUILayout.TextField("Display Name", definitionDisplayName);
            definitionFamily = (WeaponFamily)EditorGUILayout.EnumPopup("Family", definitionFamily);
            definitionHandUsage = (WeaponHandUsage)EditorGUILayout.EnumPopup("Hand Usage", definitionHandUsage);
            using (new EditorGUI.DisabledScope(weaponRoot == null))
                if (GUILayout.Button("Create WeaponDefinition Asset")) CreateDefinitionAsset();
        }

        private void DrawPreviewAndValidation()
        {
            EditorGUILayout.LabelField("4. Preview / Validation", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(characterAnimator == null || weaponRoot == null))
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Preview Equip Right Hand")) PreviewEquip(WeaponHand.Right);
                if (GUILayout.Button("Preview Equip Left Hand")) PreviewEquip(WeaponHand.Left);
                if (GUILayout.Button("Clear Preview")) ClearPreview();
                EditorGUILayout.EndHorizontal();
            }
            using (new EditorGUI.DisabledScope(weaponRoot == null))
                if (GUILayout.Button("Validate Weapon")) ShowWeaponValidation();
            using (new EditorGUI.DisabledScope(characterAnimator == null))
                if (GUILayout.Button("Validate Character")) ShowCharacterValidation();
        }

        private void ApplyPreset()
        {
            WeaponInstance instance = EnsureWeaponInstance(weaponRoot);
            EnsureComponent<WeaponAttackController>(weaponRoot);
            switch (preset)
            {
                case Preset.OneHandMelee:
                    AddGripIfMissing(weaponRoot, "Grip_Main", WeaponGripRole.Primary, WeaponHand.Any, false);
                    AddPointIfMissing(weaponRoot, "BladeStart", WeaponPointType.BladeStart);
                    AddPointIfMissing(weaponRoot, "BladeEnd", WeaponPointType.BladeEnd);
                    EnsureAttack<MeleeWeaponModule, MeleeAttackProfile>(weaponRoot, "MeleeAttack");
                    EnsureComponent<WeaponAnimationModule>(weaponRoot);
                    break;
                case Preset.TwoHandMelee:
                    AddGripIfMissing(weaponRoot, "Grip_Main", WeaponGripRole.Primary, WeaponHand.Right, false);
                    AddGripIfMissing(weaponRoot, "Grip_Support", WeaponGripRole.Support, WeaponHand.Left, true);
                    AddPointIfMissing(weaponRoot, "BladeStart", WeaponPointType.BladeStart);
                    AddPointIfMissing(weaponRoot, "BladeEnd", WeaponPointType.BladeEnd);
                    EnsureAttack<MeleeWeaponModule, MeleeAttackProfile>(weaponRoot, "MeleeAttack");
                    EnsureComponent<WeaponHandPoseModule>(weaponRoot);
                    EnsureComponent<WeaponAnimationModule>(weaponRoot);
                    break;
                case Preset.HitscanGun:
                    AddGripIfMissing(weaponRoot, "Grip_Main", WeaponGripRole.Primary, WeaponHand.Any, false);
                    AddPointIfMissing(weaponRoot, "Muzzle", WeaponPointType.Muzzle);
                    EnsureAttack<HitscanWeaponModule, HitscanAttackProfile>(weaponRoot, "HitscanAttack");
                    EnsureComponent<WeaponEffectModule>(weaponRoot);
                    EnsureComponent<WeaponAttackEffectsModule>(weaponRoot);
                    EnsureComponent<WeaponAnimationModule>(weaponRoot);
                    break;
                case Preset.TwoHandHitscan:
                    AddGripIfMissing(weaponRoot, "Grip_Main", WeaponGripRole.Primary, WeaponHand.Right, false);
                    AddGripIfMissing(weaponRoot, "Grip_Support", WeaponGripRole.Support, WeaponHand.Left, true);
                    AddPointIfMissing(weaponRoot, "Muzzle", WeaponPointType.Muzzle);
                    EnsureAttack<HitscanWeaponModule, HitscanAttackProfile>(weaponRoot, "HitscanAttack");
                    EnsureComponent<WeaponHandPoseModule>(weaponRoot);
                    EnsureComponent<WeaponEffectModule>(weaponRoot);
                    EnsureComponent<WeaponAttackEffectsModule>(weaponRoot);
                    EnsureComponent<WeaponAnimationModule>(weaponRoot);
                    break;
                case Preset.ProjectileWeapon:
                    AddGripIfMissing(weaponRoot, "Grip_Main", WeaponGripRole.Primary, WeaponHand.Any, false);
                    AddPointIfMissing(weaponRoot, "ProjectileOrigin", WeaponPointType.ProjectileOrigin);
                    EnsureAttack<ProjectileWeaponModule, ProjectileAttackProfile>(weaponRoot, "ProjectileAttack");
                    EnsureComponent<WeaponEffectModule>(weaponRoot);
                    EnsureComponent<WeaponAttackEffectsModule>(weaponRoot);
                    EnsureComponent<WeaponAnimationModule>(weaponRoot);
                    break;
                case Preset.ChainWeapon:
                    AddGripIfMissing(weaponRoot, "Grip_Main", WeaponGripRole.Primary, WeaponHand.Any, false);
                    AddPointIfMissing(weaponRoot, "ChainRoot", WeaponPointType.ChainRoot);
                    EnsureComponent<ChainWeaponPhysicsModule>(weaponRoot);
                    break;
                default:
                    AddGripIfMissing(weaponRoot, "Grip_Main", WeaponGripRole.Primary, WeaponHand.Any, false);
                    break;
            }
            instance.RefreshCache();
            EditorUtility.SetDirty(instance);
            Selection.activeGameObject = weaponRoot;
        }

        private static void EnsureAttack<TModule, TProfile>(GameObject root, string profileName)
            where TModule : WeaponAttackModule
            where TProfile : WeaponAttackProfile
        {
            TModule module = EnsureComponent<TModule>(root);
            SerializedObject serialized = new(module);
            SerializedProperty profileProp = serialized.FindProperty("profile");
            if (profileProp != null && profileProp.objectReferenceValue == null)
            {
                string folder = "Assets/AbyssWeaponFramework/AttackProfiles";
                EnsureAssetFolder(folder);
                string path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{root.name}_{profileName}.asset");
                TProfile profile = CreateInstance<TProfile>();
                AssetDatabase.CreateAsset(profile, path);
                profileProp.objectReferenceValue = profile;
                serialized.ApplyModifiedProperties();
                AssetDatabase.SaveAssets();
            }
        }

        private static T EnsureComponent<T>(GameObject root) where T : Component
        {
            T component = root.GetComponent<T>();
            return component != null ? component : Undo.AddComponent<T>(root);
        }

        private static WeaponInstance EnsureWeaponInstance(GameObject root)
        {
            WeaponInstance instance = root.GetComponent<WeaponInstance>();
            if (instance == null) instance = Undo.AddComponent<WeaponInstance>(root);
            instance.RefreshCache();
            return instance;
        }

        private static void EnsureCharacterComponents(Animator animator)
        {
            if (animator.GetComponent<WeaponOwnerProxy>() == null) Undo.AddComponent<WeaponOwnerProxy>(animator.gameObject);
            if (animator.GetComponent<WeaponMountController>() == null) Undo.AddComponent<WeaponMountController>(animator.gameObject);
            if (animator.GetComponent<WeaponHolsterController>() == null) Undo.AddComponent<WeaponHolsterController>(animator.gameObject);
            if (animator.GetComponent<WeaponSkeletonMap>() == null) Undo.AddComponent<WeaponSkeletonMap>(animator.gameObject);
            if (animator.GetComponent<WeaponHandPoseController>() == null) Undo.AddComponent<WeaponHandPoseController>(animator.gameObject);
            if (animator.GetComponent<WeaponAnimatorBridge>() == null) Undo.AddComponent<WeaponAnimatorBridge>(animator.gameObject);
            if (animator.GetComponent<WeaponAnimationEventRelay>() == null) Undo.AddComponent<WeaponAnimationEventRelay>(animator.gameObject);
            if (animator.isHuman)
            {
                if (animator.GetComponent<HumanoidWeaponIK>() == null) Undo.AddComponent<HumanoidWeaponIK>(animator.gameObject);
            }
            else if (animator.GetComponent<MappedWeaponIK>() == null) Undo.AddComponent<MappedWeaponIK>(animator.gameObject);
        }

        private static WeaponSkeletonMap EnsureSkeletonMap(Animator animator)
        {
            WeaponSkeletonMap map = animator.GetComponent<WeaponSkeletonMap>();
            return map != null ? map : Undo.AddComponent<WeaponSkeletonMap>(animator.gameObject);
        }

        private static void CreateHandSockets(Animator animator)
        {
            EnsureCharacterComponents(animator);
            if (!animator.isHuman)
            {
                EditorUtility.DisplayDialog("Abyss Weapon Framework", "For a Generic rig, create WeaponSocket objects under your mapped hand bones manually.", "OK");
                return;
            }
            Undo.RegisterFullObjectHierarchyUndo(animator.gameObject, "Create Weapon Hand Sockets");
            CreateOrRepairHandSocket(animator, HumanBodyBones.LeftHand, "LeftHand", WeaponHand.Left);
            CreateOrRepairHandSocket(animator, HumanBodyBones.RightHand, "RightHand", WeaponHand.Right);
            animator.GetComponent<WeaponMountController>()?.RefreshSockets();
        }

        private static void CreateOrRepairHandSocket(Animator animator, HumanBodyBones boneId, string socketId, WeaponHand hand)
        {
            Transform bone = animator.GetBoneTransform(boneId);
            if (bone == null) return;
            Transform child = bone.Find($"WeaponSocket_{socketId}");
            GameObject socketObject = child != null ? child.gameObject : new GameObject($"WeaponSocket_{socketId}");
            if (child == null) { Undo.RegisterCreatedObjectUndo(socketObject, "Create Weapon Socket"); socketObject.transform.SetParent(bone, false); }
            WeaponSocket socket = socketObject.GetComponent<WeaponSocket>();
            if (socket == null) socket = Undo.AddComponent<WeaponSocket>(socketObject);
            socket.EditorConfigure(socketId, WeaponSocketType.Hand, hand);
            EditorUtility.SetDirty(socket);
        }

        private static void AddGripIfMissing(GameObject root, string id, WeaponGripRole role, WeaponHand hand, bool ik)
        {
            WeaponInstance instance = EnsureWeaponInstance(root);
            instance.RefreshCache();
            if (instance.FindGrip(id) == null) AddGrip(root, id, role, hand, ik);
        }

        private static void AddPointIfMissing(GameObject root, string id, WeaponPointType type)
        {
            WeaponInstance instance = EnsureWeaponInstance(root);
            instance.RefreshCache();
            if (instance.FindPoint(id) == null && instance.FindPoint(type) == null) AddPoint(root, id, type);
        }

        private static void AddGrip(GameObject root, string id, WeaponGripRole role, WeaponHand preferredHand, bool ik)
        {
            EnsureWeaponInstance(root);
            GameObject marker = new(id); Undo.RegisterCreatedObjectUndo(marker, "Create Weapon Grip"); marker.transform.SetParent(root.transform, false);
            WeaponGripPoint grip = Undo.AddComponent<WeaponGripPoint>(marker);
            grip.EditorConfigure(id, role, true, WeaponSocketType.Hand, preferredHand, ik);
            Selection.activeGameObject = marker;
        }

        private static void AddStowGrip(GameObject root)
        {
            EnsureWeaponInstance(root);
            GameObject marker = new("Grip_Stow"); Undo.RegisterCreatedObjectUndo(marker, "Create Weapon Stow Grip"); marker.transform.SetParent(root.transform, false);
            WeaponGripPoint grip = Undo.AddComponent<WeaponGripPoint>(marker);
            grip.EditorConfigure("Grip_Stow", WeaponGripRole.Stow, false, WeaponSocketType.Any, WeaponHand.Any, false);
            Selection.activeGameObject = marker;
        }

        private static void AddPoint(GameObject root, string id, WeaponPointType type)
        {
            EnsureWeaponInstance(root);
            GameObject marker = new(id); Undo.RegisterCreatedObjectUndo(marker, "Create Weapon Point"); marker.transform.SetParent(root.transform, false);
            WeaponPoint point = Undo.AddComponent<WeaponPoint>(marker); point.EditorConfigure(id, type);
            Selection.activeGameObject = marker;
        }

        private void CreateDefinitionAsset()
        {
            GameObject prefabAsset = EditorUtility.IsPersistent(weaponRoot) ? weaponRoot : PrefabUtility.GetCorrespondingObjectFromSource(weaponRoot);
            if (prefabAsset == null) { EditorUtility.DisplayDialog("Abyss Weapon Framework", "Save the weapon as a Prefab first (or open it in Prefab Mode).", "OK"); return; }
            string folder = "Assets/AbyssWeaponFramework/Definitions"; EnsureAssetFolder(folder);
            string safeName = string.IsNullOrWhiteSpace(definitionDisplayName) ? weaponRoot.name : definitionDisplayName;
            string path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{safeName.Replace(' ', '_')}.asset");
            WeaponDefinition definition = CreateInstance<WeaponDefinition>();
            definition.EditorConfigure(definitionId, definitionDisplayName, prefabAsset, definitionFamily, definitionHandUsage);
            AssetDatabase.CreateAsset(definition, path); AssetDatabase.SaveAssets(); Selection.activeObject = definition;
        }

        private void PreviewEquip(WeaponHand hand)
        {
            ClearPreview();
            CreateHandSockets(characterAnimator);
            WeaponMountController controller = characterAnimator.GetComponent<WeaponMountController>(); controller.RefreshSockets();
            WeaponSocket socket = controller.FindSocket(hand == WeaponHand.Left ? "LeftHand" : "RightHand");
            if (socket == null) return;
            previewObject = Instantiate(weaponRoot); previewObject.name = $"[PREVIEW] {weaponRoot.name}"; previewObject.hideFlags = HideFlags.DontSaveInEditor;
            WeaponInstance instance = previewObject.GetComponent<WeaponInstance>();
            if (instance == null) { ClearPreview(); return; }
            instance.RefreshCache();
            WeaponGripPoint grip = null;
            for (int i = 0; i < instance.Grips.Count; i++) if (instance.Grips[i] != null && instance.Grips[i].Role == WeaponGripRole.Primary) { grip = instance.Grips[i]; break; }
            if (grip == null && instance.Grips.Count > 0) grip = instance.Grips[0];
            if (grip == null) { ClearPreview(); return; }
            WeaponTransformUtility.AlignGripToTarget(previewObject.transform, grip.transform, socket.MountTransform);
            previewObject.transform.SetParent(socket.MountTransform, true); Selection.activeGameObject = previewObject;
        }

        private void ClearPreview()
        {
            if (previewObject != null) DestroyImmediate(previewObject);
            previewObject = null;
        }

        private void ShowWeaponValidation()
        {
            bool valid = WeaponSystemValidator.ValidateWeaponPrefab(weaponRoot, out string report);
            EditorUtility.DisplayDialog(valid ? "Weapon PASS" : "Weapon Issues", report, "OK");
        }

        private void ShowCharacterValidation()
        {
            WeaponMountController controller = characterAnimator.GetComponent<WeaponMountController>();
            bool valid = WeaponSystemValidator.ValidateController(controller, out string report);
            EditorUtility.DisplayDialog(valid ? "Character PASS" : "Character Issues", report, "OK");
        }

        internal static void EnsureAssetFolder(string fullPath)
        {
            string[] parts = fullPath.Split('/'); string current = parts[0];
            for (int i = 1; i < parts.Length; i++) { string next = current + "/" + parts[i]; if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]); current = next; }
        }
    }
}
#endif
