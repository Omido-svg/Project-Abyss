using System.Collections.Generic;
using UnityEngine;

namespace ProjectAbyss.WeaponSystem
{
    [DisallowMultipleComponent]
    public sealed class MeleeWeaponModule : WeaponAttackModule
    {
        [SerializeField] private WeaponPoint bladeStart;
        [SerializeField] private WeaponPoint bladeEnd;
        [SerializeField] private bool sampleInFixedUpdate = true;

        private readonly HashSet<int> hitThisWindow = new();
        private bool windowOpen;
        private Vector3 previousStart;
        private Vector3 previousEnd;
        private WeaponAttackRequest activeRequest;
        private Collider[] overlapBuffer = new Collider[32];
        private RaycastHit[] sweepBuffer = new RaycastHit[32];
        private readonly List<MonoBehaviour> ownerBehaviours = new();
        private WeaponInstance activeWeapon;
        private GameObject activeOwner;

        public override WeaponAttackKind Kind => WeaponAttackKind.Melee;
        public bool IsAttackWindowOpen => windowOpen;
        protected override bool CompletesImmediately => false;

        private void FixedUpdate()
        {
            if (windowOpen && sampleInFixedUpdate) SampleSweep();
        }

        private void LateUpdate()
        {
            if (windowOpen && !sampleInFixedUpdate) SampleSweep();
        }

        protected override bool CanAttack(in WeaponAttackRequest request)
        {
            if (!base.CanAttack(in request) || Profile is not MeleeAttackProfile) return false;
            ResolveBladePoints();
            return bladeStart != null && bladeEnd != null;
        }

        protected override bool ExecuteAttack(in WeaponAttackRequest request)
        {
            return BeginWindowInternal(in request);
        }

        public void BeginAttackWindow()
        {
            WeaponAttackRequest request = default;
            TryAttack(in request);
        }

        public void BeginAttackWindow(string runtimeAttackId)
        {
            WeaponAttackRequest request = new(runtimeAttackId: runtimeAttackId);
            TryAttack(in request);
        }

        public void EndAttackWindow()
        {
            if (!windowOpen) return;

            // 정상 종료는 마지막 sweep을 한 번 보장하고 deferred attack을 완료한다.
            SampleSweep();
            CancelAttackWindow(
                notifyCompleted: true);
        }

        private void OnDisable()
        {
            // 비활성화는 정상 타격 종료가 아니라 취소다.
            // 이전 sweep 위치/request/owner가 다음 활성화에 남지 않게 전부 비운다.
            CancelAttackWindow(
                notifyCompleted: false);
        }

        public override void OnWeaponUnequipped(WeaponEquipContext context)
        {
            CancelAttackWindow(
                notifyCompleted: false);
            base.OnWeaponUnequipped(context);
        }

        private void CancelAttackWindow(
            bool notifyCompleted)
        {
            bool wasOpen = windowOpen;

            windowOpen = false;
            hitThisWindow.Clear();
            ownerBehaviours.Clear();
            activeRequest = default;
            activeWeapon = null;
            activeOwner = null;
            previousStart = default;
            previousEnd = default;

            if (wasOpen && notifyCompleted)
                CompleteDeferredAttack();
        }

        private bool BeginWindowInternal(in WeaponAttackRequest request)
        {
            ResolveBladePoints();
            if (bladeStart == null || bladeEnd == null) return false;
            activeRequest = request;
            activeWeapon = GetComponentInParent<WeaponInstance>();
            activeOwner = ResolveOwner(in activeRequest);
            WeaponHitQueryUtility.RefreshOwnerBehaviours(activeOwner, ownerBehaviours);
            hitThisWindow.Clear();
            previousStart = bladeStart.transform.position;
            previousEnd = bladeEnd.transform.position;
            windowOpen = true;
            SampleCurrentCapsule();
            return true;
        }

        private void SampleSweep()
        {
            if (Profile is not MeleeAttackProfile profile) return;
            ResolveBladePoints();
            if (bladeStart == null || bladeEnd == null) return;

            Vector3 currentStart = bladeStart.transform.position;
            Vector3 currentEnd = bladeEnd.transform.position;
            SampleCurrentCapsule();
            SampleEndpointSweep(previousStart, currentStart, profile);
            SampleEndpointSweep(previousEnd, currentEnd, profile);
            previousStart = currentStart;
            previousEnd = currentEnd;
        }

        private void SampleCurrentCapsule()
        {
            if (Profile is not MeleeAttackProfile profile || bladeStart == null || bladeEnd == null) return;
            EnsureBuffer(profile.MaxTargetsPerStep);
            int count = Physics.OverlapCapsuleNonAlloc(bladeStart.transform.position, bladeEnd.transform.position, profile.Radius, overlapBuffer, profile.HitMask, profile.TriggerInteraction);
            for (int i = 0; i < count; i++) ProcessCollider(overlapBuffer[i], bladeStart.transform.position, bladeEnd.transform.position, profile, 0f);
        }

        private void SampleEndpointSweep(Vector3 from, Vector3 to, MeleeAttackProfile profile)
        {
            Vector3 delta = to - from;
            float distance = delta.magnitude;
            if (distance <= 0.00001f) return;
            int hitCount = WeaponPhysicsQueryUtility.CastSorted(
                from,
                profile.Radius,
                delta / distance,
                distance,
                profile.HitMask,
                profile.TriggerInteraction,
                ref sweepBuffer);
            for (int i = 0; i < hitCount; i++)
                ProcessCollider(sweepBuffer[i].collider, from, to, profile, sweepBuffer[i].distance);
        }

        private void ProcessCollider(Collider collider, Vector3 a, Vector3 b, MeleeAttackProfile profile, float distance)
        {
            WeaponInstance weapon = activeWeapon != null
                ? activeWeapon
                : GetComponentInParent<WeaponInstance>();
            GameObject owner = activeOwner != null
                ? activeOwner
                : ResolveOwner(in activeRequest);
            if (WeaponHitQueryUtility.IsSelf(collider, weapon, owner)) return;
            if (!WeaponHitQueryUtility.PassesOwnerFilters(collider, weapon, ownerBehaviours)) return;
            int targetId = WeaponHitQueryUtility.ResolveTargetId(collider);
            if (!profile.AllowRepeatedHitPerWindow && hitThisWindow.Contains(targetId)) return;
            if (!profile.AllowMultipleTargets && hitThisWindow.Count > 0 && !hitThisWindow.Contains(targetId)) return;

            Vector3 center = (a + b) * 0.5f;
            Vector3 point = collider.ClosestPoint(center);
            Vector3 normal = point - collider.bounds.center;
            if (normal.sqrMagnitude < 0.000001f) normal = -transform.forward;
            Vector3 direction = (b - a).sqrMagnitude > 0.000001f ? (b - a).normalized : transform.forward;

            hitThisWindow.Add(targetId);
            WeaponHitInfo hit = new(WeaponAttackKind.Melee,
                string.IsNullOrWhiteSpace(activeRequest.RuntimeAttackId) ? profile.AttackId : activeRequest.RuntimeAttackId,
                profile.DamageChannel, profile.Power, weapon, owner, collider, point, normal.normalized, direction, distance, hitThisWindow.Count - 1);
            PublishHit(in hit);
        }

        private void ResolveBladePoints()
        {
            WeaponInstance weapon = GetComponentInParent<WeaponInstance>();
            if (weapon == null) return;
            bladeStart ??= weapon.FindPoint(WeaponPointType.BladeStart);
            bladeEnd ??= weapon.FindPoint(WeaponPointType.BladeEnd);
        }

        private void EnsureBuffer(int size)
        {
            if (overlapBuffer == null || overlapBuffer.Length < size) overlapBuffer = new Collider[Mathf.NextPowerOfTwo(size)];
        }
    }
}