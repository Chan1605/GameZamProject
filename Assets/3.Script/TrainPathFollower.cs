using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 기차가 일직선이 아니라, 맵(Group) 프리팹에 찍어둔 좌표들을 따라 움직이게 합니다.
/// MapChunkSpawner가 맵 조각들을 이어 붙이면서 SetPath()로 최신 경로를 넘겨주면,
/// 이 스크립트는 그 경로를 따라가도록 기관차의 수평(X/Z) 속도를 조종합니다.
///
/// <b>역할 분담</b> — 점프(y축 속도)와 피치(고개 숙임)는 기존 TrainController가
/// 그대로 담당합니다. 이 스크립트는 좌우/앞뒤 진행(수평 이동·좌우 회전)만
/// 책임지므로 같은 Rigidbody에서 두 스크립트가 함께 동작합니다.
/// TrainController의 Path Follower 슬롯에 이 컴포넌트를 등록하면
/// 좌우 회전(yaw)도 경로를 따라 자연스럽게 꺾입니다.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class TrainPathFollower : MonoBehaviour
{
    [Header("속도")]
    [Tooltip("비워두면 아래 Speed 값을 직접 씁니다. 채워두면 ScrollController.Speed를 우선 사용해 장애물·바닥과 속도를 맞출 수 있습니다.")]
    [SerializeField] private ScrollController scroll;
    [SerializeField] private float speed = 30f;

    [Header("경로 추종")]
    [Tooltip("현재 진행 지점보다 이만큼 앞을 보고 방향을 잡습니다. 값이 작으면 좌표를 칼같이 따라가지만 급커브에서 흔들리고, 크면 코너를 부드럽게 자르며 돕니다.")]
    [SerializeField] private float lookAheadDistance = 6f;
    [Tooltip("좌우로 꺾이는 속도 상한(도/초). 급커브에서 순간적으로 홱 꺾이지 않도록 제한합니다.")]
    [SerializeField] private float maxTurnSpeed = 120f;

    private Rigidbody rb;
    private readonly List<Vector3> path = new List<Vector3>();
    private readonly List<float> cumulative = new List<float>(); // path[i]까지의 누적 거리
    private float distanceTraveled;
    private float currentYaw;

    /// <summary>경로 접선을 반영한 현재 진행 방향(수평, 정규화됨).</summary>
    public Vector3 CurrentForward { get; private set; } = Vector3.forward;

    /// <summary>TrainController가 피치와 합성할 수 있도록 넘겨주는 현재 좌우(yaw) 각도.</summary>
    public float CurrentYaw => currentYaw;

    /// <summary>경로 시작점 기준 누적 진행 거리.</summary>
    public float DistanceTraveled => distanceTraveled;

    private float Speed => scroll != null ? scroll.Speed : speed;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        currentYaw = transform.eulerAngles.y;
        CurrentForward = transform.forward;
    }

    /// <summary>
    /// MapChunkSpawner가 새 조각을 이어 붙이거나 뒷조각을 회수해 앞으로 재배치할 때마다
    /// 최신 전체 경로(월드 좌표, 진행 순서)를 넘겨줍니다.
    /// </summary>
    public void SetPath(IReadOnlyList<Vector3> worldPoints)
    {
        path.Clear();
        cumulative.Clear();
        if (worldPoints == null || worldPoints.Count == 0) return;

        path.AddRange(worldPoints);
        cumulative.Add(0f);
        for (int i = 1; i < path.Count; i++)
        {
            cumulative.Add(cumulative[i - 1] + Vector3.Distance(path[i - 1], path[i]));
        }
    }

    private void FixedUpdate()
    {
        if (path.Count < 2) return; // 아직 경로가 없으면 대기 (MapChunkSpawner가 채워줄 때까지)

        distanceTraveled += Speed * Time.fixedDeltaTime;
        float pathLength = cumulative[cumulative.Count - 1];
        float clamped = Mathf.Clamp(distanceTraveled, 0f, Mathf.Max(0f, pathLength - 0.01f));

        float lookAt = Mathf.Min(clamped + lookAheadDistance, pathLength);
        Vector3 target = SampleAt(lookAt);

        Vector3 toTarget = target - transform.position;
        toTarget.y = 0f;
        if (toTarget.sqrMagnitude <= 0.0001f) return;

        Vector3 desiredForward = toTarget.normalized;
        CurrentForward = desiredForward;

        float targetYaw = Quaternion.LookRotation(desiredForward, Vector3.up).eulerAngles.y;
        currentYaw = Mathf.MoveTowardsAngle(currentYaw, targetYaw, maxTurnSpeed * Time.fixedDeltaTime);

        Vector3 moveDir = Quaternion.Euler(0f, currentYaw, 0f) * Vector3.forward;
        Vector3 horizontalVelocity = moveDir * Speed;

        Vector3 velocity = rb.linearVelocity;
        velocity.x = horizontalVelocity.x;
        velocity.z = horizontalVelocity.z;
        rb.linearVelocity = velocity; // y(점프·중력)는 TrainController가 그대로 관리
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

    private void OnDrawGizmos()
    {
        if (path == null || path.Count < 2) return;
        Gizmos.color = Color.yellow;
        for (int i = 1; i < path.Count; i++) Gizmos.DrawLine(path[i - 1], path[i]);
    }
}
