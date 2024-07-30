using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct RayTracingMaterial
{
    [SerializeField]
    public Color color;

    [SerializeField]
    public Color emissiveColor;

    [SerializeField, Range(0, 100)]
    public float emissiveStrength;
}
