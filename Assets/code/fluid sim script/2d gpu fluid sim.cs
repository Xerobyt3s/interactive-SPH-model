using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Gpu_Fluid_Sim : MonoBehaviour
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
    public Gpu_Initializer initializer;
    Initializer.InitializerData spawnData;
    public ComputeShader shader;

    float deltaTime = 0.01f;
    Vector2[] positions;
    Vector2[] predictedPositions;
    Vector2[] velocitys;
    float[,] densities;
    Entry[] spacialLookup;
    int[] startIndecies;
    float particleSize;


    void UpdateComputeVariables()
    {
        shader.SetInt("particleCount", positions.Length);
        shader.SetFloat("deltaTime", deltaTime);
        shader.SetFloat("gravity", gravity);
        shader.SetFloat("collisionDampening", collisionDampening);
        shader.SetFloat("smoothingRadius", smoothingRadius);
        shader.SetFloat("targetDensity", targetDensity);
        shader.SetFloat("pressureMultiplyer", pressureMultiplyer);
        shader.SetFloat("nearPressureMultiplyer", nearPressureMultiplyer);
        shader.SetFloat("viscosityMultiplyer", viscosityMultiplyer);
        shader.SetFloat("particleSize", particleSize);
        shader.SetFloat("mouseForce", mouseForce);
        shader.SetFloat("mouseRadius", mouseRadius);
        shader.SetVector("boundsSize", boundsSize);

        shader.SetFloat("nearDensityKernalConstant", 10 / (Mathf.PI * Mathf.Pow(smoothingRadius, 5)));
        shader.SetFloat("nearDensityKernalDerivativeConstant", 30 / (Mathf.Pow(smoothingRadius, 5) * Mathf.PI));
        shader.SetFloat("harshKernalConstant", 6 / (Mathf.PI * Mathf.Pow(smoothingRadius, 4)));
        shader.SetFloat("harshKernalDerivativeConstant", 12 / (Mathf.Pow(smoothingRadius, 4) * Mathf.PI));
        shader.SetFloat("gentalKernalConstant", 4 / (Mathf.PI * Mathf.Pow(smoothingRadius, 8)));

    }

    // Start is called before the first frame update
    void Start()
    {
        UpdateComputeVariables();
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
