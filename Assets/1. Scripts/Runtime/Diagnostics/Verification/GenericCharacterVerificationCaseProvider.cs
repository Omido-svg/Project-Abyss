using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class GenericCharacterVerificationCaseProvider :
    ICharacterVerificationCaseProvider
{
    public const string CoreReferences =
        "generic.data.core_references";

    public const string PrefabCompatibility =
        "generic.data.prefab_compatibility";

    public const string LoadoutIntegrity =
        "generic.data.loadout_integrity";

    public const string SkillDefinitions =
        "generic.data.skill_definitions";

    public const string Presentation =
        "generic.presentation.timeline_and_animator";

    public const string RuntimeInitialization =
        "generic.runtime.initialization";

    public const string RuntimeBodyParts =
        "generic.runtime.body_parts";

    public const string RuntimeSkills =
        "generic.runtime.skill_binding";

    public const string DeterministicRandom =
        "generic.runtime.deterministic_random";

    public const string LiveSceneBinding =
        "generic.live.scene_binding";

    public bool Supports(
        CharacterAuthoringBundle bundle,
        Character character)
    {
        return bundle != null;
    }

    public IEnumerable<CharacterVerificationCaseDefinition>
        CreateDefaultCases(
            CharacterAuthoringBundle bundle)
    {
        yield return CharacterVerificationCaseDefinition.Create(
            CoreReferences,
            "Core 데이터 참조",
            CharacterVerificationCategory.Data,
            CharacterVerificationExecutionMode.DataOnly,
            "Bundle, CharacterData, CombatLoadout, Prefab의 필수 참조를 검사합니다.");

        yield return CharacterVerificationCaseDefinition.Create(
            PrefabCompatibility,
            "Prefab·Bundle 호환성",
            CharacterVerificationCategory.Data,
            CharacterVerificationExecutionMode.DataOnly,
            "Bundle Kind와 Character Component 조합 및 필수 Prefab Component를 검사합니다.");

        yield return CharacterVerificationCaseDefinition.Create(
            LoadoutIntegrity,
            "스킬 장착·슬롯 노출",
            CharacterVerificationCategory.Data,
            CharacterVerificationExecutionMode.DataOnly,
            "카테고리별 장착 한도, 타입, 중복과 행동 슬롯에서의 노출 가능성을 검사합니다.");

        yield return CharacterVerificationCaseDefinition.Create(
            SkillDefinitions,
            "SkillDefinition 무결성",
            CharacterVerificationCategory.Data,
            CharacterVerificationExecutionMode.DataOnly,
            "스킬 ID, 굴림 수, 중복 ID와 런타임 생성 가능 여부를 검사합니다.");

        yield return CharacterVerificationCaseDefinition.Create(
            Presentation,
            "Timeline·Animator 연결",
            CharacterVerificationCategory.Presentation,
            CharacterVerificationExecutionMode.DataOnly,
            "Animator, CharacterPresentationProfile, 스킬 Timeline 세트를 검사합니다.");

        yield return CharacterVerificationCaseDefinition.Create(
            RuntimeInitialization,
            "격리 런타임 초기화",
            CharacterVerificationCategory.Boundary,
            CharacterVerificationExecutionMode.IsolatedRuntime,
            "실제 Prefab Clone을 별도 BattleContext에 초기화해 구독과 런타임 생성 실패를 검사합니다.");

        yield return CharacterVerificationCaseDefinition.Create(
            RuntimeBodyParts,
            "부위 런타임 상태",
            CharacterVerificationCategory.Status,
            CharacterVerificationExecutionMode.IsolatedRuntime,
            "부위 소유자, HP, 타입 중복, 초기 상태를 검사합니다.");

        yield return CharacterVerificationCaseDefinition.Create(
            RuntimeSkills,
            "런타임 스킬 바인딩",
            CharacterVerificationCategory.Data,
            CharacterVerificationExecutionMode.IsolatedRuntime,
            "장착 SkillDefinition이 캐릭터 고유 RuntimeSkill로 생성되었는지 검사합니다.");

        yield return CharacterVerificationCaseDefinition.Create(
            DeterministicRandom,
            "결정론적 굴림 재현",
            CharacterVerificationCategory.Boundary,
            CharacterVerificationExecutionMode.IsolatedRuntime,
            "CharacterRandomDebugOverride가 같은 Seed와 Sample에서 같은 RollResult를 생성하는지 검사합니다.");

        yield return CharacterVerificationCaseDefinition.Create(
            LiveSceneBinding,
            "실전 Scene UI·전투 연결",
            CharacterVerificationCategory.UI,
            CharacterVerificationExecutionMode.LiveScene,
            "현재 BattleManager의 플레이어, HUD, 스킬 패널이 선택 Bundle과 연결되어 있는지 검사합니다.");
    }

    public bool TryExecute(
        CharacterVerificationContext context,
        out CharacterVerificationCaseResult result)
    {
        result = context?.Definition?.CaseId switch
        {
            CoreReferences =>
                VerifyCoreReferences(context),

            PrefabCompatibility =>
                VerifyPrefabCompatibility(context),

            LoadoutIntegrity =>
                VerifyLoadout(context),

            SkillDefinitions =>
                VerifySkillDefinitions(context),

            Presentation =>
                VerifyPresentation(context),

            RuntimeInitialization =>
                VerifyRuntimeInitialization(context),

            RuntimeBodyParts =>
                VerifyBodyParts(context),

            RuntimeSkills =>
                VerifyRuntimeSkills(context),

            DeterministicRandom =>
                VerifyDeterministicRandom(context),

            LiveSceneBinding =>
                VerifyLiveSceneBinding(context),

            _ => null
        };

        return result != null;
    }

    private static CharacterVerificationCaseResult
        VerifyCoreReferences(
            CharacterVerificationContext context)
    {
        List<string> errors =
            new List<string>();

        CharacterAuthoringBundle bundle =
            context.Bundle;

        if (bundle == null)
            errors.Add("CharacterAuthoringBundle이 없습니다.");

        if (bundle?.CharacterData == null)
            errors.Add("CharacterData가 없습니다.");

        if (bundle?.CombatLoadout == null)
            errors.Add("CharacterCombatLoadout이 없습니다.");

        if (bundle?.CharacterPrefab == null)
            errors.Add("Character Prefab이 없습니다.");

        if (bundle?.CharacterData != null &&
            bundle.CombatLoadout != null &&
            bundle.CharacterData.CombatLoadout !=
            bundle.CombatLoadout)
        {
            errors.Add(
                "CharacterData.CombatLoadout과 Bundle.CombatLoadout이 다릅니다.");
        }

        return errors.Count == 0
            ? context.Pass(
                "필수 Core 참조가 모두 연결됨",
                "Bundle/Data/Loadout/Prefab 연결 정상")
            : context.Fail(
                "필수 Core 참조가 모두 연결됨",
                $"{errors.Count}개 오류",
                string.Join("\n", errors));
    }

    private static CharacterVerificationCaseResult
        VerifyPrefabCompatibility(
            CharacterVerificationContext context)
    {
        CharacterAuthoringBundle bundle =
            context.Bundle;

        Character prefab =
            bundle?.CharacterPrefab;

        if (bundle == null)
        {
            return context.Fail(
                "호환 가능한 Character Prefab",
                "Bundle 없음");
        }

        if (prefab == null)
        {
            return context.Skip(
                "선행 조건 실패: Bundle.CharacterPrefab이 없습니다. " +
                "Core 데이터 참조 결과를 먼저 수정하세요.");
        }

        List<string> errors =
            new List<string>();

        if (!bundle.IsCompatibleWith(
                prefab,
                out string reason))
        {
            errors.Add(reason);
        }

        RequireComponent<CharacterAuthoringLink>(
            prefab,
            errors);

        RequireComponent<CharacterView>(
            prefab,
            errors);

        RequireComponent<CharacterViewEventBinder>(
            prefab,
            errors);

        RequireComponent<CharacterFacingController>(
            prefab,
            errors);

        RequireComponent<CharacterActionMover>(
            prefab,
            errors);

        RequireComponent<BattleCharacterWorldClickInspector>(
            prefab,
            errors);

        RequireComponent<CharacterRandomDebugOverride>(
            prefab,
            errors);

        return errors.Count == 0
            ? context.Pass(
                "Bundle Kind와 표준 Prefab Component가 호환됨",
                $"{prefab.GetType().Name} 호환 정상")
            : context.Fail(
                "Bundle Kind와 표준 Prefab Component가 호환됨",
                $"{errors.Count}개 오류",
                string.Join("\n", errors));
    }

    private static CharacterVerificationCaseResult
        VerifyLoadout(
            CharacterVerificationContext context)
    {
        CharacterCombatLoadout loadout =
            context.Bundle?.CombatLoadout;

        CharacterData data =
            context.Bundle?.CharacterData;

        if (loadout == null ||
            data == null)
        {
            return context.Fail(
                "Loadout과 CharacterData 존재",
                "Loadout 또는 CharacterData 없음");
        }

        List<string> errors =
            new List<string>();

        ValidateCategory(
            loadout.NormalSkills,
            ActionType.NormalAttack,
            CharacterCombatLoadout.NormalLimit,
            "일반 공격",
            errors);

        ValidateCategory(
            loadout.DuelSkills,
            ActionType.Duel,
            CharacterCombatLoadout.DuelLimit,
            "결투",
            errors);

        ValidateCategory(
            loadout.PreparationSkills,
            ActionType.Preparation,
            CharacterCombatLoadout.PreparationLimit,
            "도사림",
            errors);

        ValidateCategory(
            loadout.PrestigeSkills,
            ActionType.Prestige,
            CharacterCombatLoadout.PrestigeLimit,
            "위세",
            errors);

        ActionType[] equippedTypes =
        {
            ActionType.NormalAttack,
            ActionType.Duel,
            ActionType.Preparation,
            ActionType.Prestige
        };

        for (int i = 0;
             i < equippedTypes.Length;
             i++)
        {
            ActionType actionType =
                equippedTypes[i];

            IReadOnlyList<SkillDefinition> equipped =
                loadout.GetEquipped(actionType);

            if (equipped.Count == 0)
                continue;

            bool visible =
                data.ActionSlots != null &&
                data.ActionSlots.Any(
                    slot =>
                        slot != null &&
                        slot.Enabled &&
                        slot.Allows(actionType));

            if (!visible)
            {
                errors.Add(
                    $"{actionType} 장착 스킬이 있지만 이를 허용하는 행동 슬롯이 없습니다.");
            }
        }

        return errors.Count == 0
            ? context.Pass(
                "카테고리 한도·타입·슬롯 노출 정상",
                $"일반 {loadout.NormalSkills.Count}, " +
                $"결투 {loadout.DuelSkills.Count}, " +
                $"도사림 {loadout.PreparationSkills.Count}, " +
                $"위세 {loadout.PrestigeSkills.Count}")
            : context.Fail(
                "카테고리 한도·타입·슬롯 노출 정상",
                $"{errors.Count}개 오류",
                string.Join("\n", errors));
    }

    private static CharacterVerificationCaseResult
        VerifySkillDefinitions(
            CharacterVerificationContext context)
    {
        CharacterAuthoringBundle bundle =
            context.Bundle;

        if (bundle == null)
        {
            return context.Fail(
                "SkillDefinition 존재",
                "Bundle 없음");
        }

        List<string> errors =
            new List<string>();

        HashSet<string> ids =
            new HashSet<string>(
                StringComparer.Ordinal);

        int count = 0;

        foreach (SkillDefinition definition
                 in bundle.EnumerateSkillDefinitions())
        {
            if (definition == null)
                continue;

            count++;

            if (string.IsNullOrWhiteSpace(
                    definition.SkillId))
            {
                errors.Add(
                    $"{definition.name}: SkillId가 비어 있습니다.");
            }
            else if (!ids.Add(
                         definition.SkillId))
            {
                errors.Add(
                    $"{definition.SkillId}: 중복 SkillId입니다.");
            }

            if (definition.EffectiveRollCount < 1)
            {
                errors.Add(
                    $"{definition.SkillName}: 유효 굴림 수가 1 미만입니다.");
            }

            Skill runtime =
                definition.CreateRuntimeSkill();

            if (runtime == null)
            {
                errors.Add(
                    $"{definition.SkillName}: 기본 RuntimeSkill 생성 실패");
            }
        }

        if (count == 0)
            errors.Add("검증 가능한 SkillDefinition이 없습니다.");

        return errors.Count == 0
            ? context.Pass(
                "모든 SkillDefinition ID와 Runtime 생성 정상",
                $"{count}개 스킬 정상")
            : context.Fail(
                "모든 SkillDefinition ID와 Runtime 생성 정상",
                $"{errors.Count}개 오류",
                string.Join("\n", errors));
    }

    private static CharacterVerificationCaseResult
        VerifyPresentation(
            CharacterVerificationContext context)
    {
        CharacterAuthoringBundle bundle =
            context.Bundle;

        Character prefab =
            bundle?.CharacterPrefab;

        if (bundle == null)
        {
            return context.Fail(
                "Presentation 연결",
                "Bundle 없음");
        }

        if (prefab == null)
        {
            return context.Skip(
                "선행 조건 실패: Bundle.CharacterPrefab이 없어 " +
                "Animator와 CharacterView를 검사할 수 없습니다.");
        }

        List<string> errors =
            new List<string>();

        Animator animator =
            prefab.GetComponentInChildren<Animator>(true);

        if (animator == null)
        {
            errors.Add("Animator가 없습니다.");
        }
        else if (animator.runtimeAnimatorController == null &&
                 bundle.AnimatorController == null)
        {
            errors.Add("RuntimeAnimatorController가 없습니다.");
        }

        CharacterView view =
            prefab.GetComponentInChildren<CharacterView>(true);

        if (view == null)
        {
            errors.Add("CharacterView가 없습니다.");
        }
        else if (view.PresentationProfile == null &&
                 bundle.PresentationProfile == null)
        {
            errors.Add("CharacterPresentationProfile이 없습니다.");
        }

        foreach (SkillDefinition definition
                 in bundle.EnumerateSkillDefinitions())
        {
            if (definition == null)
                continue;

            SkillVisualDefinition visual =
                definition.VisualDefinition ??
                bundle.VisualProfile?.GetDefault(
                    definition.ActionType);

            if (visual == null)
            {
                errors.Add(
                    $"{definition.SkillName}: SkillVisualDefinition이 없습니다.");
                continue;
            }

            if (context.Profile.StrictPresentationValidation &&
                !visual.HasCompleteTimelineSet)
            {
                List<string> missing =
                    visual.GetMissingRequirements();

                errors.Add(
                    $"{definition.SkillName}: " +
                    string.Join(", ", missing));
            }
        }

        return errors.Count == 0
            ? context.Pass(
                "Animator·Presentation·Timeline 연결 정상",
                "Presentation 검증 통과")
            : context.Fail(
                "Animator·Presentation·Timeline 연결 정상",
                $"{errors.Count}개 오류",
                string.Join("\n", errors));
    }

    private static CharacterVerificationCaseResult
        VerifyRuntimeInitialization(
            CharacterVerificationContext context)
    {
        Character character =
            context.Character;

        if (character == null)
        {
            return context.Fail(
                "격리 Character 초기화 성공",
                "Character 없음");
        }

        List<string> errors =
            new List<string>();

        if (!character.IsInitialized)
            errors.Add("Character.IsInitialized=false");

        if (character.Data == null)
            errors.Add("Runtime CharacterData가 없습니다.");

        if (character.BattleContext == null)
            errors.Add("Runtime BattleContext가 없습니다.");

        if (character.Mechanics == null ||
            character.Mechanics.Count == 0)
        {
            errors.Add("등록된 CombatMechanic이 없습니다.");
        }
        else
        {
            for (int i = 0;
                 i < character.Mechanics.Count;
                 i++)
            {
                CombatMechanic mechanic =
                    character.Mechanics[i];

                if (mechanic != null &&
                    !mechanic.IsRegistered)
                {
                    errors.Add(
                        $"{mechanic.MechanicName}: 등록되지 않음");
                }
            }
        }

        return errors.Count == 0
            ? context.Pass(
                "Isolated BattleContext 초기화와 메커닉 구독 성공",
                CharacterVerificationContext.DescribeSnapshot(character))
            : context.Fail(
                "Isolated BattleContext 초기화와 메커닉 구독 성공",
                $"{errors.Count}개 오류",
                string.Join("\n", errors));
    }

    private static CharacterVerificationCaseResult
        VerifyBodyParts(
            CharacterVerificationContext context)
    {
        Character character =
            context.Character;

        if (character == null)
        {
            return context.Fail(
                "부위 런타임 상태 정상",
                "Character 없음");
        }

        if (!character.UsesBodyParts)
        {
            return context.Pass(
                "Single HP 캐릭터는 부위 검사를 생략",
                $"Single HP {character.CurrentHP}/{character.MaxCombatHP}");
        }

        List<string> errors =
            new List<string>();

        HashSet<PartType> types =
            new HashSet<PartType>();

        IReadOnlyList<BodyPart> parts =
            character.BodyParts;

        if (parts == null ||
            parts.Count == 0)
        {
            errors.Add("BodyParts가 비어 있습니다.");
        }
        else
        {
            for (int i = 0;
                 i < parts.Count;
                 i++)
            {
                BodyPart part =
                    parts[i];

                if (part == null)
                {
                    errors.Add($"BodyParts[{i}]가 null입니다.");
                    continue;
                }

                if (part.Owner != character)
                    errors.Add($"{part.Type}: Owner 불일치");

                if (!types.Add(part.Type))
                    errors.Add($"{part.Type}: 중복 부위 타입");

                if (part.MaxPartHP <= 0f ||
                    part.PartHP <= 0f)
                {
                    errors.Add(
                        $"{part.Type}: 초기 HP가 유효하지 않습니다.");
                }

                if (part.State != BodyPartState.Normal)
                {
                    errors.Add(
                        $"{part.Type}: 초기 상태가 {part.State}입니다.");
                }
            }
        }

        return errors.Count == 0
            ? context.Pass(
                "부위 Owner·HP·타입·초기 상태 정상",
                $"{parts?.Count ?? 0}개 부위 정상")
            : context.Fail(
                "부위 Owner·HP·타입·초기 상태 정상",
                $"{errors.Count}개 오류",
                string.Join("\n", errors));
    }

    private static CharacterVerificationCaseResult
        VerifyRuntimeSkills(
            CharacterVerificationContext context)
    {
        Character character =
            context.Character;

        CharacterCombatLoadout loadout =
            context.Bundle?.CombatLoadout;

        if (character == null ||
            loadout == null)
        {
            return context.Fail(
                "장착 스킬의 RuntimeSkill 생성",
                "Character 또는 Loadout 없음");
        }

        List<string> errors =
            new List<string>();

        IReadOnlyList<Skill> runtimeSkills =
            character.RuntimeSkills;

        foreach (SkillDefinition definition
                 in loadout.EnumerateEquipped())
        {
            if (definition == null)
                continue;

            bool found =
                runtimeSkills != null &&
                runtimeSkills.Any(
                    skill =>
                        skill?.Definition != null &&
                        string.Equals(
                            skill.Definition.SkillId,
                            definition.SkillId,
                            StringComparison.Ordinal));

            if (!found)
            {
                errors.Add(
                    $"{definition.SkillName}: RuntimeSkill이 생성되지 않았습니다.");
            }
        }

        return errors.Count == 0
            ? context.Pass(
                "모든 장착 스킬이 RuntimeSkill로 생성됨",
                $"{runtimeSkills?.Count ?? 0}개 RuntimeSkill")
            : context.Fail(
                "모든 장착 스킬이 RuntimeSkill로 생성됨",
                $"{errors.Count}개 누락",
                string.Join("\n", errors));
    }

    private static CharacterVerificationCaseResult
        VerifyDeterministicRandom(
            CharacterVerificationContext context)
    {
        Character character =
            context.Character;

        CharacterRandomDebugOverride random =
            character?.GetComponent<CharacterRandomDebugOverride>();

        Skill skill =
            character?.RuntimeSkills?
                .FirstOrDefault(
                    item => item != null);

        if (random == null ||
            skill == null)
        {
            return context.Fail(
                "같은 Seed와 Sample에서 같은 결과",
                random == null
                    ? "CharacterRandomDebugOverride 없음"
                    : "RuntimeSkill 없음");
        }

        random.OverrideEnabled = true;
        random.Mode = DebugRandomResolverMode.Dice;
        random.DeterministicSequence = true;
        random.Seed = context.Definition.Seed;
        random.UseSkillBasePower = true;
        random.DiceCount = 2;
        random.DiceMin = 1;
        random.DiceMax = 6;
        random.ForceDiceValue = false;
        random.LogActualRolls = false;
        random.Sanitize();

        RollResult first =
            random.CreatePreview(
                skill,
                0,
                0);

        RollResult second =
            random.CreatePreview(
                skill,
                0,
                0);

        bool same =
            first != null &&
            second != null &&
            first.FinalPower == second.FinalPower &&
            first.RawValue == second.RawValue &&
            first.GetShortDisplayText() ==
            second.GetShortDisplayText();

        return same
            ? context.Pass(
                "동일 Seed·Sample은 동일 RollResult",
                first.GetShortDisplayText())
            : context.Fail(
                "동일 Seed·Sample은 동일 RollResult",
                $"First={first?.GetShortDisplayText() ?? "NULL"}, " +
                $"Second={second?.GetShortDisplayText() ?? "NULL"}");
    }

    private static CharacterVerificationCaseResult
        VerifyLiveSceneBinding(
            CharacterVerificationContext context)
    {
        BattleManager manager =
            UnityEngine.Object.FindFirstObjectByType<BattleManager>();

        if (manager == null ||
            !manager.IsInitialized ||
            manager.BattleContext?.Player == null)
        {
            return context.Skip(
                "현재 초기화된 실전 BattleManager가 없습니다.");
        }

        Character livePlayer =
            manager.BattleContext.Player;

        if (!context.Bundle.IsCompatibleWith(
                livePlayer,
                out string reason))
        {
            return context.Skip(
                "현재 Scene 플레이어가 선택한 Bundle과 다릅니다. " +
                reason);
        }

        List<string> errors =
            new List<string>();

        if (UnityEngine.Object.FindFirstObjectByType<
                CharacterMechanicHudUI>(
                    FindObjectsInactive.Include) == null)
        {
            errors.Add("CharacterMechanicHudUI가 없습니다.");
        }

        if (UnityEngine.Object.FindFirstObjectByType<
                SkillSelectPanelUI>(
                    FindObjectsInactive.Include) == null)
        {
            errors.Add("SkillSelectPanelUI가 없습니다.");
        }

        if (manager.BattleUIManager == null)
            errors.Add("BattleUIManager가 연결되지 않았습니다.");

        if (livePlayer.RuntimeSkills == null ||
            livePlayer.RuntimeSkills.Count == 0)
        {
            errors.Add("실전 플레이어 RuntimeSkills가 비어 있습니다.");
        }

        return errors.Count == 0
            ? context.Pass(
                "실전 플레이어와 전투 UI 연결 정상",
                $"{livePlayer.Data?.CharacterName ?? livePlayer.name} / " +
                $"{livePlayer.RuntimeSkills.Count}개 스킬")
            : context.Fail(
                "실전 플레이어와 전투 UI 연결 정상",
                $"{errors.Count}개 오류",
                string.Join("\n", errors));
    }

    private static void ValidateCategory(
        IReadOnlyList<SkillDefinition> definitions,
        ActionType expectedType,
        int limit,
        string label,
        ICollection<string> errors)
    {
        int count =
            definitions?.Count ?? 0;

        if (count > limit)
        {
            errors.Add(
                $"{label}: {count}/{limit}로 장착 한도를 초과합니다.");
        }

        HashSet<string> ids =
            new HashSet<string>(
                StringComparer.Ordinal);

        for (int i = 0;
             i < count;
             i++)
        {
            SkillDefinition definition =
                definitions[i];

            if (definition == null)
            {
                errors.Add(
                    $"{label}[{i}]: null SkillDefinition");
                continue;
            }

            if (definition.ActionType != expectedType)
            {
                errors.Add(
                    $"{definition.SkillName}: " +
                    $"ActionType={definition.ActionType}, " +
                    $"Expected={expectedType}");
            }

            if (!string.IsNullOrWhiteSpace(
                    definition.SkillId) &&
                !ids.Add(definition.SkillId))
            {
                errors.Add(
                    $"{label}: {definition.SkillId} 중복 장착");
            }
        }
    }

    private static void RequireComponent<T>(
        Character prefab,
        ICollection<string> errors)
        where T : Component
    {
        if (prefab.GetComponentInChildren<T>(true) == null)
        {
            errors.Add(
                $"{typeof(T).Name} Component가 없습니다.");
        }
    }
}
