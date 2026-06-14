using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class ToturialManager : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private enum TutorialStep
    {
        Welcome,
        Introduction,      
        ExplainRules1,
        ExplainRules2,
        ExplainRules3,
        ExplainRules4,
        SetupLevel0,       
        PlayingLevel0,     
        SetupLevel1_5,     
        PlayingLevel1_5,   
        Complete           
    }
    private TutorialStep currentStep = TutorialStep.Welcome;

    [Header("UI Elements")]
    [SerializeField] private GameObject Frog;
    [SerializeField] private GameObject IntroducePanel;
    [SerializeField] private Button Retry;
    [SerializeField] private Button HighLight;
    [SerializeField] private TextMeshProUGUI InstructionText;
    [SerializeField] private Image ExplainImage;

    [Header("Sprites")]
    [SerializeField] private Sprite[] ExplainSprite;


    void Start()
    {
     UpdateToToturialState();
    }

    // Update is called once per frame
    void Update()
    {
       if(Input.GetMouseButtonDown(0))
        {
            if(currentStep == TutorialStep.Welcome ||
             currentStep == TutorialStep.Introduction ||
             currentStep == TutorialStep.ExplainRules1 ||
             currentStep == TutorialStep.ExplainRules2 ||
             currentStep == TutorialStep.ExplainRules3 ||
             currentStep == TutorialStep.ExplainRules4)
            {
                AdvanceStep();
            }
        }
    }
    private void AdvanceStep()
    {
        currentStep++;
        UpdateToToturialState();
    }
    private void Talk(string message)
    {
        InstructionText.text = message;
    }
    private void UpdateToToturialState()
    {
        switch (currentStep)
        {
            case TutorialStep.Welcome:
                IntroducePanel.SetActive(true);
                Talk("Welcome to the game! I am Mr. Prince, and it's bread season now! ~~");
                break;
            case TutorialStep.Introduction:
                Talk("Let me introduce you to the game. You will control a frog, and your mission is to help me return home for the bread season!");
                break;
            case TutorialStep.ExplainRules1:
                Talk("But there are some rules to move the frog! First, Mr. Prince can only stand on the lotus leaves.");
                ChangeExplanationImage(0);
                break;
                case TutorialStep.ExplainRules2:
                Talk("Second, to move, the frog must jump in an 'L' shape (2 squares ahead and 1 square to the side). Try jumping to an available lotus leaf now!");
                    ChangeExplanationImage(1);
                break;
                case TutorialStep.ExplainRules3:
                Talk("Third, every time the frog jumps off a lotus leaf, it will sink into the water! That means you cannot stand on the same leaf twice.");
                ChangeExplanationImage(2);
                break;
                case TutorialStep.ExplainRules4:
                Talk("Finally, you win the level when the frog has successfully jumped across EVERY single lotus leaf on the screen. Let's start training, shall we?");
                ChangeExplanationImage(3);
                break;
                case TutorialStep.SetupLevel0:
                IntroducePanel.SetActive(false);
                break;
        }
    }
    private void ChangeExplanationImage(int spriteIndex)
    {
        if(ExplainImage==null || ExplainSprite==null || spriteIndex < 0 || spriteIndex >= ExplainSprite.Length)
        {
            Debug.LogWarning("Invalid sprite index or missing references.");
            return;
          
        }else if (spriteIndex >= 0 && spriteIndex < ExplainSprite.Length)
        {
            ExplainImage.gameObject.SetActive(true);
            ExplainImage.sprite = ExplainSprite[spriteIndex];
        }
        else
        {
            ExplainImage.gameObject.SetActive(false);
        }
    }
}
