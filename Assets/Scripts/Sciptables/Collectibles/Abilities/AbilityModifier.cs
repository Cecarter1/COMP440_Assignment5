using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public abstract class AbilityModifier : ScriptableObject
{
    //public TestPlayerMovement playerMovement;
    // public TestPlayerHealth playerHealth;
    //public HUDManager hudManager;

    public Sprite sprite;
    public string abilityName;
    public string descritption;

    public abstract void Activate(GameObject target);

    //public void OnEnable()
    //{
    //    //playerMovement = GameObject.Find("/Environment/Player").GetComponent<TestPlayerMovement>();
    //    //playerMovement = GameObject.FindObjectOfType<TestPlayerMovement>();
    //    //playerHealth = GameObject.Find("Player").GetComponent<TestPlayerHealth>();
    //    //hudManager = GameObject.Find("HUD Manager").GetComponent<HUDManager>();
    //}
}
