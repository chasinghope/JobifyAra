using AraJob;
using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace AraJob
{
    public class AraTrailJobMgr : MonoBehaviour
    {
        private static AraTrailJobMgr instance;
        public static AraTrailJobMgr Instance => instance;

        public const int DESIRED_JOB_SIZE = 16;

        [SerializeField]
        private List<AraTrailJob> mAraTrailJobList;
        private NativeArray<AraTrailJob.Head> mAraTrailHeadArray;

        //private JobHandle mAwakeJobHandle;
        private JobHandle mUpdateJobHandle;
        private JobHandle mFixedUpdateHandle;
        //private JobHandle mLateUpdateJobHandle;

        #region Unity Mono

        private void Awake()
        {
            instance = this;
            this.mAraTrailJobList = new List<AraTrailJob>();

            //if (!this.mAwakeJobHandle.IsCompleted)
            //    return;
            //this.mAwakeJobHandle.Complete();

            //if (this.mAraTrailJobList == null || this.mAraTrailJobList.Count == 0)
            //    return;

            ////Schedule job
            //this.HandleList();

            //WarmupJob warmupJob = new WarmupJob
            //{
            //    AraTrailArray = mAraTrailHeadArray
            //};

            //this.mAwakeJobHandle = warmupJob.Schedule(this.mAraTrailHeadArray.Length, DESIRED_JOB_SIZE, mAwakeJobHandle) ;

            //JobHandle.ScheduleBatchedJobs();


        }

        private void Update()
        {
            if (this.mAraTrailJobList == null || this.mAraTrailJobList.Count == 0)
                return;

            if (!this.mUpdateJobHandle.IsCompleted)
                return;
            this.mUpdateJobHandle.Complete();

            this.HandleList();

            UpdateJob warmupJob = new UpdateJob
            {
                AraTrailArray = mAraTrailHeadArray
            };

            this.mUpdateJobHandle = warmupJob.Schedule(this.mAraTrailHeadArray.Length, DESIRED_JOB_SIZE, mUpdateJobHandle);

            JobHandle.ScheduleBatchedJobs();
        }

        private void FixedUpdate()
        {
            if (this.mAraTrailJobList == null || this.mAraTrailJobList.Count == 0)
                return;

            if (!this.mFixedUpdateHandle.IsCompleted)
                return;
            this.mFixedUpdateHandle.Complete();

            this.HandleList();

            FixUpdateJob warmupJob = new FixUpdateJob
            {
                AraTrailArray = mAraTrailHeadArray
            };

            this.mFixedUpdateHandle = warmupJob.Schedule(this.mAraTrailHeadArray.Length, DESIRED_JOB_SIZE, mFixedUpdateHandle);

            JobHandle.ScheduleBatchedJobs();

        }

        //private void LateUpdate()
        //{
        //    if (this.mAraTrailJobList == null || this.mAraTrailJobList.Count == 0)
        //        return;

        //}

        private void OnDestroy()
        {
            this.mUpdateJobHandle.Complete();

            this.mAraTrailJobList = null;

            if (this.mAraTrailHeadArray.IsCreated)
                this.mAraTrailHeadArray.Dispose();
        }

        //public void OnValidate()
        //{

        //}


        #endregion


        /// <summary>
        /// 启动AraTrailJobManager
        /// </summary>
        public static void EnableAraTrailJobManager()
        {
            if (instance || !Application.isPlaying) return;
            var rGo = new GameObject("DynamicBoneJobManager");
            instance = rGo.AddComponent<AraTrailJobMgr>();
            GameObject.DontDestroyOnLoad(rGo);
        }

        /// <summary>
        /// 添加AraTrailJob
        /// </summary>
        /// <param name="rAraTrailJob"></param>
        public void OnEnter(AraTrailJob rAraTrailJob)
        {
            this.mAraTrailJobList.Add(rAraTrailJob);
        }

        /// <summary>
        /// 移除AraTrailJob
        /// </summary>
        /// <param name="rAraTrailJob"></param>
        public void OnExit(AraTrailJob rAraTrailJob)
        {
            this.mAraTrailJobList.Remove(rAraTrailJob);
        }



        private void HandleList()
        {
            mAraTrailHeadArray = new NativeArray<AraTrailJob.Head>(this.mAraTrailJobList.Count, Allocator.Temp);
            for (int i = 0; i < this.mAraTrailJobList.Count; i++)
            {
                //this.mAraTrailHeadArray[i] = new AraTrailJob.Head() { AraTrailJob = this.mAraTrailJobList[i] };
            }
        }



    }





    #region Jobify


    public struct WarmupJob : IJobParallelFor
    {
        public NativeArray<AraTrailJob.Head> AraTrailArray;
        
        public void Execute(int index)
        {
            //AraTrailJob araTrail = AraTrailArray[index].AraTrailJob;
            //araTrail.Warmup();
        }
    }

    public struct UpdateJob : IJobParallelFor
    {
        public NativeArray<AraTrailJob.Head> AraTrailArray;
        public void Execute(int index)
        {
            //AraTrailJob araTrail = AraTrailArray[index].AraTrailJob;
            //araTrail.FrameUpdate();
        }
    }

    public struct FixUpdateJob : IJobParallelFor
    {
        public NativeArray<AraTrailJob.Head> AraTrailArray;
        public void Execute(int index)
        {
            //AraTrailJob araTrail = AraTrailArray[index].AraTrailJob;
            //araTrail.TimeFixUpdate();
        }
    }




    #endregion


}