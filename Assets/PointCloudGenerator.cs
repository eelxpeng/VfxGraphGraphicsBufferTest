using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VFX;
using System.IO;
using UnityEngine.Video;

public class PointCloudGenerator : MonoBehaviour
{
    public ComputeShader depthToPointCloudShader;
    public VideoPlayer videoPlayer; // Add a VideoPlayer component in the inspector
    private RenderTexture videoRenderTexture;

    public Texture2D megaTexture;
    private Texture2D colorTexture;
    private Texture2D depthTexture;

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

    //Saving depth data to csv files
    private ComputeBuffer lowDepthBuffer;
    private ComputeBuffer highDepthBuffer;
    private ComputeBuffer depthValuesBuffer;

    private float[] lowDepthData;
    private float[] highDepthData;
    private float[] depthData;

    public Material heatmapMaterial;

    void Start()
    {
        vfxGraph = GetComponent<VisualEffect>();
        // Find the kernel index in the compute shader.
        kernelIndex = depthToPointCloudShader.FindKernel("CSMain");

        // Initialize resolution and camera intrinsics (focal length, principal point)
        focalLengthDepthX = 504.224f; // Set appropriate values
        focalLengthDepthY = 504.436f; // Set appropriate values
        principalPointDepthX = 322.973f; // Set appropriate values
        principalPointDepthY = 335.085f; // Set appropriate values
        focalLengthColorX = 607.828f; // Set appropriate values
        focalLengthColorY = 607.61f; // Set appropriate values
        principalPointColorX = 641.845f; // Set appropriate values
        principalPointColorY = 366.085f; // Set appropriate values
        depthToColorTransform = new Matrix4x4(); // Set appropriate values (extrinsic transformation matrix)
        depthToColorTransform.m00 = 0.999937f;
        depthToColorTransform.m01 = 0.0101022f;
        depthToColorTransform.m02 = -0.00481029f;
        depthToColorTransform.m03 = -32.1306f;

        depthToColorTransform.m10 = -0.00962843f;
        depthToColorTransform.m11 = 0.995896f;
        depthToColorTransform.m12 = 0.0899918f;
        depthToColorTransform.m13 = -2.04877f;

        depthToColorTransform.m20 = 0.00569966f;
        depthToColorTransform.m21 = -0.0899398f;
        depthToColorTransform.m22 = 0.995931f;
        depthToColorTransform.m23 = 3.81019f;

        depthToColorTransform.m30 = 0f;
        depthToColorTransform.m31 = 0f;
        depthToColorTransform.m32 = 0f;
        depthToColorTransform.m33 = 1f;

        Debug.Log($"Texture2D dimension: {megaTexture.width}x{megaTexture.height}");

        // Initialize VideoPlayer and RenderTexture
        videoRenderTexture = new RenderTexture(megaframe_width, megaframe_height, 0);
        videoPlayer.renderMode = VideoRenderMode.RenderTexture;
        videoPlayer.targetTexture = videoRenderTexture;
        videoPlayer.Play();

        // Create new textures
        colorTexture = new Texture2D(megaframe_width, color_height, TextureFormat.RGBAFloat, false);
        depthTexture = new Texture2D(megaframe_width, depth_height, TextureFormat.RGBAFloat, false);

        /*
        // Copy pixels for the first texture (1280x720)
        Color[] pixels1 = megaTexture.GetPixels(0, depth_height, megaframe_width, megaframe_height - depth_height);
        colorTexture.SetPixels(pixels1);
        colorTexture.Apply();

        // Copy pixels for the second texture (1280x576)
        Color[] pixels2 = megaTexture.GetPixels(0, 0, megaframe_width, depth_height);
        depthTexture.SetPixels(pixels2);
        depthTexture.Apply();
        */

        // Initialize depth data buffers
        lowDepthBuffer = new ComputeBuffer(depth_width * depth_height, sizeof(float));
        highDepthBuffer = new ComputeBuffer(depth_width * depth_height, sizeof(float));
        depthValuesBuffer = new ComputeBuffer(depth_width * depth_height, sizeof(float));

        lowDepthData = new float[depth_width * depth_height];
        highDepthData = new float[depth_width * depth_height];
        depthData = new float[depth_width * depth_height];

        pointCloudBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, depth_width * depth_height, sizeof(float) * 4);
        colorBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, color_width * color_height, sizeof(float) * 4);
        //depthBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, depth_width * depth_height, sizeof(float) * 4);
        //depthLowBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, depth_width * depth_height, sizeof(float) * 4);
        colorCoordinatesBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, color_width * color_height, sizeof(float) * 2);
        depthCoordinatesBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, depth_width * depth_height, sizeof(float) * 2);
        depthImageBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, depth_width * depth_height, sizeof(float) * 2);

        depthToPointCloudShader.SetBuffer(kernelIndex, "pointCloud", pointCloudBuffer);
        depthToPointCloudShader.SetBuffer(kernelIndex, "colorBuffer", colorBuffer);
        //depthToPointCloudShader.SetBuffer(kernelIndex, "depthBuffer", depthBuffer);
        //depthToPointCloudShader.SetBuffer(kernelIndex, "depthLowBuffer", depthLowBuffer);
        depthToPointCloudShader.SetBuffer(kernelIndex, "colorCoordinatesBuffer", colorCoordinatesBuffer);
        depthToPointCloudShader.SetBuffer(kernelIndex, "depthCoordinatesBuffer", depthCoordinatesBuffer);
        depthToPointCloudShader.SetBuffer(kernelIndex, "depthImageBuffer", depthImageBuffer);

        // Set depth data buffers
        depthToPointCloudShader.SetBuffer(kernelIndex, "lowDepthTransformedBuffer", lowDepthBuffer);
        depthToPointCloudShader.SetBuffer(kernelIndex, "highDepthTransformedBuffer", highDepthBuffer);
        depthToPointCloudShader.SetBuffer(kernelIndex, "depthValuesBuffer", depthValuesBuffer);


        depthToPointCloudShader.SetTexture(kernelIndex, "colorTexture", colorTexture);
        depthToPointCloudShader.SetTexture(kernelIndex, "depthTexture", depthTexture);
        depthToPointCloudShader.SetInts("megaResolution", new int[] { megaframe_width, megaframe_height});
        depthToPointCloudShader.SetInts("colorResolution", new int[] { color_width, color_height });
        depthToPointCloudShader.SetInts("depthResolution", new int[] { depth_width, depth_height });
        depthToPointCloudShader.SetFloats("focalLengthDepth", new float[] { focalLengthDepthX, focalLengthDepthY });
        depthToPointCloudShader.SetFloats("principalPointDepth", new float[] { principalPointDepthX, principalPointDepthY });
        depthToPointCloudShader.SetFloats("focalLengthColor", new float[] { focalLengthColorX, focalLengthColorY });
        depthToPointCloudShader.SetFloats("principalPointColor", new float[] { principalPointColorX, principalPointColorY });
        depthToPointCloudShader.SetMatrix("depthToColorTransform", depthToColorTransform);

        /*
        depthToPointCloudShader.Dispatch(kernelIndex, Mathf.CeilToInt(depth_width / 8.0f), Mathf.CeilToInt(depth_height / 8.0f), 1);

        vfxGraph.SetGraphicsBuffer("PointBuffer", pointCloudBuffer);
        vfxGraph.SetGraphicsBuffer("ColorBuffer", colorBuffer);

        lowDepthBuffer.GetData(lowDepthData);
        highDepthBuffer.GetData(highDepthData);
        depthValuesBuffer.GetData(depthData);

        OriginalMap();
        SaveToCSV();
        CreateHeatmap();
        */
    }

    void Update()
    {
        int count = 0;
        if (videoPlayer.isPlaying)
        {
            count++;
            Debug.Log("Video player is called:" + count);

            // Update textures from video
            RenderTexture.active = videoRenderTexture;
            colorTexture.ReadPixels(new Rect(0, depth_height, megaframe_width, megaframe_height - depth_height), 0, 0);
            colorTexture.Apply();
            depthTexture.ReadPixels(new Rect(0, 0, megaframe_width, depth_height), 0, 0);
            depthTexture.Apply();
            RenderTexture.active = null;

            // Dispatch the compute shader
            depthToPointCloudShader.Dispatch(kernelIndex, Mathf.CeilToInt(depth_width / 8.0f), Mathf.CeilToInt(depth_height / 8.0f), 1);

            // Set buffers for VFX graph
            vfxGraph.SetGraphicsBuffer("PointBuffer", pointCloudBuffer);
            vfxGraph.SetGraphicsBuffer("ColorBuffer", colorBuffer);

            lowDepthBuffer.GetData(lowDepthData);
            highDepthBuffer.GetData(highDepthData);
            depthValuesBuffer.GetData(depthData);

            OriginalMap();
            SaveToCSV();
            CreateHeatmap();
        }
    }

    private void OriginalMap()
    {
        string filename = "Assets/OriginalValues.csv";
        Color[] pixels = depthTexture.GetPixels();
        using (StreamWriter writer = new StreamWriter(filename))
        {
            for (int y = 0; y < depth_height; y++)
            {
                for (int x = 0; x < megaframe_width; x++)
                {
                    float original = pixels[y * megaframe_width + x].b * 255;
                    writer.Write(original.ToString());
                    if (x < megaframe_width - 1)
                        writer.Write(",");
                }
                writer.WriteLine();
            }
        }

    }

    void SaveToCSV()
    {
        Debug.Log("Saving to csv:");
        SaveArrayToCSV("Assets/LowDepthTransformed.csv", lowDepthData);
        SaveArrayToCSV("Assets/HighDepthTransformed.csv", highDepthData);
        SaveArrayToCSV("Assets/DepthValues.csv", depthData);
    }

    void SaveArrayToCSV(string fileName, float[] array)
    {
        using (StreamWriter writer = new StreamWriter(fileName))
        {
            for (int y = 0; y < depth_height; y++)
            {
                for (int x = 0; x < depth_width; x++)
                {
                    writer.Write(array[y * depth_width + x].ToString());
                    if (x < depth_width - 1)
                        writer.Write(",");
                }
                writer.WriteLine();
            }
        }
    }

    private void CreateHeatmap()
    {
        Texture2D heatmapTexture = new Texture2D(depth_width, depth_height);
        float minDepth = Mathf.Min(highDepthData);
        float maxDepth = Mathf.Max(highDepthData);
        float depthRange = maxDepth - minDepth;
        print("Min:" + minDepth);
        print("Max:" + maxDepth);
        for (int y = 0; y < depth_height; y++)
        {
            for (int x = 0; x < depth_width; x++)
            {
                float depthValue = highDepthData[y * depth_width + x];
                float normalizedDepth = (depthValue - minDepth) / depthRange;
                Color color = GetHeatmapColor(normalizedDepth);
                heatmapTexture.SetPixel(x, y, color);
            }
        }
        heatmapTexture.Apply();

        GameObject heatmapPlane = GameObject.CreatePrimitive(PrimitiveType.Plane);
        heatmapPlane.transform.localScale = new Vector3(6.4f, 1, 5.76f); // Adjust the scale as needed
        heatmapPlane.GetComponent<Renderer>().material = heatmapMaterial;
        heatmapMaterial.mainTexture = heatmapTexture;
    }

    private Color GetHeatmapColor(float value)
    {
        // This function maps a depth value to a color
        // Adjust the mapping logic as needed
        return new Color(value, 0f, 1f - value);
    }

        void OnDestroy()
    {
        pointCloudBuffer.Release();
        colorBuffer.Release();
        colorCoordinatesBuffer.Release();
        //depthBuffer.Release();
        //depthLowBuffer.Release();
        depthCoordinatesBuffer.Release();
        depthImageBuffer.Release();

        lowDepthBuffer.Release();
        highDepthBuffer.Release();
        depthValuesBuffer.Release();
    }
}

