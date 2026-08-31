using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 열차 편성(編成) 전체를 관리합니다. 기관차 뒤로 객차를 자동 생성하고, 각 차량 사이에
/// 인덱스를 가진 연결부(TrainCoupler)를 만듭니다.
///
/// <b>인덱스 규칙</b>
/// - 차량 인덱스: 0 = 기관차, 1, 2, 3... 뒤로 갈수록 증가
/// - 연결부 인덱스 i = car[i-1](앞)과 car[i](뒤)를 잇는 연결부
///   → BreakCouplerAt(2)를 호출하면 2번 차량부터 뒤로 전부 떨어집니다.
///
/// 이 컴포넌트는 기관차가 아닌 <b>빈 GameObject</b>에 붙이세요.
/// (기관차에 붙이면 생성된 객차가 기관차의 자식이 되어 물리가 어긋납니다.)
///
/// [수정 내역] 무적시간 추가: 한 번 데미지를 받으면 invincibilityDuration 동안
/// 추가 충돌을 무시합니다. 콜라이더 여러 개가 겹쳐서 한 프레임에 여러 번 부딪히거나,
/// 좁은 장애물 구간에서 연속으로 맞아 순식간에 편성 전체가 사라지는 걸 막기 위함입니다.
/// </summary>
public class TrainConsist : MonoBehaviour
{
    [Header("편성 구성")]
    [Tooltip("맨 앞 기관차. TrainController가 붙어 있는 오브젝트입니다.")]
    [SerializeField] private TrainCar locomotive;
    [Tooltip("뒤에 붙일 객차 프리팹. TrainCar가 없으면 자동으로 추가됩니다.")]
    [SerializeField] private GameObject carPrefab;
    [Tooltip("기관차를 제외한 객차 수. 3이면 총 4량 편성이 됩니다.")]
    [SerializeField, Min(0)] private int carCount = 3;
    [Tooltip("차량 사이를 벌릴 거리(m). 위치는 모델의 실제 연결 지점을 기준으로 자동 계산됩니다.")]
    [SerializeField] private float gap = 0.3f;

    [Header("연결부 설정")]
    [SerializeField] private CouplerSettings couplerSettings = new CouplerSettings();

    [Header("충돌 판정")]
    [Tooltip("이 레이어에 부딪혔을 때만 연결부가 파손됩니다.\n" +
             "★ 바닥/지형은 절대 포함하지 마세요. 포함하면 착지하자마자 편성이 전부 분리됩니다.\n" +
             "비워두면(Nothing) 충돌 분리가 비활성화되고, 코드로만 분리할 수 있습니다.")]
    [SerializeField] private LayerMask obstacleLayers = 0;
    [Tooltip("한 번 부딪힌 뒤 이 시간(초) 동안은 추가 충돌을 무시합니다. " +
             "겹친 콜라이더나 연속 충돌로 한 번에 여러 칸이 우르르 떨어지는 걸 막아줍니다.")]
    [SerializeField] private float invincibilityDuration = 1.2f;

    [Header("정렬")]
    [Tooltip("트레일 칸이 앞 칸을 바라보도록 매 프레임 회전을 맞춥니다.")]
    [SerializeField] private bool alignTrailRotation = true;
    [Tooltip("회전이 따라붙는 속도. 클수록 즉각적으로 정렬됩니다.")]
    [SerializeField] private float alignSmoothSpeed = 8f;

    [Header("디버그")]
    [Tooltip("아래 컨텍스트 메뉴 '테스트: 연결부 끊기'로 끊어볼 연결부 인덱스")]
    [SerializeField] private int debugBreakIndex = 1;

    private readonly List<TrainCar> cars = new List<TrainCar>();
    private readonly List<TrainCoupler> couplers = new List<TrainCoupler>();
    private Transform carContainer;
    private float invincibleUntil = -1f;

    /// <summary>기관차를 포함한 전체 차량 목록 (인덱스 순).</summary>
    public IReadOnlyList<TrainCar> Cars => cars;
    public TrainCar Locomotive => locomotive;
    public int TotalCarCount => cars.Count;

    /// <summary>차량 사이를 벌리는 거리(m). 스킬이 편성을 직접 이어 붙일 때 씁니다.</summary>
    public float Gap => gap;

    /// <summary>지금 무적 상태인지 여부. UI에서 깜빡임 연출 등에 활용할 수 있습니다.</summary>
    public bool IsInvincible => Time.time < invincibleUntil;

    /// <summary>외부(스킬 등)에서 강제로 무적 시간을 겁니다. 이미 더 긴 무적이 걸려있으면 줄이지 않습니다.</summary>
    public void ForceInvincible(float duration)
    {
        invincibleUntil = Mathf.Max(invincibleUntil, Time.time + duration);
    }

    /// <summary>연결부가 끊어졌을 때 발생. 인자는 끊어진 연결부 인덱스.</summary>
    public event Action<int> CouplerBroken;
    /// <summary>기관차가 장애물에 부딪혔을 때 발생. 게임오버 처리를 여기에 연결하세요.</summary>
    public event Action<TrainCar, Collision> LocomotiveCrashed;

    private void Awake() => Build();

    // 트레일 칸들이 매 프레임 앞 칸을 바라보도록 회전을 맞춰줍니다.
    private void FixedUpdate()
    {
        if (!alignTrailRotation) return;

        for (int i = 1; i < cars.Count; i++)
        {
            TrainCar car = cars[i];
            TrainCar front = cars[i - 1];
            if (car == null || car.IsDetached || front == null || front.IsDetached) continue;
            if (car.Body.isKinematic) continue; // 스킬이 조종 중이면 여기서 안 건드림

            Vector3 dir = front.transform.position - car.transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) continue;

            Quaternion target = Quaternion.LookRotation(dir.normalized, Vector3.up);
            Quaternion next = Quaternion.Slerp(car.Body.rotation, target,
                1f - Mathf.Exp(-alignSmoothSpeed * Time.fixedDeltaTime));
            car.Body.MoveRotation(next);
        }
    }

    private void Build()
    {
        if (locomotive == null)
        {
            Debug.LogError("[TrainConsist] 기관차(locomotive)가 지정되지 않았습니다.", this);
            enabled = false;
            return;
        }

        if (obstacleLayers.value == 0)
        {
            Debug.Log("[TrainConsist] Obstacle Layers가 비어 있어 충돌에 의한 자동 분리가 꺼져 있습니다. " +
                      "장애물을 만든 뒤 전용 레이어를 지정하세요. (바닥은 포함하지 말 것)", this);
        }

        // 생성된 객차를 담을 컨테이너. 월드 원점에 두어야 자식 차량의 물리가 왜곡되지 않습니다.
        carContainer = new GameObject("Train Cars").transform;

        locomotive.Initialize();
        RegisterCar(locomotive, 0);

        for (int i = 1; i <= carCount; i++)
        {
            TrainCar car = SpawnCar(cars[i - 1], i);
            if (car == null) break;
            RegisterCar(car, i);
        }

        IgnoreCollisionsBetweenCars();
        ConnectAllCouplers();

        // TrainController의 Consist 슬롯을 수동으로 연결하지 않아도 되도록 자동 배선합니다.
        if (locomotive.TryGetComponent(out TrainController controller))
        {
            controller.AttachConsist(this);
        }
    }

    /// <summary>앞 차량의 뒤쪽 연결 지점에 정확히 맞물리도록 객차를 배치합니다.</summary>
    private TrainCar SpawnCar(TrainCar previous, int index)
    {
        if (carPrefab == null)
        {
            Debug.LogWarning("[TrainConsist] 객차 프리팹(carPrefab)이 비어 있어 객차를 만들지 못했습니다.", this);
            return null;
        }

        Quaternion rotation = locomotive.transform.rotation;
        Vector3 rough = previous.transform.position - locomotive.transform.forward * (previous.Length + gap);

        GameObject go = Instantiate(carPrefab, rough, rotation, carContainer);
        go.name = $"Car_{index:00}";

        if (!go.TryGetComponent(out Rigidbody _)) go.AddComponent<Rigidbody>();
        if (!go.TryGetComponent(out TrainCar car)) car = go.AddComponent<TrainCar>();
        car.Initialize();

        // 모델 피벗이 중앙이 아니어도 연결 지점끼리 정확히 맞도록 위치를 보정합니다.
        Vector3 desiredAnchor = previous.transform.TransformPoint(previous.RearAnchorLocal)
                                - locomotive.transform.forward * gap;
        Vector3 currentAnchor = car.transform.TransformPoint(car.FrontAnchorLocal);
        car.transform.position += desiredAnchor - currentAnchor;

        return car;
    }

    private void RegisterCar(TrainCar car, int index)
    {
        car.CarIndex = index;
        car.Collided += HandleCarCollision;
        cars.Add(car);
    }

    private void ConnectAllCouplers()
    {
        for (int i = 1; i < cars.Count; i++)
        {
            var coupler = TrainCoupler.Connect(cars[i - 1], cars[i], i, couplerSettings);
            if (coupler == null) continue;

            coupler.Broken += OnCouplerBroken;
            couplers.Add(coupler);
        }
    }

    // 연결된 차량끼리는 서로 부딪히지 않도록 무시 처리 (조인트가 떨리는 주된 원인)
    private void IgnoreCollisionsBetweenCars()
    {
        var sets = new List<Collider[]>(cars.Count);
        foreach (var car in cars) sets.Add(car.GetComponentsInChildren<Collider>());

        for (int i = 0; i < sets.Count; i++)
        {
            for (int j = i + 1; j < sets.Count; j++)
            {
                foreach (var a in sets[i])
                {
                    if (a == null || a.isTrigger) continue;
                    foreach (var b in sets[j])
                    {
                        if (b == null || b.isTrigger) continue;
                        Physics.IgnoreCollision(a, b, true);
                    }
                }
            }
        }
    }

    // ────────────────────────────────────────────────
    // 공개 API
    // ────────────────────────────────────────────────

    /// <summary>
    /// 지정한 인덱스의 연결부를 끊고, 그 뒤에 달린 차량을 전부 떨어뜨립니다.
    /// 예) BreakCouplerAt(2) → 2번 차량부터 끝까지 추락.
    /// </summary>
    public void BreakCouplerAt(int couplerIndex)
    {
        if (couplerIndex <= 0 || couplerIndex >= cars.Count) return;
        if (cars[couplerIndex].IsDetached) return; // 이미 처리됨 (파손 이벤트 재진입 방지)

        for (int i = couplerIndex; i < cars.Count; i++)
        {
            cars[i].Detach();
        }

        CouplerBroken?.Invoke(couplerIndex);
    }

    /// <summary>지정한 차량과 그 뒤의 모든 차량을 떨어뜨립니다. (= 그 차량 앞의 연결부를 끊음)</summary>
    public void DetachFrom(int carIndex) => BreakCouplerAt(carIndex);

    /// <summary>마지막 한 량만 떼어냅니다.</summary>
    public void DetachLastCar() => BreakCouplerAt(cars.Count - 1);

    public TrainCar GetCar(int index) => (index >= 0 && index < cars.Count) ? cars[index] : null;

    public TrainCoupler GetCoupler(int couplerIndex)
    {
        foreach (var c in couplers)
        {
            if (c != null && c.CouplerIndex == couplerIndex) return c;
        }
        return null;
    }

    /// <summary>아직 연결된 채로 남아 있는 차량 수 (기관차 포함).</summary>
    public int AttachedCarCount
    {
        get
        {
            int n = 0;
            foreach (var car in cars)
            {
                if (car != null && !car.IsDetached) n++;
            }
            return n;
        }
    }
    public void AlignStraight()
    {
        TrainCar previous = locomotive;
        for (int i = 1; i < cars.Count; i++)
        {
            TrainCar car = cars[i];
            if (car == null || car.IsDetached) continue;

            Vector3 desiredAnchor = previous.transform.TransformPoint(previous.RearAnchorLocal)
                                    - locomotive.transform.forward * gap;
            Vector3 currentAnchor = car.transform.TransformPoint(car.FrontAnchorLocal);
            Vector3 delta = desiredAnchor - currentAnchor;

            car.Body.MovePosition(car.transform.position + delta);
            car.Body.MoveRotation(locomotive.transform.rotation);

            previous = car;
        }
    }

    [ContextMenu("테스트: 연결부 끊기")]
    private void DebugBreak()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[TrainConsist] 플레이 중에만 사용할 수 있습니다.", this);
            return;
        }
        BreakCouplerAt(debugBreakIndex);
    }

    // ────────────────────────────────────────────────

    private void HandleCarCollision(TrainCar car, Collision collision)
    {
        if (car.IsDetached) return;
        if (obstacleLayers.value == 0) return;
        if ((obstacleLayers.value & (1 << collision.gameObject.layer)) == 0) return;
        if (IsInvincible) return;

        invincibleUntil = Time.time + invincibilityDuration;

        if (car.CarIndex == 0)
        {
            // 머리는 트레일이 있으면 맨 뒤 칸이 대신 희생됩니다.
            int sacrifice = FindLastAttachedTrailingIndex();
            if (sacrifice > 0)
            {
                BreakCouplerAt(sacrifice);
                return;
            }
            LocomotiveCrashed?.Invoke(car, collision);
            return;
        }

        // 몸통은 닿은 칸 기준으로 그 뒤까지 같이 떨어집니다.
        BreakCouplerAt(car.CarIndex);
    }

    // 아직 연결된 채로 남아 있는 트레일 중 가장 뒤쪽 칸의 인덱스를 찾습니다.
    // 트레일이 하나도 없으면(기관차만 남았으면) 0을 반환합니다.
    private int FindLastAttachedTrailingIndex()
    {
        for (int i = cars.Count - 1; i >= 1; i--)
        {
            if (cars[i] != null && !cars[i].IsDetached) return i;
        }
        return 0;
    }

    // 힘을 못 이겨 조인트가 저절로 끊어진 경우에도 뒤쪽을 함께 떨어뜨립니다.
    private void OnCouplerBroken(TrainCoupler coupler)
    {
        BreakCouplerAt(coupler.CouplerIndex);
    }

    private void OnDestroy()
    {
        foreach (var car in cars)
        {
            if (car != null) car.Collided -= HandleCarCollision;
        }
        foreach (var coupler in couplers)
        {
            if (coupler != null) coupler.Broken -= OnCouplerBroken;
        }
    }
}