using System;
using System.Collections;
using System.Collections.Generic;
using TreeEditor;
using Unity.VisualScripting;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

public class Bvh
{
    public const int BvhMaxDepth = 18;

    public readonly struct BVHTriangle
    {
        public readonly Vector3 Centre;
        public readonly Vector3 Min;
        public readonly Vector3 Max;
        public readonly int Index;

        public BVHTriangle(Vector3 centre, Vector3 min, Vector3 max, int index)
        {
            Centre = centre;
            Min = min;
            Max = max;
            Index = index;
        }
    }

    public struct BvhNode
    {
        public int child1Index;
        public int child2Index;
        public BoundingBox bounds;
        public int triangleIndex;
        public int triangleCount;
        public int depht;
    }

    public struct BvhNodeGpu
    {
        public Vector3 boxMin;
        public int child1Index;
        public Vector3 boxMax;
        public int child2Index;
        public int triangleIndex;
        public int triangleCount;
    }

    enum Axis
    {
        X, Y, Z
    }

    public BvhNode[] BvhNodes;

    // one for mother Node
    private BVHTriangle[] Bvhtriangles;

    public Triangle[] triangles;

    public BvhNodeGpu[] GetGpuNodes()
    {
        BvhNodeGpu[] returnArray = new BvhNodeGpu[BvhNodes.Length];

        for (int i = 0; i < BvhNodes.Length; i++)
        {
            returnArray[i].boxMin = BvhNodes[i].bounds.Min;
            returnArray[i].boxMax = BvhNodes[i].bounds.Max;
            returnArray[i].child1Index = BvhNodes[i].child1Index;
            returnArray[i].child2Index = BvhNodes[i].child2Index;
            returnArray[i].triangleIndex = BvhNodes[i].triangleIndex;
            returnArray[i].triangleCount = BvhNodes[i].triangleCount;
        }

        return returnArray;
    }

    void EncapsulateTriangle(ref Bounds bounds, Vector3 min, Vector3 max)
    {
        Vector3 Min = bounds.min;
        Vector3 Max = bounds.max;

        Min.x = min.x < Min.x ? min.x : Min.x;
        Min.y = min.y < Min.y ? min.y : Min.y;
        Min.z = min.z < Min.z ? min.z : Min.z;
        Max.x = max.x > Max.x ? max.x : Max.x;
        Max.y = max.y > Max.y ? max.y : Max.y;
        Max.z = max.z > Max.z ? max.z : Max.z;

        bounds.SetMinMax(Min, Max);
    }
    /*
void SplitNode(BvhNode mother, ref BvhNode child1, ref BvhNode child2, Axis axis)
{
    Vector3 motherCenter = mother.bounds.Centre;
    Vector3 motherSize = mother.bounds.Size;

    Vector3 childSize = motherSize;
    Vector3 child1Center, child2Center;

    switch (axis)
    {
        case Axis.X:
            childSize.x *= 0.5f;
            child1Center = motherCenter - new Vector3(motherSize.x * 0.25f, 0, 0);
            child2Center = motherCenter + new Vector3(motherSize.x * 0.25f, 0, 0);
            break;

        case Axis.Y:
            childSize.y *= 0.5f;
            child1Center = motherCenter - new Vector3(0, motherSize.y * 0.25f, 0);
            child2Center = motherCenter + new Vector3(0, motherSize.y * 0.25f, 0);
            break;

        case Axis.Z:
            childSize.z *= 0.5f;
            child1Center = motherCenter - new Vector3(0, 0, motherSize.z * 0.25f);
            child2Center = motherCenter + new Vector3(0, 0, motherSize.z * 0.25f);
            break;

        default:
            throw new ArgumentException("Invalid axis for splitting bounds.");
    }

    child1.bounds = new Bounds(child1Center, childSize);
    child2.bounds = new Bounds(child2Center, childSize);
} */
    public static (int axis, float splitPos) GetSplitAxisAndPosition(BoundingBox bounds)
    {
        Vector3 size = bounds.Size;
        int splitAxis = size.x > Mathf.Max(size.y, size.z) ? 0 : size.y > size.z ? 1 : 2;

        // Calculate the split position, which will be the center along the split axis
        float splitPos = bounds.Centre[splitAxis];

        return (splitAxis, splitPos);
    }

    void InitNode(ref BvhNode[] bvhNodes, int currentNodeIndex, int depth)
    {
        ref BvhNode parent = ref bvhNodes[currentNodeIndex];
        parent.depht = depth;

        if (depth == BvhMaxDepth)
        {
            return;
        }
        // Set ChildIndex 
        parent.child1Index = currentNodeIndex * 2 + 1;
        parent.child2Index = currentNodeIndex * 2 + 2;

        ref BvhNode child1 = ref bvhNodes[parent.child1Index];
        ref BvhNode child2 = ref bvhNodes[parent.child2Index];

        // Init Child One
        child1.triangleIndex = parent.triangleIndex;
        child1.triangleCount = 0;
        // Init Child2
        child2.triangleIndex = parent.triangleIndex;
        child2.triangleCount = 0;

        (int axis, float spliPos) = Bvh.GetSplitAxisAndPosition(parent.bounds); 

        // Sorting Triangle in order to get continius triagle for gpu
        for (int i = parent.triangleIndex; i < parent.triangleIndex + parent.triangleCount; i++)
        {
            bool onSideA = Bvhtriangles[i].Centre[axis] < spliPos;

            ref BvhNode node = ref child1;
            if (onSideA)
            {
                node = ref child1;
            }
            else 
            {
                node = ref child2;
            }
            node.bounds.GrowToInclude(Bvhtriangles[i].Min, Bvhtriangles[i].Max);
            node.triangleCount++;


            if (onSideA)
            {
                int swap = node.triangleIndex + node.triangleCount - 1;
                (Bvhtriangles[i], Bvhtriangles[swap]) = ((Bvhtriangles[swap], Bvhtriangles[i]));
                child2.triangleIndex++;
            }


        }

        InitNode(ref bvhNodes, bvhNodes[currentNodeIndex].child1Index, depth + 1);
        InitNode(ref bvhNodes, bvhNodes[currentNodeIndex].child2Index, depth + 1);

    }

    public Bvh(Vector3[] verts, int[] indices, Vector3[] normals, Bounds baseBoundingBox)
    {

        int nodeSize = (1 << (BvhMaxDepth + 1)) - 1;

        BvhNodes = new BvhNode[nodeSize];
        Bvhtriangles = new BVHTriangle[indices.Length / 3];
        triangles = new Triangle[indices.Length / 3];
  

        for (int i = 0; i < indices.Length; i += 3)
        {
            Vector3 a = verts[indices[i + 0]];
            Vector3 b = verts[indices[i + 1]];
            Vector3 c = verts[indices[i + 2]];
            Vector3 centre = (a + b + c) / 3;
            Vector3 max = Vector3.Max(Vector3.Max(a, b), c);
            Vector3 min = Vector3.Min(Vector3.Min(a, b), c);
            Bvhtriangles[i / 3] = new BVHTriangle(centre, min, max, i);
        }

        ref BvhNode node = ref BvhNodes[0];
        node.bounds.Min = baseBoundingBox.min;
        node.bounds.Max = baseBoundingBox.max;

        node.triangleIndex = 0;
        node.triangleCount = Bvhtriangles.Length;

        InitNode(ref BvhNodes, 0, 0);

        for (int i = 0; i < Bvhtriangles.Length; i++)
        {
            BVHTriangle buildTri = Bvhtriangles[i];
            Vector3 a = verts[indices[buildTri.Index + 0]];
            Vector3 b = verts[indices[buildTri.Index + 1]];
            Vector3 c = verts[indices[buildTri.Index + 2]];
            Vector3 norm_a = normals[indices[buildTri.Index + 0]];
            Vector3 norm_b = normals[indices[buildTri.Index + 1]];
            Vector3 norm_c = normals[indices[buildTri.Index + 2]];
            triangles[i] = new Triangle(a, b, c, norm_a, norm_b, norm_c);
        }

      

    }


    public struct BoundingBox
    {
        public Vector3 Min;
        public Vector3 Max;
        public Vector3 Centre => (Min + Max) / 2;
        public Vector3 Size => Max - Min;
        bool hasPoint;

        public void GrowToInclude(Vector3 min, Vector3 max)
        {
            if (hasPoint)
            {
                Min.x = min.x < Min.x ? min.x : Min.x;
                Min.y = min.y < Min.y ? min.y : Min.y;
                Min.z = min.z < Min.z ? min.z : Min.z;
                Max.x = max.x > Max.x ? max.x : Max.x;
                Max.y = max.y > Max.y ? max.y : Max.y;
                Max.z = max.z > Max.z ? max.z : Max.z;
            }
            else
            {
                hasPoint = true;
                Min = min;
                Max = max;
            }
        }
    }
}