/// <summary>
/// CharacterAuthoringBundle의 캐릭터 종류별 설정을 적용받는 계약이다.
/// 기존 런타임 생성 구조는 유지하고, Authoring Bundle은 참조를 한 곳에 모으는 역할만 한다.
/// </summary>
public interface ICharacterAuthoringTarget
{
    bool ApplyCharacterAuthoring(CharacterAuthoringBundle bundle);
}
