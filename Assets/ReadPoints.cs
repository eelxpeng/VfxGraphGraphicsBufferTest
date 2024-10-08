using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VFX;

public class ReadPoints : MonoBehaviour
{
    public ComputeShader depthToPointCloudShader;
    public Texture2D megaTexture;
    private int kernelIndex;

    private GraphicsBuffer megaBuffer;
    private GraphicsBuffer megaCoordinatesBuffer;
    private GraphicsBuffer colorBuffer;
    private GraphicsBuffer depthBuffer;
    private GraphicsBuffer colorCoordinatesBuffer;
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
        megaBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, megaframe_width * megaframe_height, sizeof(float) * 4);
        megaCoordinatesBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, megaframe_width * megaframe_height, sizeof(float) * 2);
        colorBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, depth_width * depth_height, sizeof(float) * 4);
        depthBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, depth_width * depth_height, sizeof(float) * 4);
        colorCoordinatesBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, depth_width * depth_height, sizeof(float) * 2);

        depthToPointCloudShader.SetBuffer(kernelIndex, "megaBuffer", megaBuffer);
        depthToPointCloudShader.SetBuffer(kernelIndex, "megaCoordinatesBuffer", megaCoordinatesBuffer);
        depthToPointCloudShader.SetBuffer(kernelIndex, "colorBuffer", colorBuffer);
        depthToPointCloudShader.SetBuffer(kernelIndex, "depthBuffer", depthBuffer);
        depthToPointCloudShader.SetBuffer(kernelIndex, "colorCoordinatesBuffer", colorCoordinatesBuffer);

        depthToPointCloudShader.SetTexture(kernelIndex, "megaTexture", megaTexture);
        depthToPointCloudShader.SetInts("megaResolution", new int[] { megaframe_width, megaframe_height});
        depthToPointCloudShader.SetInts("colorResolution", new int[] { color_width, color_height });
        depthToPointCloudShader.SetInts("depthResolution", new int[] { depth_width, depth_height });
        depthToPointCloudShader.SetFloats("focalLengthDepth", new float[] { focalLengthDepthX, focalLengthDepthY });
        depthToPointCloudShader.SetFloats("principalPointDepth", new float[] { principalPointDepthX, principalPointDepthY });
        depthToPointCloudShader.SetFloats("focalLengthColor", new float[] { focalLengthColorX, focalLengthColorY });
        depthToPointCloudShader.SetFloats("principalPointColor", new float[] { principalPointColorX, principalPointColorY });
        depthToPointCloudShader.SetMatrix("depthToColorTransform", depthToColorTransform);

        depthToPointCloudShader.Dispatch(kernelIndex, Mathf.CeilToInt(megaframe_width / 8.0f), Mathf.CeilToInt(megaframe_height / 8.0f), 1);

        ReadBackBufferData();
    }

    int GetIndex(int x, int y, int width)
    {
        return y * width + x;
    }
    void ReadBackBufferData()
    {
        Vector4[] megaBufferData = new Vector4[megaframe_width * megaframe_height];
        megaBuffer.GetData(megaBufferData);
        Vector2[] megaCoordinatesBufferData = new Vector2[megaframe_width * megaframe_height];
        megaCoordinatesBuffer.GetData(megaCoordinatesBufferData);

        Vector2Int[] points = {
            new Vector2Int(0, 0),
            new Vector2Int(0, megaframe_height - 1),
            new Vector2Int(megaframe_width - 1, 0),
            new Vector2Int(megaframe_width - 1, megaframe_height - 1)
        };
        
        for (int i=0; i<points.Length; i++)
        {
            Vector2Int point = points[i];
            Debug.Log($"Megaframe ({point.x},{point.y}): {megaBufferData[GetIndex(point.x, point.y, megaframe_width)]}");
        }

        Debug.Log($"Megaframe coordinates");
        for (int i=0; i<points.Length; i++)
        {
            Vector2Int point = points[i];
            Debug.Log($"Megaframe ({point.x},{point.y}): {megaCoordinatesBufferData[GetIndex(point.x, point.y, megaframe_width)]}");
        }
    }


    void OnDestroy()
    {
        megaBuffer.Release();
        megaCoordinatesBuffer.Release();
        colorBuffer.Release();
        colorCoordinatesBuffer.Release();
        depthBuffer.Release();
    }
}

