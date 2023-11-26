using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Unity.Mathematics;
using Unity.VisualScripting;
using Unity.VisualScripting.Dependencies.NCalc;
using UnityEditor.Experimental.GraphView;
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
    Vector2[] predictedPositions;
    Vector2[] velocitys;
    float[] densities;
    Entry[] spacialLookup;
    int[] startIndecies;

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
        predictedPositions = new Vector2[particlesPerRow * particlesPerColumn];
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
                predictedPositions[index] = new Vector2(posX, posY);
                velocitys[index] = Vector2.zero;
            }
        }
        densities = new float[positions.Length];
        spacialLookup = new Entry[positions.Length];
        startIndecies = new int[positions.Length];
        for (int i = 0; i < positions.Length; i++)
        {
            densities[i] = 1;
        }
    }

    //convert a position of a particle to cords of the cell it is within
    (int, int) PositionToCellCords(Vector2 position, float radius)
    {
        int cellx = (int)(position.x / radius);
        int celly = (int)(position.y / radius);
        return (cellx, celly);
    }

    //hash the cell cords to a single number
    uint HashCell(int cellx, int celly)
    {
        uint a = (uint)cellx * 15823;
        uint b = (uint)celly * 9737333;
        return a + b;
    }

    //wrap the hash to the size of the spacial lookup array
    uint GetKeyFromHash(uint hash)
    {
        return hash % (uint)spacialLookup.Length;
    }

    // Update the spacial lookup array to reflect the current positions of the particles
    public void UpdateSpacialLookup(Vector2[] points, float radius)
    {
        // Create an entry for each particle
        Parallel.For(0, points.Length, i =>
        {   
            (int cellx, int celly) = PositionToCellCords(points[i], radius);
            uint cellKey = GetKeyFromHash(HashCell(cellx, celly));
            spacialLookup[i] = new Entry(i, cellKey);
            startIndecies[i] = int.MaxValue;
        });

        // Sort the array by cell key
        Array.Sort(spacialLookup);

        // Find the start index of each cell
        Parallel.For(0, points.Length, i =>
        {
            uint key = spacialLookup[i].cellKey;
            uint prevKey = i == 0 ? uint.MaxValue : spacialLookup[i - 1].cellKey;
            if (key != prevKey)
            {
                startIndecies[key] = i;
            }
        });
    }

    readonly (int, int)[] cellOffsets = new (int, int)[] { (0, 0), (1, 0), (0, 1), (1, 1), (-1, 0), (0, -1), (-1, -1), (-1, 1), (1, -1) };

    //find all particles in a radius of a point
    public void FindPointsInRadius(Vector2 center, Action<float, int> delegateFunction)
    {
        //find the cell cords of the center
        (int cellx, int celly) = PositionToCellCords(center, smoothingRadius);
        float radiusSquared = smoothingRadius * smoothingRadius;

        //loop over all cells in the radius of the center cell
        foreach ((int offsetx, int offsety) in cellOffsets)
        {
            uint cellKey = GetKeyFromHash(HashCell(cellx + offsetx, celly + offsety));
            int start = startIndecies[cellKey];

            //loop over all particles in the cell
            for (int i = start; i < spacialLookup.Length; i++)
            {
                //break if the cell key changes
                if (spacialLookup[i].cellKey != cellKey) break;

                int particleIndex = spacialLookup[i].particleIndex;
                float distanceSquared = (center - positions[particleIndex]).sqrMagnitude;

                //check if the particle is in the radius
                if (distanceSquared < radiusSquared)
                {
                    delegateFunction(Mathf.Sqrt(distanceSquared), particleIndex);
                };
            }
        }
    }

    //calculate the smoothing kernal
    static float SmoothingKernal(float radius, float distance)
    {
        if (distance >= radius) return 0;

        float volume = math.PI * math.pow(radius, 4) / 6;
        return (radius - distance) * (radius - distance) / volume;
    }

    //calculate the derivative of the smoothing kernal
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
        FindPointsInRadius(point, (distance, i) => density += SmoothingKernal(smoothingRadius, distance));
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

    Vector2 CalculatePressureForce(int particleIndex)
    {
        Vector2 force = Vector2.zero;
        

        FindPointsInRadius(positions[particleIndex], (distance, i) =>
        {
            if (particleIndex != i)
            {
                Vector2 offset = positions[i] - positions[particleIndex];
                Vector2 direction = distance == 0 ? new Vector2(0, 0) : offset / distance;

                float pressure = SmoothingKernalDerivative(smoothingRadius, distance);
                float density = densities[i];
                float sharedPressure = CalculateSharedPressure(density, densities[particleIndex]);
                force += sharedPressure * pressure * direction / density;
            }
        });

        // for (int otherParticleIndex = 0; otherParticleIndex < positions.Length; otherParticleIndex++)
        // {
        //     if (particleIndex == otherParticleIndex) continue;

        //     Vector2 offset = positions[otherParticleIndex] - positions[particleIndex];
        //     float distance = offset.magnitude;
        //     Vector2 direction = distance == 0 ? new Vector2(0, 0) : offset / distance;
           
        //     float pressure = SmoothingKernalDerivative(smoothingRadius, distance);
        //     float density = densities[otherParticleIndex];
        //     float sharedPressure = CalculateSharedPressure(density, densities[particleIndex]);
        //     force += sharedPressure * pressure * direction / density;
        // }
        
        return force;
    }

    //check and resolve collisions with bounds
    void CheckCollisions()
    {
        float halfParticleSize = particleSize / 2f;

        Parallel.For(0, positions.Length, i =>
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
        });
    }

    //simulate one step of the simulation
    void SimulationStep()
    {

        // Update velocitys
        Parallel.For(0, velocitys.Length, i =>
        {
            velocitys[i] += gravity * deltaTime * Vector2.down;
            predictedPositions[i] = positions[i] + velocitys[i] / 120f;
        });

        UpdateSpacialLookup(predictedPositions, smoothingRadius);

        // Update density
        Parallel.For(0, positions.Length, i =>
        {
            densities[i] = CalculateDensity(predictedPositions[i]);
        });

        
        Parallel.For(0, positions.Length, i =>
        {   
            Vector2 pressureForce = CalculatePressureForce(i);
            Vector2 pressureAcceleration = pressureForce / densities[i];
            velocitys[i] += pressureAcceleration  * deltaTime;
        
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
        deltaTime = Time.deltaTime;

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

class Entry : IComparable<Entry>
{
    public int particleIndex;
    public uint cellKey;

    public Entry(int particleIndex, uint cellKey)
    {
        this.particleIndex = particleIndex;
        this.cellKey = cellKey;
    }

    public int CompareTo(Entry other)
    {
        if (other == null)
            return 1;

        // Compare the cellKey values
        int cellKeyComparison = cellKey.CompareTo(other.cellKey);
        if (cellKeyComparison != 0)
            return cellKeyComparison;

        // If the cellKey values are equal, compare the particleIndex values
        return particleIndex.CompareTo(other.particleIndex);
    }
}
    

