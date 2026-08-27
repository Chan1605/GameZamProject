using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 플래피버드 3D 프로토타입 - 기관차 조작 스크립트.
/// 마우스 왼쪽 버튼을 클릭하면 Rigidbody에 위쪽 속도를 부여해
/// 대략 jumpHeight(기본 10)만큼 y값이 상승했다가 중력으로 자연스럽게 낙하합니다.
///
/// 같은 오브젝트의 TrainConsist가 편성을 만들면 자동으로 연결되며,
/// 점프할 때 뒤에 매달린 객차에도 같은 상승 속도를 넣어
/// 연결부가 갑자기 잡아채이는 현상을 막습니다.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(TrainCar))] // 기관차도 편성의 0번 차량이므로 TrainCar가 반드시 필요합니다.
public class TrainController : MonoBehaviour
{
    [Header("점프 설정")]
    [Tooltip("클릭 시 도달할 최대 점프 높이 (현재 위치 기준 y값 증가량)")]
    [SerializeField] private float jumpHeight = 10f;

    [Header("편성 연동")]
    [Tooltip("연결된 열차 편성. 비워두면 씬에서 자동으로 찾습니다.")]
    [SerializeField] private TrainConsist consist;

    [Header("기울기(피치) 연출")]
    [Tooltip("체크하면 스크립트가 회전을 전담합니다. Rigidbody의 Freeze Rotation을 직접 관리하고 싶다면 해제하세요.")]
    [SerializeField] private bool controlRotation = true;
    [Tooltip("체크하면 상승할 때 앞머리가 위로, 하강할 때 앞머리가 아래로 기울어집니다. 반대로 움직이면 체크를 해제하세요.")]
    [SerializeField] private bool noseUpWhenRising = true;
    [Tooltip("최대로 기울어지는 각도(도)")]
    [SerializeField] private float maxPitchAngle = 25f;
    [Tooltip("이 수직 속도(m/s)에 도달하면 maxPitchAngle까지 기울어집니다.")]
    [SerializeField] private float velocityForMaxPitch = 8f;
    [Tooltip("목표 각도로 부드럽게 따라가는 속도. 값이 클수록 즉각적으로 기울어집니다.")]
    [SerializeField] private float pitchSmoothSpeed = 6f;

    private Rigidbody rb;
    private float jumpVelocity;
    private float initialYaw;
    private float initialRoll;

    /// <summary>TrainConsist가 편성을 완성한 뒤 스스로 연결합니다.</summary>
    public void AttachConsist(TrainConsist value) => consist = value;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = true;
        RecalculateJumpVelocity();

        // 좌우 회전(yaw)이나 롤(roll) 값은 건드리지 않고 피치(x축)만 조작하기 위해 초기값을 저장
        Vector3 startEuler = transform.rotation.eulerAngles;
        initialYaw = startEuler.y;
        initialRoll = startEuler.z;

        // 회전을 스크립트가 제어하는 동안에는, 매달린 객차가 기관차를 흔들지 못하게 고정합니다.
        if (controlRotation)
        {
            rb.constraints |= RigidbodyConstraints.FreezeRotation;
        }
    }

    // 물리 공식(v = sqrt(2 * g * h))으로 목표 높이에 필요한 초기 속도를 계산합니다.
    private void RecalculateJumpVelocity()
    {
        float gravity = Mathf.Abs(Physics.gravity.y);
        jumpVelocity = Mathf.Sqrt(2f * gravity * jumpHeight);
    }

    private void Update()
    {
        // 새 Input System 기준 마우스 왼쪽 클릭 감지
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            Jump();
        }
    }

    private void Jump()
    {
        ApplyJumpVelocity(rb);

        // 슬롯을 비워둔 경우에도 동작하도록 한 번만 자동 탐색합니다.
        if (consist == null) consist = FindFirstObjectByType<TrainConsist>();
        if (consist == null) return;

        // 매달린 객차에도 같은 속도를 주면 조인트가 늘어나지 않고 편성 전체가 함께 떠오릅니다.
        foreach (var car in consist.Cars)
        {
            if (car == null || car.IsDetached) continue;
            if (car.Body == rb) continue;
            ApplyJumpVelocity(car.Body);
        }
    }

    private void ApplyJumpVelocity(Rigidbody body)
    {
        if (body == null || body.isKinematic) return;

        Vector3 velocity = body.linearVelocity;
        velocity.y = jumpVelocity; // 기존 y속도를 덮어써서 항상 동일한 높이로 점프
        body.linearVelocity = velocity;
    }

    private void FixedUpdate()
    {
        if (controlRotation) UpdatePitch();
    }

    // 수직 속도에 비례해 앞머리가 위/아래로 기울어지도록 회전을 보간합니다.
    private void UpdatePitch()
    {
        float verticalVelocity = rb.linearVelocity.y;
        float t = Mathf.Clamp(verticalVelocity / velocityForMaxPitch, -1f, 1f);
        float direction = noseUpWhenRising ? -1f : 1f;
        float targetPitch = t * maxPitchAngle * direction;

        Quaternion targetRotation = Quaternion.Euler(targetPitch, initialYaw, initialRoll);
        Quaternion next = Quaternion.Slerp(
            rb.rotation,
            targetRotation,
            1f - Mathf.Exp(-pitchSmoothSpeed * Time.fixedDeltaTime));

        // 조인트와 충돌하지 않도록 Transform이 아닌 Rigidbody를 통해 회전시킵니다.
        rb.MoveRotation(next);
    }

#if UNITY_EDITOR
    // 에디터에서 jumpHeight 값을 플레이 중에 바꿔도 즉시 반영되도록 처리
    private void OnValidate()
    {
        if (Application.isPlaying && rb != null)
        {
            RecalculateJumpVelocity();
        }
    }
#endif
}
