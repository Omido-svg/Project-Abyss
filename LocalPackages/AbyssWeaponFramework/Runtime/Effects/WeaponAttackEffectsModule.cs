using UnityEngine;

namespace ProjectAbyss.WeaponSystem
{
    [DisallowMultipleComponent]
    public sealed class WeaponAttackEffectsModule : WeaponModule
    {
        [SerializeField] private WeaponEffectModule effects;
        [SerializeField] private string attackEventId = "fire";
        [SerializeField] private string hitEventId = "impact";
        private WeaponAttackModule[] attacks;

        private void Awake()
        {
            effects ??= GetComponent<WeaponEffectModule>();
            attacks = GetComponentsInChildren<WeaponAttackModule>(true);
        }

        private void OnEnable() => SetSubscriptions(true);
        private void OnDisable() => SetSubscriptions(false);

        private void SetSubscriptions(bool subscribe)
        {
            attacks ??= GetComponentsInChildren<WeaponAttackModule>(true);
            for (int i = 0; i < attacks.Length; i++)
            {
                if (attacks[i] == null) continue;
                if (subscribe) { attacks[i].AttackStarted += OnAttackStarted; attacks[i].HitProduced += OnHit; }
                else { attacks[i].AttackStarted -= OnAttackStarted; attacks[i].HitProduced -= OnHit; }
            }
        }

        private void OnAttackStarted(WeaponAttackModule module) => effects?.Play(attackEventId);
        private void OnHit(WeaponHitInfo hit)
        {
            Quaternion rotation = hit.Normal.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(hit.Normal) : Quaternion.identity;
            effects?.Play(hitEventId, hit.Point, rotation);
        }
    }
}
