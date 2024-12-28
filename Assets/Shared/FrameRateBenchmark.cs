using System.Collections.Generic;
using UnityEngine;

public class FrameRateBenchmark : MonoBehaviour
{
    public int frameCount = 1000;
    private List<float> frameTimes;

    private void Start()
    {
        frameTimes = new List<float>();
    }

    private void ExitEditor()
    {
        float avgFrameTime = 0f, fps = frameTimes.Count / Time.time;
        for (int i = 0; i < frameTimes.Count; i++)
            avgFrameTime += frameTimes[i];

        avgFrameTime /= frameTimes.Count;

        Debug.Log("Benchmark Total Time: " + Time.time + " seconds");
        Debug.Log("Benchmark Average Frame Time: " + avgFrameTime + " seconds");
        Debug.Log("Benchmark Average FPS: " + fps + " fps");

        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #endif
    }

    private void Update()
    {
        frameTimes.Add(Time.deltaTime);
        if (Time.frameCount >= frameCount) ExitEditor();
    }
}
