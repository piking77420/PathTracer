using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// thanks to https://github.com/SebLague
public static class ShaderHelper
{
    public static int GetStride<T>()
    {
        return System.Runtime.InteropServices.Marshal.SizeOf(typeof(T));
    }

    public static void CreateStructuredBuffer<T>(ref ComputeBuffer buffer, int count)
    {
        int stride = GetStride<T>();
        bool createNewBuffer = buffer == null || !buffer.IsValid() || buffer.count != count || buffer.stride != stride;
        if (createNewBuffer)
        {
            Release(buffer);
            buffer = new ComputeBuffer(count, stride);
        }
    }

    public static void CreateStructuredBuffer<T>(ref ComputeBuffer buffer, T[] data) where T : struct
    {
        // Cannot create 0 length buffer (not sure why?)
        int length = Mathf.Max(1, data.Length);
        // The size (in bytes) of the given data type
        int stride = System.Runtime.InteropServices.Marshal.SizeOf(typeof(T));

        // If buffer is null, wrong size, etc., then we'll need to create a new one
        if (buffer == null || !buffer.IsValid() || buffer.count != length || buffer.stride != stride)
        {
            if (buffer != null) { buffer.Release(); }
            buffer = new ComputeBuffer(length, stride, ComputeBufferType.Structured);
        }

        buffer.SetData(data);
    }

    public static ComputeBuffer CreateStructuredBuffer<T>(int count)
    {
        return new ComputeBuffer(count, GetStride<T>());
    }

    public static ComputeBuffer CreateStructuredBuffer<T>(T[] data)
    {
        var buffer = new ComputeBuffer(data.Length, GetStride<T>());
        buffer.SetData(data);
        return buffer;
    }
 
    /// Releases supplied buffer/s if not null
    public static void Release(params ComputeBuffer[] buffers)
    {
        for (int i = 0; i < buffers.Length; i++)
        {
            if (buffers[i] != null)
            {
                buffers[i].Release();
            }
        }
    }

    /// Releases supplied render textures/s if not null
    public static void Release(params RenderTexture[] textures)
    {
        for (int i = 0; i < textures.Length; i++)
        {
            if (textures[i] != null)
            {
                textures[i].Release();
            }
        }
    }
}
