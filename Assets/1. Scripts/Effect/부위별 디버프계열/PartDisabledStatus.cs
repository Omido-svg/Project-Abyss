// 각 캐릭터 부위별 디버프는 이 클래스를 상속받아 구현
// CreateDisabledDebuff 메서드를 통해서 리턴되는 객체를 다르게 하면됨
public abstract class PartDisabledStatus : StatusEffect
{
    protected PartDisabledStatus(
        BodyPart part,
        string name)
    {
        ownerPart = part;
        Name = name;
        Duration = -1;     // 영구
    }

    public override void OnApply() { }

    public override void OnRemove() { }
}