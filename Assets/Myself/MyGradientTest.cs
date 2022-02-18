using AraJob;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.UI;

public class MyGradientTest : MonoBehaviour
{
    public Gradient gradient;
    public AnimationCurve curve;

    private NativeList<GradientColorKey> colorKeys;
    private NativeList<GradientAlphaKey> alphaKeys;

    private NativeList<Keyframe> keyframes;
    
    public void Start()
    {
        //Debug.Log($"<color=orange>0.0f  {this.gradient.Evaluate(0.0f)}</color>");
        //Debug.Log($"<color=orange>0.1f  {this.gradient.Evaluate(0.1f)}</color>");
        //Debug.Log($"<color=orange>0.3f  {this.gradient.Evaluate(0.3f)}</color>");
        //Debug.Log($"<color=orange>0.5f  {this.gradient.Evaluate(0.5f)}</color>");
        //Debug.Log($"<color=orange>0.7f  {this.gradient.Evaluate(0.7f)}</color>");
        //Debug.Log($"<color=orange>1.0f  {this.gradient.Evaluate(1.0f)}</color>");

        //colorKeys = new NativeList<GradientColorKey>(8, Allocator.Persistent);
        //alphaKeys = new NativeList<GradientAlphaKey>(8, Allocator.Persistent);
        //colorKeys.CopyFrom(this.gradient.colorKeys);
        //alphaKeys.CopyFrom(this.gradient.alphaKeys);

        //Debug.Log($"0.0f  {UnitySrcAssist.GradientEvaluate(this.colorKeys, this.alphaKeys, this.gradient.mode, 0.0f)}");
        //Debug.Log($"0.1f  {UnitySrcAssist.GradientEvaluate(this.colorKeys, this.alphaKeys, this.gradient.mode, 0.1f)}");
        //Debug.Log($"0.3f  {UnitySrcAssist.GradientEvaluate(this.colorKeys, this.alphaKeys, this.gradient.mode, 0.3f)}");
        //Debug.Log($"0.5f  {UnitySrcAssist.GradientEvaluate(this.colorKeys, this.alphaKeys, this.gradient.mode, 0.5f)}");
        //Debug.Log($"0.7f  {UnitySrcAssist.GradientEvaluate(this.colorKeys, this.alphaKeys, this.gradient.mode, 0.7f)}");
        //Debug.Log($"1.0f  {UnitySrcAssist.GradientEvaluate(this.colorKeys, this.alphaKeys, this.gradient.mode, 1.0f)}");







        //Debug.Log($"<color=#33cccc>-1f   {UnitySrcAssist.AnimationCurveEvaluate(this.keyframes, -1f)}  </color>");
        //Debug.Log($"<color=#33cccc>0.0f  {UnitySrcAssist.AnimationCurveEvaluate(this.keyframes, 0.0f)} </color>");
        //Debug.Log($"<color=#33cccc>0.1f  {UnitySrcAssist.AnimationCurveEvaluate(this.keyframes, 0.1f)} </color>");
        //Debug.Log($"<color=#33cccc>0.3f  {UnitySrcAssist.AnimationCurveEvaluate(this.keyframes, 0.3f)} </color>");
        //Debug.Log($"<color=#33cccc>0.5f  {UnitySrcAssist.AnimationCurveEvaluate(this.keyframes, 0.5f)} </color>");
        //Debug.Log($"<color=#33cccc>0.7f  {UnitySrcAssist.AnimationCurveEvaluate(this.keyframes, 0.7f)} </color>");
        //Debug.Log($"<color=#33cccc>1.0f  {UnitySrcAssist.AnimationCurveEvaluate(this.keyframes, 1.0f)} </color>");
        //Debug.Log($"<color=#33cccc>2.0f  {UnitySrcAssist.AnimationCurveEvaluate(this.keyframes, 2.0f)} </color>");


    }

    [ContextMenu("DebugCurveData")]
    public void DebugCurveData()
    {
        foreach (var item in this.curve.keys)
        {
            Debug.Log($"weightedMode: {item.weightedMode}\n time: {item.time}\n value: {item.value}\n inTangent: {item.inTangent}\n outTangent: {item.outTangent}\n inweight: {item.inWeight}\n outweight: {item.outWeight}\n weightedMode: {item.weightedMode}\n tangentMode: {item.tangentMode}");
        }

    }


    [ContextMenu("TestCurve")]
    public void TestCurve()
    {
        keyframes = new NativeList<Keyframe>(Allocator.Persistent);
        keyframes.CopyFrom(this.curve.keys);

        Debug.Log($"<color=orange>-1f   {this.curve.Evaluate(-1f)}   </color><color=#33cccc>-1f   {UnitySrcAssist.AnimationCurveEvaluate(this.keyframes, -1f)}  </color>");
        Debug.Log($"<color=orange>0.0f  {this.curve.Evaluate(0.0f)}  </color><color=#33cccc>0.0f  {UnitySrcAssist.AnimationCurveEvaluate(this.keyframes, 0.0f)} </color>");
        Debug.Log($"<color=orange>0.1f  {this.curve.Evaluate(0.1f)}  </color><color=#33cccc>0.1f  {UnitySrcAssist.AnimationCurveEvaluate(this.keyframes, 0.1f)} </color>");
        Debug.Log($"<color=orange>0.2f  {this.curve.Evaluate(0.2f)}  </color><color=#33cccc>0.2f  {UnitySrcAssist.AnimationCurveEvaluate(this.keyframes, 0.2f)} </color>");
        Debug.Log($"<color=orange>0.3f  {this.curve.Evaluate(0.3f)}  </color><color=#33cccc>0.3f  {UnitySrcAssist.AnimationCurveEvaluate(this.keyframes, 0.3f)} </color>");
        Debug.Log($"<color=orange>0.4f  {this.curve.Evaluate(0.4f)}  </color><color=#33cccc>0.4f  {UnitySrcAssist.AnimationCurveEvaluate(this.keyframes, 0.4f)} </color>");
        Debug.Log($"<color=orange>0.5f  {this.curve.Evaluate(0.5f)}  </color><color=#33cccc>0.5f  {UnitySrcAssist.AnimationCurveEvaluate(this.keyframes, 0.5f)} </color>");
        Debug.Log($"<color=orange>0.6f  {this.curve.Evaluate(0.6f)}  </color><color=#33cccc>0.6f  {UnitySrcAssist.AnimationCurveEvaluate(this.keyframes, 0.6f)} </color>");
        Debug.Log($"<color=orange>0.7f  {this.curve.Evaluate(0.7f)}  </color><color=#33cccc>0.7f  {UnitySrcAssist.AnimationCurveEvaluate(this.keyframes, 0.7f)} </color>");
        Debug.Log($"<color=orange>0.8f  {this.curve.Evaluate(0.8f)}  </color><color=#33cccc>0.8f  {UnitySrcAssist.AnimationCurveEvaluate(this.keyframes, 0.8f)} </color>");
        Debug.Log($"<color=orange>0.9f  {this.curve.Evaluate(0.9f)}  </color><color=#33cccc>0.9f  {UnitySrcAssist.AnimationCurveEvaluate(this.keyframes, 0.9f)} </color>");
        Debug.Log($"<color=orange>1.0f  {this.curve.Evaluate(1.0f)}  </color><color=#33cccc>1.0f  {UnitySrcAssist.AnimationCurveEvaluate(this.keyframes, 1.0f)} </color>");
        Debug.Log($"<color=orange>2.0f  {this.curve.Evaluate(2.0f)}  </color><color=#33cccc>2.0f  {UnitySrcAssist.AnimationCurveEvaluate(this.keyframes, 2.0f)} </color>");

        Clear();
    }


    public void OnDestroy()
    {

    }


    public void Clear()
    {
        if (colorKeys.IsCreated)
            colorKeys.Dispose();
        if (alphaKeys.IsCreated)
            alphaKeys.Dispose();
        if (keyframes.IsCreated)
            keyframes.Dispose();
    }


}