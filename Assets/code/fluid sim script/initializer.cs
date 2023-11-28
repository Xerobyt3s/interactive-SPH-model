using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using Unity.Mathematics;

public class Initializer : MonoBehaviour
{
    [Header("Spawn Settings")]
    public int spawnCount = 10;
    public Vector2 spawnSize = new Vector2(10, 10);
    public float particleSize = 0.1f;
    public float randomness = 0.0025f;

    void OnDrawGizmos()
    {
        if (!Application.isPlaying)
        {
            Gizmos.color = UnityEngine.Color.yellow;
            Gizmos.DrawWireCube(Vector2.zero, spawnSize);
        }
    }

    public InitializerData GetSpawnData()
    {
        InitializerData data = new InitializerData(spawnCount);

        data.particleSize = particleSize;

        var rng = new Unity.Mathematics.Random(42);

        float2 s = spawnSize;
        int numX = Mathf.CeilToInt(Mathf.Sqrt(s.x / s.y * spawnCount + (s.x - s.y) * (s.x - s.y) / (4 * s.y * s.y)) - (s.x - s.y) / (2 * s.y));
        int numY = Mathf.CeilToInt(spawnCount / (float)numX);
        int i = 0;

        for (int y = 0; y < numY; y++)
        {
            for (int x = 0; x < numX; x++)
            {
                if (i >= spawnCount) break;

                float tx = numX <= 1 ? 0.5f : x / (numX - 1f);
                float ty = numY <= 1 ? 0.5f : y / (numY - 1f);

                float angle = (float)rng.NextDouble() * 3.14f * 2;
                Vector2 dir = new(Mathf.Cos(angle), Mathf.Sin(angle));
                Vector2 jitter = ((float)rng.NextDouble() - 0.5f) * randomness * dir;
                data.positions[i] = new Vector2((tx - 0.5f) * spawnSize.x, (ty - 0.5f) * spawnSize.y) + jitter;
                data.velocities[i] = Vector2.zero;
                i++;
            }
        }

        return data;
    }   
    public struct InitializerData
        {
            public float2[] positions;
            public float2[] velocities;
        internal float particleSize;

        public InitializerData(int num)
            {
                positions = new float2[num];
                velocities = new float2[num];
                particleSize = 0;
            }
        }
}

