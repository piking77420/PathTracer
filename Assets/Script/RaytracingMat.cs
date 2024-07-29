using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RaytracingMat : MonoBehaviour
{
    [SerializeField]
    public Color color;

    [SerializeField]
    public Color emissiveColor = Color.black;

    [SerializeField, Range(0, 100)]
    public float emissiveStrength;

    [SerializeField, Range(0, 100)]
    public float radius;
}
