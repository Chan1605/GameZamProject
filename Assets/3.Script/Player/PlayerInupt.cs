using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInupt : MonoBehaviour
{
    public Vector2 RotateValue { get; private set; }
    public bool DoJump { get; private set; }

    public event Action OnSkillQPressed;
    public bool TimeSlow { get; private set; }

    [SerializeField] private Vector2 Viewing_value = Vector2.zero;

    [Tooltip("플레이어의 회전 관리")]
    public void Event_Rotate(InputAction.CallbackContext context)
    {
        if (context.phase.Equals(InputActionPhase.Performed))
        {
            Viewing_value = context.ReadValue<Vector2>();
        }
        else if (context.phase.Equals(InputActionPhase.Canceled))
        {
            Viewing_value = Vector2.zero;
        }

        RotateValue = Viewing_value;
        //앞뒤 회전: y값, 좌우회전: x값 (양수는 오른쪽, 음수는 왼쪽)
    }

    [Tooltip("스페이스바를 누르는 동안 위로 올라가게")]
    public void Event_Jump(InputAction.CallbackContext context)
    {
        if (context.phase.Equals(InputActionPhase.Performed))
        {
            DoJump = true;
        }
        else if (context.phase.Equals(InputActionPhase.Canceled))
        {
            DoJump = false;
        }
    }

    [Tooltip("Q를 누르면 스킬이 발동함")]
    public void Event_SkillQ(InputAction.CallbackContext context)
    {
        if (context.phase.Equals(InputActionPhase.Started))
        {
            OnSkillQPressed?.Invoke();
            //1회만 발동시에는 이벤트로 관리하는 게 더 좋음
        }
    }

    [Tooltip("E를 누르는 동안 스킬이 발동함")]
    public void Event_SkillE(InputAction.CallbackContext context)
    {
        if (context.phase.Equals(InputActionPhase.Performed))
        {
            TimeSlow = true;
        }
        else if (context.phase.Equals(InputActionPhase.Canceled))
        {
            TimeSlow = false;
        }
    }
}
