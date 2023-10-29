using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NewBehaviourScript : MonoBehaviour
{
    [Header("Spawn Settings")]
    public int spawnCount = 10;
    public Vector2 spawnSize = new Vector2(10, 10);

    [Header("Sim Settings")]
    public Vector2 boundsSize = new Vector2(10, 10);
    public float gravity = 9.81f;
    public float collisionDampening = 0.5f;
    public float targetDensity = 1f;
    Vector2[] positions;
    Vector2[] velocitys;

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(0, 1, 0, 0.4f);
        Gizmos.DrawWireCube(Vector2.zero, boundsSize);

        if (Application.isPlaying) {
            foreach (Vector2 p in positions)
            {
                Gizmos.DrawSphere(p, 0.1f);
            }
        } else {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(Vector2.zero, spawnSize);
        }
        

    }

    void Start()
        {
            //spawn particles
            positions = new Vector2[spawnCount];
            for (int i = 0; i < spawnCount; i++) {
            positions[i] = new Vector2(Random.Range(-spawnSize.x / 2, spawnSize.x / 2), Random.Range(-spawnSize.y / 2, spawnSize.y / 2));
            }

            //creates velocitys
            foreach (Vector2 p in positions)
            {
                velocitys = new Vector2[positions.Length];
            }
        }
    //apply gravity
    void UpdateVelocitys()
    {
        for (int i = 0; i < velocitys.Length; i++) {
            velocitys[i] += Vector2.down * gravity * Time.deltaTime;
        }
    }

    //check collisions with bounds
    void CheckCollisions()
    {
        for (int i = 0; i < positions.Length; i++) {
            if (positions[i].x < -boundsSize.x / 2) {
                positions[i].x = -boundsSize.x / 2;
                velocitys[i].x *= -collisionDampening;
            }
            if (positions[i].x > boundsSize.x / 2) {
                positions[i].x = boundsSize.x / 2;
                velocitys[i].x *= -collisionDampening;
            }
            if (positions[i].y < -boundsSize.y / 2) {
                positions[i].y = -boundsSize.y / 2;
                velocitys[i].y *= -collisionDampening;
            }
            if (positions[i].y > boundsSize.y / 2) {
                positions[i].y = boundsSize.y / 2;
                velocitys[i].y *= -collisionDampening;
            }
        }
    }

    

    
    // Update is called once per frame
    void Update()
    {
        UpdateVelocitys();
        CheckCollisions();



        //update positions
        for (int i = 0; i < positions.Length; i++) {
            positions[i] += velocitys[i] * Time.deltaTime;
        }


    }

    
}
