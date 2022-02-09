using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Jobs;
using UnityEngine;

namespace AraJob
{
    public class AraTrailJobManager : MonoBehaviour
    {
        private static AraTrailJobManager instance;
        public static AraTrailJobManager Instance => instance;

        private enum EChangeType
        {
            None,
            Add,
            Remove,
            RefreshWeight,
        }




        public List<AraTrailJob> AraTrailJobList;
        private List<AraTrailJob> mAraTrailJobList_Change;
        private List<EChangeType> mAraTrailJobList_ChangeType;

        private JobHandle mLateUpdateJobHandle;
        
        #region Unity Mono

        private void Awake()
        {
            instance = this;
            this.AraTrailJobList = new List<AraTrailJob>();
            this.mAraTrailJobList_Change = new List<AraTrailJob>();
            this.mAraTrailJobList_ChangeType = new List<EChangeType>();



        }

        private void Update()
        {
            if (this.AraTrailJobList == null || this.AraTrailJobList.Count == 0)
                return;

        }

        private void FixedUpdate()
        {
            if (this.AraTrailJobList == null || this.AraTrailJobList.Count == 0)
                return;


        }

        private void LateUpdate()
        {
            if (!this.mLateUpdateJobHandle.IsCompleted)
                return;
            this.mLateUpdateJobHandle.Complete();

            this.HandleChangeList();
            if (this.AraTrailJobList == null || this.AraTrailJobList.Count == 0)
                return;

            //Schedule job


            JobHandle.ScheduleBatchedJobs();
        }

        private void OnDestroy()
        {
            this.AraTrailJobList = null;
            this.mAraTrailJobList_Change = null;
            this.mAraTrailJobList_ChangeType = null;
        }

        public void OnValidate()
        {

        }


        #endregion


        /// <summary>
        /// 启动AraTrailJobManager
        /// </summary>
        public static void EnableAraTrailJobManager()
        {
            if (instance || !Application.isPlaying) return;
            var rGo = new GameObject("DynamicBoneJobManager");
            instance = rGo.AddComponent<AraTrailJobManager>();
            GameObject.DontDestroyOnLoad(rGo);
        }

        /// <summary>
        /// 添加AraTrailJob
        /// </summary>
        /// <param name="rAraTrailJob"></param>
        public void OnEnter(AraTrailJob rAraTrailJob)
        {
            this.mAraTrailJobList_Change.Add(rAraTrailJob);
            this.mAraTrailJobList_ChangeType.Add(EChangeType.Add);
        }

        /// <summary>
        /// 移除AraTrailJob
        /// </summary>
        /// <param name="rAraTrailJob"></param>
        public void OnExit(AraTrailJob rAraTrailJob)
        {
            this.mAraTrailJobList_Change.Add(rAraTrailJob);
            this.mAraTrailJobList_ChangeType.Add(EChangeType.Remove);
        }


        private void HandleChangeList()
        {


            this.mAraTrailJobList_Change.Clear();
            this.mAraTrailJobList_ChangeType.Clear();
        }

    }
}
