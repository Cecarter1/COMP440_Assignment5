using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

public class EnemyProjectile : EnemyDamage
{
    [SerializeField] private float speed;
    [SerializeField] private float resetTime;
    private float lifetime;
    private int dir;
    private SpriteRenderer spriteRenderer;

    public void ActivateProjectile(int direction)
    {
        dir = direction;
        if(dir < 0)
        {
            spriteRenderer.flipX = true;
        }

        lifetime = 0;
        gameObject.SetActive(true);
    }

    public void Update()
    {
        float movementSpeed = speed * Time.deltaTime * dir;
        transform.Translate(movementSpeed, 0, 0);
        lifetime += Time.deltaTime;

        if(lifetime > resetTime)
        {
            gameObject.SetActive(false);
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        base.OnTriggerEnter2D(collision);
        gameObject.SetActive(false);
    }
}
