using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

public class MyTestJob : MonoBehaviour
{

    NativeArray<float> nArray;

    
    // Start is called before the first frame update
    void Start()
    {
        nArray = new NativeArray<float>(1, Allocator.Persistent);

    }

    // Update is called once per frame
    void Update()
    {
        MJob mJob = new MJob
        {
            nArray = this.nArray
        };
        JobHandle jobHandle = mJob.Schedule();
        jobHandle.Complete();

        Debug.Log($"nArray[0]: {nArray[0]}");

    }

    private void OnDestroy()
    {
        if (nArray.IsCreated)
            nArray.Dispose();
    }

    [BurstCompile]
    public struct MJob : IJob
    {
        //public NativeArray<float> nArray;
        public NativeArray<float> nArray;

        public void Execute()
        {
            nArray[0] = nArray[0] + 10;
        }

       
    }

    public class Dog
    {
        public int Wang()
        {
            Debug.Log("Wang");
            return 5;
        }
    }

    //public static class UnityJobifyHelper
    //{
    //    public static Color Gradient_Evaluate(NativeArray<GradientColorKey> rGradientColorKeyArray, NativeArray<GradientAlphaKey> rGradientAlphaKeyArray, GradientMode rGradientMode, float fTime)
    //    {
    //        Gradient gradient = new Gradient() 
    //        { 
    //            colorKeys = rGradientColorKeyArray.ToArray(),
    //            alphaKeys = rGradientAlphaKeyArray.ToArray(),

    //        };
    //        //gradient.mode = rGradientMode;
    //        return gradient.Evaluate(fTime);
    //    }

    //    public static float Gradient_Evaluate(NativeArray<Keyframe> rKeyframes, float fTime)
    //    {
    //        AnimationCurve curve = new AnimationCurve(rKeyframes.ToArray());
    //        return curve.Evaluate(fTime);
    //    }
    //}

}
