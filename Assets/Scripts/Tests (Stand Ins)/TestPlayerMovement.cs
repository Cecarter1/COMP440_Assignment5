using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class TestPlayerMovement : MonoBehaviour
{
    [SerializeField] private float speed;
    private Rigidbody2D body;

    public bool canDoubleJump = false;
    public bool canWallJump = false;
    public bool canDash = false;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        Debug.Log("Player Movement: " + gameObject.GetInstanceID());
    }

    private void Update()
    {
        body.velocity = new Vector2(Input.GetAxis("Horizontal") * speed, body.velocity.y);

        if (Input.GetKey(KeyCode.Space))
            body.velocity = new Vector2(body.velocity.x, speed);
    }

    public void ApplyPowerupModifier(AbilityModifier abilityModifier, GameObject powerup)
    {
        abilityModifier.Activate(gameObject);
        var powerupModifier = abilityModifier as PowerupModifier;

        if(powerupModifier != null)
        {
            StartCoroutine(powerupModifier.StartPowerupCountdown(gameObject, powerup));
        }
    }

    public void EnableDash()
    {
        canDash = true;
    }
}
