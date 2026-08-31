using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SkillReadyUI : MonoBehaviour
{
    [SerializeField] private Sprite skillOnSprite;
    [SerializeField] private Sprite skillOffSprite;

    [SerializeField] private Image[] skillBoxUI = new Image[3]; // 3칸

    private int onSkillNum; // 0 없음 / 1 / 2 / 3 칸

    private void Start()
    {
        onSkillNum = 0;
        SoundMgr.Instance.PlayBGM("Main_BGM", 0.2f);
    }

    public void SkillOn()
    {
        if(onSkillNum>3)
        {
            onSkillNum = 3;
            return;
        }

        onSkillNum++;
        skillBoxUI[onSkillNum - 1].sprite = skillOnSprite;
        Debug.Log($"{onSkillNum} 번 이미지 변경합니다");
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
