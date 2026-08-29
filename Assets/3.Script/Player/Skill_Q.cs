using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Skill_Q : MonoBehaviour
{
    private TrainConsist consist;
    [SerializeField] private PlayerInupt input;

    [Tooltip("스킬 1개가 찰 때까지의 횟수")]
    private int skillCharge_Max = 3;
    [Tooltip("스킬이 쌓일 수 있는 최대 횟수")]
    private int skillStackLimit = 3;

    [Tooltip("현재 스킬의 충전된 정도")]
    [SerializeField] private int SkillCharge; //{ get; private set; }
    [Tooltip("현재 스킬이 쌓인 횟수")]
    [SerializeField] private int SkillStack; //{ get; private set; }

    [Tooltip("Q 스킬의 이름")]
    public string SkillName = "[SerializeTrain]";

    private void Awake()
    {
        consist = FindFirstObjectByType<TrainConsist>();
        TryGetComponent(out input);
    }

    private void Start() //게임을 시작할 때 값을 초기화
    {
        SkillCharge = 0;
        SkillStack = 1;
    }

    private void OnEnable()
    {
        if (input != null)
            input.OnSkillQPressed += HandleSkillQPressed;
    }

    private void OnDisable()
    {
        if (input != null)
            input.OnSkillQPressed -= HandleSkillQPressed;
    }

    private void HandleSkillQPressed()
    {
        Check_SkillQ();
        SkillCharge++;
        SkillStack++;
    }

    [Tooltip("Q 스킬을 실행하는 실행부")]
    public void Straighten()
    {
        if (consist == null || consist.Locomotive == null) 
        {
            return; //방어 코드
        }

        Rigidbody carriage = consist.Locomotive.Body;
        Quaternion targetRot = carriage.rotation; //객차의 회전
        Vector3 chainAnchor = carriage.position + targetRot * consist.Locomotive.RearAnchorLocal
                     - (targetRot * Vector3.forward) * consist.Gap; //다음 차량이 붙어야 할 좌표

        foreach (TrainCar car in consist.Cars)
        {
            if (car.Equals(consist.Locomotive))
            {
                continue;
            }

            if (car.IsDetached)
            {
                break;
            }

            //객차들의 위치를 이전 객차들을 기준으로 이동
            Vector3 currentAnchor = car.Body.position + targetRot * car.FrontAnchorLocal;
            Vector3 newPos = car.Body.position + (chainAnchor - currentAnchor);
            //newPos로 따로 저장해서 지연 없이 확실하게 반영되게 만듦
            car.Body.MovePosition(newPos);

            //객차들의 회전을 기관차와 동일하게 만들기
            car.Body.MoveRotation(targetRot);

            //다음 차량이 붙어야 할 자리를 계산함
            chainAnchor = newPos + targetRot * car.RearAnchorLocal - (targetRot * Vector3.forward) * consist.Gap;
        }

    }
    
    [Tooltip("Q 스킬을 충전하는 메소드")]
    public void Charge_QSkill()
    {
        SkillCharge++;

        if (SkillStack >= skillStackLimit)
        {
            SkillCharge = 0;
            return;
        }

        if (SkillCharge >= skillCharge_Max)
        {
            SkillCharge = 0;
            SkillStack++;
            //앞의 if 문을 지나쳐 이 코드에 도달했다면 반드시 SkillStack < skillStackLimit이다 
        }
    }

    public void Check_SkillQ()
    {
        if (SkillStack < 0)
        {
            return;
        }

        Straighten();
        SkillStack--;
    }

}
