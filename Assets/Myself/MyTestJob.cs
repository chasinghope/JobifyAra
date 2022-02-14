using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

public class MyTestJob : MonoBehaviour
{
    public Gradient gradient;
    public AnimationCurve curve;
    NativeArray<float> nArray;
    NativeArray<GradientColorKey> GradientColorKeyArray;
    NativeArray<GradientAlphaKey> GradientAlphaKeyArray;

    NativeArray<Keyframe> keyframeArray;
    
    // Start is called before the first frame update
    void Start()
    {
        nArray = new NativeArray<float>(1, Allocator.Persistent);
        GradientColorKeyArray = new NativeArray<GradientColorKey>(this.gradient.colorKeys.Length, Allocator.Persistent);
        for (int i = 0; i < this.gradient.colorKeys.Length; i++)
        {
            this.GradientColorKeyArray[i] = this.gradient.colorKeys[i];
        }
        GradientAlphaKeyArray = new NativeArray<GradientAlphaKey>(this.gradient.alphaKeys.Length, Allocator.Persistent);
        for (int i = 0; i < this.gradient.alphaKeys.Length; i++)
        {
            this.GradientAlphaKeyArray[i] = this.gradient.alphaKeys[i];
        }
        keyframeArray = new NativeArray<Keyframe>(this.curve.keys.Length, Allocator.Persistent);
        for (int i = 0; i < this.curve.keys.Length; i++)
        {
            keyframeArray[i] = this.curve.keys[i];
        }
    }

    // Update is called once per frame
    void Update()
    {
        MJob mJob = new MJob
        {
            GradientColorKeyArray = this.GradientColorKeyArray,
            GradientAlphaKeyArray = this.GradientAlphaKeyArray,
            gradientMode = this.gradient.mode,
            keyframeArray = this.keyframeArray,
        };
        JobHandle jobHandle = mJob.Schedule();
        jobHandle.Complete();
 

    }

    private void OnDestroy()
    {
        if (nArray.IsCreated)
            nArray.Dispose();
        if (GradientColorKeyArray.IsCreated)
            GradientColorKeyArray.Dispose();
        if (GradientAlphaKeyArray.IsCreated)
            GradientAlphaKeyArray.Dispose();
        if (keyframeArray.IsCreated)
            keyframeArray.Dispose();
    }

    [BurstCompile]
    public struct MJob : IJob
    {
        //public NativeArray<float> nArray;
        public NativeArray<GradientColorKey> GradientColorKeyArray;
        public NativeArray<GradientAlphaKey> GradientAlphaKeyArray;
        public GradientMode gradientMode;
        public NativeArray<Keyframe> keyframeArray;

        public void Execute()
        {
            Color color = UnityJobifyHelper.Gradient_Evaluate(GradientColorKeyArray, GradientAlphaKeyArray, gradientMode, 0.5f);
            float fValue = UnityJobifyHelper.AnimationCurve_Evaluate(keyframeArray, 0.5f);
            Debug.Log($"color: {color}");
            Debug.Log($"fValue: {fValue}");
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
