using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using static UnityEngine.RuleTile.TilingRuleOutput;

public class Powerup : MonoBehaviour
{
    public AbilityModifier abilityModifier;
    public HUDManager hudManager;
    public GameObject player;

    [Header("Powerup Floating")]
    public float speed = 2f;
    public float height = 0.5f;
    private Vector2 startPosition;

    [Header("Particle System")]
    public ParticleSystem fire;
    public ParticleSystem explosion;

    public Gradient powerupParticleGradient;

    public void Start()
    {
        startPosition = transform.position;
        fire = Instantiate(fire, new Vector2(transform.position.x, transform.position.y - 0.6f), Quaternion.Euler(-90, 0, 0));
        explosion = Instantiate(explosion, transform.position, Quaternion.Euler(-90, 0, 0));

        var col = fire.colorOverLifetime;
        col.color = powerupParticleGradient;
        col = fire.transform.GetChild(0).gameObject.GetComponent<ParticleSystem>().colorOverLifetime;
        col.color = powerupParticleGradient;
        col = explosion.colorOverLifetime;
        col.color = powerupParticleGradient;
    }

    public void Update()
    {
        UpdatePowerupPosition();
    }

    public void OnEnable()
    {
        fire.gameObject.SetActive(true);
        fire.Play(true);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        fire.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        fire.gameObject.SetActive(false);
        explosion.gameObject.SetActive(true);
        explosion.Play(true);
        var playerController = collision.GetComponent<TestPlayerMovement>();

        if (playerController != null)
        {
            ActivatePowerup(playerController);
        }
    }

    void ActivatePowerup(TestPlayerMovement playerController)
    { 
        var powerupModifier = abilityModifier as PowerupModifier;
        Debug.Log(playerController.isPowerupActive);

        // Decides whether to destroy or deactivate collectible
        if (powerupModifier != null)
        {
            if (!playerController.isPowerupActive)
            {
                playerController.GetComponent<TestPlayerMovement>().isPowerupActive = true;
                hudManager.UpdatePowerup(powerupModifier);
                gameObject.SetActive(false);
            }
        }
        else
        {

            playerController.ApplyPowerupModifier(abilityModifier, gameObject);
            hudManager.UpdateAbilities();
            Destroy(gameObject);
        }
    }

    public void UpdatePowerupPosition()
    {
        float newY = startPosition.y + Mathf.Sin(Time.time * speed) * height;
        transform.position = new Vector2(transform.position.x, newY);
        fire.transform.position = new Vector2(transform.position.x, transform.position.y - 0.6f);

        if (Math.Pow(player.transform.position.x - transform.position.x, 2) < 25 && !fire.isPlaying)
        {
            fire.Play(true);
        }
        else if (Math.Pow(player.transform.position.x - transform.position.x, 2) > 25 && fire.isPlaying)
        {
            fire.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    public void OnDestroy()
    {
        Destroy(fire);
        Destroy(explosion);
    }
}
