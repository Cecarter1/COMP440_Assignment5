using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FireGeyser : MonoBehaviour
{
    [Header("Timers")]
    [SerializeField] private float activationDelay;
    [SerializeField] private float activeTime;
    private Animator anim;
    //private SpriteRenderer spriteRend;
    [SerializeField] private Transform player;

    private bool triggered;
    private bool active;

    private void Awake()
    {
        anim = GetComponent<Animator>();
        //spriteRend = GetComponent<SpriteRenderer>();
    }

    private void Update()
    {
        if (Mathf.Sqrt(player.position.x) - Mathf.Sqrt(transform.position.x) < 25 && !triggered)
        {
            StartCoroutine(ActivateFireGeyser());
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.tag == "Player")
        {
            if (active)
            {
                collision.GetComponent<PlayerRespawn>().Respawn();
            }
        }
    }

    private IEnumerator ActivateFireGeyser()
    {
        triggered = true;
        anim.SetBool("triggered", true);
        yield return new WaitForSeconds(activationDelay);
        active = true;
        anim.SetBool("activated", true);
        yield return new WaitForSeconds(activeTime);
        active = false;
        triggered = false;
        anim.SetBool("activated", false);
        anim.SetBool("triggered", false);
    }
}
