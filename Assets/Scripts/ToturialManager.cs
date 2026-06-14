using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class ToturialManager : MonoBehaviour
{
    private enum TutorialStep
    {
        Welcome,
        Introduction,      
        ExplainRules1,
        ExplainRules2,
        ExplainRules3,
        ExplainRules4,
        ExplainRules5,
        ExplainRules6,
        SetupLevel0,       
        PlayingLevel0,
        WinLevel0,
        SetupLevel1,       
        PlayingLevel1,
        WinLevel1,
        SetupLevel2,       
        PlayingLevel2,
        Complete,
        LevelFailed
    }
    private TutorialStep currentStep = TutorialStep.Welcome;
    private bool isFailedState = false;

    [Header("UI Elements")]
    [SerializeField] private GameObject Frog;
    [SerializeField] private GameObject IntroducePanel;
    [SerializeField] private Button Retry;
    [SerializeField] private Button HighLight;
    [SerializeField] private TextMeshProUGUI InstructionText;
    [SerializeField] private Image ExplainImage;
    [SerializeField] private Button RetryButton;

    [Header("Sprites")]
    [SerializeField] private Sprite[] ExplainSprite;
    [SerializeField] private Image HighlightImage;
    [SerializeField] private Image RetryImage;

    [Header("Game Controller")]
    [SerializeField] private TutorialGameController gameController;

    void Start()
    {
        if (gameController == null)
        {
            gameController = Object.FindAnyObjectByType<TutorialGameController>();
        }

        if (Retry != null) Retry.onClick.AddListener(OnRetryClick);

        UpdateToToturialState();
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            if (isFailedState)
            {
                isFailedState = false;
                IntroducePanel.SetActive(false);
                if (gameController != null)
                {
                    gameController.PlayAgain();
                }
                return;
            }

            if (currentStep == TutorialStep.Welcome ||
                currentStep == TutorialStep.Introduction ||
                currentStep == TutorialStep.ExplainRules1 ||
                currentStep == TutorialStep.ExplainRules2 ||
                currentStep == TutorialStep.ExplainRules3 ||
                currentStep == TutorialStep.ExplainRules4 ||
                currentStep == TutorialStep.ExplainRules5 ||
                currentStep == TutorialStep.ExplainRules6 ||
                currentStep == TutorialStep.WinLevel0 ||
                currentStep == TutorialStep.WinLevel1 ||
                currentStep == TutorialStep.Complete)
            {
                AdvanceStep();
            }
        }
    }

    private void AdvanceStep()
    {
        Debug.Log($"[ToturialManager] AdvanceStep. Old step: {currentStep}");
        // Nếu ở bước Complete và click chuột tiếp, chuyển về màn chọn Level
        if (currentStep == TutorialStep.Complete)
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene("LevelSelectScene");
            return;
        }

        currentStep++;
        Debug.Log($"[ToturialManager] New step: {currentStep}");
        UpdateToToturialState();
    }

    private void Talk(string message)
    {
        InstructionText.text = message;
    }

    private void OnRetryClick()
    {
        isFailedState = false;
        if (IntroducePanel != null) IntroducePanel.SetActive(false);
        
        if (gameController != null)
        {
            gameController.PlayAgain();
            if (currentStep == TutorialStep.PlayingLevel0)
                Talk("First lesson: Reach every lotus leaf!");
            else if (currentStep == TutorialStep.PlayingLevel1)
                Talk("Second lesson: Plan your path carefully!");
            else if (currentStep == TutorialStep.PlayingLevel2)
                Talk("Final lesson: Complete the Knight's Tour!");
        }
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
            case TutorialStep.ExplainRules5:
                Talk("Look at the UI! If you ever get stuck and don't know where to jump next, just press the <color=#00FFCC>Highlight button</color> to reveal all available legal moves!");
                HighLight.gameObject.SetActive(true);
                ExplainImage.gameObject.SetActive(false);
                HighlightImage.gameObject.SetActive(true);
                break;
            case TutorialStep.ExplainRules6:
                Talk("And if you want to start the level over, just press the <color=#00FFCC>Retry button</color> to reset the level and try again!");
                 RetryImage.gameObject.SetActive(true);
                 HighlightImage.gameObject.SetActive(false);
                break;
            // LEVEL 0
            case TutorialStep.SetupLevel0:
                ExplainImage.gameObject.SetActive(false);
                HighlightImage.gameObject.SetActive(false);
                RetryImage.gameObject.SetActive(false);
                IntroducePanel.SetActive(false);
                if (gameController != null)
                {
                    gameController.LoadLevel(0);
                }
                currentStep = TutorialStep.PlayingLevel0;
                break;

            case TutorialStep.PlayingLevel0:
                // Trạng thái chơi, ẩn panel hội thoại để người dùng thao tác nhảy
                break;

            case TutorialStep.WinLevel0:
                IntroducePanel.SetActive(true);
                Talk("Perfect! You easily completed the first level. Let's proceed to the next one!");
                break;

            // LEVEL 1
            case TutorialStep.SetupLevel1:
                IntroducePanel.SetActive(false);
                if (gameController != null)
                {
                    gameController.LoadLevel(1);
                }
                currentStep = TutorialStep.PlayingLevel1;
                break;

            case TutorialStep.PlayingLevel1:
                break;

            case TutorialStep.WinLevel1:
                IntroducePanel.SetActive(true);
                Talk("Awesome job! You are getting really good at this. One final challenge remains!");
                break;

            // LEVEL 2
            case TutorialStep.SetupLevel2:
                IntroducePanel.SetActive(false);
                if (gameController != null)
                {
                    gameController.LoadLevel(2);
                }
                currentStep = TutorialStep.PlayingLevel2;
                break;

            case TutorialStep.PlayingLevel2:
                break;

            // HOÀN THÀNH TUTORIAL
            case TutorialStep.Complete:
                IntroducePanel.SetActive(true);
                Talk("Incredible! You have completed all training levels. You are now a master of the Knight's Tour! Click anywhere to enter the adventure.");
                break;
        }
    }

    private void ChangeExplanationImage(int spriteIndex)
    {
        if (ExplainImage == null || ExplainSprite == null)
        {
            Debug.LogWarning("Missing UI references for explanations.");
            return;
        }

        if (spriteIndex >= 0 && spriteIndex < ExplainSprite.Length)
        {
            ExplainImage.gameObject.SetActive(true);
            ExplainImage.sprite = ExplainSprite[spriteIndex];
        }
        else
        {
            ExplainImage.gameObject.SetActive(false);
        }
    }

    // ──────────── Callback khi thắng level từ TutorialGameController ────────────
    public void OnLevelCompleted()
    {
        if (currentStep == TutorialStep.PlayingLevel0)
        {
            currentStep = TutorialStep.WinLevel0;
            UpdateToToturialState();
        }
        else if (currentStep == TutorialStep.PlayingLevel1)
        {
            currentStep = TutorialStep.WinLevel1;
            UpdateToToturialState();
        }
        else if (currentStep == TutorialStep.PlayingLevel2)
        {
            currentStep = TutorialStep.Complete;
            UpdateToToturialState();
        }
    }

    // ──────────── Callback khi thua level từ TutorialGameController ────────────
    public void OnLevelFailed()
    {
        isFailedState = true;
        IntroducePanel.SetActive(true);
        Talk("Oh no! No more moves possible. Click anywhere on screen to retry this level!");
    }
}
