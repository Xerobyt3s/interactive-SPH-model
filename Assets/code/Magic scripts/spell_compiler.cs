using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Palmmedia.ReportGenerator.Core.Parser.Analysis;
using UnityEditor;
using UnityEditor.VersionControl;
using UnityEngine;

public class spell_compiler : MonoBehaviour
{
    // Method used to save the spell to a file
    public void saveSpell(spell_components spell){
        // Save spell to file
        //TODO: Add path to save file + Add spellcomponents that are used to a json file 

    }

    public List<string> loadSpell(string path)//Load spell from file and return a list of the spell components
    {
        string loadedSpell = File.ReadAllText(path);
        List<string> spell = loadedSpell.Split('/').ToList();
        return spell;
    }

    public void executeSpell(List<string> spells) // Execute spells from the format "Component value value"
    {
        spell_components spell = new spell_components();
        foreach (string component in spells)
        {
            string[] componentData = component.Split(' ');
            switch (componentData[0])
            {
                case "Target":
                    break;
                case "AreaOfEffect":
                    spell.AreaOfEffect(float.Parse(componentData[1]), float.Parse(componentData[2]));
                    break;
                case "Conjure":
                    spell.Conjure();
                    break;
                case "Impulse":
                    spell.Impulse(float.Parse(componentData[1]));
                    Debug.Log(componentData[1]);
                    break;
                case "MassExodus":
                    break;
                default:
                    break;
            }
        }
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
            executeSpell(loadSpell("Assets/Spell_files/Test.txt"));
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
    public void MassExodus(float force, int direction){

    }
}

