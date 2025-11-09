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
    public GameObject player; // Ensure this is set to the Player GameObject in the Inspector

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
        // Instantiate and position particle systems
        fire = Instantiate(fire, new Vector2(transform.position.x, transform.position.y - 0.6f), Quaternion.Euler(-90, 0, 0));
        explosion = Instantiate(explosion, transform.position, Quaternion.Euler(-90, 0, 0));

        // Apply particle gradient settings
        var col = fire.colorOverLifetime;
        col.color = powerupParticleGradient;
        col = fire.transform.GetChild(0).gameObject.GetComponent<ParticleSystem>().colorOverLifetime;
        col.color = powerupParticleGradient;
        col = explosion.colorOverLifetime;
        col.color = powerupParticleGradient;
    }

    // CRITICAL FIX: The standard Update() method must be present to call the floating logic.
    public void Update()
    {
        UpdatePowerupPosition();
    }

    public void OnEnable()
    {
        fire.gameObject.SetActive(true);
        fire.Play(true);
    }

    // The method for floating motion.
    public void UpdatePowerupPosition()
    {
        float newY = startPosition.y + Mathf.Sin(Time.time * speed) * height;
        transform.position = new Vector2(transform.position.x, newY);
        fire.transform.position = new Vector2(transform.position.x, transform.position.y - 0.6f);

        // Logic to play/stop particles based on player proximity
        if (Math.Pow(player.transform.position.x - transform.position.x, 2) < 25 && !fire.isPlaying)
        {
            fire.Play(true);
        }
        else if (Math.Pow(player.transform.position.x - transform.position.x, 2) > 25 && fire.isPlaying)
        {
            fire.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    // Handles player collision (set up as a trigger)
    private void OnTriggerEnter2D(Collider2D collision)
    {
        // Stop visuals
        fire.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        fire.gameObject.SetActive(false);
        explosion.gameObject.SetActive(true);
        explosion.Play(true);

        // *** Get the correct PlayerController component ***
        var playerController = collision.GetComponent<PlayerController>();

        if (playerController != null)
        {
            ActivatePowerup(playerController);
        }
    }

    void ActivatePowerup(PlayerController playerController)
    {
        var powerupModifier = abilityModifier as PowerupModifier;

        if (powerupModifier != null) // Handles timed power-ups (like speed/jump boost)
        {
            if (!playerController.isPowerupActive)
            {
                // Activates the effect via the AbilityModifier base class method
                abilityModifier.Activate(playerController.gameObject);

                playerController.GetComponent<PlayerController>().isPowerupActive = true;
                // hudManager.UpdatePowerup(powerupModifier);
                gameObject.SetActive(false);
            }
        }
        else // Handles permanent abilities (like Double Jump)
        {
            // Activates the permanent effect
            abilityModifier.Activate(playerController.gameObject);

            // hudManager.UpdateAbilities();
            Destroy(gameObject); // Permanently remove the collectible
        }
    }

    public void OnDestroy()
    {
        // Clean up instantiated particle systems when this object is destroyed
        Destroy(fire);
        //Destroy(explosion); 
        // Note: Explosion is often left to play out, so keep the Destroy() commented out unless needed.
    }
}