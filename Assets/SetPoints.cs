using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VFX;

public class SetPoints : MonoBehaviour
{
    GraphicsBuffer _buffer;
    VisualEffect _vfx;
    private Vector4[] data;

    // Start is called before the first frame update
    void Start()
    {
        _vfx = GetComponent<VisualEffect>();
        _buffer = new GraphicsBuffer
          (GraphicsBuffer.Target.Structured, 5, 4 * sizeof(float));
        
        // Fill the buffer with data (x, y, z, w)
        int elementCount = 5;
        float scale = 1.0f / 100;
        data = new Vector4[elementCount];
        for (int i = 0; i < elementCount; i++)
        {
            data[i] = new Vector4(i * scale , i * scale, i * scale, 0); // (x, y, z, w)
        }
        _buffer.SetData(data);

        _vfx.SetGraphicsBuffer("PointBuffer", _buffer);
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    void OnDestroy()
    {
        _buffer?.Dispose();
        _buffer = null;
    }
}