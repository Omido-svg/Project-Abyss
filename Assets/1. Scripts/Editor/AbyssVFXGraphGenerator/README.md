# Project Abyss AI VFX Graph Generator v0.1.0

고정 대상:
- Unity 6000.0.56f1
- Universal Render Pipeline 17.0.4
- Visual Effect Graph 17.0.4

설치 위치는 이미 ZIP 내부에 포함되어 있다.

`Assets/1. Scripts/Editor/AbyssVFXGraphGenerator/`

Unity 메뉴:

`Tools > Project Abyss > VFX AI > AI VFX Graph Generator`

## 사용 순서

1. Prompt를 작성하거나 Preset을 선택한다.
2. `Preset 적용` 또는 `AI 요청문 복사`를 누른다.
3. AI가 반환한 JSON을 `Recipe JSON`에 붙여넣고 `JSON → UI`를 누른다.
4. Output Folder와 선택적 Seed VFX Asset을 정한다.
5. `Recipe 검증` 후 `VFX Graph 생성`을 누른다.

## 왜 Seed VFX Asset을 쓰는가

VFX Graph는 공개된 범용 Asset 생성 API가 없다. 생성기는 Package/Sample/Project에서 기존 VisualEffectAsset을 자동 탐색하여 복사한 뒤 내부 그래프를 완전히 비우고 다시 작성한다. 자동 Seed 탐색이 실패하면 Unity에서 빈 Visual Effect Graph 하나를 만든 뒤 Seed 슬롯에 지정한다.

## v0.1.0 지원 범위

- 여러 Particle System 생성
- Burst / Constant Rate Spawn
- Capacity
- Lifetime / Size 범위
- Point 또는 Box 분포
- Radial / Directional / None 속도 초안
- Gravity / Drag
- 단색
- Alpha / Additive / Premultiplied Output 설정 시도
- Texture 경로 자동 연결 시도
- `.vfx`와 Prefab 생성
- 내부 API/버전 진단

현재 Directional Spread와 Radial은 범용성과 안정성을 위해 축 정렬 Random Vector Range로 근사한다. 최종 아트 튜닝은 생성된 VFX Graph에서 진행한다.

## 문제 발생 시

`Tools > Project Abyss > VFX AI > Run Compatibility Diagnostics`

결과가 클립보드에 복사된다. Console 오류와 함께 전달하면 내부 API 차이를 바로 추적할 수 있다.

## 주의

이 폴더의 `.asmref`는 스크립트를 `Unity.VisualEffectGraph.Editor` 어셈블리에 포함하여 internal API 접근을 허용한다. 따라서 이 폴더에는 다른 일반 Editor 스크립트를 넣지 않는다.
