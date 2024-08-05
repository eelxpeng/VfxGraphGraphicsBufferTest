using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VFX;

public class PointCloudGenerator : MonoBehaviour
{
    public ComputeShader depthToPointCloudShader;
    public Texture2D megaTexture;
    public VisualEffect vfxGraph;
    private int kernelIndex;

    private GraphicsBuffer pointCloudBuffer;
    private GraphicsBuffer colorBuffer;
    private GraphicsBuffer depthBuffer;
    private GraphicsBuffer depthLowBuffer;
    private GraphicsBuffer colorCoordinatesBuffer;
    private GraphicsBuffer depthCoordinatesBuffer;
    private GraphicsBuffer depthImageBuffer;
    private int megaframe_width = 1280;
    private int megaframe_height = 1296;
    private int color_width = 1280;
    private int color_height = 720;
    private int depth_width = 640;
    private int depth_height = 576;
    private float focalLengthDepthX;
    private float focalLengthDepthY;
    private float principalPointDepthX;
    private float principalPointDepthY;
    private float focalLengthColorX;
    private float focalLengthColorY;
    private float principalPointColorX;
    private float principalPointColorY;
    private Matrix4x4 depthToColorTransform;

    void Start()
    {
        vfxGraph = GetComponent<VisualEffect>();
        // Find the kernel index in the compute shader.
        kernelIndex = depthToPointCloudShader.FindKernel("CSMain");

        // Initialize resolution and camera intrinsics (focal length, principal point)
        focalLengthDepthX = 502.693f; // Set appropriate values
        focalLengthDepthY = 502.738f; // Set appropriate values
        principalPointDepthX = 334.378f; // Set appropriate values
        principalPointDepthY = 325.483f; // Set appropriate values
        focalLengthColorX = 608.248f; // Set appropriate values
        focalLengthColorY = 608.163f; // Set appropriate values
        principalPointColorX = 641.683f; // Set appropriate values
        principalPointColorY = 366.42f; // Set appropriate values
        depthToColorTransform = new Matrix4x4(); // Set appropriate values (extrinsic transformation matrix)
        depthToColorTransform.m00 = 0.999979f;
        depthToColorTransform.m01 = 0.00648094f;
        depthToColorTransform.m02 = -0.000547361f;
        depthToColorTransform.m03 = -32.0199f;

        depthToColorTransform.m10 = -0.00639222f;
        depthToColorTransform.m11 = 0.994841f;
        depthToColorTransform.m12 = 0.101247f;
        depthToColorTransform.m13 = -1.9547f;

        depthToColorTransform.m20 = 0.00120071f;
        depthToColorTransform.m21 = -0.101242f;
        depthToColorTransform.m22 = 0.994861f;
        depthToColorTransform.m23 = 3.81727f;

        depthToColorTransform.m30 = 0f;
        depthToColorTransform.m31 = 0f;
        depthToColorTransform.m32 = 0f;
        depthToColorTransform.m33 = 1f;

        Debug.Log($"Texture2D dimension: {megaTexture.width}x{megaTexture.height}");

        pointCloudBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, depth_width * depth_height, sizeof(float) * 4);
        colorBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, depth_width * depth_height, sizeof(float) * 4);
        depthBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, depth_width * depth_height, sizeof(float) * 4);
        depthLowBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, depth_width * depth_height, sizeof(float) * 4);
        colorCoordinatesBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, depth_width * depth_height, sizeof(float) * 2);
        depthCoordinatesBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, depth_width * depth_height, sizeof(float) * 2);
        depthImageBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, depth_width * depth_height, sizeof(float) * 2);

        depthToPointCloudShader.SetBuffer(kernelIndex, "pointCloud", pointCloudBuffer);
        depthToPointCloudShader.SetBuffer(kernelIndex, "colorBuffer", colorBuffer);
        depthToPointCloudShader.SetBuffer(kernelIndex, "depthBuffer", depthBuffer);
        depthToPointCloudShader.SetBuffer(kernelIndex, "depthLowBuffer", depthLowBuffer);
        depthToPointCloudShader.SetBuffer(kernelIndex, "colorCoordinatesBuffer", colorCoordinatesBuffer);
        depthToPointCloudShader.SetBuffer(kernelIndex, "depthCoordinatesBuffer", depthCoordinatesBuffer);
        depthToPointCloudShader.SetBuffer(kernelIndex, "depthImageBuffer", depthImageBuffer);

        depthToPointCloudShader.SetTexture(kernelIndex, "depthTexture", megaTexture);
        depthToPointCloudShader.SetInts("megaResolution", new int[] { megaframe_width, megaframe_height});
        depthToPointCloudShader.SetInts("colorResolution", new int[] { color_width, color_height });
        depthToPointCloudShader.SetInts("depthResolution", new int[] { depth_width, depth_height });
        depthToPointCloudShader.SetFloats("focalLengthDepth", new float[] { focalLengthDepthX, focalLengthDepthY });
        depthToPointCloudShader.SetFloats("principalPointDepth", new float[] { principalPointDepthX, principalPointDepthY });
        depthToPointCloudShader.SetFloats("focalLengthColor", new float[] { focalLengthColorX, focalLengthColorY });
        depthToPointCloudShader.SetFloats("principalPointColor", new float[] { principalPointColorX, principalPointColorY });
        depthToPointCloudShader.SetMatrix("depthToColorTransform", depthToColorTransform);

        depthToPointCloudShader.Dispatch(kernelIndex, Mathf.CeilToInt(depth_width / 8.0f), Mathf.CeilToInt(depth_height / 8.0f), 1);

        vfxGraph.SetGraphicsBuffer("PointBuffer", pointCloudBuffer);
        vfxGraph.SetGraphicsBuffer("ColorBuffer", colorBuffer);

        ReadBackBufferData();
    }

    void ReadBackBufferData()
    {
        float[] pointCloudData = new float[depth_width * depth_height * 4];
        pointCloudBuffer.GetData(pointCloudData);

        // Log a few points to the console for debugging
        for (int i = 0; i < 5000; i = i + 500)
        {
            Debug.Log($"Point {i}: X={pointCloudData[i * 4]}, Y={pointCloudData[i * 4 + 1]}, Z={pointCloudData[i * 4 + 2]}, W={pointCloudData[i * 4 + 3]}");
        }

        float[] depthBufferData = new float[depth_width * depth_height * 4];
        depthBuffer.GetData(depthBufferData);
        float[] depthLowBufferData = new float[depth_width * depth_height * 4];
        depthLowBuffer.GetData(depthLowBufferData);
        float[] depthCoordinatesBufferData = new float[depth_width * depth_height * 2];
        depthCoordinatesBuffer.GetData(depthCoordinatesBufferData);
        float[] depthImageBufferData = new float[depth_width * depth_height * 2];
        depthImageBuffer.GetData(depthImageBufferData);
        // Log a few points to the console for debugging
        for (int i = 0; i < 5000; i = i + 500)
        {
            Debug.Log($"Depth buffer {i} ({depthImageBufferData[i*2]}, {depthImageBufferData[i*2+1]}) => High: ({depthCoordinatesBufferData[i*2]}, {depthCoordinatesBufferData[i*2+1]}), R={depthBufferData[i * 4]}, G={depthBufferData[i * 4 + 1]}, B={depthBufferData[i * 4 + 2]}, A={depthBufferData[i * 4 + 3]}, Low: R={depthLowBufferData[i * 4]}, G={depthLowBufferData[i * 4 + 1]}, B={depthLowBufferData[i * 4 + 2]}, A={depthLowBufferData[i * 4 + 3]}");
        }

        float[] colorBufferData = new float[depth_width * depth_height * 4];
        colorBuffer.GetData(colorBufferData);
        float[] colorCoordinateData = new float[depth_width * depth_height * 2];
        colorCoordinatesBuffer.GetData(colorCoordinateData);

        // Log a few points to the console for debugging
        for (int i = 0; i < 5000; i = i + 500)
        {
            Debug.Log($"colorBuffer {i}: ({colorCoordinateData[i*2]}, {colorCoordinateData[i*2+1]}) R={colorBufferData[i * 4]}, G={colorBufferData[i * 4 + 1]}, B={colorBufferData[i * 4 + 2]}, A={colorBufferData[i * 4 + 3]}");
        }


    }


    void OnDestroy()
    {
        pointCloudBuffer.Release();
        colorBuffer.Release();
        colorCoordinatesBuffer.Release();
        depthBuffer.Release();
        depthLowBuffer.Release();
        depthCoordinatesBuffer.Release();
        depthImageBuffer.Release();
    }
}

