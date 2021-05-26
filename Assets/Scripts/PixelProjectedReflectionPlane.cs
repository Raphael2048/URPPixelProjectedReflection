using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PixelProjectedReflectionPlane : MonoBehaviour
{
    [Range(0, 2f)]
    public float PreFilterValue;

    public bool DualBlur;
    public Material Material;
}
