using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SkillReadyUI : MonoBehaviour
{
    [SerializeField] private Sprite skillOnSprite;
    [SerializeField] private Sprite skillOffSprite;

    [SerializeField] private Image[] skillBoxUI = new Image[3]; // 3Ä­

    private int onSkillNum = 0; // 0 ¾øÀ½ / 1 / 2 / 3 Ä­

    public void SkillOn()
    {
        if(onSkillNum.Equals(3))
        {
            return;
        }

        onSkillNum++;
        skillBoxUI[onSkillNum - 1].sprite = skillOnSprite;
    }

    public void QSkillUse()
    {
        if(onSkillNum <=0)
        {
            return;
        }

        skillBoxUI[onSkillNum - 1].sprite = skillOffSprite;
        onSkillNum--;
    }

    public void ESkillUse()
    {
        if(onSkillNum <3)
        {
            return;
        }

        for(int i = 0; i<skillBoxUI.Length; i++)
        {
            skillBoxUI[i].sprite = skillOffSprite;
        }

        onSkillNum = 0;
    }

    public void Debug_OnClickSkillOn()
    {
        SkillOn();
    }

    public void Debug_OnClickQSkillUse()
    {
        QSkillUse();
    }

    public void Debug_OnClickESkillUse()
    {
        ESkillUse();
    }

}
