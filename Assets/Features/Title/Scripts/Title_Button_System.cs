using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

public class Title_Button_System : MonoBehaviour
{
    /*
    public SpriteRenderer startbutton;
    public SpriteRenderer settingbutton;
    public SpriteRenderer finishbutton;
    public Sprite StartSprite;
    public Sprite StartSpriteChoice;
    public Sprite SettingSprite;
    public Sprite SettingSpriteChoice;
    public Sprite FinishSprite;
    public Sprite FinishSpriteChoice;
    public ChangeScene changescene;
    private int keyplace = 0;
    public void OnMove(InputAction.CallbackContext value)       //タイトルにおけるキー取得、現在上下矢印キーのみ認識
    {
        Vector2 v = value.ReadValue<Vector2>();

        float keyinput = v.y;

        if (keyinput >= 0.8)
        {
            keyplace += 1;              //選択を一つ上のものにする
        }
        else if (keyinput <= -0.8)
        {
            keyplace += -1;             //選択を一つ下のものにする
        }
    }
    void Update()
    {
        Choicebutton(startbutton, StartSpriteChoice, StartSprite, keyplace % 3 == 0);                               //startbutton選択中
        Choicebutton(settingbutton, SettingSpriteChoice, SettingSprite, keyplace % 3 == 1 || keyplace % 3 == -2);   //settingbutton選択中
        Choicebutton(finishbutton, FinishSpriteChoice, FinishSprite, keyplace % 3 == 2 || keyplace % 3 == -1);      //Finishbutton選択中
    }
    void Choicebutton(SpriteRenderer button, Sprite activebutton, Sprite inactivebutton,bool active)
    {
        if (active)
        {
            button.sprite = activebutton;
        }
        else
        {
            button.sprite = inactivebutton;
        }
    }
    public void Onselect(InputAction.CallbackContext value)
    {
        if(value.performed){
            SelectAction(keyplace);
        }
    }
    void SelectAction(int SelectKey){
        //開始ボタンが押されている
        if(SelectKey % 3 == 0){
            changescene.OnClickToChangeScene("OverWorldScene");
        }
        //設定ボタンが押されている
        if(keyplace % 3 == 1 || keyplace % 3 == -2){

        }
        //終了ボタンが押されている
        if(keyplace % 3 == 2 || keyplace % 3 == -1){
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

    }
    */
    public ChangeScene changescene;
    [SerializeField] private TextMeshProUGUI resetConfirmText;
    [SerializeField, Min(1)] private int resetRequiredPressCount = 10;

    private int resetRemainingPressCount;
    private bool hasCompletedReset;

    private void Awake()
    {
        HideResetConfirmText();
    }

    public void SelectStartKey(){
        changescene.OnClickToChangeScene("OverWorldScene");
    }
    public void SelectWebsiteKey()
    {
        Application.OpenURL("https://senju.dendaikyo.com/game/momodig-trial-info/");
    }
    public void SelectResetSaveKey()
    {
        if (resetConfirmText == null)
        {
            Debug.LogError("Title_Button_System: resetConfirmText is not assigned.", this);
            return;
        }

        if (hasCompletedReset)
        {
            hasCompletedReset = false;
            resetRemainingPressCount = 0;
            HideResetConfirmText();
            return;
        }

        if (resetRemainingPressCount <= 0)
        {
            resetRemainingPressCount = resetRequiredPressCount;
            UpdateResetConfirmText();
            return;
        }

        if (resetRemainingPressCount > 1)
        {
            resetRemainingPressCount--;
            UpdateResetConfirmText();
            return;
        }

        GameDataPersistenceManager persistenceManager = GameDataPersistenceManager.Instance;
        if (persistenceManager == null)
        {
            Debug.LogError("Title_Button_System: GameDataPersistenceManager is not initialized.", this);
            return;
        }

        if (!persistenceManager.DeleteSaveAndResetRuntimeState())
        {
            Debug.LogError("Title_Button_System: Failed to delete save data.", this);
            return;
        }

        hasCompletedReset = true;
        resetRemainingPressCount = 0;
        resetConfirmText.SetText("リセットしました");
    }

    private void UpdateResetConfirmText()
    {
        if (resetConfirmText == null)
        {
            return;
        }

        if (resetRemainingPressCount <= 1)
        {
            resetConfirmText.SetText("次押したらリセットされます");
            return;
        }

        resetConfirmText.SetText("あと{0}回押すとリセット", resetRemainingPressCount);
    }

    private void HideResetConfirmText()
    {
        if (resetConfirmText == null)
        {
            return;
        }

        resetConfirmText.SetText(string.Empty);
    }

    public void SelectFinishKey(){
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
