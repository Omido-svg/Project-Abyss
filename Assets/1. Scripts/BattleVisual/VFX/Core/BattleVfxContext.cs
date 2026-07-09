using UnityEngine;

public class BattleVfxContext
{
    public Character Attacker;
    public Character Target;

    public BodyPart AttackerPart;
    public BodyPart TargetPart;

    public int HitIndex = -1;
    public int Damage = 0;

    public Vector3 WorldPosition;
}