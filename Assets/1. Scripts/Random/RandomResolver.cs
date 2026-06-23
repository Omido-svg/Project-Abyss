using System;
using UnityEngine;

public abstract class RandomResolver
{
    protected Character owner;

    public RandomResolver(Character owner)
    {
        this.owner = owner;
    }

    // 합 수치 계산
    public abstract int Roll();

    // UI에서 보여줄 문자열
    public abstract string GetResultText();
    
}