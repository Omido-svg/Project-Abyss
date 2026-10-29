using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 1029 DMG-06: 흐트러짐 최대치 산정.
///
/// 정본 §6.3 / §12.2:
///   - 부위 보유 대상의 흐트러짐 최대치는 '부위 1개의 최대 체력'이다.
///   - 단일 HP형은 100이다.
///   - OverrideMaxStaggerGauge는 정본의 명시적 예외로만 유지한다.
///
/// 비균일 부위 크기에서 '어느 부위를 기준으로 삼을지'는 정본이 아직 확정하지 않았다.
/// 임의 평균을 금지하므로 **데이터 작성 순서의 첫 유효 부위**를 대표로 사용한다.
/// 정본이 확정되면 <see cref="GetRepresentativePart"/> 한 곳만 교체하면 된다.
/// </summary>
public static class StaggerGaugeMaximumResolver
{
    /// <summary>정본 §12.2: 단일 HP형의 흐트러짐 최대치.</summary>
    public const int SingleHpTargetMaximum = 100;

    public static BodyPart GetRepresentativePart(
        IReadOnlyList<BodyPart> bodyParts)
    {
        if (bodyParts == null)
            return null;

        for (int i = 0; i < bodyParts.Count; i++)
        {
            BodyPart part = bodyParts[i];

            if (part != null && part.MaxPartHP > 0f)
                return part;
        }

        return null;
    }

    public static int Resolve(
        CharacterData data,
        bool usesBodyParts,
        IReadOnlyList<BodyPart> bodyParts,
        out string source)
    {
        source = "Unset";

        if (data == null || !data.EnableStaggerGauge)
            return 0;

        // 정본의 명시적 예외: 데이터가 직접 지정한 값.
        if (data.OverrideMaxStaggerGauge)
        {
            source = "Override";
            return Mathf.Max(1, data.MaxStaggerGauge);
        }

        if (usesBodyParts)
        {
            BodyPart representative =
                GetRepresentativePart(bodyParts);

            if (representative == null)
            {
                // 부위 데이터가 아직 확정되지 않았다. 임의 기본값을 만들지 않는다.
                source = "BodyPartUnset";
                return 0;
            }

            source = $"BodyPart:{representative.Type}";
            return Mathf.Max(1, Mathf.CeilToInt(representative.MaxPartHP));
        }

        source = "SingleHp";
        return SingleHpTargetMaximum;
    }

    public static int Resolve(
        CharacterData data,
        bool usesBodyParts,
        IReadOnlyList<BodyPart> bodyParts)
    {
        return Resolve(data, usesBodyParts, bodyParts, out _);
    }
}
