using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 기차가 일직선이 아니라, 청크 프리팹에 찍어둔 좌표들을 따라 움직이게 합니다.
/// MapChunkSpawner가 청크를 이어 붙이면서 SetPath()로 최신 경로를 넘겨주면,
/// 이 스크립트는 그 경로를 따라가도록 기관차의 수평(X/Z) 속도를 조종합니다.
///
/// <b>역할 분담</b> — 점프(y축 속도)와 피치(고개 숙임)는 TrainController가 그대로
/// 담당합니다. 이 스크립트는 좌우/앞뒤 진행(수평 이동·좌우 회전)만 책임지므로
/// 같은 Rigidbody에서 두 스크립트가 함께 동작합니다.
/// TrainController의 Path Follower 슬롯에 이 컴포넌트를 등록하면
/// 좌우 회전(yaw)도 경로를 따라 자연스럽게 꺾입니다.
///
/// <b>매끄럽게 도는 원리</b>
///  1. 웨이포인트를 직선으로 잇지 않고 Catmull-Rom 곡선으로 잘게 나눠 부드러운 길을 만듭니다.
///  2. 매 프레임 <i>현재 위치에서 가장 가까운 경로 지점</i>을 다시 찾습니다.
///     (누적 거리를 세지 않으므로 청크가 재배치돼 경로가 새로 만들어져도 목표점이 튀지 않습니다.)
///  3. 거기서 속도에 비례한 거리만큼 앞을 보고, 그쪽으로 부드럽게 방향을 틉니다.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class TrainPathFollower : MonoBehaviour
{
    [Header("속도")]
    [Tooltip("비워두면 아래 Speed 값을 직접 씁니다. 채워두면 ScrollController.Speed를 우선 사용합니다.")]
    [SerializeField] private ScrollController scroll;
    [SerializeField] private float speed = 30f;

    [Header("경로 다듬기")]
    [Tooltip("웨이포인트를 곡선으로 이어 코너를 둥글게 만듭니다. 끄면 예전처럼 각진 꺾은선이 됩니다.")]
    [SerializeField] private bool smoothPath = true;

    [Tooltip("웨이포인트 사이를 몇 조각으로 나눌지. 클수록 곡선이 곱지만 계산량이 늘어납니다.")]
    [SerializeField, Range(1, 24)] private int subdivisions = 10;

    [Tooltip("곡선이 웨이포인트 밖으로 부푸는 정도. 0.5가 표준이고, 낮추면 직선에 가까워집니다.")]
    [SerializeField, Range(0f, 1f)] private float curveTension = 0.5f;

    [Header("경로 추종")]
    [Tooltip("몇 초 앞을 내다보고 방향을 잡을지. 크면 코너를 크게 돌며 부드럽고, 작으면 좌표를 칼같이 따라갑니다.")]
    [SerializeField] private float lookAheadTime = 0.8f;

    [Tooltip("속도가 느려도 최소한 이만큼은 앞을 봅니다(m).")]
    [SerializeField] private float minLookAhead = 15f;

    [Tooltip("방향이 목표까지 따라붙는 데 걸리는 대략의 시간(초). 클수록 여유롭게 돕니다.")]
    [SerializeField] private float turnSmoothTime = 0.35f;

    [Tooltip("좌우로 꺾이는 속도 상한(도/초). 급커브에서도 이보다 빨리는 안 돕니다.")]
    [SerializeField] private float maxTurnSpeed = 60f;

    private Rigidbody rb;
    private readonly List<Vector3> raw = new List<Vector3>();       // 원본 웨이포인트
    private readonly List<Vector3> path = new List<Vector3>();      // 곡선으로 잘게 나눈 실제 주행선
    private readonly List<float> cumulative = new List<float>();    // path[i]까지의 누적 거리
    private float currentYaw;
    private float yawVelocity;
    private float distanceAlongPath;

    /// <summary>경로 접선을 반영한 현재 진행 방향(수평, 정규화됨).</summary>
    public Vector3 CurrentForward { get; private set; } = Vector3.forward;

    /// <summary>TrainController가 피치와 합성할 수 있도록 넘겨주는 현재 좌우(yaw) 각도.</summary>
    public float CurrentYaw => currentYaw;

    /// <summary>현재 경로 위에서의 진행 위치(경로 시작점 기준 거리).</summary>
    public float DistanceTraveled => distanceAlongPath;

    private float Speed => scroll != null ? scroll.Speed : speed;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        currentYaw = transform.eulerAngles.y;
        CurrentForward = transform.forward;
    }

    /// <summary>
    /// MapChunkSpawner가 새 청크를 이어 붙이거나 뒷청크를 회수해 앞으로 재배치할 때마다
    /// 최신 전체 경로(월드 좌표, 진행 순서)를 넘겨줍니다.
    /// 경로가 통째로 바뀌어도 현재 위치를 다시 투영해 찾으므로 진행이 튀지 않습니다.
    /// </summary>
    public void SetPath(IReadOnlyList<Vector3> worldPoints)
    {
        raw.Clear();
        if (worldPoints != null)
        {
            for (int i = 0; i < worldPoints.Count; i++) raw.Add(worldPoints[i]);
        }

        RebuildSmoothedPath();
    }

    private void RebuildSmoothedPath()
    {
        path.Clear();
        cumulative.Clear();
        if (raw.Count == 0) return;

        if (!smoothPath || raw.Count < 3 || subdivisions <= 1)
        {
            path.AddRange(raw);
        }
        else
        {
            // 웨이포인트 사이를 Catmull-Rom 곡선으로 채웁니다.
            // 양 끝은 이웃이 없으므로 끝점을 한 번 더 쓰는 것으로 대신합니다.
            for (int i = 0; i < raw.Count - 1; i++)
            {
                Vector3 p0 = raw[Mathf.Max(i - 1, 0)];
                Vector3 p1 = raw[i];
                Vector3 p2 = raw[i + 1];
                Vector3 p3 = raw[Mathf.Min(i + 2, raw.Count - 1)];

                for (int j = 0; j < subdivisions; j++)
                {
                    path.Add(CatmullRom(p0, p1, p2, p3, j / (float)subdivisions, curveTension));
                }
            }
            path.Add(raw[raw.Count - 1]);
        }

        cumulative.Add(0f);
        for (int i = 1; i < path.Count; i++)
        {
            cumulative.Add(cumulative[i - 1] + Vector3.Distance(path[i - 1], path[i]));
        }
    }

    // 카디널 스플라인. tension 0.5가 표준 Catmull-Rom입니다.
    private static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t, float tension)
    {
        Vector3 m1 = tension * (p2 - p0);
        Vector3 m2 = tension * (p3 - p1);

        float t2 = t * t;
        float t3 = t2 * t;

        return (2f * t3 - 3f * t2 + 1f) * p1
             + (t3 - 2f * t2 + t) * m1
             + (-2f * t3 + 3f * t2) * p2
             + (t3 - t2) * m2;
    }

    private void FixedUpdate()
    {
        if (path.Count < 2) return; // 아직 경로가 없으면 대기 (MapChunkSpawner가 채워줄 때까지)

        // 1) 누적 카운터 대신, 지금 서 있는 자리를 경로 위에 투영해 현재 진행 위치를 구합니다.
        //    경로가 재배치돼도 이 값은 연속적이라 목표점이 갑자기 튀지 않습니다.
        distanceAlongPath = ProjectOntoPath(transform.position);

        float pathLength = cumulative[cumulative.Count - 1];
        float lookAhead = Mathf.Max(minLookAhead, Speed * lookAheadTime);

        // 아직 경로 시작점보다 뒤에 있으면(출발 활주 구간) 시작점을 곧장 겨냥합니다.
        // 이렇게 해야 첫 게이트를 옆으로 스치지 않고 정확히 통과합니다.
        Vector3 target = distanceAlongPath < 0f
            ? path[0]
            : SampleAt(Mathf.Min(distanceAlongPath + lookAhead, pathLength));

        Vector3 toTarget = target - transform.position;
        toTarget.y = 0f;
        if (toTarget.sqrMagnitude <= 0.0001f) return;

        Vector3 desiredForward = toTarget.normalized;
        CurrentForward = desiredForward;

        // 2) 목표 방향으로 감속·가속이 있는 부드러운 보간. maxTurnSpeed로 상한도 겁니다.
        float targetYaw = Quaternion.LookRotation(desiredForward, Vector3.up).eulerAngles.y;
        currentYaw = Mathf.SmoothDampAngle(
            currentYaw, targetYaw, ref yawVelocity,
            Mathf.Max(0.01f, turnSmoothTime), maxTurnSpeed, Time.fixedDeltaTime);

        Vector3 moveDir = Quaternion.Euler(0f, currentYaw, 0f) * Vector3.forward;
        Vector3 horizontalVelocity = moveDir * Speed;

        Vector3 velocity = rb.linearVelocity;
        velocity.x = horizontalVelocity.x;
        velocity.z = horizontalVelocity.z;
        rb.linearVelocity = velocity; // y(점프·중력)는 TrainController가 그대로 관리
    }

    // 주어진 위치에서 가장 가까운 경로 지점을 찾아, 그 지점의 누적 거리를 돌려줍니다.
    // 높이(y)는 점프로 계속 바뀌므로 수평 거리만 비교합니다.
    private float ProjectOntoPath(Vector3 worldPosition)
    {
        // 경로 시작점보다 뒤에 있으면 음수 거리를 돌려줍니다(출발 활주 구간).
        Vector3 startDirection = path[1] - path[0];
        startDirection.y = 0f;
        if (startDirection.sqrMagnitude > 0.0001f)
        {
            Vector3 fromStart = worldPosition - path[0];
            fromStart.y = 0f;

            float ahead = Vector3.Dot(fromStart, startDirection.normalized);
            if (ahead < 0f) return ahead;
        }

        float bestDistance = float.MaxValue;
        float bestAlong = 0f;

        for (int i = 1; i < path.Count; i++)
        {
            Vector3 a = path[i - 1];
            Vector3 b = path[i];

            Vector3 segment = b - a;
            segment.y = 0f;
            float segmentSqr = segment.sqrMagnitude;
            if (segmentSqr <= 0.0001f) continue;

            Vector3 toPoint = worldPosition - a;
            toPoint.y = 0f;

            float t = Mathf.Clamp01(Vector3.Dot(toPoint, segment) / segmentSqr);
            Vector3 closest = a + segment * t;

            Vector3 gap = worldPosition - closest;
            gap.y = 0f;
            float sqr = gap.sqrMagnitude;

            if (sqr < bestDistance)
            {
                bestDistance = sqr;
                bestAlong = cumulative[i - 1] + (cumulative[i] - cumulative[i - 1]) * t;
            }
        }

        return bestAlong;
    }

    // 시작점 기준 누적 거리(distance)에 해당하는 경로 상의 월드 좌표를 구합니다(구간 선형보간).
    private Vector3 SampleAt(float distance)
    {
        if (path.Count == 1) return path[0];

        for (int i = 1; i < cumulative.Count; i++)
        {
            if (distance <= cumulative[i] || i == cumulative.Count - 1)
            {
                float segStart = cumulative[i - 1];
                float segLen = cumulative[i] - segStart;
                float t = segLen > 0f ? Mathf.Clamp01((distance - segStart) / segLen) : 0f;
                return Vector3.Lerp(path[i - 1], path[i], t);
            }
        }
        return path[path.Count - 1];
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // 인스펙터에서 곡선 설정을 바꾸면 플레이 중에도 바로 반영합니다.
        if (Application.isPlaying && raw.Count > 0) RebuildSmoothedPath();
    }
#endif

    private void OnDrawGizmos()
    {
        // 노란 선 = 실제 주행선(곡선), 자홍 구슬 = 원본 웨이포인트
        if (path.Count >= 2)
        {
            Gizmos.color = Color.yellow;
            for (int i = 1; i < path.Count; i++) Gizmos.DrawLine(path[i - 1], path[i]);
        }

        Gizmos.color = new Color(1f, 0f, 1f, 0.6f);
        for (int i = 0; i < raw.Count; i++) Gizmos.DrawWireSphere(raw[i], 2f);

        // 초록 구슬 = 지금 보고 있는 목표점
        if (Application.isPlaying && path.Count >= 2)
        {
            float lookAhead = Mathf.Max(minLookAhead, Speed * lookAheadTime);
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(SampleAt(distanceAlongPath + lookAhead), 3f);
        }
    }
}
