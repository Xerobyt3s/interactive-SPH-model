using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;

public class NewBehaviourScript : MonoBehaviour
{
    [Header("Spawn Settings")]
    public int spawnCount = 10;
    public Vector2 spawnSize = new Vector2(10, 10);
    public float particleSize = 0.1f;

    [Header("Sim Settings")]
    public Vector2 boundsSize = new Vector2(10, 10);
    public float gravity = 9.81f;
    public float collisionDampening = 0.5f;
    public float smoothingRadius = 0.5f;
    public float targetDensity = 1f;
    public float pressureMultiplyer = 1f;

    float deltaTime = 0.01f;
    Vector2[] positions;
    Vector2[] velocitys;
    float[] densities;

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(0, 1, 0, 0.4f);
        Gizmos.DrawWireCube(Vector2.zero, boundsSize);

        if (Application.isPlaying)
        {
            foreach (Vector2 p in positions)
            {
                Gizmos.DrawSphere(p, particleSize);
            }
        }
        else
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(Vector2.zero, spawnSize);

            // Calculate the number of particles in each row and column
            int particlesPerRow = Mathf.CeilToInt(Mathf.Sqrt(spawnCount));
            int particlesPerColumn = Mathf.CeilToInt((float)spawnCount / particlesPerRow);

            // Calculate the spacing between particles in the grid
            float spacingX = spawnSize.x / particlesPerRow;
            float spacingY = spawnSize.y / particlesPerColumn;

            // Preview particles in a grid
            for (int i = 0; i < particlesPerColumn; i++)
            {
                for (int j = 0; j < particlesPerRow; j++)
                {
                    // Calculate the position of the particle in the grid
                    float posX = j * spacingX - spawnSize.x / 2 + spacingX / 2;
                    float posY = i * spacingY - spawnSize.y / 2 + spacingY / 2;

                    // Preview the particle as a wire sphere
                    Gizmos.DrawWireSphere(new Vector2(posX, posY), particleSize);
                }
            }
        }
    }

    //at the start of the simulation, spawn particles in a grid as close to the target about as possible
    void Start()
    {
        // Calculate the number of particles in each row and column
        int particlesPerRow = Mathf.CeilToInt(Mathf.Sqrt(spawnCount));
        int particlesPerColumn = Mathf.CeilToInt((float)spawnCount / particlesPerRow);

        // Calculate the spacing between particles in the grid
        float spacingX = spawnSize.x / particlesPerRow;
        float spacingY = spawnSize.y / particlesPerColumn;

        // Initialize the positions and velocities arrays
        positions = new Vector2[particlesPerRow * particlesPerColumn];
        velocitys = new Vector2[particlesPerRow * particlesPerColumn];

        // Spawn particles in a grid
        for (int i = 0; i < particlesPerColumn; i++)
        {
            for (int j = 0; j < particlesPerRow; j++)
            {
                int index = i * particlesPerRow + j;

                // Calculate the position of the particle in the grid
                float posX = j * spacingX - spawnSize.x / 2 + spacingX / 2;
                float posY = i * spacingY - spawnSize.y / 2 + spacingY / 2;

                // Set the position and initial velocity of the particle
                positions[index] = new Vector2(posX, posY);
                velocitys[index] = Vector2.zero;
            }
        }
        densities = new float[positions.Length];
        for (int i = 0; i < positions.Length; i++)
        {
            densities[i] = 1;
        }
    }

    static float SmoothingKernal(float radius, float distance)
    {
        if (distance >= radius) return 0;

        float volume = math.PI * math.pow(radius, 4) / 6;
        return (radius - distance) * (radius - distance) / volume;
    }

    static float SmoothingKernalDerivative(float radius, float distance)
    {
        if (distance >= radius) return 0;
        
        float scale = 12 / (math.pow(radius, 4) * math.PI);
        return (distance - radius) * scale;
    }

    //calculate density
    float CalculateDensity(Vector2 point)
    {
        float density = 0;
        for (int i = 0; i < positions.Length; i++)
        {
            float distance = Vector2.Distance(point, positions[i]);
            density += SmoothingKernal(smoothingRadius, distance);
        }
        return density;
    }

    //calculate pressure from density, more accurate to gas then liquids
    float CalculatePressure(float density)
    {
        return pressureMultiplyer * (density - targetDensity);
    }

    //calculate pressure shared between two particles
    float CalculateSharedPressure(float density1, float density2)
    {
        return (CalculatePressure(density1) + CalculatePressure(density2)) / 2;
    }

    //get a random vector direction
    Vector2 GetRandomDir()
    {
        return new Vector2(UnityEngine.Random.Range(-1f, 1f), UnityEngine.Random.Range(-1f, 1f));
    }

    Vector2 CalculatePressureForce(int particleIndex)
    {
        Vector2 force = Vector2.zero;
        
        for (int otherParticleIndex = 0; otherParticleIndex < positions.Length; otherParticleIndex++)
        {
            if (particleIndex == otherParticleIndex) continue;

            Vector2 offset = positions[otherParticleIndex] - positions[particleIndex];
            float distance = offset.magnitude;
            Vector2 direction = distance == 0 ? GetRandomDir() : offset / distance;
           
            float pressure = SmoothingKernalDerivative(smoothingRadius, distance);
            float density = densities[otherParticleIndex];
            float sharedPressure = CalculateSharedPressure(density, densities[particleIndex]);
            force += sharedPressure * pressure * direction / density;
        }

        return force;
    }

    //check collisions with bounds
    void CheckCollisions()
    {
        float halfParticleSize = particleSize / 2f;

        for (int i = 0; i < positions.Length; i++)
        {
            if (positions[i].x - halfParticleSize < -boundsSize.x / 2)
            {
                positions[i].x = -boundsSize.x / 2 + halfParticleSize;
                velocitys[i].x *= -collisionDampening;
            }
            if (positions[i].x + halfParticleSize > boundsSize.x / 2)
            {
                positions[i].x = boundsSize.x / 2 - halfParticleSize;
                velocitys[i].x *= -collisionDampening;
            }
            if (positions[i].y - halfParticleSize < -boundsSize.y / 2)
            {
                positions[i].y = -boundsSize.y / 2 + halfParticleSize;
                velocitys[i].y *= -collisionDampening;
            }
            if (positions[i].y + halfParticleSize > boundsSize.y / 2)
            {
                positions[i].y = boundsSize.y / 2 - halfParticleSize;
                velocitys[i].y *= -collisionDampening;
            }
        }
    }

    void SimulationStep()
    {
        deltaTime = Time.deltaTime;

        // Update density
        Parallel.For(0, positions.Length, i =>
        {
            densities[i] = CalculateDensity(positions[i]);
        });

        // Update velocitys
        Parallel.For(0, velocitys.Length, i =>
        {
            velocitys[i] += gravity * deltaTime * Vector2.down;
        });

        Parallel.For(0, positions.Length, i =>
        {   
            Vector2 pressureForce = CalculatePressureForce(i) * deltaTime;
            Vector2 pressureAcceleration = pressureForce / densities[i];
            velocitys[i] += pressureAcceleration;
        
        });

        // Update positions
        Parallel.For(0, positions.Length, i =>
        {
            positions[i] += velocitys[i] * deltaTime;
        });

        CheckCollisions();
    }

    
    // Update is called once per frame

    void Update()
    {
        SimulationStep();

        //check density at mouse position and log it
        if (Input.GetMouseButton(0))
        {
            Vector2 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            float density = CalculateDensity(mousePos);
            Debug.Log(density);
        }
    }

}

    

