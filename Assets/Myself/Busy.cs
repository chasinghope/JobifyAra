using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Busy : MonoBehaviour
{
    private void Update()
    {
        float a = 0;
        for (int i = 0; i < 500000; i++)
        {
            a = Mathf.Exp(i);
        }

    }
}