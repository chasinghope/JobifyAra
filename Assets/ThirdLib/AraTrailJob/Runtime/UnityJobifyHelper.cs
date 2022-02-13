using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

public static class UnityJobifyHelper
{
    public static Color Gradient_Evaluate(NativeArray<GradientColorKey> rGradientColorKeyArray, NativeArray<GradientAlphaKey> rGradientAlphaKeyArray, GradientMode rGradientMode, float fTime)
    {
        Gradient gradient = new Gradient()
        {
            colorKeys = rGradientColorKeyArray.ToArray(),
            alphaKeys = rGradientAlphaKeyArray.ToArray(),

        };
        //gradient.mode = rGradientMode;
        return gradient.Evaluate(fTime);
    }

    public static float AnimationCurve_Evaluate(NativeArray<Keyframe> rKeyframes, float fTime)
    {
        AnimationCurve curve = new AnimationCurve(rKeyframes.ToArray());
        return curve.Evaluate(fTime);
    }
}
