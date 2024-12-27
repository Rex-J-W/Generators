// Based on https://matthias-research.github.io/pages/publications/sca03.pdf
// Also used: https://www.youtube.com/watch?v=zbBwKMRyavE
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using Random = UnityEngine.Random;

public class SPHFluidSimple : MonoBehaviour
{

    //public Material mat;
    //public Transform plane;

    //private void LateUpdate()
    //{
    //    mat.mainTexture = tex;

    //    Camera cam = Camera.main;
    //    float pos = cam.nearClipPlane + 0.01f;
    //    plane.position = cam.transform.position + cam.transform.forward * pos;
    //    float h = Mathf.Tan(cam.fieldOfView * Mathf.Deg2Rad * 0.5f) * pos * 2f;
    //    plane.localScale = new Vector3(h * cam.aspect / 10f, 1f, h / 10f);
    //}


    // Create particle data structure

    [Serializable]
    [StructLayout(LayoutKind.Sequential, Size = 44)]
    public struct Particle
    {
        public float density, pressure;
        public Vector3 curForce, velocity, position;
    }

    // Variables

    public bool showSpheres;
    public Vector3Int spawnSize = Vector3Int.one * 16;

    private int TotalParticles => spawnSize.x * spawnSize.y * spawnSize.z;

    [Space(10)]
    public Vector3 boxSize = new Vector3Int(4, 10, 4);
    public Vector3 spawnCenter;
    public float particleRad = 0.1f;
    public float spawnJitter = 0.2f;

    // Fluid constants

    [Space(10f)]
    [Range(-1f, 0f)] public float boundDamping = -0.3f;
    [Range(-100f, 0f)] public float viscosity = -0.003f;
    public float particleMass = 1f;
    public float gasConstant = 2f;
    public float restingDensity = 1f;
    public float timestep = 0.007f;

    // Rendering variables

    [Space(10)]
    public Mesh particleMesh;
    private Mesh realParticleMesh;

    public float particleRenderSize = 8f;
    public Material mat;
    private ComputeShader shader;
    public bool update;
    public Particle[] particles;

    private ComputeBuffer argsBuffer, particleBuffer;
    private ComputeBuffer particleIndicies, particleCellIndicies, cellOffsets;
    private int integrateKernel, forcesKernel, densityPressureKernel, hashKernel, sortKernel, cellOffsetKernel;

    // Draw in scene
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(Vector3.zero, boxSize);

        if (!Application.isPlaying)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(spawnCenter, 0.1f);
        }
    }

    /// <summary>
    /// Run on game start
    /// </summary>
    private void Awake()
    {
        // Load shader

        shader = Resources.Load<ComputeShader>("SPHFluidComputeAdvanced");
        integrateKernel = shader.FindKernel("Integrate2");
        forcesKernel = shader.FindKernel("ComputeForces2");
        densityPressureKernel = shader.FindKernel("ComputeDensityPressure2");
        hashKernel = shader.FindKernel("HashParticles2");
        sortKernel = shader.FindKernel("BitonicSort2");
        cellOffsetKernel = shader.FindKernel("CalcCellOffsets2");

        // Spawn particles

        realParticleMesh = new Mesh();
        Vector3[] verts = particleMesh.vertices;
        for (int i = 0; i < verts.Length; i++)
        {
            verts[i] = particleMesh.vertices[i] * particleRad;
        }
        realParticleMesh.SetVertices(verts);
        realParticleMesh.SetTriangles(particleMesh.triangles, 0);
        realParticleMesh.SetUVs(0, particleMesh.uv);
        realParticleMesh.RecalculateBounds();
        realParticleMesh.RecalculateNormals();
        realParticleMesh.RecalculateTangents();

        SpawnParticles();

        // Arguments for GPU instancing

        uint[] args = {
            particleMesh.GetIndexCount(0),
            (uint)TotalParticles,
            particleMesh.GetIndexStart(0),
            particleMesh.GetBaseVertex(0),
            0,
        };

        // Setup compute buffers

        argsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
        argsBuffer.SetData(args);

        particleBuffer = new ComputeBuffer(TotalParticles, 44);
        particleBuffer.SetData(particles);

        particleIndicies = new ComputeBuffer(TotalParticles, 4);
        particleCellIndicies = new ComputeBuffer(TotalParticles, 4);
        cellOffsets = new ComputeBuffer(TotalParticles, 4);

        uint[] particleIndiciesData = new uint[TotalParticles];
        for (uint i = 0; i < (uint)TotalParticles; i++)
            particleIndiciesData[i] = i;
        particleIndicies.SetData(particleIndiciesData);

        SetupCompute();
    }

    // Used for custom shader that doesn't work
    private static readonly int SizeProp = Shader.PropertyToID("_size");
    private static readonly int ParticleBufferProp = Shader.PropertyToID("_particlesBuffer");

    /// <summary>
    /// Release buffers on exit (memory management)
    /// </summary>
    private void OnApplicationQuit()
    {
        argsBuffer.Release();
        particleBuffer.Release();
        particleIndicies.Release();
        particleCellIndicies.Release();
        cellOffsets.Release();
    }

    /// <summary>
    /// Run on game update
    /// </summary>
    private void Update()
    {
        // This is not working, custom shader needed
        mat.SetFloat(SizeProp, particleRenderSize);
        mat.SetBuffer(ParticleBufferProp, particleBuffer);

        if (showSpheres && update)
        {
            particleBuffer.GetData(particles);
            for (int i = 0; i < particles.Length; i++)
            {
                Graphics.DrawMesh(realParticleMesh, particles[i].position, Quaternion.identity, mat, 0);
            }
            //update = false;
        }
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
        shader.SetFloat("pi", Mathf.PI);

        shader.SetVector("boxSize", boxSize);
        shader.SetFloat("timestep", timestep);

        shader.Dispatch(hashKernel, TotalParticles / 256, 1, 1);

        SortParticles();

        shader.Dispatch(cellOffsetKernel, TotalParticles / 256, 1, 1);
        shader.Dispatch(densityPressureKernel, TotalParticles / 256, 1, 1);
        shader.Dispatch(forcesKernel, TotalParticles / 256, 1, 1);
        shader.Dispatch(integrateKernel, TotalParticles / 256, 1, 1);
    }

    /// <summary>
    /// Spanws particles in a cube defined by spawnSize
    /// </summary>
    private void SpawnParticles()
    {
        Vector3 spawnPt = spawnCenter;
        List<Particle> spawned = new List<Particle>();

        for (int x = 0; x < spawnSize.x; x++)
        {
            for (int y = 0; y < spawnSize.y; y++)
            {
                for (int z = 0; z < spawnSize.z; z++)
                {
                    Vector3 pos = new Vector3(x * particleRad * 2, y * particleRad * 2, z * particleRad * 2) + spawnPt;
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
    /// Sorts the particles using bitonic sort for faster neighbor calculations
    /// </summary>
    private void SortParticles()
    {
        for (var dim = 2; dim <= TotalParticles; dim <<= 1)
        {
            shader.SetInt("dim", dim);
            for (var block = dim >> 1; block > 0; block >>= 1)
            {
                shader.SetInt("block", block);
                shader.Dispatch(sortKernel, TotalParticles / 256, 1, 1);
            }
        }
    }

    /// <summary>
    /// Adds all initial variables and buffers to the compute shader
    /// </summary>
    private void SetupCompute()
    {
        shader.SetInt("particleLength", TotalParticles);

        shader.SetFloat("radius", particleRad);
        shader.SetFloat("rad2", particleRad * particleRad);
        shader.SetFloat("rad3", particleRad * particleRad * particleRad);
        shader.SetFloat("rad4", particleRad * particleRad * particleRad * particleRad);
        shader.SetFloat("rad5", particleRad * particleRad * particleRad * particleRad * particleRad);

        shader.SetBuffer(integrateKernel, "particles", particleBuffer);
        shader.SetBuffer(forcesKernel, "particles", particleBuffer);
        shader.SetBuffer(densityPressureKernel, "particles", particleBuffer);
        shader.SetBuffer(hashKernel, "particles", particleBuffer);

        shader.SetBuffer(forcesKernel, "particleIndicies", particleIndicies);
        shader.SetBuffer(densityPressureKernel, "particleIndicies", particleIndicies);
        shader.SetBuffer(hashKernel, "particleIndicies", particleIndicies);
        shader.SetBuffer(sortKernel, "particleIndicies", particleIndicies);
        shader.SetBuffer(cellOffsetKernel, "particleIndicies", particleIndicies);

        shader.SetBuffer(forcesKernel, "particleCellIndicies", particleCellIndicies);
        shader.SetBuffer(densityPressureKernel, "particleCellIndicies", particleCellIndicies);
        shader.SetBuffer(hashKernel, "particleCellIndicies", particleCellIndicies);
        shader.SetBuffer(sortKernel, "particleCellIndicies", particleCellIndicies);
        shader.SetBuffer(cellOffsetKernel, "particleCellIndicies", particleCellIndicies);

        shader.SetBuffer(forcesKernel, "cellOffsets", cellOffsets);
        shader.SetBuffer(densityPressureKernel, "cellOffsets", cellOffsets);
        shader.SetBuffer(hashKernel, "cellOffsets", cellOffsets);
        shader.SetBuffer(cellOffsetKernel, "cellOffsets", cellOffsets);
    }

}
