using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
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
    public float randomness = 0.0025f;

    [Header("Sim Settings")]
    public Vector2 boundsSize = new Vector2(10, 10);
    public float gravity = 9.81f;
    public float collisionDampening = 0.5f;
    public float smoothingRadius = 0.5f;
    public float targetDensity = 1f;
    public float pressureMultiplyer = 1f;

    [Header("input settings")]
    public float mouseForce = 1f;
    public float mouseRadius = 1f;

    float deltaTime = 0.01f;
    Vector2[] positions;
    Vector2[] predictedPositions;
    Vector2[] velocitys;
    float[] densities;
    Entry[] spacialLookup;
    int[] startIndecies;

    void OnDrawGizmos()
    {
        Gizmos.color = UnityEngine.Color.blue;
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
            Gizmos.color = UnityEngine.Color.yellow;
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

                // Add jitter to the position using the randomness variable
                posX += UnityEngine.Random.Range(-randomness, randomness);
                posY += UnityEngine.Random.Range(-randomness, randomness);

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

    //calculate the smoothing kernal
    static float SmoothingKernal(float radius, float distance)
    {
        if (distance >= radius) return 0;

		float scale = 15 / (math.PI * math.pow(radius, 6));
		float v = radius - distance;
		return v * v * v * scale;
    }

    //calculate the derivative of the smoothing kernal
    static float SmoothingKernalDerivative(float radius, float distance)
    {
        if (distance >= radius) return 0;

		float scale = 15 / (math.pow(radius, 5) * math.PI);
		float v = radius - distance;
		return -v * scale;
    }

    //calculate density
    float CalculateDensity(Vector2 point)
    {   
        float density = 0;

        //find the cell cords of the point
        (int cellx, int celly) = PositionToCellCords(point, smoothingRadius);
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
                float distance = (point - positions[particleIndex]).magnitude;

                //check if the particle is in the radius
                if (distance < smoothingRadius)
                {
                    //calculate the density
                    density += SmoothingKernal(smoothingRadius, distance);
                };
            }
        }
        return density;
    }

    //calculate pressure from density, more accurate to gas then liquids
    float CalculatePressure(float density)
    {
        return pressureMultiplyer * (density - targetDensity);
    }

    Vector2 CalculatePressureForce(int particleIndex)
    {
        Vector2 force = Vector2.zero;

        Vector2 point = predictedPositions[particleIndex];

        //find the cell cords of the point
        (int cellx, int celly) = PositionToCellCords(point, smoothingRadius);
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

                int _particleIndex = spacialLookup[i].particleIndex;
                Vector2 offsetToNeighbor = predictedPositions[_particleIndex] - point;
                float distanceSquared = math.dot(offsetToNeighbor, offsetToNeighbor); 

                //check if the particle is in the radius
                if (distanceSquared > radiusSquared) continue;
                   
                //calculate the force
                float distance = math.sqrt(distanceSquared);
                Vector2 direction = distance == 0 ? new Vector2(0, 0) : offsetToNeighbor.normalized;

                float pressure = CalculatePressure(densities[particleIndex]);
                float neighborDensity = densities[_particleIndex];
                float neighborPressure = CalculatePressure(neighborDensity);
                float sharedPressure = (pressure + neighborPressure) * 0.5f;
                force += sharedPressure * SmoothingKernalDerivative(smoothingRadius, distance) * direction / neighborDensity;
            }
        }
        return force;
    }

    //check and resolve collisions with bounds
    void CheckCollisions()
    {
        Parallel.For(0, positions.Length, i =>
        {
            float2 pos = positions[i];
	        float2 vel = velocitys[i];

	        // Keep particle inside bounds
	        float2 halfSize = boundsSize * 0.5f;
	        float2 edgeDst = halfSize - math.abs(pos);

	        if (edgeDst.x <= 0)
	        {
		        pos.x = halfSize.x * math.sign(pos.x);
		        vel.x *= -1 * collisionDampening;
	        }
	        if (edgeDst.y <= 0)
	        {
		        pos.y = halfSize.y * math.sign(pos.y);
		        vel.y *= -1 * collisionDampening;
	        }

            positions[i] = pos;
            velocitys[i] = vel;

        });
    }

    //simulate one step of the simulation
    void SimulationStep()
    {

        // Update velocitys
        Parallel.For(0, velocitys.Length, i =>
        {
            Vector2 accel = Vector2.zero;
            accel += gravity * Vector2.down;

            velocitys[i] += accel * deltaTime;
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
    

