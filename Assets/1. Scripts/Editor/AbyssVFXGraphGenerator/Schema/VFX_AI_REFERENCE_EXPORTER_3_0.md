# Project Abyss VFX AI Reference Exporter 3.0

## 목표

VFX Graph의 내부 C# 타입(`VFXGraph`, `VFXModel`, `VFXSlot`)에 컴파일 타임으로 의존하지 않고, AI가 기존 `.vfx`의 구조와 시각 결과를 재구성할 수 있는 패키지를 만든다.

## 캡처 계층

1. `VisualEffectAsset` 공개 API
   - Exposed Properties
   - Events
2. `AssetDatabase.LoadAllAssetsAtPath`
   - 로드 가능한 Main/Sub Asset의 실제 Runtime Type과 Local File ID
3. `SerializedObject`
   - 숨김 프로퍼티를 포함해 읽을 수 있는 모든 SerializedProperty
   - 같은 VFX 내부 오브젝트 연결과 외부 ObjectReference
4. 원본 Unity YAML fallback
   - 모든 `--- !u!<ClassID> &<LocalID>` 문서
   - `m_Script` GUID, Parent/Children, Flow, Slot, Data, Shader/Texture/Mesh/Subgraph 참조
   - 오브젝트별 원문 분할 파일
5. Dependency GUID scan
   - AssetDatabase 직접/재귀 의존성
   - YAML 문자열 및 직렬화 JSON 내부 GUID
6. Preview Scene 렌더
   - 고정 Seed
   - 여러 Simulation Time
   - Dark/Light Background
   - Contact Sheet

Unity가 새 VFX Graph 노드나 기능을 추가해 capability 이름 사전에 없더라도, 원본 YAML과 모든 직렬화 프로퍼티가 보존되므로 정보가 사라지지 않는다.

## 출력

- `VFX_REFERENCE.json`
- `VFX_REFERENCE.txt`
- `GRAPH_SUMMARY.md`
- `GRAPH_NODES.csv`
- `GRAPH_EDGES.csv`
- `SERIALIZED_PROPERTIES.csv`
- `GRAPH.mmd`
- `GRAPH.dot`
- `CAPABILITY_MATRIX.md`
- `YAML_OBJECTS/*.yaml`
- `RAW/*.vfx.txt`, `RAW/*.meta.txt`
- `DEPENDENCY_MANIFEST.json`
- `DEPENDENCIES/Assets/...`, `DEPENDENCIES/Packages/...`
- `DEPENDENCY_PREVIEWS/*.png`
- `PREVIEW/FRAME_*.png`
- `PREVIEW/CONTACT_SHEET.png`
- `CLONE_TEMPLATE_RECIPE.json`
- `AI_REFERENCE_PROMPT.txt`
- `PACKAGE_MANIFEST.json`

## 상태 판정

- `READY`: 원본 YAML을 포함하고 YAML 문서의 90% 이상이 구조화 Node로 표현되며 관계도 추출됨.
- `PARTIAL`: 원본은 보존됐지만 일부 타입/관계의 구조화가 부족함.
- `FAILED`: 원본 `.vfx`를 읽지 못했거나 그래프 오브젝트를 전혀 표현하지 못함.

Preview 실패는 구조 캡처 실패와 별개다. Scene Binding, Exposed Object, 특정 Event가 필요한 VFX는 자동 Preview가 실패할 수 있지만 원본과 그래프 구조는 계속 `READY`일 수 있다.
