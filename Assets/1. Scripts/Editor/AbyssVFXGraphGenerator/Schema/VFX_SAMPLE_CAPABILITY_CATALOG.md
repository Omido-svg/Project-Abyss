# Project Abyss VFX Graph 17 Capability Catalog

기준 문서: 사용자가 제공한 31개 VFX 샘플 콘텐츠 문서의 본문과 이미지.

## 지원 원칙

- **Structured Recipe**: Burst/Rate, Capacity, Lifetime, Size, Position, Velocity, Gravity, Drag, Quad/Mesh Output, Texture, Flipbook 메타데이터, Scene Mesh Object, Exposed Texture/Mesh/Color/수치 기본값을 빠르게 생성한다.
- **CloneTemplate**: 기존 `.vfx`를 원본 그래프째 복제하므로 Context, Block, Operator, 링크, Shader Graph, GPU Event, Strip, Decal, SDF 등 VFX Graph가 직렬화한 기능을 제거하지 않는다.
- Structured 생성기가 아직 직접 합성하지 않는 고급 노드는 **기존 Reference VFX를 Export → AI 분석 → CloneTemplate 변형** 절차로 사용한다.

## 샘플 문서별 사용 가능 범위

1. Contexts & Data Flow — CloneTemplate 전체 보존
2. Spawn Context — Structured + CloneTemplate
3. Capacity — Structured + CloneTemplate
4. Multiple Outputs — CloneTemplate
5. Bounds — CloneTemplate
6. Orient Face Camera — Structured 기본 Quad / CloneTemplate 세부 설정
7. Orient Fixed Axis — CloneTemplate
8. Orient Advanced — CloneTemplate
9. Rotation Angle — CloneTemplate
10. Rotation & Angular Velocity — CloneTemplate
11. TexIndex Attribute — CloneTemplate
12. Flipbook Mode — Structured 메타데이터 + CloneTemplate 실제 노드
13. Flipbook Blending — Structured 메타데이터 + CloneTemplate 실제 노드
14. TexIndex Advanced — CloneTemplate
15. Pivot Attribute — CloneTemplate
16. Pivot Advanced — CloneTemplate
17. Sample Mesh — CloneTemplate; Mesh 에셋 경로는 Recipe/Reference에 기록
18. Sample Texture 2D — CloneTemplate; Texture 의존성/Preview Export
19. Sample Signed Distance Field — CloneTemplate
20. Sampled Skinned Mesh — CloneTemplate
21. Collision Properties — CloneTemplate
22. Collision Simple — CloneTemplate
23. Collision Advanced — CloneTemplate
24. Trigger Event on Collide — CloneTemplate
25. Decal Particles — CloneTemplate
26. Strip Properties — CloneTemplate
27. Strip Spawn Rate — CloneTemplate
28. Multi-Strip Spawn Rate — CloneTemplate
29. Multi-Strip Single Burst — CloneTemplate
30. Multi-Strip Periodic Burst — CloneTemplate
31. Strip GPU-Event — CloneTemplate

## 3D Mesh와 Texture

- VFX Mesh Output: `meshAssetPath`와 VFX 호환 Shader Graph/Exposed Texture를 사용한다.
- 고유 Scene Object: `sceneObjects[].meshAssetPath` + `materialAssetPath`를 사용한다.
- Material에 Texture를 미리 지정해도 되며, VFX Graph 안에서 바꿀 값은 Blackboard에 Expose한 뒤 `exposedOverrides`로 Texture/Mesh/Color를 넣는다.
- 석상 낙하처럼 하나의 고유 오브젝트가 긴 시간 존재하는 연출은 Scene Mesh Object + Timeline/Animator가 적합하다.
- 수백 개의 돌 파편처럼 GPU에서 다량 생성하는 경우는 VFX Mesh Output이 적합하다.
