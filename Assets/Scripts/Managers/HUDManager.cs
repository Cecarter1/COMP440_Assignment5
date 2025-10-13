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

    [Header("Powerups")]
    public Image powerupImg;
    public Sprite invincibilitySpr;
    public Sprite speedBoostSpr;
    public Sprite unlimGravityManipSpr;

    public Slider powerupSlider;
    public float powerupTimer;
    public bool stopPowerupTimer = false;

    public void Start()
    {
        UpdateHealth();

        for (int i = 0; i < abilities.Length; i++)
        {
            abilities[i].enabled = false;
            abilities[i].transform.parent.GetComponent<Image>().enabled = false;
        }
        
        powerupImg.enabled = false;
        powerupImg.transform.parent.GetComponent<Image>().enabled = false;
        powerupSlider.gameObject.SetActive(false);
    }

    public void Update()
    {
        UpdateTimer();
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

    public void UpdatePowerup(PowerupModifier powerup)
    {
        powerupImg.enabled = true;
        powerupImg.transform.parent.GetComponent<Image>().enabled = true;
        if (powerup as InvincibilityModifier)
        {
            powerupImg.sprite = invincibilitySpr;
        }
        else if (powerup as SpeedBoostModifier)
        {
            powerupImg.sprite = speedBoostSpr;
        }
        else if (powerup as UnlimGravityManipModifier)
        {
            powerupImg.sprite = unlimGravityManipSpr;
        }

        powerupTimer = 0;
        powerupSlider.maxValue = powerup.powerupDeactivateSec;
        powerupSlider.value = 0;
        powerupSlider.gameObject.SetActive(true);
        stopPowerupTimer = false;
        StartPowerupTimer();
    }

    public void StartPowerupTimer()
    {
        StartCoroutine(UpdatePowerupTimer());
    }


    IEnumerator UpdatePowerupTimer()
    {
        while (stopPowerupTimer == false)
        {
            powerupTimer += Time.deltaTime;
            yield return new WaitForSeconds(0.001f);

            if (powerupTimer >= powerupSlider.maxValue)
            {
                stopPowerupTimer = true;
            }

            if (stopPowerupTimer == false)
            {
                powerupSlider.value = powerupTimer;
            }
        }
        disablePowerup();
    }

    public void disablePowerup()
    {
        powerupImg.enabled = false;
        powerupImg.transform.parent.GetComponent<Image>().enabled = false;
        powerupSlider.gameObject.SetActive(false);
    }
}
