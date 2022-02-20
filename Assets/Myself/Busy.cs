using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

public class Busy : MonoBehaviour
{
    public JobHandle jobHandle;
    public JobHandle jobHandle_Step2;
    public NativeArray<float> nArray;

    public bool isJob1Busy;
    public bool isJob2Busy;
    public Gradient gradient;
    public Gradient tgradient;

    public void Awake()
    {
        //nArray = new NativeArray<float>(2, Allocator.Persistent);

        //Debug.Log($"0.0f  {this.gradient.Evaluate(0f)}");
        //Debug.Log($"0.1f  {this.gradient.Evaluate(0.1f)}");
        //Debug.Log($"0.3f  {this.gradient.Evaluate(0.3f)}");
        //Debug.Log($"0.5f  {this.gradient.Evaluate(0.5f)}");
        //Debug.Log($"0.7f  {this.gradient.Evaluate(0.7f)}");
        //Debug.Log($"1.0f  {this.gradient.Evaluate(1.0f)}");

        for (int i = 0; i < this.gradient.colorKeys.Length; i++)
        {
            Debug.Log($"{i}  {this.gradient.colorKeys[i].color}");
        }



    }
    //private void Update()
    //{
    //    //float a = 0;
    //    //for (int i = 0; i < 1000000; i++)
    //    //{
    //    //    a = Mathf.Exp(i);
    //    //}
    //    //Debug.Log(a);

    //    //MyJob myjob = new MyJob();
    //    //myjob.nArray = this.nArray;
    //    //jobHandle = myjob.Schedule();

    //    //this.jobHandle.Complete();

    //    if (!isJob1Busy && this.jobHandle_Step2.IsCompleted)
    //    {
    //        Debug.Log("bbb");
    //        this.jobHandle_Step2.Complete();
    //        isJob2Busy = false;

    //        Debug.Log("Step2: " + nArray[0] + "     " + nArray[1]);
    //        MyJob myjob = new MyJob();
    //        myjob.nArray = this.nArray;
    //        isJob1Busy = true;
    //        jobHandle = myjob.Schedule();
    //    }

    //    if (!isJob2Busy && this.jobHandle.IsCompleted)
    //    {
    //        Debug.Log("aaa");
    //        this.jobHandle.Complete();
    //        isJob1Busy = false;

    //        Debug.Log("Step1: " + nArray[0] + "     " + nArray[1]);
    //        nArray[0] = -1f * nArray[0];
    //        nArray[1] = -1f * nArray[1];

    //        MyJob2 job2 = new MyJob2();
    //        job2.nArray = this.nArray;
    //        isJob2Busy = true;
    //        this.jobHandle_Step2 = job2.Schedule();


    //    }







    //}

    private void FixedUpdate()
    {
        //MyJob3 myJob3 = new MyJob3() { nArray = this.nArray };
        //myJob3.Schedule().Complete();
    }

    private void OnDestroy()
    {
        jobHandle.Complete();
        jobHandle_Step2.Complete();
        nArray.Dispose();
    }

    [ContextMenu("Show Detail data")]
    public void ShowData()
    {
        Debug.Log(this.transform.right);
    }

    public struct MyJob : IJob
    {
        public NativeArray<float> nArray;

        public void Execute()
        {
            float a = 0;
            for (int i = 0; i < 1000000; i++)
            {
                a = Mathf.Exp(i);
            }
            nArray[0] = -10f;
            nArray[1] = -10f;
        }
    }


    public struct MyJob2 : IJob
    {
        public NativeArray<float> nArray;

        public void Execute()
        {
            float a = 0;
            for (int i = 0; i < 1000000; i++)
            {
                a = Mathf.Exp(i);
            }
            nArray[0] = nArray[0] + 10f;
            nArray[1] = nArray[1] + 10f;
        }
    }

    public struct MyJob3 : IJob
    {
        public NativeArray<float> nArray;

        public void Execute()
        {
            float a = 0;
            for (int i = 0; i < 10000; i++)
            {
                a = Mathf.Exp(i);
            }
            nArray[0] = nArray[0] + 1;
            nArray[1] = nArray[0] + 1;
        }
    }

}