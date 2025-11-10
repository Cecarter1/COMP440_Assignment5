using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FireBallTrap : MonoBehaviour
{
    [SerializeField] private float attackCooldown;
    [SerializeField] private Transform firePoint;
    [SerializeField] private GameObject[] fireballs;
    private float cooldownTimer;
    [SerializeField] private int direction;
    private void Update()
    {
        cooldownTimer += Time.deltaTime;

        if(cooldownTimer >= attackCooldown)
        {
            Shoot();
        }
    }
    private void Shoot()
    {
        cooldownTimer = 0;

        fireballs[FindFireball()].transform.position = firePoint.position;
        fireballs[FindFireball()].GetComponent<EnemyProjectile>().ActivateProjectile(direction);
    }

    private int FindFireball()
    {
        for (int i = 0; i < fireballs.Length; i++)
        {
            if (!fireballs[i].activeInHierarchy)
            {
                return i;
            }
        }
        return 0;
    }
}
