#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class Canonical1112Verification
{
    public static string LastReport { get; private set; }
    [MenuItem("Tools/Project Abyss/1112/Verify Confirmed Rules")]
    public static void Run()
    {
        var randomBefore = UnityEngine.Random.state;
        UnityEngine.Random.InitState(1112);
        int passed = 0, failed = 0;
        var lines = new List<string>();
        var objects = new List<UnityEngine.Object>();
        var ctx = new BattleContext { SuppressPresentation = true, RunProgression = new RunProgressionState() };
        ctx.Services = new BattleRuntimeServices();
        ctx.EffectResolver = new BattleEffectResolver(ctx);
        ctx.Services.MomentumManager = new MomentumManager(ctx);
        ctx.Services.ActionManager = new ActionManager();
        ctx.Services.SpeedManager = new SpeedManager(ctx);
        ctx.Services.DamageManager = new DamageManager(ctx, ctx.Services.MomentumManager);
        ctx.Services.FervorManager = new FervorManager(ctx, ctx.Services.MomentumManager);
        void Check(string id, bool result)
        {
            if (result) passed++; else failed++;
            lines.Add((result ? "PASS " : "FAIL ") + id);
        }
        T Spawn<T>(string file) where T : Character
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/2. Data/TestEncounters/Prefabs/" + file + ".prefab");
            var go = UnityEngine.Object.Instantiate(prefab);
            go.hideFlags = HideFlags.DontSave;
            objects.Add(go);
            T c = go.GetComponent<T>();
            // Never modify a source asset when exercising configurable pending hooks.
            var data = UnityEngine.Object.Instantiate(c.Data);
            objects.Add(data);
            c.ConfigureAuthoringCore(data, c.Data.CombatLoadout);
            c.Initialize(ctx);
            return c;
        }
        SkillDefinition Definition(string id, ActionType type)
        {
            var d = ScriptableObject.CreateInstance<SkillDefinition>();
            objects.Add(d);
            d.name = id; d.SkillName = id; d.ActionType = type;
            d.OverrideEnergyCost = true; d.EnergyCost = 0;
            var so = new SerializedObject(d);
            so.FindProperty("skillId").stringValue = id;
            so.ApplyModifiedPropertiesWithoutUndo();
            return d;
        }
        try
        {
            var y = Spawn<Yujin>("Yujin_Test");
            var target = Spawn<Yujin>("Yujin_Test");
            var normal = Spawn<NormalEnemy>("NormalEnemy_Test");
            ctx.Player = y; ctx.Enemies.Add(target); ctx.Enemies.Add(normal);
            var left = y.GetBodyPart(PartType.LEFT_HAND);
            var head = y.GetBodyPart(PartType.HEAD);
            var legs = y.GetBodyPart(PartType.LEGS);
            var targetPart = target.GetBodyPart(PartType.LEFT_HAND);
            var mech = y.YujinMechanic;
            mech.SetWeaponForVerification(YujinWeaponType.Baeku);
            var def = Definition(YujinSkillIds.HwanhyeongAttack, ActionType.Preparation);
            var prep = new YujinPreparationRuntimeSkill(def); prep.Initialize(y, ctx._battleEvent);
            var slot = new ActionSlot { ActionId = 100, Owner = y, Part = legs, Skill = prep,
                PlanningChoiceId = YujinWeaponType.Nakil.ToString(), Phase = ActionPhase.FORESIGHT };
            Check("H01 환형 빛 0", prep.EnergyCost == 0);
            Check("H02 환형 즉시 분류", PreparationActivationPolicy.IsImmediate(prep));
            Check("H03 환형 계획 commit", ((IActionPlanningCommitRule)mech).TryCommitPlannedSlot(slot, out _));
            Check("H04 같은 턴 무기 적용", mech.CurrentWeapon == YujinWeaponType.Nakil && !mech.HasPendingWeapon);
            var attackDef = Definition("yujin.normal.test1112", ActionType.NormalAttack);
            var attack = new YujinNormalRuntimeSkill(attackDef); attack.Initialize(y, ctx._battleEvent);
            Check("H05 낙일 1굴림", attack.ExchangeRollCount == 1);
            ((IActionPlanningCommitRule)mech).RollbackPlannedSlot(slot);
            Check("H06 취소 시 원래 무기 복구", mech.CurrentWeapon == YujinWeaponType.Baeku && attack.ExchangeRollCount == 3);
            var regular = new DataPreparationSkill(Definition("common.preparation.crouch", ActionType.Preparation));
            regular.Initialize(y, ctx._battleEvent);
            Check("P01 방어 도사림 해결 분류", !PreparationActivationPolicy.IsImmediate(regular));
            var blood = new DataPreparationSkill(Definition(OlafSkillIds.BreathBoughtWithBlood, ActionType.Preparation));
            blood.Initialize(y, ctx._battleEvent);
            Check("P02 빛 생산 도사림 즉시 분류", PreparationActivationPolicy.IsImmediate(blood));
            var sorter = new ActionPhaseSorter();
            Check("P03 도사림 다리 우선", sorter.CompareForExecution(
                new ActionSlot { Owner = y, Part = legs, Phase = ActionPhase.FORESIGHT, Speed = 1, ActionId = 2 },
                new ActionSlot { Owner = y, Part = head, Phase = ActionPhase.FORESIGHT, Speed = 99, ActionId = 1 }) < 0);
            var duelDef = Definition(YujinSkillIds.FinishIt, ActionType.Duel);
            var duel = new YujinDuelRuntimeSkill(duelDef); duel.Initialize(y, ctx._battleEvent);
            Check("Y01 끝장을 보다 무기 굴림 수", duel.ExchangeRollCount == 3);
            Check("Y02 평타 재굴림 불가", !YujinMechanic.IsSenseEligibleAction(attack));
            Check("Y03 결투 재굴림 가능", YujinMechanic.IsSenseEligibleAction(duel));
            var advance = new YujinDuelRuntimeSkill(Definition(YujinSkillIds.AdvanceTiming, ActionType.Duel));
            advance.Initialize(y, ctx._battleEvent);
            Check("Y04 현재 감 부족은 배치 하드 게이트 아님", advance.CanUseByResource(y));
            mech.QueueMarkForNextTurn(target, targetPart, 2);
            target.AddPartStatus(targetPart, new DeferredStatusEffect(StatusEffectId.Designation, 5, 1), y);
            mech.QueueMarkForNextTurn(target, targetPart, 1);
            Check("Q01 예약은 현재 표식 아님", mech.GetMark(target, targetPart) == 0);
            ctx.TurnStartEffects.Drain();
            Check("Q02 표식 지정 표식 전역 FIFO", mech.GetMark(target, targetPart) == 8);
            Check("Q03 예약 큐 소진", ctx.TurnStartEffects.Count == 0 && mech.PendingMarkCount == 0);
            var sequence = new List<int>();
            ctx.TurnStartEffects.Enqueue(() => { sequence.Add(1); ctx.TurnStartEffects.Enqueue(() => sequence.Add(3)); });
            ctx.TurnStartEffects.Enqueue(() => sequence.Add(2)); ctx.TurnStartEffects.Drain();
            Check("Q04 처리 중 추가 예약도 FIFO", sequence.SequenceEqual(new[] { 1, 2, 3 }));
            y.AddPartStatus(left, new YujinStealthStatus(2), y);
            y.AddPartStatus(left, new YujinStealthStatus(1), y);
            Check("S01 은신 독립 인스턴스", left.StatusEffects.OfType<YujinStealthStatus>().Count() == 2);
            ctx._battleEvent.RaiseActionEnd(new BattleAction { Slot = new ActionSlot { Owner = y, Part = left, Skill = attack } });
            Check("S02 스킬 행동 1회만 차감", left.StatusEffects.OfType<YujinStealthStatus>().Single().Stack == 1);
            YujinStealthStatus.Reserve(y, left, 2, y);
            YujinStealthStatus.ClearActive(y, left);
            Check("S03 활성 해제", !YujinStealthStatus.IsHidden(left));
            ctx.TurnStartEffects.Drain();
            Check("S04 활성 해제는 예약 유지", YujinStealthStatus.IsHidden(left));
            Check("T01 파괴 부위 조준 금지", !AITargetSelector.ShouldIncludeBrokenTargets(attack));
            mech.SetWeaponForVerification(YujinWeaponType.Nakil);
            var action = new BattleAction { Slot = new ActionSlot { Owner = y, Part = left, Skill = attack,
                TargetCharacter = target, TargetPart = targetPart } };
            Check("D01 낙일 뒷면도 처형 권한", attack.ResolvePartBreakMode(action, targetPart) == PartBreakMode.IgnoreWeakenedPrerequisite);
            target.ClearBlock();
            targetPart.SetDebugState(100, 100, false, false);
            ctx.Services.DamageManager.ApplyDamageContext(action, 1, false, false, false);
            Check("D02 피해 부족은 즉사 아님", !targetPart.IsBroken && !targetPart.IsWeakened);
            ctx.Services.DamageManager.ApplyDamageContext(action, 250, false, false, false);
            Check("D03 충분한 피해는 정상 부위 직접 처형", targetPart.IsBroken);
            bool moved = mech.TransferTargetMark(target, targetPart, action);
            int movedSum = target.BodyParts.Where(p => !p.IsBroken).Sum(p => mech.GetMark(target, p));
            Check("Y05 숨통 표식 전이 moved=" + moved + " sum=" + movedSum + " hp=" + target.CurrentHP + " dead=" + target.IsDead, moved && movedSum == 8);
            Check("Y06 표식 전이는 추가 표식을 만들지 않음", !mech.TransferTargetMark(target, targetPart, action));
            int before = normal.CurrentEnergy;
            normal.TryConsumeEnergy(before);
            ctx.Services.MomentumManager.SetMomentumForDebug(-100);
            ctx.Services.MomentumManager.FinalizeTurn();
            ctx.Services.MomentumManager.BeginTurn();
            Check("M01 일반 몹 짓누름 빛 보상", normal.CurrentEnergy == 1);
            Check("M02 일반 몹 발악 없음", !ctx.Services.MomentumManager.HasLastStandJudgmentBonus(normal));
            Check("U01 미정 덫 지속 임의 생성 없음", mech.RegisterMarkDeadline(target, target.GetBodyPart(PartType.RIGHT_HAND), 3, 2) == 0);
            StageOneEnemy1112Mechanic machineBoss = null, inquisitor = null;
            foreach (StageOneEnemyKind1112 kind in Enum.GetValues(typeof(StageOneEnemyKind1112)))
            {
                if (kind == StageOneEnemyKind1112.None) continue;
                var prefab = Resources.Load<GameObject>(StageOneEnemy1112Mechanic.ResourcePath + "/" + kind + "/" + kind);
                Check("E01 " + kind + " prefab", prefab != null);
                var go = UnityEngine.Object.Instantiate(prefab); objects.Add(go);
                var enemy = go.GetComponent<EliteEnemy>(); enemy.Initialize(ctx); ctx.Enemies.Add(enemy);
                var m = enemy.GetMechanic<StageOneEnemy1112Mechanic>();
                bool boss = kind == StageOneEnemyKind1112.Inquisitor || kind == StageOneEnemyKind1112.MachineBoss;
                Check("E02 " + kind + " 5부위와 전체 HP", enemy.BodyParts.Count == 5 && enemy.MaxCombatHP == (boss ? 1500 : 750));
                Check("E03 " + kind + " 흐트러짐", enemy.GetMechanic<StaggerGaugeMechanic>().MaxGauge == (boss ? 360 : 240));
                ctx.Services.SpeedManager.RollAllSpeed();
                var plans = m.Plan();
                Check("E04 " + kind + " 전체 부위 계획", plans.Count == 5);
                Check("E05 " + kind + " 보통 자세 공격 3", plans.Count(p => p.Phase == ActionPhase.COMBAT) == 3);
                Check("E06 " + kind + " 즉시 도사림 2", plans.Count(p => p.PlanningEffectCommitted && p.SkipResolution) == 2);
                Check("E07 " + kind + " 부위별 스킬 원본", plans.Where(p => p.Phase == ActionPhase.COMBAT).All(p => p.Skill is StageOneEnemySkill1112));
                if (kind == StageOneEnemyKind1112.MachineBoss) machineBoss = m;
                if (kind == StageOneEnemyKind1112.Inquisitor) inquisitor = m;
            }
            var bossPart = machineBoss.Owner.BodyParts[0];
            // Start from a clean part that was assigned an attack (no charging preparation).
            int needed = 6 - machineBoss.ChargeAt(bossPart);
            machineBoss.GainCharge(bossPart, needed);
            Check("B01 충전 6에서 오버클록", machineBoss.IsOverclocked(bossPart) && machineBoss.ChargeAt(bossPart) == 0);
            Check("B02 오버클록 냉각 카드 지급", machineBoss.CoolingCards > 0);
            machineBoss.GainCharge(bossPart, 6);
            Check("B03 두 번째 충전 6에서 폭주", machineBoss.IsRampaging(bossPart) && !machineBoss.IsOverclocked(bossPart));
            machineBoss.GainCharge(bossPart, 5);
            Check("B04 폭주 중 충전 금지", machineBoss.ChargeAt(bossPart) == 0);
            var coolPart = machineBoss.Owner.BodyParts[2];
            machineBoss.GainCharge(coolPart, 6 - machineBoss.ChargeAt(coolPart));
            var temp = y.GetSelectableSkills(legs).OfType<StageOneTemporarySkill1112>().FirstOrDefault(t => t.IsCooling);
            Check("B05 냉각 임시 카드 선택 UI 연결", temp != null && temp.Choices().Count > 0);
            var service = new BattleActionPlanCommandService(ctx.Services.ActionManager, ctx.Services.SpeedManager);
            y.AddEnergy(100);
            var result = service.TryAssign(new ActionPlanAssignmentRequest { Owner = y, OwnerPart = legs,
                Skill = temp, Target = y, TargetPart = legs, TargetRule = TargetSelectionRule.LivingPartOnly,
                PlanningChoiceId = coolPart.PartId });
            Check("B06 냉각 실제 계획 적용 " + result.FailureReason, result.Success && !machineBoss.IsOverclocked(coolPart));
            if (result.Success)
            {
                var cancel = new PlanningActionCancellationService(ctx.Services.ActionManager);
                Check("B07 냉각 취소 복구", cancel.Cancel(result.Slot, out _) && machineBoss.IsOverclocked(coolPart));
            }
            int beforeInvalidChoice = y.CurrentEnergy;
            var invalidChoice = service.TryAssign(new ActionPlanAssignmentRequest { Owner = y, OwnerPart = legs,
                Skill = temp, Target = y, TargetPart = legs, TargetRule = TargetSelectionRule.LivingPartOnly,
                PlanningChoiceId = "invalid-part" });
            Check("B08 잘못된 냉각 대상은 비용 없이 거절", !invalidChoice.Success && y.CurrentEnergy == beforeInvalidChoice);
            // Execute actual production ClashManager, including gates, damage,
            // consumption and ActionEnd, for every new enemy family.
            foreach (var enemy in ctx.Enemies.OfType<EliteEnemy>().ToArray())
            {
                var rule = enemy.GetMechanic<StageOneEnemy1112Mechanic>();
                if (rule == null) continue;
                var victim = Spawn<Yujin>("Yujin_Test"); ctx.Player = victim;
                ctx.Services.SpeedManager.RollAllSpeed(); enemy.AddEnergy(100);
                var planned = rule.Plan().Where(p => p.Phase == ActionPhase.COMBAT).ToArray();
                var clash = new ClashManager(ctx, ctx.Services.DamageManager, ctx.Services.MomentumManager);
                int hits = 0;
                foreach (var plannedSlot in planned)
                {
                    plannedSlot.ActionId = 1000 + hits;
                    var r = clash.ResolvePair(new ClashPair(plannedSlot));
                    hits += r?.Exchanges?.Count ?? 0;
                    victim.RestoreCurrentHP(10000);
                }
                Check("X01 " + rule.Kind + " 실제 일방 해결", hits > 0);
                var enemyPart = enemy.BodyParts.First(p => !p.IsBroken);
                string key = rule.Kind == StageOneEnemyKind1112.Inquisitor ? "Confess" : "B";
                var d = Resources.LoadAll<SkillDefinition>(StageOneEnemy1112Mechanic.ResourcePath + "/" + rule.Kind).First(v => v.name == key);
                var enemySkill = new StageOneEnemySkill1112(d, key, rule, enemyPart);
                enemySkill.Initialize(enemy, ctx._battleEvent); enemy.AddEnergy(100);
                var loseDef = Definition("1112.test.losing_duel", ActionType.Duel);
                loseDef.BasePowerRule = SkillBasePowerRule.LegacyExplicit; loseDef.BasePower = 0;
                loseDef.ResolverType = SkillResolverType.Dice; loseDef.DiceMin = loseDef.DiceMax = 0;
                loseDef.ExchangeRollCount = 1;
                var loseSkill = new DataDuelSkill(loseDef); loseSkill.Initialize(victim, ctx._battleEvent);
                var victimPart = victim.BodyParts.First(p => !p.IsBroken);
                var a = new ActionSlot { ActionId = 2001, Owner = enemy, Part = enemyPart, Skill = enemySkill,
                    TargetCharacter = victim, TargetPart = victimPart, Phase = ActionPhase.COMBAT };
                var b = new ActionSlot { ActionId = 2002, Owner = victim, Part = victimPart, Skill = loseSkill,
                    TargetCharacter = enemy, TargetPart = enemyPart, Phase = ActionPhase.COMBAT };
                var paired = clash.ResolvePair(new ClashPair(a, b));
                Check("X02 " + rule.Kind + " 실제 결투 게이트 해결", paired?.Exchanges?.Any(e => e.IsDuelExchange) == true);
            }
            ctx.Player = y;
            ctx.TemporaryCards1112.SealSkill(y, inquisitor);
            Check("I01 고해 스킬 봉인 1장", ctx.TemporaryCards1112.HasSealedSkill);
            var confessions = y.BodyParts.SelectMany(p => y.GetSelectableSkills(p)).OfType<StageOneTemporarySkill1112>().Where(t => !t.IsCooling).ToArray();
            Check("I02 고해 교체 카드 UI 연결", confessions.Length > 0);
            var confession = confessions.FirstOrDefault();
            if (confession != null)
            {
                var sourcePart = y.BodyParts.First(p => y.GetSelectableSkills(p).Contains(confession));
                var r = service.TryAssign(new ActionPlanAssignmentRequest { Owner = y, OwnerPart = sourcePart,
                    Skill = confession, Target = y, TargetPart = sourcePart, TargetRule = TargetSelectionRule.LivingPartOnly,
                    PlanningChoiceId = sourcePart.PartId });
                Check("I03 고해 계획 즉시 원래 스킬 복구 " + r.FailureReason, r.Success && !ctx.TemporaryCards1112.HasSealedSkill);
            }

        }
        finally
        {
            UnityEngine.Random.state = randomBefore;
            ctx.TurnStartEffects.Clear();
            foreach (var obj in objects.AsEnumerable().Reverse())
                if (obj != null) UnityEngine.Object.DestroyImmediate(obj);
        }
        LastReport = "1112 RESULT PASS=" + passed + " FAIL=" + failed + "\n" + string.Join("\n", lines);
        var path = Path.Combine(Application.dataPath, "../Logs/Canonical1112Verification.txt");
        File.WriteAllText(path, LastReport);
        Debug.Log(LastReport);
        if (failed > 0) throw new InvalidOperationException("1112 verification failures=" + failed);
    }
}
#endif
