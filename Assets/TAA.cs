using System.Collections.Generic;
using System.Dynamic;
using UnityEngine;

public class TAA : MonoBehaviour
{
    Camera Camera;
    [SerializeField]
    RenderTexture HistoryBuffer;
    [SerializeField]
    RenderTexture DebugBuffer;

    [SerializeField]
    bool enable = true;
    [SerializeField]
    bool debug = true;

    [SerializeField]
    ComputeShader ComputerShaderTAA;

    [SerializeField]
    float weight = 0.1f;
    [SerializeField]
    float depthThreshold = 0.1f;

    Vector3 OriginalPos;

    [SerializeField]
    float JitterStrength = 0.01f;

    [SerializeField]
    bool ClipAABB = true;
    [SerializeField]
    bool ClipRGBSpace = false;
    

    [SerializeField]
    List<Vector2> seq;

    int index = 0;
    bool reset = false;
    
    // Start is called before the first frame update
    void Awake()
    {
        Camera=GetComponent<Camera>();
        Camera.depthTextureMode |= DepthTextureMode.Depth | DepthTextureMode.MotionVectors;
        seq = GenerateHaltonSequence(16, 2,new int[]{ 2,3});
    }

    static List<Vector2> GenerateHaltonSequence(int n, int d, int[] bases)
    {
        List<Vector2> sequence = new List<Vector2>();
        for (int i = 0; i < n; i++)
        {
            Vector2 point = new Vector2(0f, 0f);
            for (int j = 0; j < d; j++)
            {
                int x = i;
                float f = 1.0f;
                while (x > 0)
                {
                    f /= bases[j];
                    point[j] += (x % bases[j]) * f;
                    x = x / bases[j];
                }
                point[j] += 0.5f;
                point[j] = point[j] > 1 ? point[j] - 1 : point[j];
            }
            sequence.Add(point);
        }
        return sequence;
    }

    private void Update()
    {
        if(Input.GetKeyDown(KeyCode.Space))
        {
            enable = !enable;
        }
        if (Input.GetKeyDown(KeyCode.X))
        {
            debug = !debug;
        }
    }

    private void LateUpdate()
    {
        Camera.ResetProjectionMatrix();
        if (enable)
        {
            var newPM = Matrix4x4.identity;
            float jitterX = (seq[index].x - 0.5f) * 2*JitterStrength / Screen.width;
            float jitterY = (seq[index].y - 0.5f) * 2*JitterStrength / Screen.height;
            ComputerShaderTAA.SetFloat("jitterx", jitterX);
            ComputerShaderTAA.SetFloat("jittery", jitterY);
            newPM.m03 = jitterX;
            newPM.m13 = jitterY;
            var temp = Camera.projectionMatrix;
            Camera.projectionMatrix = newPM * Camera.projectionMatrix;
            Camera.nonJitteredProjectionMatrix = temp;
            index = (index + 1) % seq.Count;
        }
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if (enable)
        {
            if(HistoryBuffer==null)
            {
                CreateHistory(source);
            }
            if ((HistoryBuffer.width != Screen.width || HistoryBuffer.height != Screen.height))
            {
                HistoryBuffer.Release();
                DebugBuffer.Release();
                CreateHistory(source);
            }
            ComputerShaderTAA.SetTexture(0, "HistoryBuff", HistoryBuffer);
            ComputerShaderTAA.SetTexture(0, "History", HistoryBuffer);
            ComputerShaderTAA.SetTexture(0, "Debug", DebugBuffer);
            ComputerShaderTAA.SetTexture(0, "Input", source);

            ComputerShaderTAA.SetTextureFromGlobal(0, "Depth", "_CameraDepthTexture");
            ComputerShaderTAA.SetTextureFromGlobal(0, "Motion", "_CameraMotionVectorsTexture");
            
            ComputerShaderTAA.SetFloat("Depththreshold", depthThreshold);
            ComputerShaderTAA.SetFloat("weight", reset?1:weight);
            reset = !reset && reset;
            ComputerShaderTAA.SetInt("height", Screen.height);
            ComputerShaderTAA.SetInt("width", Screen.width);
            ComputerShaderTAA.SetBool("debug", debug);
            ComputerShaderTAA.SetBool("rgb", ClipRGBSpace);
            ComputerShaderTAA.SetBool("clipaabb", ClipAABB);

            ComputerShaderTAA.Dispatch(0, Mathf.CeilToInt((float)Screen.width / 32), Mathf.CeilToInt((float)Screen.height / 32), 1);
            if(debug)
                Graphics.Blit(DebugBuffer, destination);
            else
                Graphics.Blit(HistoryBuffer, destination);                        
        }
        else
        {
            Graphics.Blit(source, destination);
        }        
    }

    private void CreateHistory(RenderTexture source)
    {
        DebugBuffer = new RenderTexture(source.width, source.height, source.depth);
        DebugBuffer.enableRandomWrite = true;
        HistoryBuffer = new RenderTexture(source.width, source.height, source.depth);
        HistoryBuffer.enableRandomWrite = true;
        reset = true;
    }

    private void OnDestroy()
    {
        HistoryBuffer.Release();
    }
}
