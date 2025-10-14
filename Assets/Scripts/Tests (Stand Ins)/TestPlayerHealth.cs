using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TestPlayerHealth : MonoBehaviour
{
    public int health = 10;
    public int maxHealth = 10;

    HUDManager hudManager;
    TestPlayerMovement playerMovement;

    public bool isInvincible;

    public void OnEnable()
    {
        playerMovement = GameObject.Find("Player").GetComponent<TestPlayerMovement>();
        hudManager = GameObject.Find("HUD Manager").GetComponent<HUDManager>();
        Debug.Log("Player Health: " + gameObject.GetInstanceID());
    }

    public void AddHealth(int healthAmount)
    {
        if (health + healthAmount <= maxHealth)
        {
            health += healthAmount;
        }
        else
        {
            health = maxHealth;
        }

            hudManager.UpdateHealth();
    }

    public void SubtractHealth(int healthAmount)
    {
        if (!isInvincible)
        {
            if(health - healthAmount > 0)
            {
                health -= healthAmount;
            }
            else
            {
                healthAmount = 0;
            }
            hudManager.UpdateHealth();
        }
    }
}
