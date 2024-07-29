using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RaytracingMat : MonoBehaviour
{
    [SerializeField]
    public Color color;

    [SerializeField]
    public Color emmisiveColor = Color.black;

    [SerializeField, Range(0, 10)]
    public float emmisiveStrenght;

    [SerializeField, Range(0, 100)]
    public float radius;
}
