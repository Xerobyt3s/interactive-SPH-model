using UnityEngine;

public class ParticleDisplay3D : MonoBehaviour
{

    public Shader shader3d;
    public Shader shader2d;
    public bool Render_2d;
    public float scale;
    Mesh mesh;
    public Color col;
    Material mat;

    ComputeBuffer argsBuffer;
    Bounds bounds;

    public Gradient colourMap;
    public int gradientResolution;
    public float velocityDisplayMax;
    Texture2D gradientTexture;
    bool needsUpdate;

    public int meshResolution;
    public int debug_MeshTriCount;

    public void Init(Gpu_Fluid_Sim sim)
    {
        if (!Render_2d)
        {
            mat = new Material(shader3d);
            mesh = SebStuff.SphereGenerator.GenerateSphereMesh(meshResolution);
        } else
        {
            mat = new Material(shader2d);
            mesh = Seb.Helpers.QuadGenerator.GenerateQuadMesh();
        }

        mat.SetBuffer("Positions", sim.PositionsBuffer);
        mat.SetBuffer("Velocities", sim.VelocitiesBuffer);

        debug_MeshTriCount = mesh.triangles.Length / 3;
        argsBuffer = ComputeHelper.CreateArgsBuffer(mesh, sim.PositionsBuffer.count);
        bounds = new Bounds(Vector3.zero, Vector3.one * 10000);
    }

    void LateUpdate()
    {

        UpdateSettings();
        Graphics.DrawMeshInstancedIndirect(mesh, 0, mat, bounds, argsBuffer);
    }

    void UpdateSettings()
    {
        if (needsUpdate)
        {
            needsUpdate = false;
            smoothedParticleDisplayGpu.TextureFromGradient(ref gradientTexture, gradientResolution, colourMap);
            mat.SetTexture("ColourMap", gradientTexture);
        }
        mat.SetFloat("scale", scale);
        mat.SetColor("colour", col);
        mat.SetFloat("velocityMax", velocityDisplayMax);

        Vector3 s = transform.localScale;
        transform.localScale = Vector3.one;
        var localToWorld = transform.localToWorldMatrix;
        transform.localScale = s;

        mat.SetMatrix("localToWorld", localToWorld);
    }

    private void OnValidate()
    {
        needsUpdate = true;
    }

    void OnDestroy()
    {
        ComputeHelper.Release(argsBuffer);
    }
}
