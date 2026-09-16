#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Phase F — §1~§19 Traceability + G-01~G-17 Completion Gate.
///
/// 한 Player fixture로 Olaf/Yujin/Hifumi 런타임 Gate를 동시에 검증할 수 없으므로,
/// Phase F는 각 캐릭터 Gate와 Phase E Data Gate의 최신 보고서를 증거로 모아 최종 폐쇄한다.
/// Phase F 패치/데이터 변경 이전 보고서는 stale evidence로 취급한다.
/// </summary>
public static class PhaseFCompletionGate
{
    public const string PhaseFProfileId = "PHASE_F_COMPLETION_V2";

    private enum EvidenceKind
    {
        Olaf,
        Yujin,
        Hifumi,
        PhaseE
    }

    private sealed class EvidenceDefinition
    {
        public EvidenceKind Kind;
        public string Key;
        public string DisplayName;
        public string SignatureCaseId;
        public string RunInstruction;
    }

    private sealed class LoadedReport
    {
        public string Path;
        public GameSystemVerificationReport Report;
        public DateTime FinishedUtc;
    }

    private sealed class EvidenceSelection
    {
        public EvidenceDefinition Definition;
        public LoadedReport Loaded;
        public GameSystemVerificationStatus Status;
        public string Actual;
        public string Details;
    }

    private sealed class TraceDefinition
    {
        public int Section;
        public string Name;
        public EvidenceKind[] Evidence;
        public string[] Requirements;
        public string[] RequiredCaseIds;
    }

    private static readonly EvidenceDefinition[] EvidenceDefinitions =
    {
        new()
        {
            Kind = EvidenceKind.Olaf,
            Key = "olaf",
            DisplayName = "Phase D Olaf + 공통 A/B/C Runtime 증거",
            SignatureCaseId = "phased.olaf.o01.madness_core",
            RunInstruction = "Play Mode / Olaf Player에서 Phase D Olaf Gate 실행"
        },
        new()
        {
            Kind = EvidenceKind.Yujin,
            Key = "yujin",
            DisplayName = "Phase D Yujin + 공통 A/B/C Runtime 증거",
            SignatureCaseId = "phased.yujin.y01.weapon_profiles",
            RunInstruction = "Play Mode / Yujin Player에서 Phase D Yujin Gate 실행"
        },
        new()
        {
            Kind = EvidenceKind.Hifumi,
            Key = "hifumi",
            DisplayName = "Phase D Hifumi + 공통 A/B/C Runtime 증거",
            SignatureCaseId = "phased.hifumi.h01.bone_scale",
            RunInstruction = "Play Mode / Hifumi Player에서 Phase D Hifumi Gate 실행"
        },
        new()
        {
            Kind = EvidenceKind.PhaseE,
            Key = "phase_e",
            DisplayName = "Phase E Content/Core Data 증거",
            SignatureCaseId = "phasee.c31.normal_enemy",
            RunInstruction = "Edit Mode에서 Phase E Data Gate 실행"
        }
    };

    // 0915 checklist의 §1~§19 역방향 Traceability를 현재 0916 Phase E 범위로 갱신한 표.
    // Phase E의 O-02/Y-06은 현재 0916 풀(Olaf 49 / Yujin 35)을 기준으로 한다.
    private static readonly TraceDefinition[] TraceDefinitions =
    {
        T(1,  "전투 구조 · 부위", E(EvidenceKind.Olaf, EvidenceKind.PhaseE), R("C-02","C-12","C-13","C-15","C-45","C-31"), C("phasef.regression.heal.dual_pool")),
        T(2,  "턴 진행", E(EvidenceKind.Olaf), R("C-03","C-04","C-45","C-06"), C("system.runtime.planning.preparation_immediate_commit","system.runtime.planning.attack_cost_no_refund","system.runtime.planning.prestige_slotless")),
        T(3,  "합 — 굴림 소모전", E(EvidenceKind.Olaf), R("C-09","C-22","C-42","C-47"), C("system.runtime.pairing.exact_target_ignores_speed","phasef.regression.pairing.last_challenger")),
        T(4,  "굴림", E(EvidenceKind.Olaf, EvidenceKind.Yujin, EvidenceKind.Hifumi, EvidenceKind.PhaseE), R("C-10","C-05","C-44","C-43","Y-02","H-07","C-25")),
        T(5,  "기세 · 고조 · 열광", E(EvidenceKind.Olaf, EvidenceKind.Yujin, EvidenceKind.Hifumi), R("C-46","C-20","C-19","C-21","C-22","O-03","Y-01","H-06")),
        T(6,  "타격 타입 · 내성 · 흐트러짐", E(EvidenceKind.Olaf, EvidenceKind.Yujin, EvidenceKind.PhaseE), R("C-31","C-41","C-25","Y-01")),
        T(7,  "빛(에너지)", E(EvidenceKind.Olaf), R("C-04","C-17","C-18")),
        T(8,  "도사림", E(EvidenceKind.Olaf, EvidenceKind.PhaseE), R("C-03","C-24","O-02","Y-06","H-08")),
        T(9,  "위세", E(EvidenceKind.Olaf), R("C-22","C-02","C-03","C-45"), C("system.runtime.planning.prestige_slotless")),
        T(10, "데미지 · 방어도", E(EvidenceKind.Olaf), R("C-25","C-12","C-23","C-24","C-27")),
        T(11, "공용 상태이상", E(EvidenceKind.Olaf), R("C-27","C-48","C-49")),
        T(12, "적 설계", E(EvidenceKind.Olaf, EvidenceKind.PhaseE), R("C-43","C-31","C-41","C-40")),
        T(13, "스킬 구성 · 키워드 · 설계 원칙", E(EvidenceKind.Olaf, EvidenceKind.Yujin, EvidenceKind.Hifumi, EvidenceKind.PhaseE), R("C-45","O-02","Y-06","C-42","C-47")),
        T(14, "감정 증강", E(EvidenceKind.Olaf, EvidenceKind.PhaseE), R("C-34","C-35","C-32")),
        T(15, "스킬 강화", E(EvidenceKind.Olaf, EvidenceKind.PhaseE), R("C-36","C-37","C-38")),
        T(16, "아이템 · 런 경제", E(EvidenceKind.Olaf, EvidenceKind.PhaseE), R("C-33","C-39")),
        T(17, "올라프", E(EvidenceKind.Olaf, EvidenceKind.PhaseE), R("O-01","O-03","O-04","O-05","O-02")),
        T(18, "유진", E(EvidenceKind.Yujin, EvidenceKind.PhaseE), R("Y-01","Y-02","Y-03","Y-05","Y-07","Y-06")),
        T(19, "히후미", E(EvidenceKind.Hifumi, EvidenceKind.PhaseE), R("H-01","H-02","H-03","H-05","H-06","H-07","H-09","H-10","H-08","C-21"))
    };

    public static GameSystemVerificationReport Run()
    {
        DateTime started = DateTime.Now;
        DateTime sourceCutoffUtc = GetSourceCutoffUtc();
        List<LoadedReport> reports = LoadReports();
        Dictionary<EvidenceKind, EvidenceSelection> evidence = SelectEvidence(reports, sourceCutoffUtc);

        GameSystemVerificationReport report = new()
        {
            SessionId = started.ToString("yyyyMMdd_HHmmss_fff"),
            SpecId = $"{CanonicalGameSystemVerificationSpec.SpecId}__{PhaseFProfileId}__{GetBalanceProfile()}",
            StartedAt = started.ToString("O"),
            SceneName = SceneManager.GetActiveScene().name,
            PlayerName = "MULTI_EVIDENCE"
        };

        // 4 evidence rows + 19 trace rows + 17 completion rows = exactly 40 Phase F results.
        AddEvidenceRows(report, evidence, sourceCutoffUtc);

        List<GameSystemVerificationCaseResult> traceRows = new();
        foreach (TraceDefinition trace in TraceDefinitions)
        {
            GameSystemVerificationCaseResult result = EvaluateTrace(trace, evidence);
            traceRows.Add(result);
            report.Results.Add(result);
        }

        AddCompletionRows(report, evidence, traceRows);

        report.FinishedAt = DateTime.Now.ToString("O");
        report.RecalculateCounts();
        return report;
    }

    private static void AddEvidenceRows(
        GameSystemVerificationReport report,
        IReadOnlyDictionary<EvidenceKind, EvidenceSelection> evidence,
        DateTime cutoffUtc)
    {
        foreach (EvidenceDefinition definition in EvidenceDefinitions)
        {
            EvidenceSelection selection = evidence[definition.Kind];
            report.Results.Add(new GameSystemVerificationCaseResult
            {
                CaseId = $"phasef.evidence.{definition.Key}",
                RequirementId = "F-EVIDENCE",
                ModuleId = "phase_f.completion",
                DisplayName = definition.DisplayName,
                Category = GameSystemVerificationCategory.Coverage,
                ExecutionMode = GameSystemVerificationExecutionMode.StaticContract,
                Status = selection.Status,
                Required = true,
                Expected = $"{definition.RunInstruction}; report >= {cutoffUtc:O}; Result=PASSED",
                Actual = selection.Actual,
                Details = selection.Details
            });
        }
    }

    private static void AddCompletionRows(
        GameSystemVerificationReport report,
        IReadOnlyDictionary<EvidenceKind, EvidenceSelection> evidence,
        IReadOnlyList<GameSystemVerificationCaseResult> traceRows)
    {
        report.Results.Add(G01());
        report.Results.Add(Gate("G-02", "Timing Contract", E(EvidenceKind.Olaf), evidence,
            R("C-42","C-47"), C("system.contract.timing.authoring_five","system.contract.condition.canonical_layer"),
            "신규 Skill Authoring 5종 + Condition layer"));
        report.Results.Add(Gate("G-03", "Planning Economy", E(EvidenceKind.Olaf), evidence,
            R("C-02","C-03","C-04","C-45"),
            C("system.runtime.planning.preparation_immediate_commit","system.runtime.planning.attack_cost_no_refund","system.runtime.planning.prestige_slotless"),
            "도사림/위세 즉시, 공격 비용 계획 차감, 무환불, 위세 슬롯리스"));
        report.Results.Add(Gate("G-04", "Clash / Speed", E(EvidenceKind.Olaf), evidence,
            R("C-06","C-09"), C("system.runtime.speed.boundaries","system.runtime.pairing.exact_target_ignores_speed","phasef.regression.pairing.last_challenger","phaseb.c09.continuation"),
            "정확 TargetSlot 합, 속도 1~5/+1·6+/+2, 파괴/처치 후 남은 굴림 소멸"));
        report.Results.Add(Gate("G-05", "Damage / Part", E(EvidenceKind.Olaf, EvidenceKind.Yujin, EvidenceKind.Hifumi), evidence,
            R("C-10","C-12","C-13","C-23","C-25","O-03","Y-01","H-06"), null,
            "RED/BLUE, 최소1→방어도, Whole 이중차감, 약화 재타격, 명시적 파괴권한"));
        report.Results.Add(Gate("G-06", "Momentum / Fervor", E(EvidenceKind.Olaf, EvidenceKind.Hifumi), evidence,
            R("C-46","C-19","C-20","C-21"), null,
            "B reset/20·40 이동/시간축 분리/짓누름·짓눌림 보상/4·8·10"));
        report.Results.Add(Gate("G-07", "Prestige", E(EvidenceKind.Olaf), evidence,
            R("C-22","C-02","C-03"), C("phaseb.c22.prestige_events","system.runtime.planning.prestige_slotless"),
            "합 시작+1/교환+1/합승리+2/처치+5, 슬롯리스 계획 발동"));
        report.Results.Add(Gate("G-08", "Status", E(EvidenceKind.Olaf), evidence,
            R("C-27","C-48","C-49"), null,
            "힘/쇠약 전 굴림, 반대 스택 상쇄, 재생 dimensions, 미정 키워드 안전성"));
        report.Results.Add(Gate("G-09", "Emotion", E(EvidenceKind.Olaf, EvidenceKind.PhaseE), evidence,
            R("C-34","C-35","C-32"), null,
            "고정 3장 + 63장 + runtime/rulebreaker 연결"));
        report.Results.Add(Gate("G-10", "Upgrade", E(EvidenceKind.Olaf, EvidenceKind.PhaseE), evidence,
            R("C-36","C-37","C-38"), null,
            "2회 강화 schema + Level2가 0916 XLSX 만렙 결과에 도달"));
        report.Results.Add(Gate("G-11", "Run Economy", E(EvidenceKind.Olaf, EvidenceKind.PhaseE), evidence,
            R("C-39","C-33"), null,
            "아이템/상점/판매/획득 + n식/정비/스테이지 회복"));
        report.Results.Add(Gate("G-12", "Enemy Baseline", E(EvidenceKind.Olaf, EvidenceKind.PhaseE), evidence,
            R("C-43","C-31","C-40","C-41"), null,
            "일반몹 D8·1x3·HP135/101·내성 + 기준 보스 A/B"));
        report.Results.Add(Gate("G-13", "Olaf", E(EvidenceKind.Olaf, EvidenceKind.PhaseE), evidence,
            R("O-01","O-03","O-04","O-05","O-02"), null,
            "광기/출혈/공포/파괴/블로토/룰브레이커 + 0916 전체 풀"));
        report.Results.Add(Gate("G-14", "Yujin", E(EvidenceKind.Yujin, EvidenceKind.PhaseE), evidence,
            R("Y-01","Y-02","Y-03","Y-05","Y-07","Y-06"), null,
            "무기/표식/환형/살수의 감/지연 K·L + 0916 전체 풀"));
        report.Results.Add(Gate("G-15", "Hifumi", E(EvidenceKind.Hifumi, EvidenceKind.PhaseE), evidence,
            R("H-01","H-02","H-03","H-05","H-06","H-07","H-09","H-10","H-08"), null,
            "친치로/뼈/반격/골단/과감한 판단/거버너/포커페이스"));
        report.Results.Add(G16());

        bool tracePass = traceRows != null && traceRows.Count == 19 &&
                         traceRows.All(x => x != null && x.Status == GameSystemVerificationStatus.Pass);
        string traceDetails = traceRows == null
            ? "Trace rows missing"
            : string.Join("\n", traceRows.Where(x => x != null && x.Status != GameSystemVerificationStatus.Pass)
                .Select(x => $"{x.CaseId}: {x.Status} / {x.Actual}"));
        report.Results.Add(Result(
            "phasef.g17.traceability",
            "G-17",
            "Traceability",
            tracePass ? GameSystemVerificationStatus.Pass : ResolveAggregateStatus(traceRows),
            "§1~§19 각 행이 PASS이며 UNKNOWN/미해결 0",
            tracePass ? "19/19 trace rows PASS" : $"PASS {traceRows?.Count(x => x?.Status == GameSystemVerificationStatus.Pass) ?? 0}/19",
            string.IsNullOrWhiteSpace(traceDetails) ? null : traceDetails));
    }

    private static GameSystemVerificationCaseResult G01()
    {
        Dictionary<SkillEffectTiming, int> serialized = new()
        {
            { SkillEffectTiming.OnExecute, 0 }, { SkillEffectTiming.OnClashWin, 1 },
            { SkillEffectTiming.OnClashLose, 2 }, { SkillEffectTiming.AfterDamage, 3 },
            { SkillEffectTiming.OnCritical, 4 }, { SkillEffectTiming.OnKill, 5 },
            { SkillEffectTiming.OnActionEnd, 6 }, { SkillEffectTiming.OnExchangeWin, 7 },
            { SkillEffectTiming.OnExchangeLose, 8 }, { SkillEffectTiming.OnOneSideHit, 9 },
            { SkillEffectTiming.OnClashDraw, 10 }, { SkillEffectTiming.OnMultiRollPenaltyStart, 11 },
            { SkillEffectTiming.OnMultiRollPenaltyAfterRoll, 12 }, { SkillEffectTiming.OnMultiRollPenaltyEnd, 13 },
            { SkillEffectTiming.OnRollWin, 14 }, { SkillEffectTiming.OnRollLose, 15 },
            { SkillEffectTiming.OnBattleStart, 16 }, { SkillEffectTiming.OnTurnStart, 17 },
            { SkillEffectTiming.BeforeUse, 18 }, { SkillEffectTiming.OnClashStart, 19 },
            { SkillEffectTiming.OnOneSidedStart, 20 }, { SkillEffectTiming.BeforeAttack, 21 },
            { SkillEffectTiming.OnRollStart, 22 }, { SkillEffectTiming.OnRollSuccess, 23 },
            { SkillEffectTiming.OnRollFailure, 24 }, { SkillEffectTiming.OnHit, 25 },
            { SkillEffectTiming.OnRollEnd, 26 }, { SkillEffectTiming.OnAttackEnd, 27 },
            { SkillEffectTiming.OnSkillEnd, 28 }, { SkillEffectTiming.OnTurnEnd, 29 },
            { SkillEffectTiming.OnBattleEnd, 30 }, { SkillEffectTiming.OnDuelMatched, 31 },
            { SkillEffectTiming.OnPartWeakened, 32 }, { SkillEffectTiming.OnPartBroken, 33 },
            { SkillEffectTiming.OnClashEnd, 34 }
        };

        List<string> bad = serialized
            .Where(pair => (int)pair.Key != pair.Value)
            .Select(pair => $"{pair.Key}={(int)pair.Key}, expected={pair.Value}")
            .ToList();

        bool pass = bad.Count == 0;
        return Result(
            "phasef.g01.compile_serialization",
            "G-01",
            "Compile / Serialization",
            pass ? GameSystemVerificationStatus.Pass : GameSystemVerificationStatus.Fail,
            "현재 Editor domain compile 성공 + SkillEffectTiming serialized ID 0~34 보존",
            pass ? $"Editor domain loaded; serialized timings {serialized.Count}/{serialized.Count} stable" : $"Mismatch={bad.Count}",
            bad.Count == 0 ? null : string.Join("\n", bad));
    }

    private static GameSystemVerificationCaseResult G16()
    {
        PhaseEContentManifest manifest = LoadPhaseEManifest();

        List<string> failures = new();
        if (manifest == null)
        {
            failures.Add("PhaseEContentManifest missing");
        }
        else
        {
            if (!manifest.TempBalanceActive || manifest.ActiveBalanceProfile != PhaseEContentManifest.TempBalanceProfileId)
                failures.Add($"BalanceProfile={manifest.ActiveBalanceProfile}, active={manifest.TempBalanceActive}");
            if (manifest.PendingReasons != null && manifest.PendingReasons.Count > 0)
                failures.Add("PendingReasons=" + string.Join(" | ", manifest.PendingReasons));
            if (manifest.TempBalanceNotes == null || manifest.TempBalanceNotes.Count == 0)
                failures.Add("TEMP balance notes missing");
        }

        AuditTempAssets<TempBalanceRunItem>(failures, path => path.IndexOf("/TEMP_BALANCE_V1/", StringComparison.Ordinal) >= 0);
        AuditTempAssets<TempBalanceSkillProxyEffectDefinition>(failures, path => path.IndexOf("/TEMP_BALANCE_V1/", StringComparison.Ordinal) >= 0);
        AuditTempAssets<TempBalanceEmotionProxyEffectDefinition>(failures,
            path => path.IndexOf("TEMP_BALANCE_V1", StringComparison.Ordinal) >= 0);

        bool pass = failures.Count == 0;
        return Result(
            "phasef.g16.tbd_safety",
            "G-16",
            "미정 안전성 / TEMP profile 추적",
            pass ? GameSystemVerificationStatus.Pass : GameSystemVerificationStatus.Fail,
            "미정값은 Unset이거나 TEMP_BALANCE_V1로 명시 추적되고 정식 canonical 값으로 위장되지 않음",
            pass
                ? $"Profile={manifest?.ActiveBalanceProfile}; TEMP assets tracked; PendingReasons=0"
                : $"Violation={failures.Count}",
            pass ? null : string.Join("\n", failures));
    }

    private static void AuditTempAssets<T>(List<string> failures, Func<string, bool> pathPredicate)
        where T : UnityEngine.Object
    {
        string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrWhiteSpace(path) || !pathPredicate(path))
                failures.Add($"Untracked TEMP asset type={typeof(T).Name}: {path}");
        }
    }

    private static GameSystemVerificationCaseResult EvaluateTrace(
        TraceDefinition trace,
        IReadOnlyDictionary<EvidenceKind, EvidenceSelection> evidence)
    {
        Evaluation evaluation = Evaluate(trace.Evidence, evidence, trace.Requirements, trace.RequiredCaseIds);
        return new GameSystemVerificationCaseResult
        {
            CaseId = $"phasef.trace.{trace.Section:00}",
            RequirementId = $"TRACE-{trace.Section:00}",
            ModuleId = "phase_f.traceability",
            DisplayName = $"§{trace.Section} {trace.Name}",
            Category = GameSystemVerificationCategory.Coverage,
            ExecutionMode = GameSystemVerificationExecutionMode.StaticContract,
            Status = evaluation.Status,
            Required = true,
            Expected = $"§{trace.Section}의 연결 Requirement/Regression evidence 전부 PASS",
            Actual = evaluation.Actual,
            Details = evaluation.Details
        };
    }

    private static GameSystemVerificationCaseResult Gate(
        string gateId,
        string name,
        EvidenceKind[] kinds,
        IReadOnlyDictionary<EvidenceKind, EvidenceSelection> evidence,
        string[] requirements,
        string[] caseIds,
        string expected)
    {
        Evaluation evaluation = Evaluate(kinds, evidence, requirements, caseIds);
        string normalized = gateId.Replace("G-", "g").ToLowerInvariant();
        return Result(
            $"phasef.{normalized}.{SanitizeId(name)}",
            gateId,
            name,
            evaluation.Status,
            expected,
            evaluation.Actual,
            evaluation.Details);
    }

    private sealed class Evaluation
    {
        public GameSystemVerificationStatus Status;
        public string Actual;
        public string Details;
    }

    private static Evaluation Evaluate(
        EvidenceKind[] kinds,
        IReadOnlyDictionary<EvidenceKind, EvidenceSelection> evidence,
        string[] requirements,
        string[] caseIds)
    {
        kinds ??= Array.Empty<EvidenceKind>();
        requirements ??= Array.Empty<string>();
        caseIds ??= Array.Empty<string>();

        List<EvidenceSelection> selected = kinds
            .Distinct()
            .Select(kind => evidence[kind])
            .ToList();

        EvidenceSelection failedEvidence = selected.FirstOrDefault(x => x.Status == GameSystemVerificationStatus.Fail || x.Status == GameSystemVerificationStatus.Error);
        if (failedEvidence != null)
        {
            // 파생 Trace/Gate가 같은 원인 하나를 수십 개의 독립 FAIL처럼 증폭하지 않게 한다.
            // 실제 원인은 phasef.evidence.* 행에 FAIL로 남고, 의존 행은 BLOCKED/PENDING이다.
            return new Evaluation
            {
                Status = GameSystemVerificationStatus.Pending,
                Actual = $"BLOCKED by failed evidence: {failedEvidence.Definition.DisplayName}",
                Details = failedEvidence.Details ?? failedEvidence.Actual
            };
        }

        List<EvidenceSelection> pendingEvidence = selected
            .Where(x => x.Status != GameSystemVerificationStatus.Pass)
            .ToList();
        if (pendingEvidence.Count > 0)
        {
            return new Evaluation
            {
                Status = GameSystemVerificationStatus.Pending,
                Actual = "Fresh evidence required: " + string.Join(", ", pendingEvidence.Select(x => x.Definition.Key)),
                Details = string.Join("\n", pendingEvidence.Select(x => $"- {x.Definition.RunInstruction}: {x.Actual}"))
            };
        }

        List<GameSystemVerificationCaseResult> rows = selected
            .SelectMany(x => x.Loaded.Report.Results ?? new List<GameSystemVerificationCaseResult>())
            .Where(x => x != null)
            .ToList();

        List<string> failures = new();
        foreach (string requirement in requirements.Distinct(StringComparer.Ordinal))
        {
            List<GameSystemVerificationCaseResult> matches = rows
                .Where(x => string.Equals(x.RequirementId, requirement, StringComparison.Ordinal) && x.Required)
                .ToList();

            if (matches.Count == 0)
            {
                failures.Add($"{requirement}: required evidence case missing");
                continue;
            }

            List<GameSystemVerificationCaseResult> bad = matches
                .Where(x => x.Status != GameSystemVerificationStatus.Pass)
                .ToList();
            if (bad.Count > 0)
                failures.Add($"{requirement}: " + string.Join(", ", bad.Select(x => $"{x.CaseId}={x.Status}")));
        }

        foreach (string caseId in caseIds.Distinct(StringComparer.Ordinal))
        {
            List<GameSystemVerificationCaseResult> matches = rows
                .Where(x => string.Equals(x.CaseId, caseId, StringComparison.Ordinal))
                .ToList();

            if (matches.Count == 0)
            {
                failures.Add($"{caseId}: case missing");
                continue;
            }

            // Optional Phase A probes are Phase F에서는 더 이상 optional이 아니다.
            // 세 캐릭터 증거 중 최소 하나에서 실제 PASS가 있어야 완료로 인정한다.
            if (!matches.Any(x => x.Status == GameSystemVerificationStatus.Pass))
                failures.Add($"{caseId}: no PASS ({string.Join(", ", matches.Select(x => x.Status))})");
        }

        if (failures.Count > 0)
        {
            return new Evaluation
            {
                Status = GameSystemVerificationStatus.Fail,
                Actual = $"Evidence mismatch {failures.Count}",
                Details = string.Join("\n", failures)
            };
        }

        return new Evaluation
        {
            Status = GameSystemVerificationStatus.Pass,
            Actual = $"Requirements={requirements.Distinct().Count()}, Cases={caseIds.Distinct().Count()} / PASS",
            Details = null
        };
    }

    private static Dictionary<EvidenceKind, EvidenceSelection> SelectEvidence(
        IReadOnlyList<LoadedReport> reports,
        DateTime sourceCutoffUtc)
    {
        Dictionary<EvidenceKind, EvidenceSelection> result = new();

        foreach (EvidenceDefinition definition in EvidenceDefinitions)
        {
            List<LoadedReport> matching = reports
                .Where(x => HasCase(x.Report, definition.SignatureCaseId))
                .Where(x => string.Equals(x.Report.SpecId, CanonicalGameSystemVerificationSpec.SpecId, StringComparison.Ordinal))
                .OrderByDescending(x => x.FinishedUtc)
                .ToList();

            LoadedReport latest = matching.FirstOrDefault();
            if (latest == null)
            {
                result[definition.Kind] = new EvidenceSelection
                {
                    Definition = definition,
                    Status = GameSystemVerificationStatus.Pending,
                    Actual = "No compatible report",
                    Details = definition.RunInstruction
                };
                continue;
            }

            bool fresh = latest.FinishedUtc >= sourceCutoffUtc.AddSeconds(-2);
            if (!fresh)
            {
                result[definition.Kind] = new EvidenceSelection
                {
                    Definition = definition,
                    Loaded = latest,
                    Status = GameSystemVerificationStatus.Pending,
                    Actual = $"STALE report {latest.FinishedUtc:O}",
                    Details = $"Project verification source cutoff={sourceCutoffUtc:O}\n{definition.RunInstruction}\nReport={latest.Path}"
                };
                continue;
            }

            latest.Report.RecalculateCounts();
            bool signaturePass = latest.Report.Results != null && latest.Report.Results.Any(x =>
                x != null &&
                string.Equals(x.CaseId, definition.SignatureCaseId, StringComparison.Ordinal) &&
                x.Status == GameSystemVerificationStatus.Pass);
            bool livePass = definition.Kind == EvidenceKind.PhaseE ||
                            (latest.Report.Results != null && latest.Report.Results.Any(x =>
                                x != null && x.CaseId == "system.live.manager_initialized" &&
                                x.Status == GameSystemVerificationStatus.Pass));

            bool pass = latest.Report.Succeeded && signaturePass && livePass;
            result[definition.Kind] = new EvidenceSelection
            {
                Definition = definition,
                Loaded = latest,
                Status = pass ? GameSystemVerificationStatus.Pass : GameSystemVerificationStatus.Fail,
                Actual = pass
                    ? $"PASS {latest.Report.PassCount} / {latest.FinishedUtc:O}"
                    : $"NOT CLOSED: PASS {latest.Report.PassCount}, FAIL {latest.Report.FailCount}, SKIP {latest.Report.SkipCount}, PENDING {latest.Report.PendingCount}, ERROR {latest.Report.ErrorCount}",
                Details = $"Report={latest.Path}\nPlayer={latest.Report.PlayerName}\nSignaturePass={signaturePass}, LivePass={livePass}"
            };
        }

        return result;
    }

    private static List<LoadedReport> LoadReports()
    {
        List<LoadedReport> result = new();
        string root = GameSystemVerificationReportWriter.RootDirectory;
        if (!Directory.Exists(root))
            return result;

        foreach (string path in Directory.EnumerateFiles(root, "report.json", SearchOption.AllDirectories))
        {
            try
            {
                string json = File.ReadAllText(path);
                GameSystemVerificationReport report = JsonUtility.FromJson<GameSystemVerificationReport>(json);
                if (report == null || report.Results == null)
                    continue;

                if (!TryParseUtc(report.FinishedAt, out DateTime finishedUtc))
                    finishedUtc = File.GetLastWriteTimeUtc(path);

                result.Add(new LoadedReport
                {
                    Path = path,
                    Report = report,
                    FinishedUtc = finishedUtc
                });
            }
            catch
            {
                // 깨진 과거 report 하나 때문에 Phase F scanner 자체를 중단하지 않는다.
            }
        }

        return result;
    }

    private static DateTime GetSourceCutoffUtc()
    {
        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string[] relativeRoots =
        {
            "Assets/1. Scripts",
            "Assets/2. Data/Progression/PhaseE",
            "Assets/2. Data/Progression/EmotionAugments",
            "Assets/2. Data/Progression/RunItems",
            "Assets/2. Data/Characters/Design2026",
            "Assets/2. Data/TestEncounters"
        };

        DateTime latest = DateTime.MinValue;
        foreach (string relative in relativeRoots)
        {
            string root = Path.Combine(projectRoot, relative.Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(root))
                continue;

            foreach (string file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            {
                string extension = Path.GetExtension(file);
                if (!string.Equals(extension, ".cs", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(extension, ".asset", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(extension, ".asmdef", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                DateTime time = File.GetLastWriteTimeUtc(file);
                if (time > latest)
                    latest = time;
            }
        }

        return latest == DateTime.MinValue ? DateTime.UtcNow.AddYears(-1) : latest;
    }

    private static PhaseEContentManifest LoadPhaseEManifest()
    {
        PhaseEContentManifest manifest = AssetDatabase.LoadAssetAtPath<PhaseEContentManifest>(
            PhaseEContentMigration.ManifestPath);
        if (manifest != null)
            return manifest;

        // 일부 Play/Edit 전환 직후 AssetDatabase typed load가 null을 반환한 사례를 방어한다.
        // Phase F는 Edit Mode 실행이 정식 경로지만, canonical path가 일시적으로 해석되지 않아도
        // 실제 manifest asset을 type search로 재확인한다.
        string[] guids = AssetDatabase.FindAssets("t:PhaseEContentManifest");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            PhaseEContentManifest candidate =
                AssetDatabase.LoadAssetAtPath<PhaseEContentManifest>(path);
            if (candidate != null)
                return candidate;
        }

        return null;
    }

    private static string GetBalanceProfile()
    {
        PhaseEContentManifest manifest = LoadPhaseEManifest();
        return string.IsNullOrWhiteSpace(manifest?.ActiveBalanceProfile)
            ? "NO_BALANCE_PROFILE"
            : manifest.ActiveBalanceProfile;
    }

    private static bool HasCase(GameSystemVerificationReport report, string caseId) =>
        report?.Results != null && report.Results.Any(x => x != null && x.CaseId == caseId);

    private static bool TryParseUtc(string value, out DateTime utc)
    {
        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime parsed))
        {
            utc = parsed.Kind == DateTimeKind.Utc ? parsed : parsed.ToUniversalTime();
            return true;
        }

        utc = default;
        return false;
    }

    private static GameSystemVerificationStatus ResolveAggregateStatus(
        IEnumerable<GameSystemVerificationCaseResult> rows)
    {
        if (rows == null)
            return GameSystemVerificationStatus.Pending;
        if (rows.Any(x => x != null && (x.Status == GameSystemVerificationStatus.Fail || x.Status == GameSystemVerificationStatus.Error)))
            return GameSystemVerificationStatus.Fail;
        if (rows.Any(x => x != null && x.Status != GameSystemVerificationStatus.Pass))
            return GameSystemVerificationStatus.Pending;
        return GameSystemVerificationStatus.Pass;
    }

    private static GameSystemVerificationCaseResult Result(
        string caseId,
        string requirementId,
        string name,
        GameSystemVerificationStatus status,
        string expected,
        string actual,
        string details = null) =>
        new()
        {
            CaseId = caseId,
            RequirementId = requirementId,
            ModuleId = "phase_f.completion",
            DisplayName = name,
            Category = GameSystemVerificationCategory.Phase,
            ExecutionMode = GameSystemVerificationExecutionMode.StaticContract,
            Status = status,
            Required = true,
            Expected = expected,
            Actual = actual,
            Details = details
        };

    private static string SanitizeId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "gate";
        return new string(value.ToLowerInvariant()
            .Select(c => char.IsLetterOrDigit(c) ? c : '_')
            .ToArray()).Trim('_');
    }

    private static TraceDefinition T(
        int section,
        string name,
        EvidenceKind[] evidence,
        string[] requirements,
        string[] cases = null) =>
        new()
        {
            Section = section,
            Name = name,
            Evidence = evidence ?? Array.Empty<EvidenceKind>(),
            Requirements = requirements ?? Array.Empty<string>(),
            RequiredCaseIds = cases ?? Array.Empty<string>()
        };

    private static EvidenceKind[] E(params EvidenceKind[] kinds) => kinds;
    private static string[] R(params string[] requirements) => requirements;
    private static string[] C(params string[] cases) => cases;
}
#endif
