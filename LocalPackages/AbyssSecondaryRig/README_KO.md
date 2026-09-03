# Project Abyss Secondary Rig for Unity v1.2.0

Blender의 Project Abyss Target/Spring 리깅 JSON을 Unity에서 재사용 가능한 런타임 물리로 연결하는 독립 UPM 패키지입니다.

목표는 **캐릭터마다 코드를 다시 쓰지 않고** 다음 워크플로우를 반복하는 것입니다.

```text
Blender
  TGT / SPR Bone + Weight + JSON
        ↓
Unity Setup Wizard
        ↓
TGT/SPR 자동 연결
Preset 자동 연결
Body Collider 자동 초안
        ↓
Prefab에서 Collider / Preset만 튜닝
        ↓
런타임 Secondary Physics
```

패키지는 Project Abyss의 Battle/Character 코드에 의존하지 않습니다. 다른 Unity 프로젝트에서도 그대로 재사용할 수 있습니다.

---

## 1. 설치

이 폴더를 프로젝트 외부에 보관한 뒤 Unity에서:

```text
Window > Package Manager
+ > Add package from disk...
> ProjectAbyssSecondaryRig/package.json
```

또는 프로젝트의 다음 위치에 복사합니다.

```text
Packages/com.projectabyss.secondary-rig/
```

Unity 6에서 사용하도록 제작했으며 Runtime 코드는 Unity 2022.3+ API 범위로 작성했습니다.

---

## 2. 처음 한 번 설정

1. Blender에서 생성한 `*_secondary_rig.json`을 Unity `Assets` 아래에 넣습니다. `.json`은 Unity에서 `TextAsset`으로 읽힙니다.
2. 캐릭터 Prefab을 준비합니다. FBX Import의 **Optimize Game Objects는 OFF**여야 TGT/SPR Transform을 직접 찾을 수 있습니다.
3. `Tools > Project Abyss > Secondary Rig Setup`을 엽니다.
4. `Character Root / Prefab`에 캐릭터 Prefab을 지정합니다.
5. `Secondary Rig JSON`에 JSON을 지정합니다.
6. `Create / Load Recommended Presets`를 누릅니다.
7. `Analyze`를 누르고 Missing Bone References가 0인지 확인합니다.
8. `Build / Refresh Secondary Rig`를 누릅니다.

Wizard는 Prefab Asset 자체를 지정해도 `LoadPrefabContents`를 통해 직접 구성하고 저장합니다.

---

## 3. Yujin 현재 파일에서 예상되는 결과

제공된 `ABYSS_SECONDARY_RIG_Armature.json`에는 Legacy/Multi-Region 중복 기록이 존재합니다.

패키지는 **Spring Root 이름별로 자동 중복 제거**하며, 같은 Root가 여러 번 기록된 경우 가장 긴 Chain을 선택합니다.

현재 Yujin 데이터의 예상 결과:

```text
Hair_Rigging    : 5 unique chains
Jacket_Rigging  : 10 chains
--------------------------------
Total Dynamic   : 15 chains
```

`NeckTie_Rigging(FITTED)`와 `가면_Rigging(RIGID)`는 Blender에서 이미 Base Weight/고정 Weight가 끝났으므로 Secondary Solver 대상에서 자동 제외됩니다.

JSON의 Object 이름 `Armature`, `Hair_Rigging`이 Unity에서 `.001` 등으로 바뀌어도 상관없습니다. 연결은 실제 TGT/SPR **Bone 이름**을 기준으로 합니다.

---

## 4. 물리 구조

각 Spring Bone은 Rigidbody가 아닙니다. Bone Tip을 가상 Particle로 계산합니다.

```text
Animator / Body Bone
        ↓
TGT Bone = 애니메이션 기준 자세
        ↓
Spring Solver
  - inertia
  - stiffness
  - damping
  - gravity
  - max angle
  - length constraint
  - collision
        ↓
SPR Bone = 실제 SkinnedMesh 변형
```

`SecondaryRigController`는 `DefaultExecutionOrder(10000)`으로 Animator 이후 LateUpdate에서 실행됩니다.

직접 업데이트 순서를 관리하고 싶으면 Controller의 `Update Mode = Manual`로 바꾸고:

```csharp
controller.Simulate(deltaTime);
```

을 원하는 지점에서 호출할 수 있습니다.

---

## 5. Preset Library — 실사용에서 가장 많이 만질 곳

Wizard가 기본적으로 생성하는 위치:

```text
Assets/SecondaryRig/SecondaryRigPresetLibrary.asset
```

기본 Preset:

```text
HAIR
LONG_HAIR
SHORT_HAIR
LONG_COAT
SKIRT
CAPE
RIBBON
ACCESSORY
```

캐릭터마다 15개 Chain을 개별 수정하지 말고 **Preset Library를 튜닝하는 것이 기본 워크플로우**입니다.

주요 값:

- `Stiffness`: TGT 방향으로 돌아가려는 힘
- `Damping`: 관성 감쇠
- `Gravity`: 아래로 처지는 정도
- `Max Angle`: TGT 기준 최대 굽힘 각도
- `Particle Radius`: Secondary-Secondary/Body 충돌에서 사용하는 가상 두께
- `Layer / Collides With`: Hair, Cloth, Body 등 충돌 필터
- `Enable Cross Chain Constraint`: 넓은 코트/치마의 옆 Chain 간 폭 유지

특정 한 가닥만 다르게 만들고 싶을 때만 Controller의 해당 Chain에서 `Use Custom Settings`를 켭니다.

---

## 6. Body Collider

Wizard에서 `Auto-create recommended Humanoid body colliders`를 켜면 다음 Proxy를 자동 생성합니다.

```text
SecondaryRigColliders
├─ Head          Sphere
├─ Torso         Capsule
├─ Hips          Capsule
├─ LeftThigh     Capsule
├─ RightThigh    Capsule
├─ LeftShoulder  Sphere
└─ RightShoulder Sphere
```

이 Collider들은 Unity PhysX Rigidbody/Collider가 아닙니다. Secondary Solver 전용 수학 Proxy라서 가볍고 캐릭터 Rigidbody 구성과 충돌하지 않습니다.

자동 생성 값은 **초안**입니다. Prefab을 열고 각 Collider를 선택하면 Scene View에 Position/Radius Handle이 나타나므로 실제 몸 표면에 맞춰 조정합니다.

추가 Collider는 Hierarchy 우클릭:

```text
Project Abyss > Secondary Rig > Sphere Collider
Project Abyss > Secondary Rig > Capsule Collider
```

로 만들 수 있습니다.

---

## 7. Hair ↔ Jacket / Cloth ↔ Cloth 충돌

각 Spring Bone Tip은 `Particle Radius`를 가진 Sphere Particle로 취급됩니다.

기본 Collision Layer:

```text
Body
Hair
Cloth
Accessory
Custom1
Custom2
```

기본 Preset은:

```text
Hair  ↔ Body / Cloth
Cloth ↔ Body / Hair / 다른 Cloth Part
```

를 허용합니다.

같은 Jacket 안의 10개 Chain은 기본적으로 서로 Particle Collision을 하지 않습니다. 같은 의상 내부에서 모든 Chain을 서로 밀어내면 과도하게 부풀 수 있기 때문입니다.

대신 `LONG_COAT`는 `Cross Chain Constraint`로 인접 Chain 사이의 Rest 폭을 유지합니다.

서로 다른 Dynamic Cloth Part(예: Coat와 Skirt)는 PartName이 다르므로 Cloth↔Cloth 충돌이 동작합니다.

같은 Part 내부에도 충돌이 필요하면 Preset 또는 Chain Override에서 `Collide Within Same Part` / `Enable Self Collision`을 켭니다.

---

## 8. Cross Chain Constraint

넓은 코트는 세로 Chain들이 완전히 독립되면 서로 교차하거나 면이 접힐 수 있습니다.

`LONG_COAT`는 Setup 시 같은 Part/Region의 인접 Chain을 다음처럼 연결합니다.

```text
C01 ●────● C02
    │    │
    ●────●
    │    │
    ●────●
```

가로 Rest Distance를 약하게 유지하면서 세로 Bone Length도 계속 보존합니다.

`Cross Chain Stiffness`가 너무 높으면 딱딱한 판처럼 되므로 0.2~0.4부터 조정하는 것을 권장합니다.

---

## 9. Spawn / Teleport / LOD

Controller는 기본적으로 캐릭터 Root가 한 프레임에 크게 이동/회전하면 Simulation을 TGT 위치로 Reset합니다.

기본값:

```text
Teleport Distance = 0.75m
Teleport Angle    = 75°
```

캐릭터 풀링/Spawn 시에는:

```csharp
secondaryRigController.ResetSimulation();
```

을 호출하면 됩니다.

LOD에서 Controller를 `enabled = false`로 껐다가 다시 켜도 OnEnable에서 다시 초기화됩니다.

---

## 10. 프로젝트 간 재사용 방식

권장 구조:

```text
Packages/
└─ com.projectabyss.secondary-rig/   <- 공용 코드

Assets/
└─ SecondaryRig/
   ├─ SecondaryRigPresetLibrary.asset <- 프로젝트 공용 물성
   └─ Characters/
      ├─ Yujin_secondary_rig.json
      ├─ CharacterB_secondary_rig.json
      └─ ...
```

캐릭터 추가 작업은:

```text
Blender에서 리깅
→ FBX + JSON
→ Unity Wizard
→ Collider 미세조정
→ Preset 선택/튜닝
```

으로 끝내는 것을 목표로 합니다.

---

## 11. Validation

Controller Inspector의:

```text
Validate
Rebuild Runtime
Reset Simulation
Open Setup Wizard
Select Preset Library
```

를 사용할 수 있습니다.

`Validate`에서 확인하는 핵심:

- Target/Spring 배열 길이 일치
- Missing Transform
- Duplicate Spring Root
- 실제 Runtime Chain 수
- Collider 수

Wizard는 Missing Bone이나 동일 이름 Transform이 여러 개 존재하는 경우 잘못된 본을 조용히 선택하지 않고 Build를 중단합니다.

---

## 12. 현재 구현 범위와 의도적인 제한

포함:

- JSON v2 파싱
- Multi-Region/Legacy 중복 Chain 정리
- Target/Spring 자동 Transform 바인딩
- Preset 기반 물성
- Verlet 스타일 Bone-tip spring simulation
- Length / Max Angle constraint
- Sphere / Capsule body collision
- Hair↔Cloth 및 서로 다른 Cloth Part 간 particle collision
- Coat cross-chain width constraint
- Teleport reset
- Manual update mode
- Prefab Asset 직접 Setup
- Collider Scene handles

의도적으로 제외:

- 실제 Skinned Mesh triangle-vs-triangle collision
- 고비용 완전 Cloth self-collision
- PhysX Rigidbody 기반 Secondary Bone

이 세 가지는 게임 캐릭터 실시간 리깅에서 비용 대비 효율이 낮아서 기본 파이프라인에 넣지 않았습니다.

---

## 13. 첫 테스트 권장 순서

Yujin에서는 처음부터 모든 충돌을 평가하지 말고:

```text
1. Wizard Build
2. Play → 머리/몸을 빠르게 회전
3. HAIR 흔들림 확인
4. LONG_COAT 흔들림 확인
5. Head/Shoulder Collider 크기 조정
6. Hips/Thigh Collider 크기 조정
7. Hair ↔ Jacket 충돌 확인
8. LONG_COAT Cross Chain Stiffness 조정
9. 공격/피격/합 이동에서 Teleport Reset 임계값 확인
```

순서로 검수합니다.

---

# v1.1.0 추가 기능

## 11. Collider Scene View 표시 개선

v1.0.0에서는 Collider 자체의 Gizmo와 Custom Editor Handle이 동시에 그려져 Sphere가 두 겹으로 보이거나 Capsule이 여러 Sphere가 겹친 것처럼 보일 수 있었습니다.

v1.1.0부터는 **실제 Solver가 사용하는 충돌 볼륨 하나를 기준으로** 표시합니다.

### Sphere

```text
      .-------.
    /           \
   |      +------[]  Radius Handle 1개
    \           /
      '-------'
```

- 3축 Wire Sphere = 실제 Sphere Collision Volume
- 가운데 Position Handle = Local Center 이동
- 옆의 작은 Handle 하나 = Radius 조절
- 별도의 중복 Sphere Gizmo는 더 이상 그리지 않습니다.

### Capsule

```text
       ______
     /        \
    |          |
 A  +          +  B
    |          |
     \________/
          []      Radius Handle 1개
```

- A/B = Capsule 선분 끝점
- A/B Position Handle로 길이/방향 조절
- 외곽의 Capsule 선이 실제 Solver의 Capsule 영역
- 가운데에 추가 Sphere가 존재하는 것이 아닙니다.

자동 Collider를 다시 `Build / Refresh`하면 `SecondaryRigController > Collider Root`도 가능하면 `SecondaryRigColliders`로 직접 지정됩니다. 기존처럼 Character Root 전체를 지정해도 동작 자체는 동일합니다.

---

## 12. Secondary Rig Stress Test

새 캐릭터를 세팅할 때 Idle/T-Pose만 보고 Spring이 작동하는지 판단하기 어려우므로 재사용 가능한 QA 컴포넌트를 포함합니다.

추가 방법은 셋 중 하나입니다.

```text
SecondaryRigController Inspector
> Add Stress Test Component
```

또는:

```text
Add Component
> Project Abyss
> Secondary Rig
> Diagnostics
> Stress Test
```

또는 Hierarchy 우클릭:

```text
Project Abyss > Secondary Rig > Stress Test
```

### 권장 첫 테스트

```text
Motion Target       = Character Root
Stress Preset       = Strong
Run On Play         = ON
Translation         = ON
Rotation            = ON
```

Strong 기본값:

```text
Translation Amplitude = (0.12, 0.03, 0.08)m
Translation Frequency = 1.15 cycles/sec
Rotation Amplitude    = (8, 35, 12) deg
Rotation Frequency    = 1.35 cycles/sec
```

이 정도는 기본 `Teleport Distance = 0.75m`보다 충분히 작아서 보통 Teleport Reset을 반복 유발하지 않습니다.

### Inspector Quick Target

- `Use Self / Character Root`: 가장 먼저 권장하는 전체 캐릭터 흔들기
- `Use Humanoid Head`: Hair만 강하게 확인할 때 사용

Head Bone을 직접 흔들 경우 Animator의 최종 Head Pose 위에 테스트 Pose를 유지하려는 목적보다는 **순수 Hair QA용**으로 사용합니다. 실제 애니메이션 품질 검수는 Root Stress 또는 실제 Animation Clip으로 확인하는 것을 권장합니다.

### Stress Preset

- `Gentle`: Idle/Walk 수준
- `Strong`: 일반적인 최초 QA 권장
- `Extreme`: 폭주, Max Angle, Collider penetration을 빠르게 찾는 용도

### Play Mode 버튼

```text
Start Stress Test
Stop + Restore
Capture Baseline
```

`Stop + Restore`는 테스트 전 Local Position/Rotation으로 Target을 복구하고 Secondary Solver도 Reset합니다.

### 권장 QA 순서

```text
1. Body Collision OFF
2. Secondary-to-Secondary Collision OFF
3. Strong Stress Test
4. Hair / Jacket의 순수 Spring 움직임 확인
5. Body Collision ON
6. 같은 Stress Test로 Head/Torso/Hips/Thigh 관통 확인
7. Secondary-to-Secondary Collision ON
8. Hair <-> Jacket / Cloth <-> Cloth 확인
9. 마지막에 실제 Walk/Run/Attack Animation으로 검수
```

`SecondaryRigStressTest`의 실행 순서는 8500이며, 자동 Collider Follower(9000)와 Secondary Solver(10000)보다 먼저 LateUpdate에서 실행되도록 구성되어 있습니다.


---

## 15. v1.2 Production Tools

### Collider Duplicate / Mirror

Sphere 또는 Capsule Collider를 선택하면 Inspector 아래에:

```text
Production Tools
├─ Select Follow Target
├─ Auto Fit To Weighted Skinned Mesh
├─ Duplicate
└─ Mirror L/R
```

가 표시됩니다.

`Mirror L/R`은 캐릭터 `Simulation Root`의 local X=0 평면을 기준으로 충돌 형상을 반전하고, Humanoid Avatar에서 Left/Right 대응 Bone을 찾을 수 있으면 `Follow Target`도 자동 교체합니다. 자동 매핑이 실패하면 형상만 반전하므로 Follow Target을 직접 확인하십시오.

Hierarchy 메뉴에서도:

```text
GameObject > Project Abyss > Secondary Rig
├─ Duplicate Selected Collider
└─ Mirror Selected Collider L-R
```

를 사용할 수 있습니다.

### Weighted Skinned-Mesh Auto Fit

`Auto Fit To Weighted Skinned Mesh`는 `Follow Target` Bone의 Skin Weight를 가진 실제 SkinnedMesh Vertex를 샘플링하여 Sphere/Capsule 초안을 맞춥니다.

- Sphere: 영향 Vertex cloud를 감싸는 Sphere 초안
- Capsule: 현재 Capsule 축 또는 Bone→Child 축을 기준으로 영향 Vertex cloud를 감싸는 Capsule 초안
- 아주 작은 Weight는 무시합니다.
- 결과는 항상 Scene View에서 캐릭터 외곽에 맞게 최종 확인합니다.

Controller Inspector의 `Auto Fit All Body Colliders`로 현재 Collider Root 전체를 한 번에 시도할 수도 있습니다.

### Scene Gizmo

```text
Tools > Project Abyss > Secondary Rig > Scene Gizmos
├─ Show All Colliders
└─ Show Labels
```

`Show All Colliders`는 선택되지 않은 Proxy도 옅은 단일 외곽선으로 보여줍니다. 선택된 Collider는 편집 가능한 선 하나 + Position Handle + Radius Handle만 표시합니다.

---

## 16. Distance LOD

`SecondaryRigController > Distance LOD`에서 캐릭터 거리별 비용을 낮출 수 있습니다.

기본값:

```text
0 ~ 10m   Full
10 ~ 25m  Reduced
25 ~ 40m  Minimal
40m+      Disabled
```

권장 기본 동작:

```text
Full
- 원래 Substeps / Constraint Iterations
- Body Collision ON
- Secondary ↔ Secondary ON

Reduced
- 1 Substep / 1 Iteration
- Body Collision ON
- Secondary ↔ Secondary OFF

Minimal
- 1 Substep / 1 Iteration
- Body Collision OFF
- Secondary ↔ Secondary OFF

Disabled
- 진입할 때 SPR을 TGT로 Reset
- Simulation 정지
```

Camera Override가 비어 있으면 `Camera.main`을 사용합니다. Distance Reference가 비어 있으면 Simulation Root를 사용합니다.

---

## 17. Live Performance Statistics

Play Mode에서 `SecondaryRigController` Inspector 아래에 다음이 표시됩니다.

```text
LOD / Distance
Active Chains / Nodes
Body Collider Count
Cross-Chain Constraint Count
Current Substeps / Iterations
Last Simulation ms
Smoothed Simulation ms
```

이 값으로 캐릭터 수가 늘어날 때 LOD 거리와 Collision 옵션을 조정할 수 있습니다.

---

## 18. Production Validation

Controller 또는 Setup Wizard에서:

```text
Validate Production Setup
```

을 실행할 수 있습니다.

검사 항목:

- SecondaryRigController 존재
- Animator / Humanoid 상태
- Preset Library 연결/중복 Preset
- TGT/SPR Binding 유효성
- Duplicate Spring Root
- Character Root 바깥 Transform 참조
- Library에 없는 Preset 이름
- Collider 개수 / 0 Radius
- Collider Follower의 Missing Follow Target
- LOD 거리 순서

`Error=0`을 기준으로 Prefab QA를 통과시키고, Warning은 의도된 설정인지 확인합니다.

---

## 19. Preset Library v2

v1.2부터 Preset Library에 다음 관리 기능이 추가되었습니다.

```text
Add Missing Recommended Presets
Validate Preset Library
Duplicate Preset
Reset ALL to Recommended Defaults
```

업데이트할 때는 `Reset ALL` 대신 **Add Missing Recommended Presets**를 사용해야 기존 프로젝트 튜닝이 유지됩니다.

v1.0/v1.1에서 생성한 Preset asset이 패키지 교체 후 Missing Script로 표시되는 경우, Setup Wizard의 `Create / Load Recommended Presets`로 v2 Preset Library를 한 번 생성해 Controller에 지정하십시오. TGT/SPR Chain과 Collider/Stress Test 데이터는 그대로 유지됩니다.

---

## 20. 권장 Production QA 순서

```text
1. Blender FBX + JSON
2. Setup Wizard Analyze / Build
3. Validate Production Setup
4. Collider Auto Fit 초안
5. Scene View에서 Collider 미세조정
6. Stress Test / Collision OFF → 순수 Spring 확인
7. Body Collision ON → 관통 확인
8. Secondary ↔ Secondary ON → Hair/Cloth 확인
9. 실제 Walk / Run / Attack / Crouch QA
10. LOD 거리 이동 테스트
11. Live Performance ms 확인
12. Spawn/Pooling 시 ResetSimulation() 연결
```
