public abstract class Enemy : Character
{
    public abstract BattleAction DecideAction(BattleContext context);
    
    protected float CalculateTargetScore(BodyPart part)
    {
        float score = 0f;

        // 이미 부숴진 부위는 대미지가 더 잘 들어간다면 높은 우선순위
        if (part.IsBroken)
            score += 100f;

        // HP가 적을수록 마무리하기 쉬움
        score += part.PartMaxHP - part.PartHP;

        // 속도가 빠른 부위를 우선 제거
        score += part.CurrentSpeed * 3f;

        return score;
    }
}