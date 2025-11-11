using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyDamage : MonoBehaviour
{
    public PlayerRespawn playerRespawn;
    protected void OnTriggerEnter2D(Collider2D collision)
    {
        Debug.Log("Collision Detected");
        if(collision.tag == "Player")
        {
            Debug.Log("Player Detected");
            playerRespawn.Respawn();
        }
    }
}
