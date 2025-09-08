using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
public class Title_Button_System : MonoBehaviour
{
    public SpriteRenderer startbutton;
    public SpriteRenderer settingbutton;
    public SpriteRenderer finishbutton;
    public Sprite StartSprite;
    public Sprite StartSpriteChoice;
    public Sprite SettingSprite;
    public Sprite SettingSpriteChoice;
    public Sprite FinishSprite;
    public Sprite FinishSpriteChoice;
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
}