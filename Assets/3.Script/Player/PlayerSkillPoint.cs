using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerSkillPoint : MonoBehaviour
{
    [SerializeField] public SkillReadyUI skillUI;

    public int CurrentSkillPoint { get; private set; } = 0;

    public void AddSkillPoint()
    {
        if (CurrentSkillPoint>=3)
        {
            CurrentSkillPoint = 3;
            return;
        }

        CurrentSkillPoint++;
        skillUI.SkillOn();
    }

    public bool UseSkillPoint()
    {
        if (CurrentSkillPoint<1)
        {
            return false;
        }

        CurrentSkillPoint--;
        skillUI.QSkillUse();

        return true;
    }

    public bool UseSkillPoint(int point)
    {
        if(CurrentSkillPoint<point)
        {
            return false;
        }

        CurrentSkillPoint -= point;
        skillUI.ESkillUse();

        return true;
    }
}
