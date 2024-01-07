using UnityEngine;
using Unity.Mathematics;

public class Gpu_Fluid_Sim : MonoBehaviour
{
    public event System.Action SimulationStepCompleted;

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
    Initializer.ParticleSpawnData particleData;
    public ComputeShader shader;
    public smoothedParticleDisplayGpu display;

    float deltaTime = 0.01f;

    //particle varialbles
    int particleCount;
    public ComputeBuffer PositionsBuffer { get; private set;}
    ComputeBuffer predictedPositionBuffer;
    public ComputeBuffer VelocitiesBuffer { get; private set;}
    public ComputeBuffer DensitiesBuffer { get; private set;}
    ComputeBuffer spacialLookUp;
    ComputeBuffer startIndecies;
    float particleSize;
    GPUSort gpuSort;

    //kernal id
    const int OutsideForceKernel = 0;
    const int UpdateSpatialLookUpKernel = 1;
    const int CalculateDensitiesKernel = 2;
    const int CalculatePressureForceKernel = 3;
    const int CalculateViscosityKernel = 4;
    const int UpdateParticlePositionKernel = 5;


    void UpdateComputeVariables(float timeStep)
    {
        // Set shader variables to there equivilent c# variables:
        shader.SetInt("particleCount", particleCount);
        shader.SetFloat("deltaTime", timeStep);
        shader.SetFloat("gravity", gravity);
        shader.SetFloat("collisionDampening", collisionDampening);
        shader.SetFloat("smoothingRadius", smoothingRadius);
        shader.SetFloat("targetDensity", targetDensity);
        shader.SetFloat("pressureMultiplier", pressureMultiplyer);
        shader.SetFloat("nearPressureMultiplier", nearPressureMultiplyer);
        shader.SetFloat("viscosityMultiplier", viscosityMultiplyer);
        shader.SetFloat("mouseForce", mouseForce);
        shader.SetFloat("mouseRadius", mouseRadius);
        shader.SetVector("boundsSize", boundsSize);

        // Poly6 and Spiky kernel scaling factors:
        shader.SetFloat("Poly6ScalingFactor", 4 / (Mathf.PI * Mathf.Pow(smoothingRadius, 8)));
        shader.SetFloat("SpikyPow2ScalingFactor", 6 / (Mathf.PI * Mathf.Pow(smoothingRadius, 4)));
        shader.SetFloat("SpikyPow3ScalingFactor", 10 / (Mathf.PI * Mathf.Pow(smoothingRadius, 5)));
        shader.SetFloat("SpikyPow2DerivativeScalingFactor", 12 / (Mathf.Pow(smoothingRadius, 4) * Mathf.PI));
        shader.SetFloat("SpikyPow3DerivativeScalingFactor", 30 / (Mathf.Pow(smoothingRadius, 5) * Mathf.PI));

        // Mouse interaction settings:
        Vector2 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        bool isPullInteraction = Input.GetMouseButton(0);
        bool isPushInteraction = Input.GetMouseButton(1);
        float currInteractStrength = 0;
        if (isPushInteraction || isPullInteraction)
        {
            currInteractStrength = isPushInteraction ? -mouseForce : mouseForce;
        }

        shader.SetVector("mousePosition", mousePos);
        shader.SetFloat("mouseInputStrength", currInteractStrength);
        shader.SetFloat("mouseRadius", mouseRadius);
    }

    // Set the buffers data to the spawn data:
    void SetBufferData(Initializer.ParticleSpawnData spawnData)
    {
        float2[] allPoints = new float2[spawnData.positions.Length];
        System.Array.Copy(spawnData.positions, allPoints, spawnData.positions.Length);

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

        particleData = initializer.GetSpawnData();

        particleCount = particleData.positions.Length;

        //create the buffers
        PositionsBuffer = ComputeHelper.CreateStructuredBuffer<float2>(particleCount);
        predictedPositionBuffer = ComputeHelper.CreateStructuredBuffer<float2>(particleCount);
        VelocitiesBuffer = ComputeHelper.CreateStructuredBuffer<float2>(particleCount);
        DensitiesBuffer = ComputeHelper.CreateStructuredBuffer<float2>(particleCount);
        spacialLookUp = ComputeHelper.CreateStructuredBuffer<uint3>(particleCount);
        startIndecies = ComputeHelper.CreateStructuredBuffer<uint>(particleCount);

        SetBufferData(particleData);

        //set the buffers
        ComputeHelper.SetBuffer(shader, PositionsBuffer, "positions", OutsideForceKernel, UpdateParticlePositionKernel);
        ComputeHelper.SetBuffer(shader, predictedPositionBuffer, "predictedPositions", OutsideForceKernel, UpdateSpatialLookUpKernel, CalculateDensitiesKernel, CalculatePressureForceKernel, CalculateViscosityKernel);
        ComputeHelper.SetBuffer(shader, spacialLookUp, "spacialLookUp", UpdateSpatialLookUpKernel, CalculateDensitiesKernel, CalculatePressureForceKernel, CalculateViscosityKernel);
        ComputeHelper.SetBuffer(shader, startIndecies, "startIndecies", UpdateSpatialLookUpKernel, CalculateDensitiesKernel, CalculatePressureForceKernel, CalculateViscosityKernel);
        ComputeHelper.SetBuffer(shader, DensitiesBuffer, "densities", CalculateDensitiesKernel, CalculatePressureForceKernel, CalculateViscosityKernel);
        ComputeHelper.SetBuffer(shader, VelocitiesBuffer, "velocities", OutsideForceKernel, CalculatePressureForceKernel, CalculateViscosityKernel, UpdateParticlePositionKernel);

        shader.SetInt("particleCount", particleCount);

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
        ComputeHelper.Dispatch(shader, particleCount, kernelIndex: OutsideForceKernel);
        ComputeHelper.Dispatch(shader, particleCount, kernelIndex: UpdateSpatialLookUpKernel);
        gpuSort.SortAndCalculateOffsets();
        ComputeHelper.Dispatch(shader, particleCount, kernelIndex: CalculateDensitiesKernel);
        ComputeHelper.Dispatch(shader, particleCount, kernelIndex: CalculatePressureForceKernel);
        ComputeHelper.Dispatch(shader, particleCount, kernelIndex: CalculateViscosityKernel);
        ComputeHelper.Dispatch(shader, particleCount, kernelIndex: UpdateParticlePositionKernel);
    }

    void OnDestroy()
    {
        ComputeHelper.Release(PositionsBuffer, predictedPositionBuffer, VelocitiesBuffer, DensitiesBuffer, spacialLookUp, startIndecies);
    }

    void OnDrawGizmos()
    {
        Gizmos.color = UnityEngine.Color.blue;
        Gizmos.DrawWireCube(Vector2.zero, boundsSize);
    }
}
