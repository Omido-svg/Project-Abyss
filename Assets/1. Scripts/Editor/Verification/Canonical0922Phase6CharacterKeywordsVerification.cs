#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class Canonical0922Phase6CharacterKeywordsVerification
{
    private const string Phase5ReportPath =
        "Logs/GameSystemVerification/0922_Phase5_FactoryAuthoring.md";

    private sealed class Check
    {
        public string Id;
        public string Name;
        public bool Passed;
        public string Actual;
    }

    [MenuItem("Game System Verification/0922 Canonical/Phase 6 - Verify Olaf and Yujin Keywords")]
    public static void VerifyFromMenu()
    {
        List<Check> checks = new();

        AddPrerequisites(checks);
        AddOlafChecks(checks);
        AddYujinChecks(checks);
        AddWiringChecks(checks);

        int pass = checks.Count(x => x.Passed);
        int fail = checks.Count - pass;

        string result =
            fail == 0
                ? "PASS_CHARACTER_KEYWORDS"
                : "FAIL";

        WriteReport(
            checks,
            pass,
            fail,
            result);

        string summary =
            "[0922 Phase 6 · Character Keywords: Olaf / Yujin]\n" +
            $"PASS={pass} FAIL={fail}\n\n" +
            "PASS\n- " +
            string.Join(
                "\n- ",
                checks.Where(x => x.Passed)
                    .Select(x => $"{x.Id} | {x.Name} | {x.Actual}")) +
            "\n\nFAIL\n- " +
            (fail == 0
                ? "NONE"
                : string.Join(
                    "\n- ",
                    checks.Where(x => !x.Passed)
                        .Select(x => $"{x.Id} | {x.Name} | {x.Actual}"))) +
            $"\n\nPHASE6_RESULT={result}";

        if (fail == 0)
            Debug.Log(summary);
        else
            Debug.LogError(summary);
    }

    private static void AddPrerequisites(
        List<Check> checks)
    {
        Add(
            checks,
            "P6-H00",
            "0922 canonical identity",
            Canonical0922BaselineSpec.CanonicalVersion == "0922",
            $"Canonical={Canonical0922BaselineSpec.CanonicalVersion}");

        string root =
            Path.GetDirectoryName(
                Application.dataPath) ??
            string.Empty;

        string path =
            Path.Combine(
                root,
                Phase5ReportPath);

        bool exists =
            File.Exists(path);

        string report =
            exists
                ? File.ReadAllText(path)
                : string.Empty;

        bool pass =
            report.Contains(
                "RESULT: PASS_FACTORY_AUTHORING",
                StringComparison.Ordinal) ||
            report.Contains(
                "PHASE5_RESULT=PASS_FACTORY_AUTHORING",
                StringComparison.Ordinal);

        Add(
            checks,
            "P6-H01",
            "Phase 5 factory/authoring closed",
            exists && pass,
            !exists
                ? "Phase5 report missing"
                : $"ReportPresent=True, ResultPass={pass}");
    }

    private static void AddOlafChecks(
        List<Check> checks)
    {
        GameObject sourceGo = null;
        GameObject targetGo = null;

        try
        {
            sourceGo =
                new GameObject(
                    "__0922_PHASE6_OLAF_SOURCE__");

            targetGo =
                new GameObject(
                    "__0922_PHASE6_OLAF_TARGET__");

            NormalEnemy source =
                sourceGo.AddComponent<NormalEnemy>();

            NormalEnemy target =
                targetGo.AddComponent<NormalEnemy>();

            CharacterStatusController controller =
                InstallStatusController(
                    target);

            controller.AddStatus(
                new OlafFearStatus(5),
                source);

            controller.AddStatus(
                new OlafFearStatus(2),
                source);

            OlafFearStatus fear =
                controller.CharacterStatuses
                    .OfType<OlafFearStatus>()
                    .FirstOrDefault();

            int fearCount =
                controller.CharacterStatuses
                    .Count(x => x is OlafFearStatus);

            int fearPenalty =
                CommonStatusAlgebra.GetFearRollPenalty(
                    controller.CharacterStatuses);

            OlafFearStatus blotoFear =
                new OlafFearStatus();

            bool fearPass =
                fear != null &&
                fearCount == 1 &&
                fear.Duration == 5 &&
                fear.NumericValue == 0 &&
                fear.StorageKind == StatusEffectStorageKind.PresenceTimed &&
                fearPenalty == OlafFearStatus.RollPenalty &&
                blotoFear.Duration == OlafFearStatus.DefaultDuration &&
                OlafFearStatus.DefaultDuration == 3;

            Add(
                checks,
                "P6-O01",
                "Fear = fixed -1 / no stacking / max refresh / Bloto T3",
                fearPass,
                $"Count={fearCount}, T={fear?.Duration}, Numeric={fear?.NumericValue}, " +
                $"Penalty={fearPenalty}, BlotoT={blotoFear.Duration}");

            controller.ClearAll();

            controller.AddStatus(
                new Bleeding(2),
                source);

            controller.AddStatus(
                new Bleeding(3),
                source);

            Bleeding bleeding =
                controller.CharacterStatuses
                    .OfType<Bleeding>()
                    .FirstOrDefault();

            int bleedingCount =
                controller.CharacterStatuses
                    .Count(x => x is Bleeding);

            int stackBefore =
                bleeding?.Stack ??
                0;

            StatusEffectTickContext tick =
                bleeding == null
                    ? null
                    : new StatusEffectTickContext(
                        target,
                        null,
                        bleeding,
                        StatusEffectTickTiming.TurnEnd);

            bleeding?.OnTurnEnd(tick);

            bool tickPass =
                bleeding != null &&
                bleedingCount == 1 &&
                stackBefore == 5 &&
                tick?.RequestedDamage == 5 &&
                bleeding.Stack == 4 &&
                bleeding.Duration == StatusEffect.InfiniteDuration &&
                bleeding.StorageKind == StatusEffectStorageKind.Bespoke;

            Add(
                checks,
                "P6-O02",
                "Bleeding = bespoke additive stack / TurnEnd damage / decrement",
                tickPass,
                $"Count={bleedingCount}, Stack={stackBefore}->{bleeding?.Stack}, " +
                $"RequestedDamage={tick?.RequestedDamage}, Duration={bleeding?.Duration}");

            int consumed =
                bleeding?.ConsumeAll() ??
                0;

            bool consumePass =
                bleeding != null &&
                consumed == 4 &&
                bleeding.Stack == 0 &&
                !bleeding.CanExplode;

            Add(
                checks,
                "P6-O03",
                "Bleeding consume-all contract preserves exact current stack",
                consumePass,
                $"Consumed={consumed}, Remaining={bleeding?.Stack}, CanExplode={bleeding?.CanExplode}");

            controller.ClearAll();

            Bleeding one =
                new Bleeding(1);

            controller.AddStatus(
                one,
                source);

            StatusEffectTickContext lastTick =
                new StatusEffectTickContext(
                    target,
                    null,
                    one,
                    StatusEffectTickTiming.TurnEnd);

            one.OnTurnEnd(lastTick);

            bool zeroRemoved =
                one.Stack == 0 &&
                !controller.CharacterStatuses.Contains(one) &&
                lastTick.RequestedDamage == 1;

            Add(
                checks,
                "P6-O04",
                "Bleeding zero stack removes the keyword",
                zeroRemoved,
                $"Stack={one.Stack}, StillStored={controller.CharacterStatuses.Contains(one)}, Damage={lastTick.RequestedDamage}");
        }
        catch (Exception exception)
        {
            Add(
                checks,
                "P6-OXX",
                "Olaf keyword runtime probe completed without exception",
                false,
                exception.GetType().Name +
                ": " +
                exception.Message);
        }
        finally
        {
            if (sourceGo != null)
                UnityEngine.Object.DestroyImmediate(sourceGo);

            if (targetGo != null)
                UnityEngine.Object.DestroyImmediate(targetGo);
        }
    }

    private static void AddYujinChecks(
        List<Check> checks)
    {
        GameObject ownerGo = null;
        GameObject targetGo = null;
        BattleContext context = null;
        YujinMechanic mechanic = null;

        try
        {
            ownerGo =
                new GameObject(
                    "__0922_PHASE6_YUJIN_OWNER__");

            targetGo =
                new GameObject(
                    "__0922_PHASE6_YUJIN_TARGET__");

            NormalEnemy owner =
                ownerGo.AddComponent<NormalEnemy>();

            NormalEnemy target =
                targetGo.AddComponent<NormalEnemy>();

            CharacterStatusController targetStatuses =
                InstallStatusController(
                    target);

            context =
                new BattleContext
                {
                    Player = owner,
                    SuppressPresentation = true
                };

            context.Enemies.Add(
                target);

            mechanic =
                new YujinMechanic(
                    YujinWeaponType.Baeku);

            mechanic.Initialize(
                owner,
                context);

            bool registered =
                mechanic.TryRegister();

            targetStatuses.AddStatus(
                new YujinDesignationStatus(
                    2,
                    3),
                owner);

            int designationCount1 =
                targetStatuses.CharacterStatuses
                    .Count(x => x is YujinDesignationStatus);

            int designationTotal1 =
                mechanic.GetDesignationTotal(
                    target,
                    null);

            YujinMarkGainResult first =
                mechanic.ApplyMarkGain(
                    target,
                    null,
                    5);

            targetStatuses.AddStatus(
                new YujinDesignationStatus(
                    3,
                    1),
                owner);

            int designationCount2 =
                targetStatuses.CharacterStatuses
                    .Count(x => x is YujinDesignationStatus);

            int designationTotal2 =
                mechanic.GetDesignationTotal(
                    target,
                    null);

            YujinMarkGainResult second =
                mechanic.ApplyMarkGain(
                    target,
                    null,
                    5);

            bool designationPass =
                registered &&
                designationCount1 == 1 &&
                designationTotal1 == 2 &&
                first.Applied &&
                !first.Ignited &&
                first.DesignationBonus == 2 &&
                first.ActualGain == 7 &&
                first.MarkAfter == 7 &&
                designationCount2 == 2 &&
                designationTotal2 == 5 &&
                second.DesignationBonus == 5 &&
                second.ActualGain == 10 &&
                second.MarkAfter == 17;

            Add(
                checks,
                "P6-Y01",
                "Designation N/T entries stay independent and current total N feeds Mark",
                designationPass,
                $"Registered={registered}, Entries={designationCount1}->{designationCount2}, " +
                $"Total={designationTotal1}->{designationTotal2}, Mark={first.MarkAfter}->{second.MarkAfter}, " +
                $"Bonus={first.DesignationBonus}/{second.DesignationBonus}");

            long trap =
                mechanic.RegisterMarkTrap(
                    target,
                    null,
                    20);

            long deadline =
                mechanic.RegisterMarkDeadline(
                    target,
                    null,
                    3,
                    2);

            int ridersBefore =
                mechanic.PendingDelayedTriggerCount;

            YujinMarkGainResult forced =
                mechanic.ForceMarkIgnition(
                    target,
                    null);

            int markAfterIgnition =
                mechanic.GetMark(
                    target,
                    null);

            bool atomicPass =
                trap > 0 &&
                deadline > 0 &&
                ridersBefore == 2 &&
                forced.Applied &&
                forced.Ignited &&
                forced.PreviousMark == 17 &&
                forced.DesignationBonus == 5 &&
                markAfterIgnition == 0 &&
                mechanic.PendingDelayedTriggerCount == 0;

            Add(
                checks,
                "P6-Y02",
                "Mark atomic flow = designation -> 44 ignition -> overflow discard -> K/L riders",
                atomicPass,
                $"Prev={forced.PreviousMark}, Base={forced.BaseAmount}, Designation={forced.DesignationBonus}, " +
                $"Gain={forced.ActualGain}, Ignited={forced.Ignited}, MarkAfter={markAfterIgnition}, " +
                $"Riders={ridersBefore}->{mechanic.PendingDelayedTriggerCount}");

            bool queued =
                mechanic.QueueMarkForNextTurn(
                    target,
                    null,
                    5);

            int pendingBefore =
                mechanic.PendingMarkCount;

            context._battleEvent.RaiseTurnStart(1);

            int delayedMark =
                mechanic.GetMark(
                    target,
                    null);

            bool delayedPass =
                queued &&
                pendingBefore == 1 &&
                mechanic.PendingMarkCount == 0 &&
                delayedMark == 10;

            Add(
                checks,
                "P6-Y03",
                "Delayed Mark materializes through the same designation-aware atomic path",
                delayedPass,
                $"Queued={queued}, Pending={pendingBefore}->{mechanic.PendingMarkCount}, Mark={delayedMark}");

            CombatStatusAnchor singleHpAnchor =
                CombatStatusAnchor.Resolve(
                    target,
                    null);

            bool singleHpPass =
                target.BodyParts != null &&
                target.BodyParts.Count == 0 &&
                singleHpAnchor.IsValid &&
                !singleHpAnchor.IsPartAnchor &&
                mechanic.GetMark(target, null) == delayedMark &&
                mechanic.GetDesignationTotal(target, null) == 5;

            Add(
                checks,
                "P6-Y04",
                "SingleHP enemy uses character anchor for Mark and Designation",
                singleHpPass,
                $"Parts={target.BodyParts?.Count ?? -1}, IsPartAnchor={singleHpAnchor.IsPartAnchor}, " +
                $"Mark={mechanic.GetMark(target, null)}, Designation={mechanic.GetDesignationTotal(target, null)}");
        }
        catch (Exception exception)
        {
            Add(
                checks,
                "P6-YXX",
                "Yujin keyword runtime probe completed without exception",
                false,
                exception.GetType().Name +
                ": " +
                exception.Message);
        }
        finally
        {
            try
            {
                mechanic?.Unregister();
                mechanic?.Release();
                context?._battleEvent?.Dispose();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }

            if (ownerGo != null)
                UnityEngine.Object.DestroyImmediate(ownerGo);

            if (targetGo != null)
                UnityEngine.Object.DestroyImmediate(targetGo);
        }
    }

    private static void AddWiringChecks(
        List<Check> checks)
    {
        string yujin =
            ReadSource(
                "Assets/1. Scripts/Runtime/Characters/Yujin/Mechanics/YujinMechanic.cs");

        bool noFixedTwo =
            !yujin.Contains(
                "amount += 2",
                StringComparison.Ordinal) &&
            yujin.Contains(
                "GetDesignationTotal",
                StringComparison.Ordinal) &&
            yujin.Contains(
                "ApplyMarkGain",
                StringComparison.Ordinal);

        Add(
            checks,
            "P6-W01",
            "Yujin Mark no longer uses boolean designation +2",
            noFixedTwo,
            noFixedTwo
                ? "Designation total + atomic ApplyMarkGain detected"
                : "Legacy boolean +2 or missing atomic path detected");

        string oEffect =
            ReadSource(
                "Assets/1. Scripts/Runtime/Skills/Effects/Canonical0917/Yujin0917SkillEffectDefinition.cs");

        bool oCentralized =
            oEffect.Contains(
                "ForceMarkIgnition(",
                StringComparison.Ordinal) &&
            !oEffect.Contains(
                "MarkIgnitionThreshold -",
                StringComparison.Ordinal);

        Add(
            checks,
            "P6-W02",
            "Yujin O delegates to canonical Mark ignition path",
            oCentralized,
            oCentralized
                ? "O -> YujinMechanic.ForceMarkIgnition"
                : "O still duplicates threshold/ignition logic");

        string olaf =
            ReadSource(
                "Assets/1. Scripts/Runtime/Characters/Olaf/Mechanics/OlafMadnessMechanic.cs");

        bool fearRollback =
            olaf.Contains(
                "HadFear",
                StringComparison.Ordinal) &&
            olaf.Contains(
                "StatusEffect.InfiniteDuration",
                StringComparison.Ordinal) &&
            olaf.Contains(
                "OlafFearStatus.DefaultDuration",
                StringComparison.Ordinal);

        Add(
            checks,
            "P6-W03",
            "Bloto Fear rollback preserves finite/infinite pre-existing presence",
            fearRollback,
            fearRollback
                ? "HadFear + exact duration snapshot detected"
                : "Fear rollback still loses presence/infinite state");

        string bleeding =
            ReadSource(
                "Assets/1. Scripts/Runtime/Status/DamageOverTime/Bleeding.cs");

        bool bleedingRemoval =
            bleeding.Contains(
                "CurrentTurnEndDamage",
                StringComparison.Ordinal) &&
            bleeding.Contains(
                "Stack = Mathf.Max(0, Stack - 1);",
                StringComparison.Ordinal) &&
            bleeding.Contains(
                "if (Stack <= 0)",
                StringComparison.Ordinal) &&
            bleeding.Contains(
                "RemoveStatus();",
                StringComparison.Ordinal);

        Add(
            checks,
            "P6-W04",
            "Bleeding TurnEnd zero-removal wiring is canonical",
            bleedingRemoval,
            bleedingRemoval
                ? "Damage=current stack -> -1 -> zero remove"
                : "Bleeding TurnEnd wiring missing");
    }

    private static CharacterStatusController InstallStatusController(
        Character character)
    {
        if (character == null)
            throw new ArgumentNullException(nameof(character));

        FieldInfo field =
            typeof(Character).GetField(
                "statusController",
                BindingFlags.Instance |
                BindingFlags.NonPublic);

        if (field == null)
        {
            throw new MissingFieldException(
                typeof(Character).FullName,
                "statusController");
        }

        CharacterStatusController controller =
            new CharacterStatusController(
                character);

        field.SetValue(
            character,
            controller);

        return controller;
    }

    private static string ReadSource(
        string relative)
    {
        string root =
            Path.GetDirectoryName(
                Application.dataPath) ??
            string.Empty;

        string path =
            Path.Combine(
                root,
                relative.Replace(
                    '/',
                    Path.DirectorySeparatorChar));

        return File.Exists(path)
            ? File.ReadAllText(path)
            : string.Empty;
    }

    private static void Add(
        List<Check> checks,
        string id,
        string name,
        bool passed,
        string actual)
    {
        checks.Add(
            new Check
            {
                Id = id,
                Name = name,
                Passed = passed,
                Actual = actual
            });
    }

    private static void WriteReport(
        IReadOnlyList<Check> checks,
        int pass,
        int fail,
        string result)
    {
        string root =
            Path.GetDirectoryName(
                Application.dataPath) ??
            string.Empty;

        string directory =
            Path.Combine(
                root,
                "Logs",
                "GameSystemVerification");

        Directory.CreateDirectory(
            directory);

        string path =
            Path.Combine(
                directory,
                "0922_Phase6_CharacterKeywords.md");

        List<string> lines = new()
        {
            "# 0922 Phase 6 — Character Keywords: Olaf / Yujin",
            string.Empty,
            $"- PASS: {pass}",
            $"- FAIL: {fail}",
            $"- RESULT: {result}",
            string.Empty,
            "## Checks",
            string.Empty
        };

        foreach (Check check in checks)
        {
            lines.Add(
                $"- [{(check.Passed ? "x" : " ")}] " +
                $"`{check.Id}` {check.Name} — {check.Actual}");
        }

        File.WriteAllLines(
            path,
            lines);

        Debug.Log(
            "[0922 Phase 6] Report written: " +
            path);
    }
}
#endif
