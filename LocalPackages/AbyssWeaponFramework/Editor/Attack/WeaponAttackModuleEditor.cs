#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace ProjectAbyss.WeaponSystem.Editor
{
    [CustomEditor(typeof(WeaponAttackModule), true)]
    public sealed class WeaponAttackModuleEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            WeaponAttackModule attack = (WeaponAttackModule)target;
            EditorGUILayout.Space();
            if (attack.Profile == null) EditorGUILayout.HelpBox("Attack Profile is required.", MessageType.Error);
            else if (attack.Profile.Kind != attack.Kind) EditorGUILayout.HelpBox($"Profile kind {attack.Profile.Kind} does not match module kind {attack.Kind}.", MessageType.Error);
            else EditorGUILayout.HelpBox($"Attack ID: {attack.AttackId} / Cooldown: {attack.Profile.Cooldown:0.###}s", MessageType.Info);

            using (new EditorGUI.DisabledScope(!Application.isPlaying || attack.Profile == null))
            {
                if (attack is MeleeWeaponModule melee)
                {
                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button("Begin Melee Window")) melee.BeginAttackWindow();
                    if (GUILayout.Button("End Melee Window")) melee.EndAttackWindow();
                    EditorGUILayout.EndHorizontal();
                }
                else if (GUILayout.Button("Test Attack"))
                {
                    WeaponAttackRequest request = default;
                    attack.TryAttack(in request);
                }
            }
        }
    }
}
#endif
