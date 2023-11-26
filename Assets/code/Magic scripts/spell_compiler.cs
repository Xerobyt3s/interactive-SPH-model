using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class spell_compiler : MonoBehaviour
{
    // Method used to save the spell to a file
    public void saveSpell(spell_components spell){
        // Save spell to file
        //TODO: Add path to save file + Add spellcomponents that are used to a json file 
    }

    public void loadSpell(){
        // Load spell from file
        //TODO: Add spell execution depending on the spell components in the json file 
    }

    // Start is called before the first frame update
    void Start()
    {
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.E))
        {
            spell_components spell = new spell_components();
            spell.AreaOfEffect(5, 0.25f);
            spell.Conjure();
            spell.Impulse(1000);
        }
    }
}

public class spell_components : spell_compiler
{
    // Declare variables
    public GameObject caster;
    public GameObject target;
    public Vector3 AOE;
    public float AOE_radius;

    // The two target methods utilize polymorphism to allow for different types of targeting depending on wich data is passed to the method
    // Method used to specify the target of the spell
    public void Target(string material){} //TODO: Add material + variable for saving targeted material

    public void Target(GameObject entity){} 
    // Specify wich area the spell will affect
    public void AreaOfEffect(float distance, float radius)
    {
        Debug.Log("Area of Effect");
        //Assign the player as to caster
        caster = GameObject.Find("player");
        AOE_radius = radius;
        // Get the position of the player
        Vector3 casterPosition = caster.transform.position;
        // Calculate the origin of the AoE
        Vector3 AoEOrigin = casterPosition + caster.transform.forward * distance;
        AOE = AoEOrigin;
        Debug.Log(AoEOrigin);
    }
    // Method used to conjure materials or phenomenon
    public void Conjure()
    {
        Debug.Log("Conjuring");
        GameObject sphere = Instantiate(Resources.Load("Prefabs/Sphere", typeof(GameObject))) as GameObject;
        sphere.transform.position = AOE;
        sphere.transform.localScale = new Vector3(AOE_radius, AOE_radius, AOE_radius);
        target = sphere;
    }
    // Method used to apply a force to the target
    public void Impulse(float force){
        Camera playerCamera = caster.GetComponentInChildren<Camera>();//TODO: change so that it uses a rotation vector from AOE origin instead
        // Get the direction the player's camera is facing
        Vector3 direction = playerCamera.transform.forward;

        // Apply the impulse in the direction the player's camera is facing  
        target.GetComponent<Rigidbody>().AddForce(direction * force, ForceMode.Impulse);
    }
    
}

