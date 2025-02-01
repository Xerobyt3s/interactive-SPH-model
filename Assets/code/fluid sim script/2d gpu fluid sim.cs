using UnityEngine;
using Unity.Mathematics;

public class Gpu_Fluid_Sim : MonoBehaviour
{
    public event System.Action SimulationStepCompleted;

    [Header("Sim Settings")]
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
    public Spawner3D initializer;
    public ComputeShader shader;
    public ParticleDisplay3D display;

    float deltaTime = 0.01f;

    //particle varialbles
    int particleCount;
    public ComputeBuffer PositionsBuffer { get; private set;}
    ComputeBuffer predictedPositionBuffer;
    public ComputeBuffer VelocitiesBuffer { get; private set;}
    public ComputeBuffer DensitiesBuffer { get; private set;}
    ComputeBuffer spacialLookUp;
    ComputeBuffer startIndecies;
    GPUSort gpuSort;

    //state
    bool isPaused;
    bool pausedNextFrame;
    Spawner3D.SpawnData spawnData;

    //kernal id
    const int OutsideForceKernel = 0;
    const int UpdateSpatialLookUpKernel = 1;
    const int CalculateDensitiesKernel = 2;
    const int CalculatePressureForceKernel = 3;
    const int CalculateViscosityKernel = 4;
    const int UpdateParticlePositionKernel = 5;


    void UpdateComputeVariables(float timeStep)
    {
        Vector3 simBoundsSize = transform.localScale;
        Vector3 simBoundsCentre = transform.position;

        // Set shader variables to there equivilent c# variables:
        shader.SetFloat("deltaTime", timeStep);
        shader.SetFloat("gravity", gravity);
        shader.SetFloat("collisionDampening", collisionDampening);
        shader.SetFloat("smoothingRadius", smoothingRadius);
        shader.SetFloat("targetDensity", targetDensity);
        shader.SetFloat("pressureMultiplier", pressureMultiplyer);
        shader.SetFloat("nearPressureMultiplier", nearPressureMultiplyer);
        shader.SetFloat("viscosityMultiplier", viscosityMultiplyer);
        shader.SetVector("boundsSize", simBoundsSize);
        shader.SetVector("centre", simBoundsCentre);

        shader.SetMatrix("localToWorld", transform.localToWorldMatrix);
        shader.SetMatrix("worldToLocal", transform.worldToLocalMatrix);

        // Poly6 and Spiky kernel scaling factors:
        shader.SetFloat("Poly6ScalingFactor", 315 / (64 * Mathf.PI * Mathf.Pow(smoothingRadius, 9)));
        shader.SetFloat("SpikyPow2ScalingFactor", 15 / (2 * Mathf.PI * Mathf.Pow(smoothingRadius, 5)));
        shader.SetFloat("SpikyPow3ScalingFactor", 15 / (Mathf.PI * Mathf.Pow(smoothingRadius, 6)));
        shader.SetFloat("SpikyPow2DerivativeScalingFactor", 15 / (Mathf.Pow(smoothingRadius, 5) * Mathf.PI));
        shader.SetFloat("SpikyPow3DerivativeScalingFactor", 45 / (Mathf.Pow(smoothingRadius, 6) * Mathf.PI));
    }

    // Set the buffers data to the spawn data:
    void SetBufferData(Spawner3D.SpawnData spawnData)
    {
        float3[] allPoints = new float3[spawnData.points.Length];
        System.Array.Copy(spawnData.points, allPoints, spawnData.points.Length);

        PositionsBuffer.SetData(allPoints);
        predictedPositionBuffer.SetData(allPoints);
        VelocitiesBuffer.SetData(spawnData.velocities);
    }

    // Start is called before the first frame update
    void Start()
    {
        //set the physics framerate to 60fps
        deltaTime = 1/60f;
        Time.fixedDeltaTime = deltaTime;

        spawnData = initializer.GetSpawnData();

        particleCount = spawnData.points.Length;

        //create the buffers
        PositionsBuffer = ComputeHelper.CreateStructuredBuffer<float3>(particleCount);
        predictedPositionBuffer = ComputeHelper.CreateStructuredBuffer<float3>(particleCount);
        VelocitiesBuffer = ComputeHelper.CreateStructuredBuffer<float3>(particleCount);
        DensitiesBuffer = ComputeHelper.CreateStructuredBuffer<float2>(particleCount);
        spacialLookUp = ComputeHelper.CreateStructuredBuffer<uint3>(particleCount);
        startIndecies = ComputeHelper.CreateStructuredBuffer<uint>(particleCount);

        SetBufferData(spawnData);

        //set the buffers
        ComputeHelper.SetBuffer(shader, PositionsBuffer, "positions", OutsideForceKernel, UpdateParticlePositionKernel);
        ComputeHelper.SetBuffer(shader, predictedPositionBuffer, "predictedPositions", OutsideForceKernel, UpdateSpatialLookUpKernel, CalculateDensitiesKernel, CalculatePressureForceKernel, CalculateViscosityKernel, UpdateParticlePositionKernel);
        ComputeHelper.SetBuffer(shader, spacialLookUp, "spacialLookUp", UpdateSpatialLookUpKernel, CalculateDensitiesKernel, CalculatePressureForceKernel, CalculateViscosityKernel);
        ComputeHelper.SetBuffer(shader, startIndecies, "startIndecies", UpdateSpatialLookUpKernel, CalculateDensitiesKernel, CalculatePressureForceKernel, CalculateViscosityKernel);
        ComputeHelper.SetBuffer(shader, DensitiesBuffer, "densities", CalculateDensitiesKernel, CalculatePressureForceKernel, CalculateViscosityKernel);
        ComputeHelper.SetBuffer(shader, VelocitiesBuffer, "velocities", OutsideForceKernel, CalculatePressureForceKernel, CalculateViscosityKernel, UpdateParticlePositionKernel);

        shader.SetInt("particleCount", PositionsBuffer.count);

        gpuSort = new();
        gpuSort.SetBuffers(spacialLookUp, startIndecies);

        display.Init(this);
    }

    // Update is called once per frame
    void Update()
    {
        RunFrame();
    }

    void RunFrame()
    {
        float timeStep = Time.deltaTime / iterationsPerFrame;

        UpdateComputeVariables(timeStep);

        for (int i = 0; i < iterationsPerFrame; i++) RunIteration(); SimulationStepCompleted?.Invoke();
    }

    void RunIteration()
    {
        // Run the compute shader and dispatch the kernels:
        ComputeHelper.Dispatch(shader, PositionsBuffer.count, kernelIndex: OutsideForceKernel);
        ComputeHelper.Dispatch(shader, PositionsBuffer.count, kernelIndex: UpdateSpatialLookUpKernel);
        gpuSort.SortAndCalculateOffsets();
        ComputeHelper.Dispatch(shader, PositionsBuffer.count, kernelIndex: CalculateDensitiesKernel);
        ComputeHelper.Dispatch(shader, PositionsBuffer.count, kernelIndex: CalculatePressureForceKernel);
        ComputeHelper.Dispatch(shader, PositionsBuffer.count, kernelIndex: CalculateViscosityKernel);
        ComputeHelper.Dispatch(shader, PositionsBuffer.count, kernelIndex: UpdateParticlePositionKernel);
    }

    void OnDestroy()
    {
        ComputeHelper.Release(PositionsBuffer, predictedPositionBuffer, VelocitiesBuffer, DensitiesBuffer, spacialLookUp, startIndecies);
    }

    void OnDrawGizmos()
    {
        // Draw Bounds
        var m = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(0, 1, 0, 0.5f);
        Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
        Gizmos.matrix = m;
    }
}
