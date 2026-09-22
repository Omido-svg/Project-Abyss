#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class Canonical0922Phase5FactoryAuthoringVerification
{
    private const string Phase4ReportPath =
        "Logs/GameSystemVerification/0922_Phase4_TimingDeferred.md";

    private sealed class Check
    {
        public string Id;
        public string Name;
        public bool Passed;
        public string Actual;
    }

    [MenuItem("Game System Verification/0922 Canonical/Phase 5 - Verify Factory and Authoring")]
    public static void VerifyFromMenu()
    {
        List<Check> checks = new();

        AddPrerequisites(checks);
        AddKindChecks(checks);
        AddFactoryChecks(checks);
        AddAuthoringChecks(checks);
        AddConditionQueryChecks(checks);
        AddAssetAuditChecks(checks);

        int pass = checks.Count(x => x.Passed);
        int fail = checks.Count - pass;

        string result =
            fail == 0
                ? "PASS_FACTORY_AUTHORING"
                : "FAIL";

        WriteReport(
            checks,
            pass,
            fail,
            result);

        string summary =
            "[0922 Phase 5 · Factory / Authoring / Data Schema]\n" +
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
            $"\n\nPHASE5_RESULT={result}";

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
            "P5-H00",
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
                Phase4ReportPath);

        bool exists =
            File.Exists(path);

        string report =
            exists
                ? File.ReadAllText(path)
                : string.Empty;

        bool pass =
            report.Contains(
                "RESULT: PASS_TIMING_DEFERRED",
                StringComparison.Ordinal) ||
            report.Contains(
                "PHASE4_RESULT=PASS_TIMING_DEFERRED",
                StringComparison.Ordinal);

        Add(
            checks,
            "P5-H01",
            "Phase 4 timing/deferred closed",
            exists && pass,
            !exists
                ? "Phase4 report missing"
                : $"ReportPresent=True, ResultPass={pass}");
    }

    private static void AddKindChecks(
        List<Check> checks)
    {
        bool appendOnlyIds =
            (int)StatusEffectId.Bleeding == 0 &&
            (int)StatusEffectId.Swift == 14 &&
            (int)StatusEffectId.Stagnation == 15 &&
            (int)StatusEffectId.Designation == 16 &&
            (int)StatusEffectId.Fear == 17;

        Add(
            checks,
            "P5-K00",
            "StatusEffectId serialization remains append-only",
            appendOnlyIds,
            $"Bleeding={(int)StatusEffectId.Bleeding}, Swift={(int)StatusEffectId.Swift}, " +
            $"Stagnation={(int)StatusEffectId.Stagnation}, Designation={(int)StatusEffectId.Designation}, Fear={(int)StatusEffectId.Fear}");

        StatusEffectId[] numeric =
        {
            StatusEffectId.Strength,
            StatusEffectId.Weakness,
            StatusEffectId.Fracture,
            StatusEffectId.Protection,
            StatusEffectId.Rupture,
            StatusEffectId.Heat,
            StatusEffectId.Stagnation,
            StatusEffectId.Swift,
            StatusEffectId.Sturdy,
            StatusEffectId.Disarm,
            StatusEffectId.Regeneration,
            StatusEffectId.Designation
        };

        bool numericPass =
            numeric.All(
                x => StatusEffectFactory.GetStorageKind(x) ==
                     StatusEffectStorageKind.NumericTimed);

        Add(
            checks,
            "P5-K01",
            "Canonical NumericTimed ids are classified",
            numericPass,
            "Numeric=" + string.Join(",", numeric));

        bool presencePass =
            StatusEffectFactory.GetStorageKind(StatusEffectId.Pain) ==
                StatusEffectStorageKind.PresenceTimed &&
            StatusEffectFactory.GetStorageKind(StatusEffectId.Fear) ==
                StatusEffectStorageKind.PresenceTimed;

        Add(
            checks,
            "P5-K02",
            "Canonical PresenceTimed ids are classified",
            presencePass,
            "Presence=Pain,Fear");

        bool bespokePass =
            StatusEffectFactory.GetStorageKind(StatusEffectId.Bleeding) ==
                StatusEffectStorageKind.Bespoke &&
            StatusEffectFactory.GetStorageKind(StatusEffectId.OlafBloodWound) ==
                StatusEffectStorageKind.Bespoke &&
            !StatusEffectFactory.IsCanonicalGenericAuthorable(
                StatusEffectId.Bleeding);

        Add(
            checks,
            "P5-K03",
            "Bespoke keywords are excluded from canonical generic authoring",
            bespokePass,
            $"BleedingKind={StatusEffectFactory.GetStorageKind(StatusEffectId.Bleeding)}");
    }

    private static void AddFactoryChecks(
        List<Check> checks)
    {
        StrengthStatus legacyStrength =
            StatusEffectFactory.Create(
                StatusEffectId.Strength,
                2,
                3) as StrengthStatus;

        PainStatus legacyPain =
            StatusEffectFactory.Create(
                StatusEffectId.Pain,
                5,
                99) as PainStatus;

        bool legacySafe =
            legacyStrength != null &&
            legacyStrength.Stack == 2 &&
            legacyStrength.Duration == 1 &&
            legacyPain != null &&
            legacyPain.Duration == 5;

        Add(
            checks,
            "P5-F01",
            "Legacy factory meaning remains unchanged",
            legacySafe,
            $"LegacyStrength={legacyStrength?.Stack}·{legacyStrength?.Duration}, " +
            $"LegacyPainT={legacyPain?.Duration}");

        StrengthStatus canonicalStrength =
            StatusEffectFactory.CreateCanonical0922(
                StatusEffectId.Strength,
                2,
                3) as StrengthStatus;

        StagnationStatus canonicalStagnation =
            StatusEffectFactory.CreateCanonical0922(
                StatusEffectId.Stagnation,
                4,
                2) as StagnationStatus;

        bool numericFinite =
            canonicalStrength != null &&
            canonicalStrength.NumericValue == 2 &&
            canonicalStrength.Duration == 3 &&
            canonicalStagnation != null &&
            canonicalStagnation.NumericValue == 4 &&
            canonicalStagnation.Duration == 2;

        Add(
            checks,
            "P5-F02",
            "Canonical Numeric N/T factory preserves value and duration",
            numericFinite,
            $"Strength={canonicalStrength?.NumericValue}·{canonicalStrength?.Duration}, " +
            $"Stagnation={canonicalStagnation?.NumericValue}·{canonicalStagnation?.Duration}");

        StrengthStatus infinite =
            StatusEffectFactory.CreateCanonical0922(
                StatusEffectId.Strength,
                2,
                StatusEffect.InfiniteDuration) as StrengthStatus;

        Add(
            checks,
            "P5-F03",
            "Canonical infinite duration uses real ∞ abstraction",
            infinite != null &&
            infinite.IsInfiniteDuration &&
            infinite.Duration == StatusEffect.InfiniteDuration,
            $"Duration={infinite?.Duration}, Infinite={infinite?.IsInfiniteDuration}");

        PainStatus pain =
            StatusEffectFactory.CreateCanonical0922(
                StatusEffectId.Pain,
                99,
                5) as PainStatus;

        OlafFearStatus fear =
            StatusEffectFactory.CreateCanonical0922(
                StatusEffectId.Fear,
                77,
                3) as OlafFearStatus;

        bool presence =
            pain != null &&
            pain.Stack == 1 &&
            pain.NumericValue == 0 &&
            pain.Duration == 5 &&
            fear != null &&
            fear.Stack == 1 &&
            fear.NumericValue == 0 &&
            fear.Duration == 3;

        Add(
            checks,
            "P5-F04",
            "Presence factory is T-only and ignores fake N",
            presence,
            $"Pain(Stack={pain?.Stack},Numeric={pain?.NumericValue},T={pain?.Duration}), " +
            $"Fear(Stack={fear?.Stack},Numeric={fear?.NumericValue},T={fear?.Duration})");

        PainStatus infinitePain =
            StatusEffectFactory.CreateCanonical0922(
                StatusEffectId.Pain,
                123,
                StatusEffect.InfiniteDuration) as PainStatus;

        Add(
            checks,
            "P5-F04B",
            "PresenceTimed supports canonical ∞ without fake sentinel turns",
            infinitePain != null &&
            infinitePain.IsInfiniteDuration &&
            infinitePain.NumericValue == 0,
            $"PainT={infinitePain?.Duration}, Infinite={infinitePain?.IsInfiniteDuration}");

        YujinDesignationStatus designation =
            StatusEffectFactory.CreateCanonical0922(
                StatusEffectId.Designation,
                3,
                4) as YujinDesignationStatus;

        Add(
            checks,
            "P5-F05",
            "Designation supports canonical N/T creation",
            designation != null &&
            designation.NumericValue == 3 &&
            designation.Duration == 4,
            $"Designation={designation?.NumericValue}·{designation?.Duration}");

        Add(
            checks,
            "P5-F06",
            "Canonical generic factory rejects Bespoke",
            StatusEffectFactory.CreateCanonical0922(
                StatusEffectId.Bleeding,
                5,
                3) == null,
            "CreateCanonical0922(Bleeding)=null");

        RegenerationStatus regeneration =
            StatusEffectFactory.CreateCanonical0922(
                StatusEffectId.Regeneration,
                4,
                3) as RegenerationStatus;

        Add(
            checks,
            "P5-F07",
            "Canonical Regeneration uses N/T without legacy channel semantics",
            regeneration != null &&
            regeneration.HealAmount == 4 &&
            regeneration.Duration == 3,
            $"Regen={regeneration?.HealAmount}·{regeneration?.Duration}");
    }

    private static void AddAuthoringChecks(
        List<Check> checks)
    {
        bool serializedEnumSafety =
            (int)StatusEffectAuthoringSchema.Legacy == 0 &&
            (int)SkillEffectConditionType.RuntimeAfterDamage == 29 &&
            (int)SkillEffectConditionType.StatusPresent == 30 &&
            (int)SkillEffectConditionType.StatusNumericTotalRange == 31 &&
            (int)SkillEffectConditionType.StatusEffectiveAxisRange == 32;

        Add(
            checks,
            "P5-A00",
            "Authoring/condition enums preserve serialized values",
            serializedEnumSafety,
            $"SchemaLegacy={(int)StatusEffectAuthoringSchema.Legacy}, " +
            $"RuntimeAfterDamage={(int)SkillEffectConditionType.RuntimeAfterDamage}, " +
            $"New=30/31/32");

        AddBodyPartStatusEffect legacy =
            ScriptableObject.CreateInstance<AddBodyPartStatusEffect>();
        AddBodyPartStatusEffect source =
            ScriptableObject.CreateInstance<AddBodyPartStatusEffect>();
        AddBodyPartStatusEffect clone =
            ScriptableObject.CreateInstance<AddBodyPartStatusEffect>();

        try
        {
            bool legacyDefault =
                legacy.AuthoringSchema == StatusEffectAuthoringSchema.Legacy &&
                !legacy.InfiniteDuration;

            Add(
                checks,
                "P5-A01",
                "New authoring fields default to Legacy-safe values",
                legacyDefault,
                $"Schema={legacy.AuthoringSchema}, Infinite={legacy.InfiniteDuration}");

            source.StatusEffectId = StatusEffectId.Strength;
            source.AuthoringSchema = StatusEffectAuthoringSchema.Canonical0922;
            source.Stack = 2;
            source.Duration = 7;
            source.InfiniteDuration = true;

            string json =
                EditorJsonUtility.ToJson(source);

            EditorJsonUtility.FromJsonOverwrite(
                json,
                clone);

            bool serialized =
                clone.StatusEffectId == StatusEffectId.Strength &&
                clone.AuthoringSchema == StatusEffectAuthoringSchema.Canonical0922 &&
                clone.Stack == 2 &&
                clone.Duration == 7 &&
                clone.InfiniteDuration &&
                clone.ResolveAuthoredDuration(null) ==
                    StatusEffect.InfiniteDuration;

            Add(
                checks,
                "P5-A02",
                "Canonical ∞ authoring survives Unity serialization round-trip",
                serialized,
                $"Schema={clone.AuthoringSchema}, N={clone.Stack}, T={clone.Duration}, Infinite={clone.InfiniteDuration}");

            AddBodyPartStatusEffect presence =
                ScriptableObject.CreateInstance<AddBodyPartStatusEffect>();

            try
            {
                presence.StatusEffectId = StatusEffectId.Pain;
                presence.AuthoringSchema = StatusEffectAuthoringSchema.Canonical0922;
                presence.Stack = 99;
                presence.Duration = 5;

                bool presenceAuthoring =
                    presence.ResolveAuthoredValue(null) == 1 &&
                    presence.ResolveAuthoredDuration(null) == 5 &&
                    presence.AuthoredStorageKind ==
                        StatusEffectStorageKind.PresenceTimed;

                Add(
                    checks,
                    "P5-A03",
                    "Presence authoring ignores fake Stack/N",
                    presenceAuthoring,
                    $"ResolvedValue={presence.ResolveAuthoredValue(null)}, T={presence.ResolveAuthoredDuration(null)}");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(presence);
            }

            SkillEffectOverrides overrides =
                new SkillEffectOverrides
                {
                    OverrideInfiniteDuration = true,
                    InfiniteDuration = true
                };

            Add(
                checks,
                "P5-A04",
                "SkillEffectOverrides can explicitly author ∞",
                overrides.ResolveInfiniteDuration(false),
                $"OverrideInfinite={overrides.OverrideInfiniteDuration}, Value={overrides.InfiniteDuration}");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(legacy);
            UnityEngine.Object.DestroyImmediate(source);
            UnityEngine.Object.DestroyImmediate(clone);
        }
    }

    private static void AddConditionQueryChecks(
        List<Check> checks)
    {
        GameObject go = null;

        try
        {
            go =
                new GameObject(
                    "__0922_PHASE5_QUERY_PROBE__");

            NormalEnemy owner =
                go.AddComponent<NormalEnemy>();

            CharacterStatusController controller =
                new CharacterStatusController(owner);

            FieldInfo statusControllerField =
                typeof(Character).GetField(
                    "statusController",
                    BindingFlags.Instance | BindingFlags.NonPublic);

            statusControllerField?.SetValue(
                owner,
                controller);

            controller.AddStatus(
                new StrengthStatus(3, 3),
                owner);
            controller.AddStatus(
                new StrengthStatus(2, 1),
                owner);
            controller.AddStatus(
                new WeaknessStatus(4, 2),
                owner);
            controller.AddStatus(
                new PainStatus(5),
                owner);
            controller.AddStatus(
                new OlafFearStatus(3),
                owner);
            controller.AddStatus(
                new HeatStatus(3, 2),
                owner);
            controller.AddStatus(
                new StagnationStatus(1, 3),
                owner);

            bool rawPresence =
                StatusEffectFactory.HasStatus(
                    owner,
                    null,
                    StatusEffectId.Pain) &&
                StatusEffectFactory.HasStatus(
                    owner,
                    null,
                    StatusEffectId.Fear);

            Add(
                checks,
                "P5-Q01",
                "Condition API raw presence is independent of numeric value",
                rawPresence,
                $"Pain={StatusEffectFactory.HasStatus(owner, null, StatusEffectId.Pain)}, " +
                $"Fear={StatusEffectFactory.HasStatus(owner, null, StatusEffectId.Fear)}");

            int strengthTotal =
                StatusEffectFactory.GetNumericTotal(
                    owner,
                    null,
                    StatusEffectId.Strength);
            int painNumeric =
                StatusEffectFactory.GetNumericTotal(
                    owner,
                    null,
                    StatusEffectId.Pain);

            Add(
                checks,
                "P5-Q02",
                "Condition API numeric total excludes Presence marker",
                strengthTotal == 5 &&
                painNumeric == 0,
                $"StrengthTotal={strengthTotal}, PainNumeric={painNumeric}");

            int rollAxis =
                StatusEffectFactory.GetEffectiveAxis(
                    owner,
                    null,
                    StatusEffectEffectiveAxis.RollShift);
            int prestigeAxis =
                StatusEffectFactory.GetEffectiveAxis(
                    owner,
                    null,
                    StatusEffectEffectiveAxis.TurnEndPrestige);

            Add(
                checks,
                "P5-Q03",
                "Condition API effective axes use algebraic canonical result",
                rollAxis == 0 &&
                prestigeAxis == 2,
                $"RollShift={rollAxis} (5-4-Fear1), Prestige={prestigeAxis} (3-1)");
        }
        catch (Exception exception)
        {
            Add(
                checks,
                "P5-QXX",
                "Condition query probe completed without exception",
                false,
                exception.GetType().Name +
                ": " +
                exception.Message);
        }
        finally
        {
            if (go != null)
                UnityEngine.Object.DestroyImmediate(go);
        }
    }

    private static void AddAssetAuditChecks(
        List<Check> checks)
    {
        int scanned = 0;
        int legacy = 0;
        int canonical = 0;
        int invalid = 0;
        List<string> invalidPaths = new();

        AuditAssets<AddBodyPartStatusEffect>(
            ref scanned,
            ref legacy,
            ref canonical,
            ref invalid,
            invalidPaths);

        AuditAssets<ApplyStatusIfConditionEffect>(
            ref scanned,
            ref legacy,
            ref canonical,
            ref invalid,
            invalidPaths);

        Add(
            checks,
            "P5-D01",
            "Existing status effect assets are schema-safe",
            scanned > 0 && invalid == 0,
            $"Scanned={scanned}, Legacy={legacy}, Canonical0922={canonical}, Invalid={invalid}" +
            (invalidPaths.Count == 0
                ? string.Empty
                : ", Paths=" + string.Join(" | ", invalidPaths.Take(5))));
    }

    private static void AuditAssets<T>(
        ref int scanned,
        ref int legacy,
        ref int canonical,
        ref int invalid,
        List<string> invalidPaths)
        where T : SkillEffectDefinition
    {
        string[] guids =
            AssetDatabase.FindAssets(
                $"t:{typeof(T).Name}",
                new[] { "Assets/2. Data" });

        foreach (string guid in guids)
        {
            string path =
                AssetDatabase.GUIDToAssetPath(guid);

            T asset =
                AssetDatabase.LoadAssetAtPath<T>(path);

            if (asset == null)
                continue;

            scanned++;

            StatusEffectAuthoringSchema schema;
            StatusEffectId id;
            bool infinite;

            if (asset is AddBodyPartStatusEffect add)
            {
                schema = add.AuthoringSchema;
                id = add.StatusEffectId;
                infinite = add.InfiniteDuration;
            }
            else if (asset is ApplyStatusIfConditionEffect conditional)
            {
                schema = conditional.AuthoringSchema;
                id = conditional.StatusEffectId;
                infinite = conditional.InfiniteDuration;
            }
            else
            {
                continue;
            }

            if (schema == StatusEffectAuthoringSchema.Legacy)
            {
                legacy++;

                // 새 필드가 직렬화 노이즈로 켜져 있어도 Legacy에서는 적용되지 않지만,
                // 데이터 청결성 차원에서 invalid로 잡아 migration 전 오염을 막는다.
                if (infinite)
                {
                    invalid++;
                    invalidPaths.Add(path + " [Legacy+Infinite]");
                }

                continue;
            }

            canonical++;

            if (!StatusEffectFactory.IsCanonicalGenericAuthorable(id))
            {
                invalid++;
                invalidPaths.Add(path + $" [Canonical+Bespoke:{id}]");
            }
        }
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

        Directory.CreateDirectory(directory);

        string path =
            Path.Combine(
                directory,
                "0922_Phase5_FactoryAuthoring.md");

        List<string> lines = new()
        {
            "# 0922 Phase 5 — Factory / Authoring / Data Schema",
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
            "[0922 Phase 5] Report written: " +
            path);
    }
}
#endif
