using UnityEngine;
using Unity.Mathematics;

public class Gpu_Fluid_Sim : MonoBehaviour
{
    [Header("Sim Settings")]
    public bool run;
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
    Initializer.InitializerData particleData;
    public ComputeShader shader;
    public ParticleDisplay2D display;

    float deltaTime = 0.01f;

    //particle varialbles
    int particleCount;
    public ComputeBuffer positionsBuffer { get; private set;}
    ComputeBuffer predictedPositionBuffer;
    public ComputeBuffer VelocitiesBuffer { get; private set;}
    public ComputeBuffer DensitiesBuffer { get; private set;}
    ComputeBuffer spacialLookUp;
    ComputeBuffer startIndecies;
    float particleSize;
    GPUSort gpuSort;

    //kernal id
    const int OutsideForce = 0;
    const int UpdateSpatialLookUp = 1;
    const int CalculateDensities = 2;
    const int CalculatePressureForce = 3;
    const int CalculateViscosity = 4;
    const int UpdateParticlePosition = 5;


    void UpdateComputeVariables(float timeStep)
    {
        shader.SetFloat("particleCount", particleCount);
        shader.SetFloat("deltaTime", timeStep);
        shader.SetFloat("gravity", gravity);
        shader.SetFloat("collisionDampening", collisionDampening);
        shader.SetFloat("smoothingRadius", smoothingRadius);
        shader.SetFloat("targetDensity", targetDensity);
        shader.SetFloat("pressureMultiplyer", pressureMultiplyer);
        shader.SetFloat("nearPressureMultiplyer", nearPressureMultiplyer);
        shader.SetFloat("viscosityMultiplyer", viscosityMultiplyer);
        shader.SetFloat("mouseForce", mouseForce);
        shader.SetFloat("mouseRadius", mouseRadius);
        shader.SetVector("boundsSize", boundsSize);

        shader.SetFloat("nearDensityKernalConstant", 10 / (Mathf.PI * Mathf.Pow(smoothingRadius, 5)));
        shader.SetFloat("nearDensityKernalDerivativeConstant", 30 / (Mathf.Pow(smoothingRadius, 5) * Mathf.PI));
        shader.SetFloat("harshKernalConstant", 6 / (Mathf.PI * Mathf.Pow(smoothingRadius, 4)));
        shader.SetFloat("harshKernalDerivativeConstant", 12 / (Mathf.Pow(smoothingRadius, 4) * Mathf.PI));
        shader.SetFloat("gentalKernalConstant", 4 / (Mathf.PI * Mathf.Pow(smoothingRadius, 8)));

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

    void SetBufferData(Initializer.InitializerData particleData)
    {
        float2[] allPoints = new float2[particleData.positions.Length];
        System.Array.Copy(particleData.positions, allPoints, particleData.positions.Length);

        positionsBuffer.SetData(allPoints);
        predictedPositionBuffer.SetData(allPoints);
        VelocitiesBuffer.SetData(particleData.velocities);
    }

    // Start is called before the first frame update
    void Start()
    {
        //set the physics framerate to 60fps
        deltaTime = 1/60f;
        Time.fixedDeltaTime = deltaTime;

        particleData = initializer.GetSpawnData();

        particleCount = particleData.positions.Length;

        positionsBuffer = ComputeHelper.CreateStructuredBuffer<float2>(particleCount);
        predictedPositionBuffer = ComputeHelper.CreateStructuredBuffer<float2>(particleCount);
        VelocitiesBuffer = ComputeHelper.CreateStructuredBuffer<float2>(particleCount);
        DensitiesBuffer = ComputeHelper.CreateStructuredBuffer<float2>(particleCount);
        spacialLookUp = ComputeHelper.CreateStructuredBuffer<uint3>(particleCount);
        startIndecies = ComputeHelper.CreateStructuredBuffer<uint>(particleCount);

        SetBufferData(particleData);

        ComputeHelper.SetBuffer(shader, positionsBuffer, "positions", OutsideForce, UpdateParticlePosition);
        ComputeHelper.SetBuffer(shader, predictedPositionBuffer, "predictedPositions", OutsideForce, UpdateSpatialLookUp, CalculateDensities, CalculatePressureForce, CalculateViscosity);
        ComputeHelper.SetBuffer(shader, spacialLookUp, "spacialLookUp", UpdateSpatialLookUp, CalculateDensities, CalculatePressureForce, CalculateViscosity);
        ComputeHelper.SetBuffer(shader, startIndecies, "startIndecies", UpdateSpatialLookUp, CalculateDensities, CalculatePressureForce, CalculateViscosity);
        ComputeHelper.SetBuffer(shader, DensitiesBuffer, "densities", CalculateDensities, CalculatePressureForce, CalculateViscosity);
        ComputeHelper.SetBuffer(shader, VelocitiesBuffer, "velocities", OutsideForce, CalculatePressureForce, CalculateViscosity, UpdateParticlePosition);

        shader.SetInt("particleCount", particleCount);

        gpuSort = new();
        gpuSort.SetBuffers(spacialLookUp, startIndecies);

        display.Init(this);
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        if (run)
        {
            RunFrame();
        }
        
    }

    void RunFrame()
    {
        float timeStep = Time.fixedDeltaTime / iterationsPerFrame;

        UpdateComputeVariables(timeStep);

        for (int i = 0; i < iterationsPerFrame; i++) RunIteration();
    }

    void RunIteration()
    {
        ComputeHelper.Dispatch(shader, particleCount, kernelIndex: OutsideForce);
        ComputeHelper.Dispatch(shader, particleCount, kernelIndex: UpdateSpatialLookUp);
        gpuSort.SortAndCalculateOffsets();
        ComputeHelper.Dispatch(shader, particleCount, kernelIndex: CalculateDensities);
        ComputeHelper.Dispatch(shader, particleCount, kernelIndex: CalculatePressureForce);
        ComputeHelper.Dispatch(shader, particleCount, kernelIndex: CalculateViscosity);
        ComputeHelper.Dispatch(shader, particleCount, kernelIndex: UpdateParticlePosition);
    }

    void OnDestroy()
    {
        ComputeHelper.Release(positionsBuffer, predictedPositionBuffer, VelocitiesBuffer, DensitiesBuffer, spacialLookUp, startIndecies);
    }

    void OnDrawGizmos()
    {
        Gizmos.color = UnityEngine.Color.blue;
        Gizmos.DrawWireCube(Vector2.zero, boundsSize);
    }
}
