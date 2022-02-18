using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using Unity.Burst;
using UnityEngine.Jobs;
using System;

namespace AraJob
{
    public class AraTrailJobManager : MonoBehaviour
    {
        private static AraTrailJobManager mInstance;
        public static AraTrailJobManager Instance => mInstance;

        public const int HEAD_SIZE = 128;
        public const int POINT_CHUNK_SIZE = 64;
        public const int VERTICES_SIZE = 256;
        public const int TRIANGLE_MUL = 3;
        public const int GRADIENT_COUNT = 8;
        public const int KEYFRAME_COUNT = 16;

        private enum EChangeType
        {
            None,
            Add,
            Remove,
            RefreshWeight,
        }

        public static void CheckCreate()
        {
            if (mInstance || !Application.isPlaying) return;
            var rGo = new GameObject("AraTrailJobManager");
            mInstance = rGo.AddComponent<AraTrailJobManager>();
            GameObject.DontDestroyOnLoad(rGo);
        }

        private List<AraTrailJob_Frame> mAraJobList;
        //private NativeList<AraTrailJob_Frame.AraHead> mHeadList;

        private NativeList<AraTrailJob_Frame.Head> mHeadList;                 
        private NativeList<AraTrailJob_Frame.Point> mPoints;
        private NativeList<int> mDiscontinuities;
        private NativeList<Keyframe> mLengthCurve;
        private NativeList<GradientColorKey> mLengthGradientColor;
        private NativeList<GradientAlphaKey> mLengthGradientAlpha;
        private NativeList<GradientMode> mLengthGradientMode;
        private NativeList<Keyframe> mTimeCurve;
        private NativeList<GradientColorKey> mTimeGradientColor;
        private NativeList<GradientAlphaKey> mTimeGradientAlpha;
        private NativeList<GradientMode> mTimeGradientMode;

        private NativeList<Vector3> vertices;
        private NativeList<Vector4> tangents;
        private NativeList<Color> vertColors;
        private NativeList<Vector3> uvs;
        private NativeList<int> tris;
        private NativeList<Vector3> normals;


        private JobHandle mLateUpdateJobHandle;

        private void Awake()
        {
            mInstance = this;
            this.Initialize();
        }

        private void OnDestroy()
        {
            this.mLateUpdateJobHandle.Complete();

            //if (this.mHeadList.IsCreated)
            //{
            //    this.mHeadList.Dispose();
            //}
        }



        private void Initialize()
        {
            this.mHeadList = new NativeList<AraTrailJob_Frame.Head>(HEAD_SIZE, Allocator.Persistent);
            this.mPoints = new NativeList<AraTrailJob_Frame.Point>(HEAD_SIZE * POINT_CHUNK_SIZE, Allocator.Persistent);
            this.mDiscontinuities = new NativeList<int>(HEAD_SIZE * POINT_CHUNK_SIZE, Allocator.Persistent);

            this.mLengthCurve = new NativeList<Keyframe>(HEAD_SIZE * KEYFRAME_COUNT, Allocator.Persistent);
            this.mLengthGradientColor = new NativeList<GradientColorKey>(HEAD_SIZE * GRADIENT_COUNT, Allocator.Persistent);
            this.mLengthGradientAlpha = new NativeList<GradientAlphaKey>(HEAD_SIZE * GRADIENT_COUNT, Allocator.Persistent);
            this.mLengthGradientMode = new NativeList<GradientMode>(HEAD_SIZE, Allocator.Persistent);

            this.mTimeCurve = new NativeList<Keyframe>(HEAD_SIZE * KEYFRAME_COUNT, Allocator.Persistent);
            this.mTimeGradientColor = new NativeList<GradientColorKey>(HEAD_SIZE * GRADIENT_COUNT, Allocator.Persistent);
            this.mTimeGradientAlpha = new NativeList<GradientAlphaKey>(HEAD_SIZE * GRADIENT_COUNT, Allocator.Persistent);
            this.mTimeGradientMode = new NativeList<GradientMode>(HEAD_SIZE, Allocator.Persistent);

            //this.vertices = new NativeList<Vector3>(HEAD_SIZE * POINT_CHUNK_SIZE * );

            
        }

        public void OnEnter(AraTrailJob_Frame rAraTrailJob)
        {
            //this.mAraJobList.Add(rAraTrailJob);
            //this.mDynamicBoneChangeTypeList.Add(EChangeType.Add);
        }
        public void OnExit(AraTrailJob_Frame rAraTrailJob)
        {
            //this.mAraJobList.Add(rDynamicBone);
            //this.mDynamicBoneChangeTypeList.Add(EChangeType.Remove);
        }



        [ContextMenu("打印空间占用")]
        public void ShowMemoryCost()
        {
            int num = 0;
            num += HEAD_SIZE * POINT_CHUNK_SIZE;

            Debug.Log($"{num}");
        }
    }
}