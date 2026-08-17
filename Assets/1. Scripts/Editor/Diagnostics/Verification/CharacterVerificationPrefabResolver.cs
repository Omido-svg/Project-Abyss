#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// CharacterAuthoringBundle에 Character Prefab 참조가 빠졌을 때
/// 이미 Bundle을 참조하고 있는 호환 Prefab을 찾아 안전하게 연결한다.
/// 검증 전용 임시 Prefab을 새로 만들지 않으며 기존 Prefab 에셋만 사용한다.
/// </summary>
public static class CharacterVerificationPrefabResolver
{
    [MenuItem(
        "Tools/Project Abyss/Character Verification/Repair Missing Character Prefab References")]
    public static void RepairAllMissingPrefabReferencesMenu()
    {
        RepairAllMissingPrefabReferences(true);
    }

    public static int RepairAllMissingPrefabReferences(
        bool logResult)
    {
        string[] bundleGuids =
            AssetDatabase.FindAssets(
                "t:CharacterAuthoringBundle");

        int repaired = 0;
        int unresolved = 0;

        for (int i = 0;
             i < bundleGuids.Length;
             i++)
        {
            string path =
                AssetDatabase.GUIDToAssetPath(
                    bundleGuids[i]);

            CharacterAuthoringBundle bundle =
                AssetDatabase.LoadAssetAtPath<
                    CharacterAuthoringBundle>(
                        path);

            if (bundle == null)
                continue;

            if (bundle.CharacterPrefab != null &&
                bundle.IsCompatibleWith(
                    bundle.CharacterPrefab,
                    out _))
            {
                continue;
            }

            if (TryRepair(
                    bundle,
                    out Character prefab,
                    out string detail,
                    saveAssets: false))
            {
                repaired++;

                if (logResult)
                {
                    Debug.Log(
                        "[CharacterVerification] Prefab 참조 복구 / " +
                        $"Bundle={bundle.name}, " +
                        $"Prefab={AssetDatabase.GetAssetPath(prefab)} / " +
                        detail,
                        bundle);
                }
            }
            else
            {
                unresolved++;

                if (logResult)
                {
                    Debug.LogWarning(
                        "[CharacterVerification] 호환 Prefab을 찾지 못했습니다 / " +
                        $"Bundle={bundle.name} / {detail}",
                        bundle);
                }
            }
        }

        if (repaired > 0)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        if (logResult)
        {
            Debug.Log(
                "[CharacterVerification] Prefab 참조 복구 완료 / " +
                $"Repaired={repaired}, Unresolved={unresolved}");
        }

        return repaired;
    }

    public static bool TryRepair(
        CharacterAuthoringBundle bundle,
        out Character resolvedPrefab,
        out string detail,
        bool saveAssets = true)
    {
        resolvedPrefab = null;
        detail = string.Empty;

        if (bundle == null)
        {
            detail = "Bundle이 없습니다.";
            return false;
        }

        Character current =
            bundle.CharacterPrefab;

        if (current != null &&
            bundle.IsCompatibleWith(
                current,
                out _))
        {
            resolvedPrefab = current;
            detail = "기존 Prefab 참조가 이미 정상입니다.";
            return true;
        }

        Candidate best =
            FindBestCandidate(bundle);

        if (best == null ||
            best.Character == null)
        {
            detail =
                "Bundle Kind와 호환되고 CharacterAuthoringLink가 연결된 " +
                "Prefab Asset이 없습니다.";
            return false;
        }

        Undo.RecordObject(
            bundle,
            "Repair Character Verification Prefab Reference");

        bundle.ConfigurePrefab(
            best.Character);

        EditorUtility.SetDirty(bundle);

        if (saveAssets)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        resolvedPrefab =
            best.Character;

        detail =
            $"Score={best.Score}, Reason={best.Reason}";

        return true;
    }

    private static Candidate FindBestCandidate(
        CharacterAuthoringBundle bundle)
    {
        List<Candidate> candidates =
            new List<Candidate>();

        HashSet<string> visitedPaths =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        string preferredPath =
            GetPreferredTestPrefabPath(
                bundle.Kind);

        AddCandidate(
            bundle,
            preferredPath,
            visitedPaths,
            candidates,
            preferredPathBonus: 400);

        string[] prefabGuids =
            AssetDatabase.FindAssets(
                "t:Prefab");

        for (int i = 0;
             i < prefabGuids.Length;
             i++)
        {
            string path =
                AssetDatabase.GUIDToAssetPath(
                    prefabGuids[i]);

            AddCandidate(
                bundle,
                path,
                visitedPaths,
                candidates,
                preferredPathBonus: 0);
        }

        candidates.Sort(
            CompareCandidates);

        return candidates.Count > 0
            ? candidates[0]
            : null;
    }

    private static void AddCandidate(
        CharacterAuthoringBundle bundle,
        string path,
        ISet<string> visitedPaths,
        ICollection<Candidate> candidates,
        int preferredPathBonus)
    {
        if (string.IsNullOrWhiteSpace(path) ||
            !visitedPaths.Add(path))
        {
            return;
        }

        GameObject root =
            AssetDatabase.LoadAssetAtPath<GameObject>(
                path);

        if (root == null)
            return;

        Character character =
            root.GetComponentInChildren<Character>(
                true);

        if (character == null ||
            !bundle.IsCompatibleWith(
                character,
                out _))
        {
            return;
        }

        int score =
            preferredPathBonus;

        List<string> reasons =
            new List<string>();

        if (preferredPathBonus > 0)
            reasons.Add("종류별 테스트 Prefab 경로");

        CharacterAuthoringLink link =
            root.GetComponentInChildren<
                CharacterAuthoringLink>(
                    true);

        if (link != null &&
            link.Bundle == bundle)
        {
            score += 1000;
            reasons.Add("CharacterAuthoringLink.Bundle 일치");
        }

        if (PathContains(
                path,
                bundle.DisplayName))
        {
            score += 180;
            reasons.Add("DisplayName 일치");
        }

        if (PathContains(
                path,
                bundle.Kind.ToString()))
        {
            score += 140;
            reasons.Add("Kind 이름 일치");
        }

        if (PathContains(
                path,
                "TestEncounters/Prefabs"))
        {
            score += 90;
            reasons.Add("테스트 전투 Prefab");
        }

        if (PathContains(
                path,
                "Complete"))
        {
            score += 60;
            reasons.Add("완성 Prefab 이름");
        }

        if (PathContains(
                path,
                "Preview"))
        {
            score -= 300;
            reasons.Add("Preview 감점");
        }

        candidates.Add(
            new Candidate(
                character,
                path,
                score,
                reasons.Count == 0
                    ? "호환 Character Prefab"
                    : string.Join(", ", reasons)));
    }

    private static int CompareCandidates(
        Candidate left,
        Candidate right)
    {
        int scoreCompare =
            right.Score.CompareTo(
                left.Score);

        if (scoreCompare != 0)
            return scoreCompare;

        return string.Compare(
            left.Path,
            right.Path,
            StringComparison.OrdinalIgnoreCase);
    }

    private static string GetPreferredTestPrefabPath(
        CharacterAuthoringKind kind)
    {
        const string root =
            "Assets/2. Data/TestEncounters/Prefabs/";

        return kind switch
        {
            CharacterAuthoringKind.Olaf =>
                root + "Olaf_Test.prefab",

            CharacterAuthoringKind.Yujin =>
                root + "Yujin_Test.prefab",

            CharacterAuthoringKind.Hifumi =>
                root + "Hifumi_Test.prefab",

            CharacterAuthoringKind.EliteEnemy =>
                root + "EliteEnemy_Test.prefab",

            CharacterAuthoringKind.NormalEnemy =>
                root + "NormalEnemy_Test.prefab",

            _ =>
                string.Empty
        };
    }

    private static bool PathContains(
        string path,
        string value)
    {
        return !string.IsNullOrWhiteSpace(path) &&
               !string.IsNullOrWhiteSpace(value) &&
               path.IndexOf(
                   value,
                   StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private sealed class Candidate
    {
        public Candidate(
            Character character,
            string path,
            int score,
            string reason)
        {
            Character = character;
            Path = path;
            Score = score;
            Reason = reason;
        }

        public Character Character { get; }
        public string Path { get; }
        public int Score { get; }
        public string Reason { get; }
    }
}
#endif