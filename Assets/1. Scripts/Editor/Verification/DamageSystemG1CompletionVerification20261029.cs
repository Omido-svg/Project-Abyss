using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 1029 데미지 개편 G1 — 완료 게이트 검증.
///
/// 이 단계는 **검증 전용**이다. 제품 코드는 변경하지 않고,
/// 계획서 §6의 남은 시나리오(T13~T16, T20 확장)와 §8 G1 체크리스트 중
/// 자동화 가능한 항목을 고정한다.
///
/// T13: 정상 부위 HP10 + 일반 피해 15 → 부위 HP1·약화, 전체 HP 85 (단일 타격 파괴 불가)
/// T14: 이미 약화된 부위 HP1 + 파괴 권한 피해 1 → 부위 파괴, 전체 HP 99, 이벤트 중복 없음
/// T15: 정상 부위 HP10 + 처형 권한(선행 약화 무시) 피해 10 → 단일 타격 파괴, 전체 HP 90
/// T16: 파괴된 부위에 다음 피해 10 → 부위 HP0 유지, 전체 HP -10, 파괴 이벤트 중복 없음
/// T20(확장): 같은 행동의 추가타 2회 → 원인 SkillPart 유지, DamageResult 중복 없음
/// Observer: 피해 1회당 이벤트 1회, 해지 후 미전달, 재진입(새 Fixture) 시 중복 없음
///
/// Play Mode + 초기화된 BattleManager 필요.
/// </summary>
public static class DamageSystemG1CompletionVerification20261029
{
    private const string LogPrefix = "[Damage G1]";

    private static int pass;
    private static int fail;

    private static readonly List<SkillDefinition> createdDefinitions = new();

    [MenuItem("Game System Verification/1029 Damage G1 - Completion Gate (Play Mode)")]
    public static void VerifyFromMenu()
    {
        if (!Application.isPlaying)
        {
            throw new InvalidOperationException(
                "1029 Damage G1 검증은 Play Mode에서 실행해야 합니다.");
        }

        BattleManager manager =
            UnityEngine.Object.FindFirstObjectByType<BattleManager>();

        if (manager == null ||
            manager.BattleContext?.Player == null ||
            manager.BattleContext.Enemies == null)
        {
            throw new InvalidOperationException(
                "초기화된 BattleManager와 Player/Enemy가 필요합니다.");
        }

        Character bodyPartEnemy = FindEnemy(
            manager,
            candidate => candidate.UsesBodyParts);

        Character singleHpEnemy = FindEnemy(
            manager,
            candidate => !candidate.UsesBodyParts);

        if (bodyPartEnemy == null || singleHpEnemy == null)
        {
            throw new InvalidOperationException(
                "부위형/단일HP Enemy가 각각 필요합니다.");
        }

        pass = 0;
        fail = 0;
        createdDefinitions.Clear();

        try
        {
            RunWithFixture(
                manager, bodyPartEnemy,
                fixture => VerifyT13NormalHitCannotBreak(fixture));

            RunWithFixture(
                manager, bodyPartEnemy,
                fixture => VerifyT14BreakAuthorityOnWeakened(fixture));

            RunWithFixture(
                manager, bodyPartEnemy,
                fixture => VerifyT15ExecutionAuthoritySingleHit(fixture));

            RunWithFixture(
                manager, bodyPartEnemy,
                fixture => VerifyT16BrokenPartNoDuplicate(fixture));

            RunWithFixture(
                manager, bodyPartEnemy,
                fixture => VerifyT20AdditionalHitSingleResult(fixture));

            RunWithFixture(
                manager, singleHpEnemy,
                fixture => VerifyObserverDeliversOnce(fixture));

            RunWithFixture(
                manager, singleHpEnemy,
                fixture => VerifyObserverUnsubscribe(fixture));

            RunWithFixture(
                manager, singleHpEnemy,
                fixture => VerifyObserverAcrossFixtures(
                    manager, singleHpEnemy, fixture));

            RunWithFixture(
                manager, singleHpEnemy,
                fixture => VerifySharedDamageManager(fixture));
        }
        finally
        {
            foreach (SkillDefinition definition in createdDefinitions)
            {
                if (definition != null)
                    UnityEngine.Object.DestroyImmediate(definition);
            }

            createdDefinitions.Clear();
        }

        Debug.Log($"{LogPrefix} RESULT PASS={pass} FAIL={fail}");

        if (fail != 0)
        {
            throw new InvalidOperationException(
                $"1029 Damage G1 검증 실패: {fail} checks");
        }
    }

    private static Character FindEnemy(
        BattleManager manager,
        Func<Character, bool> predicate)
    {
        foreach (Character candidate in manager.BattleContext.Enemies)
        {
            if (candidate == null || candidate.IsDead)
                continue;

            if (predicate(candidate))
                return candidate;
        }

        return null;
    }

    private static BodyPart FirstPart(Character character)
    {
        if (character?.BodyParts == null)
            return null;

        foreach (BodyPart part in character.BodyParts)
        {
            if (part != null)
                return part;
        }

        return null;
    }

    private static void RunWithFixture(
        BattleManager manager,
        Character enemySource,
        Action<GameSystemVerificationFixture> body)
    {
        using GameSystemVerificationFixture fixture =
            GameSystemVerificationFixture.Create(
                manager.BattleContext.Player,
                enemySource,
                manager.BattleRules);

        CharacterVerificationScenarioTools.ResetCombatState(fixture.Player);
        CharacterVerificationScenarioTools.ResetCombatState(fixture.Enemy);

        fixture.MomentumManager?.Reset();

        body(fixture);
    }

    //====================================================================
    // T13 — 정상 부위, 일반 피해 : 약화까지만
    //====================================================================

    private static void VerifyT13NormalHitCannotBreak(
        GameSystemVerificationFixture fixture)
    {
        DamageManager manager = fixture.DamageManager;
        Character target = fixture.Enemy;
        BodyPart part = FirstPart(target);

        if (manager == null || target.RuntimeStatus == null || part == null)
        {
            Check("T13 manager/status/part exists", false);
            return;
        }

        part.SetDebugState(10f, 10f, false, false);
        target.RuntimeStatus.currentHP = 100;

        int weakenedEvents = 0;
        int breakEvents = 0;

        void OnWeakened(Character owner, BodyPart changed)
        {
            if (changed == part)
                weakenedEvents++;
        }

        void OnBroken(Character owner, BodyPart changed)
        {
            if (changed == part)
                breakEvents++;
        }

        BattleEvent battleEvent = target.BattleEvent;
        battleEvent.OnBodyPartWeakened += OnWeakened;
        battleEvent.OnBodyPartDestroyed += OnBroken;

        try
        {
            DamageContext context = manager.ApplyDamageContext(
                BuildPartRequest(fixture.Player, target, part, 15));

            Check("T13 normal hit lands on the target part",
                context != null && context.FinalDamage == 15,
                $"final={context?.FinalDamage}");

            Check("T13 part hp stops at 1",
                Mathf.CeilToInt(part.PartHP) == 1,
                $"partHp={part.PartHP}");

            Check("T13 part becomes weakened, not broken",
                part.State == BodyPartState.Weakened &&
                !part.IsBroken,
                $"state={part.State}");

            Check("T13 whole hp takes the full 15",
                target.CurrentHP == 85,
                $"hp={target.CurrentHP}");

            Check("T13 weakened event is raised exactly once",
                weakenedEvents == 1,
                $"weakenedEvents={weakenedEvents}");

            Check("T13 break event is not raised",
                breakEvents == 0,
                $"breakEvents={breakEvents}");
        }
        finally
        {
            battleEvent.OnBodyPartWeakened -= OnWeakened;
            battleEvent.OnBodyPartDestroyed -= OnBroken;
        }
    }

    //====================================================================
    // T14 — 이미 약화된 부위, 파괴 권한 : 파괴 1회
    //====================================================================

    private static void VerifyT14BreakAuthorityOnWeakened(
        GameSystemVerificationFixture fixture)
    {
        DamageManager manager = fixture.DamageManager;
        Character target = fixture.Enemy;
        BodyPart part = FirstPart(target);

        if (manager == null || target.RuntimeStatus == null || part == null)
        {
            Check("T14 manager/status/part exists", false);
            return;
        }

        part.SetDebugState(1f, 10f, true, false);
        target.RuntimeStatus.currentHP = 100;

        int weakenedEvents = 0;
        int breakEvents = 0;

        void OnWeakened(Character owner, BodyPart changed)
        {
            if (changed == part)
                weakenedEvents++;
        }

        void OnBroken(Character owner, BodyPart changed)
        {
            if (changed == part)
                breakEvents++;
        }

        BattleEvent battleEvent = target.BattleEvent;
        battleEvent.OnBodyPartWeakened += OnWeakened;
        battleEvent.OnBodyPartDestroyed += OnBroken;

        try
        {
            DamageRequest request =
                BuildPartRequest(fixture.Player, target, part, 1);

            request.CanBreakPart = true;
            request.BreakMode = PartBreakMode.WeakenedOnly;

            DamageContext context = manager.ApplyDamageContext(request);

            Check("T14 break-authority hit resolves",
                context != null && context.FinalDamage == 1,
                $"final={context?.FinalDamage}");

            Check("T14 weakened part is broken",
                part.IsBroken &&
                part.State == BodyPartState.Broken,
                $"state={part.State}");

            Check("T14 broken part hp is 0",
                Mathf.Approximately(part.PartHP, 0f),
                $"partHp={part.PartHP}");

            Check("T14 whole hp loses exactly 1",
                target.CurrentHP == 99,
                $"hp={target.CurrentHP}");

            Check("T14 break event is raised exactly once",
                breakEvents == 1,
                $"breakEvents={breakEvents}");

            Check("T14 weakened event is not raised again",
                weakenedEvents == 0,
                $"weakenedEvents={weakenedEvents}");
        }
        finally
        {
            battleEvent.OnBodyPartWeakened -= OnWeakened;
            battleEvent.OnBodyPartDestroyed -= OnBroken;
        }
    }

    //====================================================================
    // T15 — 처형 권한 : 선행 약화 없이 단일 타격 파괴
    //====================================================================

    private static void VerifyT15ExecutionAuthoritySingleHit(
        GameSystemVerificationFixture fixture)
    {
        DamageManager manager = fixture.DamageManager;
        Character target = fixture.Enemy;
        BodyPart part = FirstPart(target);

        if (manager == null || target.RuntimeStatus == null || part == null)
        {
            Check("T15 manager/status/part exists", false);
            return;
        }

        part.SetDebugState(10f, 10f, false, false);
        target.RuntimeStatus.currentHP = 100;

        int weakenedEvents = 0;
        int breakEvents = 0;

        void OnWeakened(Character owner, BodyPart changed)
        {
            if (changed == part)
                weakenedEvents++;
        }

        void OnBroken(Character owner, BodyPart changed)
        {
            if (changed == part)
                breakEvents++;
        }

        BattleEvent battleEvent = target.BattleEvent;
        battleEvent.OnBodyPartWeakened += OnWeakened;
        battleEvent.OnBodyPartDestroyed += OnBroken;

        try
        {
            DamageRequest request =
                BuildPartRequest(fixture.Player, target, part, 10);

            request.CanBreakPart = true;
            request.BreakMode =
                PartBreakMode.IgnoreWeakenedPrerequisite;

            DamageContext context = manager.ApplyDamageContext(request);

            Check("T15 execution hit resolves in one application",
                context != null && context.FinalDamage == 10,
                $"final={context?.FinalDamage}");

            Check("T15 normal part is broken in a single hit",
                part.IsBroken,
                $"state={part.State}");

            Check("T15 broken part hp is 0",
                Mathf.Approximately(part.PartHP, 0f),
                $"partHp={part.PartHP}");

            Check("T15 whole hp loses exactly 10",
                target.CurrentHP == 90,
                $"hp={target.CurrentHP}");

            Check("T15 break event is raised exactly once",
                breakEvents == 1,
                $"breakEvents={breakEvents}");

            Check("T15 execution bypasses the weakened step",
                weakenedEvents == 0,
                $"weakenedEvents={weakenedEvents}");
        }
        finally
        {
            battleEvent.OnBodyPartWeakened -= OnWeakened;
            battleEvent.OnBodyPartDestroyed -= OnBroken;
        }
    }

    //====================================================================
    // T16 — 파괴된 부위에 다음 타격 : 중복 이벤트 없음
    //====================================================================

    private static void VerifyT16BrokenPartNoDuplicate(
        GameSystemVerificationFixture fixture)
    {
        DamageManager manager = fixture.DamageManager;
        Character target = fixture.Enemy;
        BodyPart part = FirstPart(target);

        if (manager == null || target.RuntimeStatus == null || part == null)
        {
            Check("T16 manager/status/part exists", false);
            return;
        }

        part.SetDebugState(0f, 10f, false, true);
        target.RuntimeStatus.currentHP = 100;

        int breakEvents = 0;

        void OnBroken(Character owner, BodyPart changed)
        {
            if (changed == part)
                breakEvents++;
        }

        BattleEvent battleEvent = target.BattleEvent;
        battleEvent.OnBodyPartDestroyed += OnBroken;

        try
        {
            DamageContext context = manager.ApplyDamageContext(
                BuildPartRequest(fixture.Player, target, part, 10));

            Check("T16 hit on a broken part still resolves",
                context != null,
                "context=null");

            Check("T16 broken part hp stays 0",
                Mathf.Approximately(part.PartHP, 0f) &&
                part.IsBroken,
                $"partHp={part.PartHP}, state={part.State}");

            Check("T16 whole hp still loses exactly 10",
                target.CurrentHP == 90,
                $"hp={target.CurrentHP}");

            Check("T16 break event is not raised again",
                breakEvents == 0,
                $"breakEvents={breakEvents}");
        }
        finally
        {
            battleEvent.OnBodyPartDestroyed -= OnBroken;
        }
    }

    //====================================================================
    // T20(확장) — 같은 행동의 추가타 : 원인 유지·결과 중복 없음
    //====================================================================

    private static void VerifyT20AdditionalHitSingleResult(
        GameSystemVerificationFixture fixture)
    {
        DamageManager manager = fixture.DamageManager;
        Character player = fixture.Player;
        Character target = fixture.Enemy;
        BodyPart part = FirstPart(target);

        if (manager == null || target.RuntimeStatus == null || part == null)
        {
            Check("T20 manager/status/part exists", false);
            return;
        }

        part.SetDebugState(50f, 50f, false, false);
        target.RuntimeStatus.currentHP = 100;

        Skill skill =
            CreateSkill(player, 10, SkillColor.Red, ActionType.NormalAttack);

        ActionSlot slot =
            CreateSlot(player, target, part, skill, 10290701);

        BattleAction action = new BattleAction { Slot = slot };

        int resolvedEvents = 0;

        void OnResolved(DamageContext context)
        {
            if (context?.Target == target)
                resolvedEvents++;
        }

        BattleEvent battleEvent = target.BattleEvent;
        battleEvent.OnDamageResolved += OnResolved;

        try
        {
            DamageRequest main = DamageRequest.FromAction(
                action,
                DamageType.SkillPart,
                canBreakPart: false,
                isClashDamage: false,
                targetLostClash: false);

            main.Damage = 10;
            main.RawPower = 10;
            main.ApplyPhysicalResistance = false;
            main.ApplyGuard = false;

            DamageRequest additional = main;
            additional.Damage = 6;
            additional.RawPower = 6;
            additional.ApplyMomentum = false;

            DamageContext first = manager.ApplyDamageContext(main);
            DamageContext second = manager.ApplyDamageContext(additional);

            Check("T20 main hit resolves",
                first != null && first.FinalDamage == 10,
                $"final={first?.FinalDamage}");

            Check("T20 additional hit resolves separately",
                second != null && second.FinalDamage == 6,
                $"final={second?.FinalDamage}");

            Check("T20 additional hit keeps the SkillPart origin",
                first?.DamageType == DamageType.SkillPart &&
                second?.DamageType == DamageType.SkillPart,
                $"{first?.DamageType}/{second?.DamageType}");

            Check("T20 both hits share the same source action",
                first?.Action == action && second?.Action == action,
                "action mismatch");

            Check("T20 each hit produces its own result object",
                first?.Result != null &&
                second?.Result != null &&
                !ReferenceEquals(first.Result, second.Result),
                "result duplicated");

            Check("T20 hp loses exactly the sum of both hits",
                target.CurrentHP == 84,
                $"hp={target.CurrentHP}");

            Check("T20 one damage event per hit",
                resolvedEvents == 2,
                $"resolvedEvents={resolvedEvents}");
        }
        finally
        {
            battleEvent.OnDamageResolved -= OnResolved;
        }
    }

    //====================================================================
    // Observer — 1회 전달 / 해지 / 재진입
    //====================================================================

    private static void VerifyObserverDeliversOnce(
        GameSystemVerificationFixture fixture)
    {
        DamageManager manager = fixture.DamageManager;
        Character target = fixture.Enemy;

        if (manager == null)
        {
            Check("Observer manager exists", false);
            return;
        }

        target.RuntimeStatus.currentHP = 100;
        target.RuntimeStatus.currentBlock = 0;

        int resolved = 0;
        int taken = 0;

        void OnResolved(DamageContext context) => resolved++;
        void OnTaken(Character character, int amount) => taken++;

        BattleEvent battleEvent = target.BattleEvent;
        battleEvent.OnDamageResolved += OnResolved;
        battleEvent.OnDamageTaken += OnTaken;

        try
        {
            DamageContext context = manager.ApplyDamageContext(
                BuildDirectRequest(fixture.Player, target, 12));

            Check("Observer damage resolves",
                context != null && context.FinalDamage == 12,
                $"final={context?.FinalDamage}");

            Check("Observer OnDamageResolved is delivered once",
                resolved == 1,
                $"resolved={resolved}");

            Check("Observer OnDamageTaken is delivered once",
                taken == 1,
                $"taken={taken}");

            Check("Observer hp is reduced once",
                target.CurrentHP == 88,
                $"hp={target.CurrentHP}");

            manager.ApplyDamageContext(
                BuildDirectRequest(fixture.Player, target, 12));
            manager.ApplyDamageContext(
                BuildDirectRequest(fixture.Player, target, 12));

            Check("Observer repeated hits produce one event each",
                resolved == 3 && taken == 3,
                $"resolved={resolved}, taken={taken}");

            Check("Observer hp equals three applications",
                target.CurrentHP == 64,
                $"hp={target.CurrentHP}");
        }
        finally
        {
            battleEvent.OnDamageResolved -= OnResolved;
            battleEvent.OnDamageTaken -= OnTaken;
        }
    }

    private static void VerifyObserverUnsubscribe(
        GameSystemVerificationFixture fixture)
    {
        DamageManager manager = fixture.DamageManager;
        Character target = fixture.Enemy;

        if (manager == null)
        {
            Check("Unsubscribe manager exists", false);
            return;
        }

        target.RuntimeStatus.currentHP = 100;
        target.RuntimeStatus.currentBlock = 0;

        int resolved = 0;

        void OnResolved(DamageContext context) => resolved++;

        BattleEvent battleEvent = target.BattleEvent;
        battleEvent.OnDamageResolved += OnResolved;

        manager.ApplyDamageContext(
            BuildDirectRequest(fixture.Player, target, 5));

        int afterFirst = resolved;

        battleEvent.OnDamageResolved -= OnResolved;

        manager.ApplyDamageContext(
            BuildDirectRequest(fixture.Player, target, 5));

        Check("Unsubscribe first delivery happened once",
            afterFirst == 1,
            $"afterFirst={afterFirst}");

        Check("Unsubscribe stops later deliveries",
            resolved == 1,
            $"resolved={resolved}");

        Check("Unsubscribe does not block damage itself",
            target.CurrentHP == 90,
            $"hp={target.CurrentHP}");
    }

    private static void VerifyObserverAcrossFixtures(
        BattleManager manager,
        Character enemySource,
        GameSystemVerificationFixture current)
    {
        Character target = current.Enemy;
        int resolved = 0;

        void OnResolved(DamageContext context) => resolved++;

        BattleEvent disposedEvent = target.BattleEvent;
        disposedEvent.OnDamageResolved += OnResolved;

        // 1회 전달 확인 후 Fixture를 폐기한다(전투 종료/씬 재진입 모사).
        current.DamageManager.ApplyDamageContext(
            BuildDirectRequest(current.Player, target, 3));

        int beforeDispose = resolved;

        disposedEvent.OnDamageResolved -= OnResolved;

        using GameSystemVerificationFixture reentered =
            GameSystemVerificationFixture.Create(
                manager.BattleContext.Player,
                enemySource,
                manager.BattleRules);

        CharacterVerificationScenarioTools.ResetCombatState(reentered.Player);
        CharacterVerificationScenarioTools.ResetCombatState(reentered.Enemy);

        reentered.Enemy.RuntimeStatus.currentHP = 100;
        reentered.Enemy.RuntimeStatus.currentBlock = 0;

        int reenteredEvents = 0;

        void OnReentered(DamageContext context) => reenteredEvents++;

        BattleEvent reenteredEvent = reentered.Enemy.BattleEvent;
        reenteredEvent.OnDamageResolved += OnReentered;

        try
        {
            reentered.DamageManager.ApplyDamageContext(
                BuildDirectRequest(reentered.Player, reentered.Enemy, 7));
        }
        finally
        {
            reenteredEvent.OnDamageResolved -= OnReentered;
        }

        Check("Re-entry observer receives its own single event",
            reenteredEvents == 1,
            $"reenteredEvents={reenteredEvents}");

        Check("Re-entry does not leak the previous battle observer",
            resolved == beforeDispose,
            $"beforeDispose={beforeDispose}, resolved={resolved}");

        Check("Re-entry applies damage exactly once",
            reentered.Enemy.CurrentHP == 93,
            $"hp={reentered.Enemy.CurrentHP}");
    }

    //====================================================================
    // 공통 피해 경로 — 3인 캐릭터가 같은 DamageManager를 쓴다
    //====================================================================

    private static void VerifySharedDamageManager(
        GameSystemVerificationFixture fixture)
    {
        DamageManager manager = fixture.DamageManager;

        Check("Shared manager exists",
            manager != null);

        Check("Shared manager is the context service instance",
            ReferenceEquals(manager, fixture.Context?.Services?.DamageManager),
            "service mismatch");

        Check("Player resolves the same damage manager",
            ReferenceEquals(
                fixture.Player.BattleContext?.Services?.DamageManager,
                manager),
            "player mismatch");

        Check("Enemy resolves the same damage manager",
            ReferenceEquals(
                fixture.Enemy.BattleContext?.Services?.DamageManager,
                manager),
            "enemy mismatch");

        Check("There is a single damage manager per battle context",
            ReferenceEquals(
                fixture.Context?.ResolveDamageManager(),
                manager),
            "context mismatch");
    }

    //====================================================================
    // Helpers
    //====================================================================

    private static DamageRequest BuildPartRequest(
        Character source,
        Character target,
        BodyPart part,
        int damage)
    {
        DamageRequest request = DamageRequest.Custom(
            DamageType.SkillPart,
            source,
            target,
            part,
            damage,
            1f,
            canBreakPart: false,
            applyMomentum: false,
            applyGuard: false);

        request.ApplyPhysicalResistance = false;
        return request;
    }

    private static DamageRequest BuildDirectRequest(
        Character source,
        Character target,
        int damage)
    {
        DamageRequest request = DamageRequest.Custom(
            DamageType.Direct,
            source,
            target,
            null,
            damage,
            1f,
            canBreakPart: false,
            applyMomentum: false,
            applyGuard: true);

        request.ApplyPhysicalResistance = false;
        return request;
    }

    private static Skill CreateSkill(
        Character owner,
        int power,
        SkillColor color,
        ActionType actionType)
    {
        SkillDefinition definition =
            ScriptableObject.CreateInstance<SkillDefinition>();

        definition.name = $"G1Verification_{power}_{color}_{actionType}";
        definition.ActionType = actionType;
        definition.Color = color;
        definition.ResolverType = SkillResolverType.Dice;
        definition.ExchangeRollCount = 1;
        definition.Rolls = new List<SkillRollData>
        {
            new()
            {
                Index = 0,
                Type = CombatRollType.Attack,
                DiceMode = DicePowerMode.AbsoluteRange,
                MinPower = power,
                MaxPower = power,
                RngSource = RollRngSource.CharacterDefault
            }
        };
        definition.Rolls[0].Sanitize(0);

        createdDefinitions.Add(definition);

        Skill skill = definition.CreateRuntimeSkill();
        skill.Initialize(owner, owner.BattleEvent);
        return skill;
    }

    private static ActionSlot CreateSlot(
        Character owner,
        Character target,
        BodyPart targetPart,
        Skill skill,
        long actionId)
    {
        return new ActionSlot
        {
            ActionId = actionId,
            Owner = owner,
            Part = owner.BodyParts != null && owner.BodyParts.Count > 0
                ? owner.BodyParts[0]
                : null,
            Skill = skill,
            Speed = 10,
            ActionIndex = 0,
            SlotId = "VERIFY:DMG-G1",
            Phase = ActionPhase.COMBAT,
            TargetCharacter = target,
            TargetPart = targetPart,
            ResourceCostCommitted = true
        };
    }

    private static void Check(
        string label,
        bool condition,
        string detail = null)
    {
        if (condition)
        {
            pass++;
            Debug.Log($"{LogPrefix} PASS: {label}");
            return;
        }

        fail++;
        string suffix =
            string.IsNullOrEmpty(detail)
                ? string.Empty
                : $" ({detail})";

        Debug.LogError($"{LogPrefix} FAIL: {label}{suffix}");
    }
}
