using UnityEngine;

/// <summary>
/// 무한 스크롤의 기준점이자 속도 관리자.
/// 기차는 제자리에 있고, 장애물·바닥이 이 컨트롤러가 정한 속도로 기차 쪽으로 흘러옵니다.
///
/// <b>배치 방법</b>: 빈 GameObject를 만들어 기차와 같은 위치·같은 회전으로 두세요.
/// 이 오브젝트의 <b>forward(파란 화살표)가 기차가 바라보는 방향</b>이 되고,
/// 장애물은 그 앞쪽에서 생성되어 반대 방향으로 흘러옵니다.
/// </summary>
public class ScrollController : MonoBehaviour
{
    [Header("스크롤 속도")]
    [Tooltip("장애물과 바닥이 다가오는 속도 (m/s). 기차의 체감 전진 속도와 같습니다.")]
    [SerializeField] private float speed = 30f;
    [Tooltip("체크를 풀면 스크롤이 멈춥니다. 게임오버 시 Stop()을 호출하세요.")]
    [SerializeField] private bool running = true;

    /// <summary>현재 스크롤 속도. 정지 상태면 0.</summary>
    public float Speed => running ? speed : 0f;

    /// <summary>이번 프레임에 흘러갈 거리.</summary>
    public float DeltaDistance => Speed * Time.deltaTime;

    /// <summary>기차가 바라보는 방향. 장애물은 이 방향 저 멀리서 생성됩니다.</summary>
    public Vector3 Forward => transform.forward;

    /// <summary>장애물이 실제로 움직이는 방향 (플레이어 쪽).</summary>
    public Vector3 MoveDirection => -transform.forward;

    /// <summary>레인의 기준 원점. 장애물·바닥의 위치는 모두 이 지점 기준으로 계산됩니다.</summary>
    public Vector3 Origin => transform.position;

    /// <summary>주어진 위치가 원점에서 앞으로(+) / 뒤로(-) 얼마나 떨어져 있는지.</summary>
    public float DistanceAlongAxis(Vector3 worldPosition)
    {
        return Vector3.Dot(worldPosition - transform.position, transform.forward);
    }

    public void SetSpeed(float value) => speed = Mathf.Max(0f, value);
    public void Stop() => running = false;
    public void Resume() => running = true;

    private void OnDrawGizmos()
    {
        // 레인 방향을 씬 뷰에서 확인할 수 있도록 화살표를 그립니다.
        Gizmos.color = Color.cyan;
        Vector3 tip = transform.position + transform.forward * 20f;
        Gizmos.DrawLine(transform.position, tip);
        Gizmos.DrawWireSphere(tip, 1.5f);
    }
}
