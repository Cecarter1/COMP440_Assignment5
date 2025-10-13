using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HUDManager : MonoBehaviour
{
    [Header("Hearts")]
    public TestPlayerHealth playerHealth;
    public Image[] hearts;
    public Sprite fullHeart;
    public Sprite halfHeart;
    public Sprite emptyHeart;

    [Header("Timer")]
    public float elapsedTime;
    public TMP_Text timerText;

    [Header("Abilities")]
    public TestPlayerMovement playerMovement;
    public Image[] abilities;
    public Image[] frames;
    public Sprite doubleJump;
    public Sprite wallJump;
    public Sprite dash;

    [Header("Powerups")]
    public Image powerupImg;
    public Image powerupTimerImg;
    public bool powerupActive;

    public void Start()
    {
        UpdateHealth();

        Debug.Log("Start1: " + abilities[2]);
        for (int i = 0; i < abilities.Length; i++)
        {
            abilities[i].enabled = false;
            abilities[i].transform.parent.GetComponent<Image>().enabled = false;
        }
        
        Debug.Log("Start2: " + abilities[2]);
        powerupImg.enabled = false;
        powerupImg.transform.parent.GetComponent<Image>().enabled = false;

        Debug.Log("HUD Player Health: " + playerHealth.GetInstanceID());
        Debug.Log("HUD Player Movement: " + playerMovement.GetInstanceID());
    }

    public void Update()
    {
        UpdateTimer();
        //UpdateAbilities();
    }

    public void UpdateTimer()
    {
        elapsedTime += Time.deltaTime;
        int minutes = Mathf.FloorToInt(elapsedTime / 60);
        int seconds = Mathf.FloorToInt(elapsedTime % 60);
        timerText.text = string.Format("{0:00}:{1:00}", minutes, seconds);
    }

    public void UpdateHealth()
    {
        int numFullHearts = Mathf.FloorToInt(playerHealth.health / 2);
        int numHalfHearts = Mathf.CeilToInt(playerHealth.health % 2);

        for (int i = 0; i < numFullHearts; i ++)
        {
            hearts[i].sprite = fullHeart;
        }
        
        if (numHalfHearts > 0)
        {
            hearts[numFullHearts].sprite = halfHeart;
        }

        if (playerHealth.health < 9)
        {
            for (int i = 4; i > numFullHearts; i --)
            {
                hearts[i].sprite = emptyHeart;
            }
        }
    }

    public void UpdateAbilities()
    {
        Debug.Log("UpdateAbilities: " + abilities[2]);
        if (playerMovement.canDoubleJump)
        {
            abilities[0].enabled = true;
            frames[0].enabled = true;
        }
        if (playerMovement.canWallJump)
        {
            abilities[1].enabled = true;
            frames[1].enabled = true;
        }
        if (playerMovement.canDash)
        {
            abilities[2].enabled = true;
            frames[2].enabled = true;
        }
    }

    public void UpdatePowerup()
    {
        
    }
}
