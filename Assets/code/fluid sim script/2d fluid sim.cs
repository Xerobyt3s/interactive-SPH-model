using System;
using System.Threading.Tasks;
using Unity.Mathematics;
using UnityEngine;



public class Fluid_Sim : MonoBehaviour
{
    [Header("Sim Settings")]
    public Vector2 boundsSize = new Vector2(10, 10);
    public float gravity = 9.81f;
    public float collisionDampening = 0.5f;
    public float smoothingRadius = 0.5f;
    public float targetDensity = 1f;
    public float pressureMultiplyer = 1f;
    public float nearPressureMultiplyer = 1f;
    public float viscosityMultiplyer = 0.1f;
    public int iterationsPerFrame = 1;

    [Header("input settings")]
    public float mouseForce = 1f;
    public float mouseRadius = 1f;

    [Header("external scripts")]
    public Initializer initializer;
    Initializer.ParticleSpawnData spawnData;

    float deltaTime = 0.01f;
    Vector2[] positions;
    Vector2[] predictedPositions;
    Vector2[] velocitys;
    float[,] densities;
    Entry[] spacialLookup;
    int[] startIndecies;
    float particleSize;

    //retrive data from initializer
    void SetInitialData(Initializer.ParticleSpawnData spawnData)
    {
        positions = new Vector2[spawnData.positions.Length];
        predictedPositions = new Vector2[spawnData.positions.Length];
        velocitys = new Vector2[spawnData.positions.Length];
        densities = new float[2, spawnData.positions.Length];
        spacialLookup = new Entry[spawnData.positions.Length];
        startIndecies = new int[spawnData.positions.Length];

        for (int i = 0; i < spawnData.positions.Length; i++)
        {
            positions[i] = spawnData.positions[i];
            predictedPositions[i] = spawnData.positions[i];
            velocitys[i] = spawnData.velocities[i];
        };
    }

    void Start() 
    {
        deltaTime = 1/60f;
        Time.fixedDeltaTime = deltaTime;

        spawnData = initializer.GetSpawnData();

        SetInitialData(spawnData);
        SimulationStep();

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


    static float NearDensitySmoothingKernal(float radius, float distance)
    {
        if (distance < radius)
	    {
            float scale = 10 / (Mathf.PI * Mathf.Pow(radius, 5));
		    float v = radius - distance;
		    return v * v * v * scale;
	    }
	   return 0;
    }

    static float NearDensitySmoothingKernalDerivative(float radius, float distance)
    {
        if (distance <= radius)
	    {
            float scale = 30 / (Mathf.Pow(radius, 5) * Mathf.PI);
		    float v = radius - distance;
		    return -v * v * scale;
	    }
	    return 0;
    }

    //calculate the smoothing kernal
    static float HarshSmoothingKernal(float radius, float distance)
    {
        if (distance >= radius) return 0;

		float scale = 6 / (Mathf.PI * Mathf.Pow(radius, 4));
		float v = radius - distance;
		return v * v * v * scale;
    }

    //calculate the derivative of the smoothing kernal
    static float HarshSmoothingKernalDerivative(float radius, float distance)
    {
        if (distance >= radius) return 0;

		float scale = 12 / (Mathf.Pow(radius, 4) * Mathf.PI);
		float v = radius - distance;
		return -v * scale;
    }

    static float GentalSmoothingKernal(float radius, float distance)
    {
        float value = math.max(0, radius * radius - distance * distance);
        return value * value * value * (4 / (Mathf.PI * Mathf.Pow(radius, 8)));
    }
    
    //calculate density
    float[] CalculateDensity(Vector2 point)
    {   
        float density = 0;
        float nearDensity = 0;

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
                float distance = (point - predictedPositions[particleIndex]).magnitude;

                //check if the particle is in the radius
                if (distance < smoothingRadius)
                {
                    //calculate the density
                    density += HarshSmoothingKernal(smoothingRadius, distance);
                    nearDensity += NearDensitySmoothingKernal(smoothingRadius, distance);
                };
            }
        }
        return new float[] { density, nearDensity };
    }

    //calculate pressure from density, more accurate to gas then liquids
    float CalculatePressure(float density)
    {
        return pressureMultiplyer * (density - targetDensity);
    }

    float CalculateNearPressure(float nearDensity)
    {
        return nearPressureMultiplyer * nearDensity;
    }

    Vector2 CalculatePressureForce(int particleIndex)
    {
        Vector2 force = Vector2.zero;

        Vector2 point = predictedPositions[particleIndex];

        //variables of the particle
        float density = densities[0, particleIndex];
        float nearDensity = densities[1, particleIndex];
        float pressure = CalculatePressure(density);
        float nearPressure = CalculateNearPressure(nearDensity);

        //find the cell cords of the point
        (int cellx, int celly) = PositionToCellCords(point, smoothingRadius);
        float radiusSquared = smoothingRadius * smoothingRadius;

        //loop over all cells in the radius of the center cell
        foreach ((int offsetx, int offsety) in cellOffsets)
        {
            uint hash = HashCell(cellx + offsetx, celly + offsety);
            uint cellKey = GetKeyFromHash(hash);
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

                float neighborDensity = densities[0, _particleIndex];
                float neighborNearDensity = densities[1, _particleIndex];
                float neighborPressure = CalculatePressure(neighborDensity);
                float neighborNearPressure = CalculateNearPressure(neighborNearDensity);
                
                float sharedPressure = (pressure + neighborPressure) * 0.5f;
                float sharedNearPressure = (nearPressure + neighborNearPressure) * 0.5f;

                force += sharedPressure * HarshSmoothingKernalDerivative(smoothingRadius, distance) * direction / neighborDensity;
                force += sharedNearPressure * NearDensitySmoothingKernalDerivative(smoothingRadius, distance) * direction / neighborNearDensity;
            }
        }
        return force;
    }

    Vector2 CalculateViscosity(int particleIndex)
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
                force += (velocitys[_particleIndex] - velocitys[particleIndex]) * GentalSmoothingKernal(smoothingRadius, math.sqrt(distanceSquared));
            }
        }
        return force * viscosityMultiplyer;
    }

    //check and resolve collisions with bounds
    void CheckCollisions(int particleIndex)
    {
        float2 pos = positions[particleIndex];
        float2 vel = velocitys[particleIndex];

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

        // Update position and velocity
        positions[particleIndex] = pos;
        velocitys[particleIndex] = vel;
    }

    //simulate one step of the simulation
    void SimulationStep()
    {
        // Update spacial lookup
        UpdateSpacialLookup(predictedPositions, smoothingRadius);

        // Update density
        Parallel.For(0, positions.Length, i =>
        {
            densities[0, i] = CalculateDensity(predictedPositions[i])[0];
            densities[1, i] = CalculateDensity(predictedPositions[i])[1];
        });

        // Update velocitys
        Parallel.For(0, velocitys.Length, i =>
        {
            Vector2 accel = Vector2.zero;
            accel += gravity * Vector2.down;

            velocitys[i] += accel * deltaTime;
            predictedPositions[i] = positions[i] + velocitys[i] / 120f;
        });

        // pull particles towards the mouse while the left mouse button is held down and pushes them away when the right mouse button is held down
        if (Input.GetMouseButton(0) || Input.GetMouseButton(1))
        {
            Vector2 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            float Force = Input.GetMouseButton(0) ? mouseForce : -mouseForce;
            Parallel.For(0, positions.Length, i =>
            {   
                Vector2 velocity = velocitys[i];
                Vector2 offset = mousePos - positions[i];
                float distance = offset.magnitude;
                if (distance < mouseRadius)
                {
                    velocitys[i] += (mouseRadius - distance) * Force * offset.normalized / mouseRadius;
                }
            });
        }

        Parallel.For(0, positions.Length, i =>
        {   
            Vector2 pressureForce = CalculatePressureForce(i);
            Vector2 pressureAcceleration = pressureForce / densities[0, i];
            if (float.IsNaN(pressureAcceleration.x) || float.IsNaN(pressureAcceleration.y))
            {
                pressureAcceleration = Vector2.zero;
            }
            velocitys[i] += pressureAcceleration  * deltaTime;
        
        });

        Parallel.For(0, positions.Length, i =>
        {
            Vector2 viscosityForce = CalculateViscosity(i);
            Vector2 viscosityAcceleration = viscosityForce / densities[0, i];
            if (float.IsNaN(viscosityAcceleration.x) || float.IsNaN(viscosityAcceleration.y))
            {
                viscosityAcceleration = Vector2.zero;
            }
            velocitys[i] += viscosityAcceleration * deltaTime;
        });

        // Update positions
        Parallel.For(0, positions.Length, i =>
        {
            positions[i] += velocitys[i] * deltaTime;
        });

        Parallel.For(0, positions.Length, i =>
        {
            CheckCollisions(i);
        });
    }

    
    // Update is called once per frame

    void Update()
    {
        deltaTime = Time.deltaTime /iterationsPerFrame;
        
        for (int i = 0; i < iterationsPerFrame; i++)
        {
            SimulationStep();
        }
    }

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
    

