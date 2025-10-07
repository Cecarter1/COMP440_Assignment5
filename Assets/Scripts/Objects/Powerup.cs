using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static UnityEngine.RuleTile.TilingRuleOutput;

public class Powerup : MonoBehaviour
{
    public AbilityModifier abilityModifier;

    public float speed = 2f;
    public float height = 0.5f;

    private Vector2 startPosition;

    public ParticleSystem fire;
    public ParticleSystem explosion;
    public GameObject player;

    public void Start()
    {
        startPosition = transform.position;
        fire = Instantiate(fire, transform.position, Quaternion.Euler(-90, 0, 0));
        explosion = Instantiate(explosion, transform.position, Quaternion.Euler(-90, 0, 0));
    }

    public void Update()
    {
        float newY = startPosition.y + Mathf.Sin(Time.time * speed) * height;
        transform.position = new Vector2(transform.position.x, newY);
        fire.transform.position = transform.position;

        if (Math.Pow(player.transform.position.x - transform.position.x, 2) < 25 && !fire.isPlaying)
        {
            fire.Play(true);
        }
        else if (Math.Pow(player.transform.position.x - transform.position.x, 2) > 25 && fire.isPlaying)
        {
            fire.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

    }

    public void OnEnable()
    {
        fire.gameObject.SetActive(true);
        fire.Play(true);
        Debug.Log(fire.isPlaying);
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
        playerController.ApplyPowerupModifier(abilityModifier, gameObject);
        var powerupModifier = abilityModifier as PowerupModifier;
        
        if (powerupModifier != null)
        {
            gameObject.SetActive(false);
        }
        else
        {
            Destroy(gameObject);
        }
    }
}
