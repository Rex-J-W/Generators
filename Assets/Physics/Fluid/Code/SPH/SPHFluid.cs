// Based on https://matthias-research.github.io/pages/publications/sca03.pdf
// Also used: https://www.youtube.com/watch?v=zbBwKMRyavE
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.VFX;
using Random = UnityEngine.Random;

public class SPHFluid : MonoBehaviour
{
    // Create particle data structure

    [Serializable]
    [StructLayout(LayoutKind.Sequential, Size = 44)]
    [VFXType(VFXTypeAttribute.Usage.GraphicsBuffer)]
    public struct Particle
    {
        public float density, pressure;
        public Vector3 curForce, velocity, position;
    }

    // Variables

    [Space(10)]
    [Header("Fluid Setup")]
    [Tooltip("Must be power of 2, (eg. spawnSize^3 % 256 = 0)")]
    public int spawnSize = 16;
    [Range(0.0001f, 2f)] public float particleRad = 0.04f;
    [Range(0f, 1f)] public float spawnJitter = 0.2f;
    [Range(4, 32)] public int maxNeighbors = 10;

    // Fluid constants

    [Space(10)]
    [Header("Fluid Constants")]
    [Range(0.001f, 1f)] public float boundDamping = 0.3f;
    [Range(-100f, 0f)] public float viscosity = -0.003f;
    [Range(0f, 100f)] public float particleMass = 1f;
    [Range(0f, 10f)] public float gasConstant = 1f;
    [Range(0.0001f, 10f)] public float restingDensity = 1f;
    [Range(0f, 1f)] public float timestep = 0.007f;

    // Rendering variables

    [Space(10)]
    [Header("Optional Interact Obj")]
    public Transform interactableSphere;

    // Hidden variables in inspector

    private VisualEffect visualize;
    //private SPHRenderer rend;
    private ComputeShader shader;
    private Particle[] particles;

    public ComputeBuffer neighborsBuffer;
    public GraphicsBuffer particleBuffer;
    private int integrateKernel, computeForcesKernel, densityPressureKernel, findNeighborsKernel;

    /// <summary>
    /// The total particles simultated
    /// </summary>
    private int TotalParticles => spawnSize * spawnSize * spawnSize;

    /// <summary>
    /// Returns the fluid boundary volume
    /// </summary>
    public Bounds FluidBounds => new Bounds(transform.position, transform.localScale);

    /// <summary>
    /// Run on game start
    /// </summary>
    private void Awake()
    {
        // Load shader

        shader = Resources.Load<ComputeShader>("SPHFluidCompute");
        
        integrateKernel = shader.FindKernel("Integrate");
        computeForcesKernel = shader.FindKernel("ComputeForces");
        densityPressureKernel = shader.FindKernel("ComputeDensityPressure");
        findNeighborsKernel = shader.FindKernel("FindNeighbors");

        // Create VFX visualization

        VisualEffectAsset vfxAsset = Resources.Load<VisualEffectAsset>("VisualizeWater");
        GameObject vfx = new GameObject("_SPHViewer");
        vfx.transform.SetParent(transform);
        vfx.AddComponent<VisualEffect>().visualEffectAsset = vfxAsset;
        visualize = vfx.GetComponent<VisualEffect>();

        // Initialize raymarching shader

        //GameObject renderObj = new GameObject("_SPHVolume");
        //renderObj.transform.SetParent(transform);
        //Volume rendVol = renderObj.AddComponent<Volume>();
        
        //VolumeProfile profile = Resources.Load<VolumeProfile>("SPHRenderVolume");
        //rendVol.sharedProfile = profile;
        //rendVol.isGlobal = true;
        //rendVol.priority = 100;
        //rendVol.profile.TryGet(out rend);
        //rend.active = true;
        //rend.enabled.value = true;

        // Spawn particles

        SpawnParticles();

        // Setup compute buffers

        particleBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, TotalParticles, 44);
        particleBuffer.SetData(particles);

        int[] neighbors = new int[TotalParticles * maxNeighbors];
        neighborsBuffer = new ComputeBuffer(neighbors.Length, 4);
        neighborsBuffer.SetData(neighbors);

        SetupCompute();
    }

    /// <summary>
    /// Release buffers on exit (memory management)
    /// </summary>
    private void OnApplicationQuit()
    {
        neighborsBuffer.Release();
        particleBuffer.Release();
    }

    /// <summary>
    /// Run on game update
    /// </summary>
    private void Update()
    {
        //Debug.Log(rend.IsActive());
        //rend.SetFluidVars(particleBuffer, FluidBounds, particleRad * 20f);
        visualize.SetGraphicsBuffer("ParticleBuffer", particleBuffer);
        visualize.SetInt("ParticleCount", TotalParticles);
        visualize.SetFloat("FrameTime", Time.deltaTime);
    }

    /// <summary>
    /// Runs every physics update
    /// </summary>
    private void FixedUpdate()
    {
        shader.SetFloat("particleMass", particleMass);
        shader.SetFloat("viscosity", viscosity);
        shader.SetFloat("gasConstant", gasConstant);
        shader.SetFloat("restDensity", restingDensity);
        shader.SetFloat("boundDamping", boundDamping);

        if (interactableSphere != null)
        {
            shader.SetVector("spherePos", interactableSphere.position);
            shader.SetFloat("sphereRad", interactableSphere.localScale.x / 2f);
        }
        else
        {
            shader.SetVector("spherePos", Vector3.zero);
            shader.SetFloat("sphereRad", -1f);
        }

        shader.SetVector("boxCenter", transform.position);
        shader.SetVector("boxSize", transform.localScale);
        shader.SetFloat("timestep", timestep);

        shader.Dispatch(findNeighborsKernel, spawnSize / 8, spawnSize / 8, spawnSize / 8);
        shader.Dispatch(densityPressureKernel, spawnSize / 8, spawnSize / 8, spawnSize / 8);
        shader.Dispatch(computeForcesKernel, spawnSize / 8, spawnSize / 8, spawnSize / 8);
        shader.Dispatch(integrateKernel, spawnSize / 8, spawnSize / 8, spawnSize / 8);
    }

    /// <summary>
    /// Spanws particles in a cube defined by spawnSize
    /// </summary>
    private void SpawnParticles()
    {
        Vector3 min = transform.position - transform.localScale / 2f, 
            max = transform.position + transform.localScale / 2f;
        List<Particle> spawned = new List<Particle>();

        max.y /= 2f;
        for (int x = 0; x < spawnSize; x++)
        {
            for (int y = 0; y < spawnSize; y++)
            {
                for (int z = 0; z < spawnSize; z++)
                {
                    float xPos = Mathf.Lerp(min.x, max.x, x / (float)spawnSize);
                    float yPos = Mathf.Lerp(min.y, max.y, y / (float)spawnSize);
                    float zPos = Mathf.Lerp(min.z, max.z, z / (float)spawnSize);

                    Vector3 pos = new Vector3(xPos, yPos, zPos);
                    pos += particleRad * spawnJitter * Random.onUnitSphere;
                    Particle particle = new Particle
                    {
                        position = pos,
                    };
                    spawned.Add(particle);
                }
            }
        }

        particles = spawned.ToArray();
    }

    /// <summary>
    /// Sets up variables and buffers in the SPH compute shader
    /// </summary>
    private void SetupCompute()
    {
        shader.SetInt("particleLength", TotalParticles);
        shader.SetInt("particleComputeSize", spawnSize);
        shader.SetInt("particleComputeSize2", spawnSize * spawnSize);
        shader.SetInt("maxNeighbors", maxNeighbors);
        shader.SetFloat("pi", Mathf.PI);

        shader.SetFloat("radius", particleRad);
        shader.SetFloat("rad2", particleRad * particleRad);
        shader.SetFloat("rad3", particleRad * particleRad * particleRad);
        shader.SetFloat("rad4", particleRad * particleRad * particleRad * particleRad);
        shader.SetFloat("rad5", particleRad * particleRad * particleRad * particleRad * particleRad);

        shader.SetBuffer(integrateKernel, "particles", particleBuffer);

        shader.SetBuffer(findNeighborsKernel, "particles", particleBuffer);
        shader.SetBuffer(findNeighborsKernel, "neighbors", neighborsBuffer);

        shader.SetBuffer(computeForcesKernel, "particles", particleBuffer);
        shader.SetBuffer(computeForcesKernel, "neighbors", neighborsBuffer);

        shader.SetBuffer(densityPressureKernel, "particles", particleBuffer);
        shader.SetBuffer(densityPressureKernel, "neighbors", neighborsBuffer);
    }

}
