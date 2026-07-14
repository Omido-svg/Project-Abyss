using UnityEngine;

public class StatusDamageVisualRequest
{
    public Character Source;
    public Character Target;
    public BodyPart TargetPart;

    public int Damage;
    public string StatusKey;

    public bool HasDamageColorOverride;
    public Color DamageColor = Color.white;

    public DamageContext DamageContext;
}
