# FrappyBird_Prototype — 스크립트 문서

Unity 6000.2.8f1 / URP 17.2.0 / Input System 1.14.2 / Cinemachine 2.10.5 / ProBuilder 6.0.9

모든 스크립트 위치: `Assets/3.Script/`

---

## 전체 구조

기차는 **제자리에 떠 있고**, 장애물과 바닥이 기차 쪽으로 흘러옵니다.
플레이어는 마우스 클릭으로 고도만 조절하며, 장애물에 부딪히면 그 지점 뒤의 객차가 분리·추락합니다.

(선택 사항) 맵이 직선이 아니라 곡선으로 휘어야 한다면 8~10번의 `MapPath` / `MapChunkSpawner` / `TrainPathFollower`로 대체·병행할 수 있습니다 — 이 경우 기차가 실제로 좌표를 따라 앞으로 나아갑니다.

```
[ 조작 ]                    [ 편성 ]                      [ 월드 ]
TrainController  ──────▶  TrainConsist  ──▶ TrainCar     ScrollController
 (클릭 점프/기울기)         (편성 관리)      (차량 1량)      (속도·방향 기준)
                                │                              │
                                ▼                    ┌─────────┴─────────┐
                          TrainCoupler          ObstacleSpawner   GroundScroller
                          (연결부, 인덱스)        (장애물 스폰/풀)   (바닥 무한반복)
```

---

## 1. TrainController.cs — 기관차 조작

마우스 왼쪽 클릭으로 점프하고, 수직 속도에 따라 앞머리를 기울입니다.

| 항목 | 내용 |
|---|---|
| 부착 위치 | 기관차 (Rigidbody, TrainCar와 함께) |
| 입력 | `Mouse.current.leftButton.wasPressedThisFrame` (새 Input System) |

**주요 Inspector 값**

- `Jump Height` (10) — 클릭 시 올라갈 높이. `v = √(2gh)` 공식으로 초기 속도를 계산하므로 이 값이 곧 실제 상승 높이
- `Consist` — 비워두면 TrainConsist가 자동으로 연결
- `Path Follower` — 맵 좌표를 따라 움직이는 경우 `TrainPathFollower`를 여기에 등록. 비워두면 처음 바라보던 방향(직선)을 그대로 유지
- `Control Rotation` (true) — 스크립트가 회전을 전담. 끄면 Rigidbody의 Freeze Rotation을 직접 관리 가능
- `Nose Up When Rising` (true) — 상승 시 앞머리가 위로. 모델 방향이 반대면 해제
- `Max Pitch Angle` (25) / `Velocity For Max Pitch` (8) / `Pitch Smooth Speed` (6)

**동작 요점**

- 점프 시 기관차뿐 아니라 **매달린 객차 전원에게 같은 y속도**를 넣습니다. 기관차만 튀면 연결부가 잡아채여 떨림이 생깁니다.
- 회전은 `transform`이 아닌 `rb.MoveRotation()`으로 처리 — 조인트와 충돌하지 않기 위함
- `[RequireComponent(typeof(TrainCar))]` — 기관차도 편성의 0번 차량이라 반드시 필요

---

## 2. TrainCar.cs — 차량 1량

편성 내 위치(`CarIndex`)를 가지며, 분리 시 물리적으로 추락합니다.

**공개 API**

```csharp
car.CarIndex      // 0 = 기관차
car.IsDetached    // 이미 떨어진 차량인지
car.Body          // Rigidbody
car.Length        // 모델의 앞뒤 길이 (간격 계산용)
car.Detach();     // 앞 연결부를 끊고 추락
car.Collided += (car, collision) => { };  // 충돌 이벤트
```

**자동 처리**

- 자식 메시들의 실제 크기를 재서 **BoxCollider를 자동 부착** (콜라이더가 하나도 없을 때만)
- 앞/뒤 **연결 지점을 모델 끝단에서 자동 계산** — 씬 뷰에서 차량 선택 시 하늘색(앞)·노란색(뒤) 구체로 표시
- 조인트 안정화: `solverIterations 16`, `Interpolate`
- 질량을 전 차량 동일하게(기본 1) 맞춤

**분리 연출** — `Detach Impulse`(1.5), `Detach Torque`(4), `Destroy Delay`(6초, 0이면 유지)

---

## 3. TrainCoupler.cs — 연결부

`ConfigurableJoint`를 감싸 인덱스를 부여한 연결부입니다.

**★ 인덱스 규칙 (가장 중요)**

> 연결부 인덱스 `i` = `car[i-1]`(앞)과 `car[i]`(뒤)를 잇는 연결부
> 즉 **연결부 번호 = 그게 끊겼을 때 가장 먼저 떨어지는 차량 번호**

**CouplerSettings (TrainConsist Inspector에서 조절)**

- `Break Force` / `Break Torque` (0 = 무한, 끊어지지 않음) — 값을 주면 힘을 못 이겨 저절로 끊어짐
- `Pitch Swing Limit` (12°) / `Yaw Swing Limit` (12°) — 연결부가 꺾일 수 있는 각도
- `Slack` (0) — 0이면 완전 고정, 값을 주면 로프처럼 헐렁하게

**조인트 설정 근거**

- 위치 Locked / 회전은 pitch·yaw 제한 스윙, roll 잠금 → 점프할 때 뒤 차량이 자연스럽게 따라 들림
- `projectionMode = PositionAndRotation`, `enablePreprocessing = false` → 늘어짐·떨림 억제
- 조인트는 Rigidbody와 같은 GameObject에만 붙으므로 **연결부는 항상 뒤쪽 차량에 올라갑니다**

씬 뷰에 연결 상태가 선으로 표시됩니다. **초록 = 연결됨, 빨강 = 끊김.**

---

## 4. TrainConsist.cs — 편성 관리자

객차 자동 생성, 연결부 배선, 충돌 → 분리 판정을 담당합니다.

**공개 API**

```csharp
consist.BreakCouplerAt(2);   // 2번 차량부터 끝까지 추락
consist.DetachFrom(2);       // 동일 (읽기 편한 별칭)
consist.DetachLastCar();     // 마지막 한 량만
consist.GetCar(index);
consist.AttachedCarCount;    // 아직 붙어 있는 차량 수

consist.CouplerBroken     += index => { };        // 점수 차감·사운드·파티클 연결 지점
consist.LocomotiveCrashed += (car, col) => { };   // 게임오버 연결 지점
```

**Inspector**

- `Locomotive` — 기관차. **TrainCar가 있어야 슬롯에 드롭됩니다**
- `Car Prefab` / `Car Count` (3) — 객차 프리팹과 수
- `Gap` (0.3) — 차량 사이 여유 거리
- `Obstacle Layers` (기본 Nothing) — ⚠️ 아래 주의사항 참조
- `Debug Break Index` — 우클릭 컨텍스트 메뉴 **"테스트: 연결부 끊기"** 로 플레이 중 분리 테스트

**동작 요점**

- 객차 배치는 spacing 추정이 아니라 **모델의 실제 연결 지점끼리 맞물리도록** 계산 → 피벗이 중앙이 아니어도 정확
- 연결된 차량끼리 `Physics.IgnoreCollision` 적용 (조인트 떨림의 주원인 제거)
- 편성 완성 후 TrainController에 자기 자신을 자동 배선
- 기관차(0번)가 부딪히면 `LocomotiveCrashed` 발생 후 편성 전체 붕괴

---

## 5. ScrollController.cs — 스크롤 기준점

속도와 방향의 **단일 기준**. 장애물과 바닥이 모두 여기서 값을 읽어가므로 항상 같은 속도로 움직입니다.

```csharp
scroll.Speed            // 현재 속도 (정지 시 0)
scroll.DeltaDistance    // 이번 프레임 이동 거리
scroll.Forward          // 기차가 바라보는 방향 (장애물 생성 쪽)
scroll.MoveDirection    // 장애물이 흘러오는 방향
scroll.Origin           // 레인 기준 원점
scroll.DistanceAlongAxis(pos);   // 원점 기준 앞(+)/뒤(-) 거리

scroll.SetSpeed(40f);
scroll.Stop();     // 게임오버 시 — 전체가 한 번에 멈춤
scroll.Resume();
```

**배치** — 빈 GameObject를 기차와 같은 위치·같은 회전으로. 씬 뷰의 하늘색 화살표가 장애물이 날아오는 방향입니다.

---

## 6. ObstacleSpawner.cs — 장애물 무한 스폰

**Inspector**

| 항목 | 기본값 | 설명 |
|---|---|---|
| `Obstacle Prefabs` | — | 여러 개 넣으면 무작위로 골라 배치 |
| `Spacing` | 40 | 장애물 간격(m). 속도와 무관하게 유지 |
| `Spawn Distance` | 200 | 앞쪽 생성 거리 |
| `Despawn Distance` | 60 | 뒤쪽 회수 거리 |
| `Prewarm` | true | 시작 시 레인을 미리 채움 |
| `Min Y` / `Max Y` | 2 / 40 | 높이 랜덤 범위 |
| `Random Roll` | false | 진행 축 기준 무작위 회전 |

**동작 요점**

- 간격을 시간이 아니라 **누적 이동 거리**로 계산 → 나중에 난이도 곡선으로 속도를 올려도 간격이 흐트러지지 않음
- 파괴 대신 **오브젝트 풀링** (프리팹별 Queue)
- 스폰된 장애물에 **키네마틱 Rigidbody 자동 부착** — 콜라이더를 매 프레임 움직일 때 물리엔진이 정적 구조를 재구성하지 않게 하기 위함
- `ResetSpawner()` — 전부 회수 후 처음부터 (재시작용)

---

## 7. GroundScroller.cs — 바닥 무한 반복

타일 여러 장을 이어 붙여 흘려보내고, 뒤로 빠진 타일을 맨 앞으로 되돌립니다.

- `Tile Prefab` / `Tile Count` (6) / `Tile Length` (0 = 프리팹 크기 자동 측정)
- `Ground Y` (0) / `Start Behind` (60)

---

## 8. MapPath.cs — 맵 조각에 찍는 경로 좌표

Group 같은 맵 프리팹 루트에 붙여, 기차가 따라갈 좌표(진행 방향 순서)를 정의합니다.

- `Points` — 순서대로 등록할 빈 오브젝트 배열. 비워두면 `Points Container`(또는 자기 자신)의 자식들을 하이러키 순서 그대로 자동 수집
- 씬 뷰에서 보라색 구슬 + 번호로 순서가 보입니다.
- **이음매 요령**: 이 조각의 첫 좌표는 로컬 Z ≈ 0, 마지막 좌표는 로컬 Z ≈ (진행축 길이) 부근에 두세요. `MapChunkSpawner`가 조각을 정확히 그 길이만큼 이격해 배치하므로, 그렇게 두면 앞 조각의 마지막 좌표와 다음 조각의 첫 좌표가 자연스럽게 이어집니다.

---

## 9. MapChunkSpawner.cs — 맵(Group) 무한 이어붙이기

Group 프리팹을 진행 방향으로 이어 붙여 끝없는 트랙을 만듭니다. `GroundScroller`와 달리 조각들은 흐르지 않고 **제자리에 고정**되며, 기차가 그 안의 `MapPath` 좌표를 따라 실제로 앞으로 나아갑니다.

**Inspector**

| 항목 | 기본값 | 설명 |
|---|---|---|
| `Chunk Prefab` | — | Group 프리팹. 루트에 `MapPath`가 있어야 함 |
| `Chunk Count` | 4 | 동시에 유지할 조각 수 |
| `Chunk Length` | 0 | 조각 하나의 진행축 길이(m). 0이면 렌더러 크기로 자동 측정. `Tools ▸ 맵 크기 측정 (Group)`으로 미리 잰 값을 직접 넣어도 됨 |
| `Axis Reference` | — | 이어붙일 기준 방향. 비워두면 자기 자신의 forward |
| `Despawn Behind` | 40 | 기차보다 이만큼 뒤처지면 조각을 맨 앞으로 재배치 |
| `Train` | — | 비워두면 씬에서 `TrainPathFollower` 자동 탐색 |

**동작 요점**

- 조각을 정확히 `Chunk Length`만큼 이격해 배치 → 겹치거나 틈이 생기지 않음
- 조각이 재배치될 때마다 살아있는 조각들의 `MapPath` 좌표를 진행 순서로 이어 붙여 `TrainPathFollower.SetPath()`에 넘김
- ⚠️ 진행 축 하나를 기준으로 앞뒤를 판정하므로, 완만한 커브(좌우로 살짝 휘는 정도)에는 잘 맞지만 **90도 이상 크게 꺾이거나 루프 형태의 트랙**에는 맞지 않습니다. 그런 트랙이 필요해지면 회수 판정을 경로 누적거리 기준으로 바꿔야 합니다.

---

## 10. TrainPathFollower.cs — 좌표를 따라 움직이는 기차

기차가 일직선이 아니라 `MapChunkSpawner`가 넘겨준 좌표들을 따라 움직이게 합니다. 같은 Rigidbody에서 **좌우/앞뒤 진행**만 담당하고, 점프(y축)·피치는 기존 `TrainController`가 그대로 담당합니다.

```csharp
follower.SetPath(worldPoints);   // MapChunkSpawner가 자동 호출
follower.CurrentYaw              // TrainController가 회전에 합성
follower.CurrentForward
follower.DistanceTraveled
```

**Inspector**

- `Scroll` — 비워두면 `Speed` 값을 직접 사용. 채워두면 `ScrollController.Speed`를 우선 사용해 장애물·바닥과 속도를 맞춤
- `Speed` (30)
- `Look Ahead Distance` (6) — 현재 지점보다 얼마나 앞을 보고 방향을 잡을지. 작으면 좌표를 칼같이 따라가지만 급커브에서 흔들리고, 크면 코너를 부드럽게 자르며 돎
- `Max Turn Speed` (120°/s) — 급커브에서 순간적으로 홱 꺾이지 않도록 회전 속도 제한

**동작 요점**

- 매 FixedUpdate마다 진행 거리를 누적하고, 경로 상의 look-ahead 지점을 향하도록 수평 속도(x, z)만 설정 — 조인트로 매달린 객차들은 물리적으로 자연스럽게 따라옴(코너를 살짝 안쪽으로 자르는 실제 기차 같은 움직임)
- 경로가 비어 있으면(아직 `MapChunkSpawner`가 안 채워줬으면) 대기

---

## 씬 세팅 절차

1. **기관차** — 씬의 train에 `Rigidbody` → `TrainCar` → `TrainController` 추가
2. **편성 관리자** — 빈 GameObject에 `TrainConsist` 부착 (**기관차에 붙이면 안 됨**: 객차가 자식이 되어 물리가 깨짐)
   - Locomotive에 기관차, Car Prefab에 객차 프리팹, Car Count 입력
3. **스크롤** — 빈 GameObject를 기차와 같은 위치·회전으로 두고 `ScrollController` + `ObstacleSpawner` + `GroundScroller` 부착
   - Obstacle Prefabs에 링 게이트, Tile Prefab에 바닥 판 등록
   - **기존 고정 Plane은 씬에서 제거** (흐르는 타일과 겹침)
4. **레이어** — 장애물 전용 레이어를 만들어 프리팹에 지정하고, TrainConsist의 `Obstacle Layers`에 그 레이어만 체크
5. **카메라** — Cinemachine Virtual Camera 생성 → Follow = 기관차, Look At = 앞머리에 둔 빈 오브젝트
   - Body: Transposer, Binding Mode **Lock To Target With World Up** (Lock To Target은 피치까지 따라가 멀미남)
   - Aim: Composer + Dead Zone Height 0.2~0.3 (피치로 인한 까딱거림 억제)
   - Brain의 Update Method는 기본값 Smart Update 유지
6. **(선택) 맵 좌표 경로 추종** — 기차를 직선이 아니라 Group에 찍은 좌표대로 움직이려면
   - Group 프리팹 루트에 `MapPath` 부착 → 진행 방향 순서로 빈 오브젝트를 자식으로 만들어 경로 좌표로 등록
   - 기관차(Rigidbody가 있는 오브젝트)에 `TrainPathFollower` 부착
   - 빈 GameObject에 `MapChunkSpawner` 부착 → Chunk Prefab에 Group, Train 슬롯에 위 `TrainPathFollower` 등록
   - `TrainController`의 `Path Follower` 슬롯에도 같은 `TrainPathFollower`를 등록해야 좌우 회전(yaw)이 경로를 따라 꺾임
   - 정확한 이격 거리가 궁금하면 `Tools ▸ 맵 크기 측정 (Group)` 메뉴 실행 → Console에서 size.x/y/z 확인

---

## ⚠️ 함정 모음 (실제로 겪은 것들)

**Obstacle Layers에 바닥을 넣지 말 것**
초기 기본값이 Everything이었을 때, 열차가 Plane에 닿는 순간 전 차량이 분리되고 랜덤 토크로 옆으로 눕는 버그가 있었습니다. 기본값을 Nothing으로 변경해 해결.

**train.fbx의 Generate Colliders는 반드시 OFF**
켜면 파츠 64개에 concave MeshCollider가 붙어 *"Concave Mesh Colliders are not supported when used with dynamic Rigidbody"* 경고가 64개 발생합니다. 차량당 BoxCollider 1개면 충분.

**링 게이트에 Convex MeshCollider를 쓰면 구멍이 막힘**
Convex는 볼록 껍질이라 오목한 구멍을 표현하지 못해, 겉보기엔 뚫려 있는데 물리적으로는 꽉 찬 원반이 됩니다. 통과가 안 됩니다.

**논-convex MeshCollider는 트리거가 될 수 없음**
PhysX가 삼각형 메시를 트리거 shape으로 허용하지 않습니다. 따라서 "테두리는 막히고 가운데는 통과 감지"를 **콜라이더 하나로는 만들 수 없습니다.**
→ 실체(논-convex MeshCollider 또는 캡슐 여러 개) + 구멍 트리거(Sphere/Box + Is Trigger) **2개로 분리**하고, 서로 다른 레이어를 줄 것.

**모든 차량의 질량을 동일하게 유지**
질량이 다르면 조인트가 불안정해집니다.

**기관차 Freeze Rotation은 TrainController가 관리**
Rigidbody에서 수동으로 만지려면 `Control Rotation`을 끄세요.

---

## 미구현 (기획서 대비 남은 것)

- 방향키 좌우 조작(경로 기준 좌우 스티어링·장애물 회피) / 연료 시스템 / 점수 시스템
- 링 게이트 통과 판정 (트리거 + 진행 방향 내적 검사로 역방향 통과 배제)
- 공중 ↔ 수중 전환, 캐릭터 변신
- 난이도 커브 (ScrollController.SetSpeed로 속도 상승)
- UI/HUD, 사운드
