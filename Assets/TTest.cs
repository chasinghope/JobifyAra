﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TTest : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void OnDisable()
    {
        Debug.Log("qUIT");
    }

    private void OnDestroy()
    {
        Debug.Log("OnDestroy");
    }
}