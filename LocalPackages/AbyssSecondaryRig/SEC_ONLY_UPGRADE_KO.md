# Project Abyss Secondary Rig v1.3.0 — SEC-only 업그레이드

## 목적

Blender `project_abyss_garment_rig_v0_8_1_wiggle2`의 권장 런타임 구조를 그대로 Unity에서 사용합니다.

```text
Blender
Secondary Workflow = Unity Runtime Physics
Bone Structure      = Single Bone / SEC Only

Body Bone
└─ *_SEC_C01_01
   └─ *_SEC_C01_02
      └─ *_SEC_C01_03

FBX + *_secondary_rig.json
        ↓
Unity AbyssSecondaryRig v1.3
        ↓
Virtual Target + SEC Runtime Physics
```

별도 TGT Bone을 FBX에 넣지 않습니다.

## Virtual Target 원리

SEC-only에서는 Solver가 현재 SEC 회전을 그대로 목표로 사용하면 Physics가 자기 결과를 다시 목표로 읽는 피드백 문제가 생깁니다.

v1.3은 Setup 시 각 SEC Bone의 `localPosition/localRotation/localScale`을 Reference Pose로 저장합니다. 매 프레임 Animator가 Body Parent를 움직인 뒤:

```text
Animated Body Parent World Matrix
× SEC Root Rest Local Matrix
× SEC Child Rest Local Matrix ...
= Virtual Target Pose
```

를 재구성합니다.

따라서 Animator에는 일반 Body Animation만 있고, SEC Bone은 `LateUpdate`의 Secondary Solver가 최종 회전을 적용합니다.

## 설치

이 ZIP을 Project Abyss 저장소 루트에 풀어 기존 파일을 덮어씁니다.

```text
<Project-Abyss>/
└─ LocalPackages/
   └─ AbyssSecondaryRig/
```

`Packages/manifest.json`의 Local Package 연결은 기존 경로를 그대로 사용하므로 다시 Add package from disk 할 필요가 없습니다.

Unity가 재컴파일한 뒤 Package Manager/Inspector에서 버전은 `1.3.0`입니다.

## Blender Export

Blender 애드온:

```text
Secondary Workflow            = Unity Runtime Physics
Bone Structure                = Single Bone / SEC Only
Export Runtime JSON Manifest  = ON
```

`Export Character FBX` 실행 결과:

```text
Yujin_CHARACTER.fbx
Yujin_CHARACTER_secondary_rig.json
```

v3 JSON의 SINGLE chain 예:

```json
{
  "boneStructure": "SINGLE",
  "targetRoot": "",
  "secondaryRoot": "HAIR_SEC_C01_01",
  "springRoot": "HAIR_SEC_C01_01",
  "targetBones": [],
  "secondaryBones": [
    "HAIR_SEC_C01_01",
    "HAIR_SEC_C01_02",
    "HAIR_SEC_C01_03"
  ],
  "springBones": [
    "HAIR_SEC_C01_01",
    "HAIR_SEC_C01_02",
    "HAIR_SEC_C01_03"
  ]
}
```

## Unity Setup

1. FBX와 JSON을 `Assets` 아래에 넣습니다.
2. FBX의 Optimize Game Objects는 Secondary Transform을 직접 찾아야 하므로 우선 OFF 권장입니다.
3. `Tools > Project Abyss > Secondary Rig Setup`
4. Character Root / Prefab 지정
5. Secondary Rig JSON 지정
6. `Analyze`
7. 아래가 정상인지 확인:

```text
SEC-only / virtual target: N
Missing bone references: 0
Ambiguous transform names: 0
```

8. `Build / Refresh Secondary Rig`
9. `Validate Production Setup`
10. Stress Test 또는 실제 Walk/Run/Attack으로 확인

## 기존 v2 / Dual 호환

기존 JSON:

```text
TGT Bone + SPR/SEC Bone
project-abyss.secondary-bone-rig.v2
```

도 그대로 작동합니다. DUAL chain은 기존처럼 실제 TargetBones를 사용합니다.

따라서 기존 Yujin `ABYSS_SECONDARY_RIG_Armature.json`을 즉시 폐기할 필요는 없습니다. 새 Blender SEC-only 리그부터 v3 SINGLE을 사용하면 됩니다.

## 기존 Controller를 v1.3 SEC-only로 바꿀 때

패키지 파일만 교체한 것으로 기존 Controller의 Chain Binding이 자동으로 SINGLE로 변환되지는 않습니다.

새 v3 JSON을 지정하고:

```text
Build / Refresh Secondary Rig
```

을 한 번 실행하십시오. 기존 Chain의 Custom Settings는 같은 Secondary Root 이름이면 유지됩니다.

## 확인 포인트

- SEC Root가 실제 Body Bone 아래에 Parent되어 있어야 합니다.
- JSON `parentBone`이 Unity FBX hierarchy에 존재해야 합니다.
- SEC Bone 이름은 Character Root 아래에서 유일해야 합니다.
- 일반 Gameplay Clip에는 SEC Animation Curve를 Bake하지 않는 것을 권장합니다.
- Cutscene처럼 Secondary를 이미 Bake한 Clip을 쓸 때는 Runtime Secondary Controller를 끄거나 해당 Chain을 비활성화해야 이중 적용을 피할 수 있습니다.


---

## v1.3.1 - 0 usable runtime chains 수정

Blender v0.8.1에서 이미 만든 Region은 `Secondary Workflow` UI를 나중에
`Unity Runtime Physics`로 바꿔도 Region Record 안의 `secondary_mode`가
기존 `OFFLINE_BAKE`로 남아 있을 수 있습니다.

v1.3.0은 이 메타데이터를 엄격하게 필터링했기 때문에, 실제 SEC-only chain이
정상이어도 Unity Setup Wizard에서 0 chain으로 탈락할 수 있었습니다.

v1.3.1부터는:
- Setup Wizard에 JSON을 직접 지정한 행위를 runtime intent로 취급합니다.
- DYNAMIC chain의 구조가 유효하면 workflow 문자열이 오래된 값이어도 Build합니다.
- Analyze에는 Offline metadata 개수를 표시하여 Blender 쪽 기록 상태는 확인할 수 있습니다.

Blender 쪽을 깔끔하게 맞추려면 이후 리깅/업데이트 시:
`Secondary Workflow = Unity Runtime Physics`
`Bone Structure = Single Bone / SEC Only`
상태에서 `Build / Update Current Region`을 사용하십시오.
기존 리깅을 이 오류 때문에 다시 만들 필요는 없습니다.
