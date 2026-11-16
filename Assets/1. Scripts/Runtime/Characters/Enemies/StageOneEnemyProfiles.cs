using System.Collections.Generic;

public static class StageOneEnemyProfiles
{
    public static List<EnemyBodyPartDefinition> BodyParts(StageOneEnemyKind kind)
    {
        bool human = kind == StageOneEnemyKind.Fanatic || kind == StageOneEnemyKind.Inquisitor;
        bool boss = kind == StageOneEnemyKind.Inquisitor || kind == StageOneEnemyKind.MachineBoss;
        var result = new List<EnemyBodyPartDefinition>();
        string[] names = { "머리", "몸통", "왼팔", "오른팔", "다리" };
        for (int i = 0; i < 5; i++)
        {
            int hp = human ? (boss ? (i == 0 ? 190 : 140) : (i == 0 ? 140 : 90)) :
                boss ? (i == 4 ? 210 : 135) : (i == 4 ? 140 : 90);
            result.Add(new EnemyBodyPartDefinition { PartId = "canonical1112_part_" + (i + 1),
                DisplayName = human ? names[i] : "부위 " + (i + 1), MaxPartHP = hp,
                LegacyType = PartType.CUSTOM, SlotRole = BodyPartSlotRole.Hybrid });
        }
        return result;
    }
}
