using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Checkpoint : MonoBehaviour
{
    [SerializeField] private Animator animator;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.tag == "Player")
        {
            animator.SetTrigger("Disabled");
            GameObject.Find("Fire Particle System").GetComponent<ParticleSystem>().Stop();
            GameObject.Find("Explosion Particle System").GetComponent<ParticleSystem>().Play();
        }
    }
}
