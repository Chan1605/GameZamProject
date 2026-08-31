using System.Collections.Generic;
using UnityEngine;

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

    [Header("디버그")]
    [Tooltip("체크하면 1초마다 핵심 상태값을 콘솔에 찍습니다. 원인 파악되면 꺼도 됩니다.")]
    [SerializeField] private bool debugLog = true;

    private Rigidbody rb;
    private readonly List<Vector3> raw = new List<Vector3>();
    private readonly List<Vector3> path = new List<Vector3>();
    private readonly List<float> cumulative = new List<float>();
    private float currentYaw;
    private float yawVelocity;
    private float distanceAlongPath;


    public Vector3 CurrentForward { get; private set; } = Vector3.forward;
    public float CurrentYaw => currentYaw;
    public float DistanceTraveled => distanceAlongPath;
    public IReadOnlyList<Vector3> RawWaypoints => raw;
    private float Speed => scroll != null ? scroll.Speed : speed;

    public bool ExternalOverride { get; set; }

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        currentYaw = transform.eulerAngles.y;
        CurrentForward = transform.forward;

    }

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

        if (ExternalOverride || rb.isKinematic)
        {

            return;
        }

        if (path.Count < 2)
        {

            return;
        }

        distanceAlongPath = ProjectOntoPath(transform.position);

        float pathLength = cumulative[cumulative.Count - 1];
        float lookAhead = Mathf.Max(minLookAhead, Speed * lookAheadTime);

        Vector3 target = distanceAlongPath < 0f
            ? path[0]
            : SampleAt(Mathf.Min(distanceAlongPath + lookAhead, pathLength));

        Vector3 toTarget = target - transform.position;
        toTarget.y = 0f;

        if (toTarget.sqrMagnitude <= 0.0001f)
        {

            return;
        }

        Vector3 desiredForward = toTarget.normalized;
        CurrentForward = desiredForward;

        float targetYaw = Quaternion.LookRotation(desiredForward, Vector3.up).eulerAngles.y;
        currentYaw = Mathf.SmoothDampAngle(
            currentYaw, targetYaw, ref yawVelocity,
            Mathf.Max(0.01f, turnSmoothTime), maxTurnSpeed, Time.fixedDeltaTime);

        Vector3 moveDir = Quaternion.Euler(0f, currentYaw, 0f) * Vector3.forward;
        Vector3 horizontalVelocity = moveDir * Speed;

        Vector3 velocity = rb.linearVelocity;
        velocity.x = horizontalVelocity.x;
        velocity.z = horizontalVelocity.z;
        rb.linearVelocity = velocity;


    }

    private float ProjectOntoPath(Vector3 worldPosition)
    {
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
        if (Application.isPlaying && raw.Count > 0) RebuildSmoothedPath();
    }
#endif

    private void OnDrawGizmos()
    {
        if (path.Count >= 2)
        {
            Gizmos.color = Color.yellow;
            for (int i = 1; i < path.Count; i++) Gizmos.DrawLine(path[i - 1], path[i]);
        }

        Gizmos.color = new Color(1f, 0f, 1f, 0.6f);
        for (int i = 0; i < raw.Count; i++) Gizmos.DrawWireSphere(raw[i], 2f);

        if (Application.isPlaying && path.Count >= 2)
        {
            float lookAhead = Mathf.Max(minLookAhead, Speed * lookAheadTime);
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(SampleAt(distanceAlongPath + lookAhead), 3f);
        }
    }
}