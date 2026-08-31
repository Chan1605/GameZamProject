using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// E 스킬 — 누르고 있으면 시간이 느려지면서 열차가 멈추고, 머리를 원하는 방향으로 돌린 뒤
/// 손을 떼면 그 방향으로 돌진합니다.
///
/// <b>편성 유지</b> — 조준하는 동안 뒤 객차들은 매 물리 프레임마다 머리 뒤에 다시 이어 붙습니다.
/// 앞 칸의 뒤쪽 연결 지점(RearAnchor)에서 Gap만큼 떨어진 자리에 다음 칸의 앞쪽 연결 지점
/// (FrontAnchor)이 오도록 <b>위치와 회전을 같이</b> 잡아주기 때문에, 머리를 돌리면 꼬리가
/// 매달린 채로 따라 휩니다. (회전만 바꾸면 객차가 제자리에서 도는 '분리된' 모양이 됩니다.)
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class TrainSlowSkill : MonoBehaviour
{
    [SerializeField] private TrainConsist consist;
    [SerializeField] private TrainPathFollower pathFollower;
    [SerializeField] private PlayerSkillPoint skillPoint;

    [Header("입력")]
    [SerializeField] private Key SkillKey = Key.E;

    [Tooltip("이 스킬을 쓰는 데 필요한 스킬 포인트. Skill Point가 비어 있으면 조건 없이 발동합니다.")]
    [SerializeField, Min(0)] private int skillCost = 3;

    [Header("조준")]
    [SerializeField, Range(0.05f, 1f)] private float SlowScale = 0.25f;

    [Header("편성 유지")]
    [Tooltip("객차가 머리 뒤에 붙어 따라오는 속도. 0이면 즉시 딱 붙습니다(꼬리가 순간적으로 꺾임). " +
             "값을 올릴수록 한 박자 늦게 휘어 채찍처럼 보입니다. 8~15 정도가 부드럽습니다.")]
    [SerializeField, Min(0f)] private float chainFollowSpeed = 0f;

    [Tooltip("돌진하는 동안에도 객차를 머리 뒤에 매단 채로 끌고 갑니다. " +
             "끄면 예전처럼 객차에도 같은 속도를 줘서 나란히 날아갑니다.")]
    [SerializeField] private bool keepChainDuringBash = true;

    [Header("돌진")]
    [SerializeField] private float bashForce = 70f;
    [SerializeField] private float bashDuration = 0.35f;
    [SerializeField] private float invincibleDuration = 0.4f;

    private Rigidbody rb;
    private bool isAiming;
    private bool isBashing;
    private bool timeScaleTouched;
    private Coroutine bashRoutine;

    public bool IsAiming => isAiming;
    public bool IsBashing => isBashing;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (consist == null) consist = GetComponent<TrainConsist>();
        if (consist == null) consist = FindFirstObjectByType<TrainConsist>();
        if (pathFollower == null) pathFollower = GetComponent<TrainPathFollower>();
        if (pathFollower == null) pathFollower = FindFirstObjectByType<TrainPathFollower>();
        if (skillPoint == null) skillPoint = FindFirstObjectByType<PlayerSkillPoint>();
    }

    private void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (keyboard[SkillKey].wasPressedThisFrame && !isBashing && !isAiming)
        {
            // 스킬 포인트가 연결돼 있을 때만 소모 검사를 합니다(없으면 그냥 발동).
            if (skillPoint != null && skillCost > 0 && !skillPoint.UseSkillPoint(skillCost)) return;
            StartAim();
        }
        else if (keyboard[SkillKey].wasReleasedThisFrame && isAiming)
        {
            ReleaseBash();
        }
    }

    private void FixedUpdate()
    {
        // 조준 중에는 항상, 돌진 중에는 옵션이 켜져 있을 때만 편성을 다시 이어 붙입니다.
        if (isAiming || (isBashing && keepChainDuringBash)) ChainBehindHead();
    }

    // ────────────────────────────────────────────────────────────────
    // 편성 이어 붙이기
    // ────────────────────────────────────────────────────────────────

    /// <summary>
    /// 머리를 기준으로 뒤 칸들을 한 칸씩 이어 붙입니다.
    /// 앞 칸의 실제 위치를 다시 읽지 않고 <b>방금 계산한 목표값</b>을 그대로 이어받기 때문에,
    /// 칸이 늘어나도 한 프레임씩 밀리는 현상이 쌓이지 않습니다.
    /// </summary>
    private void ChainBehindHead()
    {
        if (consist == null) return;

        var cars = consist.Cars;
        if (cars == null || cars.Count < 2) return;

        float gap = consist.Gap;

        TrainCar prevCar = null;
        Vector3 prevPos = Vector3.zero;
        Quaternion prevRot = Quaternion.identity;
        Quaternion headRot = Quaternion.identity;

        for (int i = 0; i < cars.Count; i++)
        {
            TrainCar car = cars[i];

            // 이미 떨어져 나간 칸부터 뒤로는 편성이 아니므로 건드리지 않습니다.
            if (car == null || car.IsDetached) break;

            if (prevCar == null)
            {
                // 첫 칸(머리)은 플레이어가 직접 조종하므로 기준으로만 씁니다.
                prevCar = car;
                prevPos = car.Body.position;
                prevRot = car.Body.rotation;
                headRot = prevRot;
                continue;
            }

            // 앞 칸의 뒤쪽 연결 지점에서 gap만큼 뒤 → 이 칸의 앞쪽 연결 지점이 놓일 자리
            Quaternion targetRot = headRot;
            Vector3 prevRear = prevPos + prevRot * prevCar.RearAnchorLocal;
            Vector3 anchor = prevRear - (targetRot * Vector3.forward) * gap;

            // 회전을 먼저 정하고, 그 회전으로 앞쪽 연결 지점을 되돌려 몸통 중심을 구합니다.
            Vector3 targetPos = anchor - targetRot * car.FrontAnchorLocal;

            if (chainFollowSpeed > 0f)
            {
                float t = 1f - Mathf.Exp(-chainFollowSpeed * Time.fixedDeltaTime);
                targetPos = Vector3.Lerp(car.Body.position, targetPos, t);
                targetRot = Quaternion.Slerp(car.Body.rotation, targetRot, t);
            }

            if (car.Body.isKinematic)
            {
                // 키네마틱일 때는 MovePosition/MoveRotation이 보간돼서 더 부드럽습니다.
                car.Body.MovePosition(targetPos);
                car.Body.MoveRotation(targetRot);
            }
            else
            {
                car.Body.position = targetPos;
                car.Body.rotation = targetRot;
            }

            prevCar = car;
            prevPos = targetPos;
            prevRot = targetRot;
        }
    }

    // ────────────────────────────────────────────────────────────────
    // 조준 / 돌진
    // ────────────────────────────────────────────────────────────────

    private void StartAim()
    {
        isAiming = true;

        Time.timeScale = SlowScale;
        timeScaleTouched = true;

        if (pathFollower != null) pathFollower.ExternalOverride = true;

        // 속도를 0으로 누르는 대신, 아예 물리 시뮬레이션에서 빼서 완전히 고정
        SetHeadKinematic(true);
        SetTrailKinematic(true);
    }

    private void ReleaseBash()
    {
        isAiming = false;

        Time.timeScale = 1f;
        timeScaleTouched = false;

        // 머리는 물리로 날아가야 하므로 반드시 다시 다이내믹으로 돌립니다.
        SetHeadKinematic(false);

        // 매달고 갈 거면 객차는 키네마틱으로 둔 채 ChainBehindHead가 끌고 갑니다.
        if (!keepChainDuringBash) SetTrailKinematic(false);

        Vector3 direction = transform.forward;
        if (bashRoutine != null) StopCoroutine(bashRoutine);
        bashRoutine = StartCoroutine(BashRoutine(direction));
    }

    private IEnumerator BashRoutine(Vector3 direction)
    {
        isBashing = true;
        consist?.ForceInvincible(invincibleDuration);

        Vector3 velocity = direction.normalized * bashForce;
        ApplyVelocity(rb, velocity);

        // 객차를 매달고 가지 않는 경우에만 각자에게 같은 속도를 줍니다.
        if (!keepChainDuringBash && consist != null)
        {
            foreach (var car in consist.Cars)
            {
                if (car == null || car.IsDetached) continue;
                if (car.Body == rb) continue;
                ApplyVelocity(car.Body, velocity);
            }
        }

        yield return new WaitForSeconds(bashDuration);

        // 돌진이 끝나면 편성을 평소 물리 상태로 되돌립니다.
        SetTrailKinematic(false);
        SetHeadKinematic(false);

        if (pathFollower != null) pathFollower.ExternalOverride = false;
        isBashing = false;
        bashRoutine = null;
    }

    // ────────────────────────────────────────────────────────────────

    private void SetHeadKinematic(bool kinematic)
    {
        if (rb != null) rb.isKinematic = kinematic;
    }

    private void SetTrailKinematic(bool kinematic)
    {
        if (consist == null) return;

        foreach (var car in consist.Cars)
        {
            if (car == null || car.IsDetached) continue;
            if (car.Body == rb) continue;
            car.Body.isKinematic = kinematic;
        }
    }

    private static void ApplyVelocity(Rigidbody body, Vector3 velocity)
    {
        if (body == null || body.isKinematic) return;
        body.linearVelocity = velocity;
    }

    // 조준 중에 게임오버·씬 전환이 일어나면 timeScale이 0.25로 남아버리므로 반드시 되돌립니다.
    private void OnDisable()
    {
        if (timeScaleTouched)
        {
            Time.timeScale = 1f;
            timeScaleTouched = false;
        }

        if (isAiming || isBashing)
        {
            SetHeadKinematic(false);
            SetTrailKinematic(false);
            isAiming = false;
            isBashing = false;
        }

        if (pathFollower != null) pathFollower.ExternalOverride = false;
    }
}
