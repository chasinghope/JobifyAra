﻿using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Jobs;
using static Unity.Mathematics.math;

public class DynamicBoneJobManager : MonoBehaviour
{
    private static DynamicBoneJobManager mInstance;
    public static DynamicBoneJobManager Instance
    {
        get
        {
            return mInstance;
        }
    }
    public static void CheckCreate()
    {
        if (mInstance || !Application.isPlaying) return;
        var rGo = new GameObject("DynamicBoneJobManager");
        mInstance = rGo.AddComponent<DynamicBoneJobManager>();
        GameObject.DontDestroyOnLoad(rGo);
    }
    private void Awake()
    {
        mInstance = this;
        this.Initialize();
    }
    private enum EChangeType
    {
        None,
        Add,
        Remove,
        RefreshWeight,
    }

    public const int HEAD_INITIALIZE_SIZE = 128;
    public const int SINGLE_CHUNK_SIZE = 16;
    public const int DESIRED_JOB_SIZE = 16;

    public List<DynamicBoneJob> mDynamicBoneList;
    private bool bIsInitalize = false;
    private List<DynamicBoneJob> mDynamicBoneChangeList;
    private List<EChangeType> mDynamicBoneChangeTypeList;

    private NativeList<DynamicBoneJob.Head> mHeadList;
    private TransformAccessArray mHeadTransformList;
    private TransformAccessArray mHeadRootTransformList;
    private TransformAccessArray mHeadReferenceObjectTransformList;
    private NativeList<DynamicBoneJob.Particle> mParticleList;
    private TransformAccessArray mParticleTransformList;
    private NativeList<DynamicBoneJob.Collider> mColliderList;
    private TransformAccessArray mColliderTransformList;

    private JobHandle mLateUpdateJobHandle;

    private void Initialize()
    {
        this.mDynamicBoneList = new List<DynamicBoneJob>();
        this.mDynamicBoneChangeList = new List<DynamicBoneJob>();
        this.mDynamicBoneChangeTypeList = new List<EChangeType>();

        this.mHeadList = new NativeList<DynamicBoneJob.Head>(HEAD_INITIALIZE_SIZE, Allocator.Persistent);
        this.mHeadTransformList = new TransformAccessArray(HEAD_INITIALIZE_SIZE);
        this.mHeadRootTransformList = new TransformAccessArray(HEAD_INITIALIZE_SIZE);
        this.mHeadReferenceObjectTransformList = new TransformAccessArray(HEAD_INITIALIZE_SIZE);
        this.mParticleList = new NativeList<DynamicBoneJob.Particle>(HEAD_INITIALIZE_SIZE * SINGLE_CHUNK_SIZE, Allocator.Persistent);
        this.mParticleTransformList = new TransformAccessArray(HEAD_INITIALIZE_SIZE * SINGLE_CHUNK_SIZE, DESIRED_JOB_SIZE);
        this.mColliderList = new NativeList<DynamicBoneJob.Collider>(HEAD_INITIALIZE_SIZE * SINGLE_CHUNK_SIZE, Allocator.Persistent);
        this.mColliderTransformList = new TransformAccessArray(HEAD_INITIALIZE_SIZE * SINGLE_CHUNK_SIZE, DESIRED_JOB_SIZE);

        this.bIsInitalize = true;
    }
    private void OnDestroy()
    {
        this.mLateUpdateJobHandle.Complete();
        this.bIsInitalize = false;
        // 还原现有的DynamicBone骨骼
        for (int i = 0; i < this.mDynamicBoneList.Count; i++)
        {
            this.mDynamicBoneList[i].InitTransforms();
        }
        this.mDynamicBoneList = null;
        this.mDynamicBoneChangeList = null;
        this.mDynamicBoneChangeTypeList = null;


        if (this.mHeadList.IsCreated)
        {
            this.mHeadList.Dispose();
        }
        if (this.mHeadTransformList.isCreated)
        {
            this.mHeadTransformList.Dispose();
        }
        if (this.mHeadRootTransformList.isCreated)
        {
            this.mHeadRootTransformList.Dispose();
        }
        if (this.mHeadReferenceObjectTransformList.isCreated)
        {
            this.mHeadReferenceObjectTransformList.Dispose();
        }
        if (this.mParticleList.IsCreated)
        {
            this.mParticleList.Dispose();
        }
        if (this.mParticleTransformList.isCreated)
        {
            this.mParticleTransformList.Dispose();
        }
        if (this.mColliderList.IsCreated)
        {
            this.mColliderList.Dispose();
        }
        if (this.mColliderTransformList.isCreated)
        {
            this.mColliderTransformList.Dispose();
        }
    }
    public void OnEnter(DynamicBoneJob rDynamicBone)
    {
        if (!this.bIsInitalize) return;
        this.mDynamicBoneChangeList.Add(rDynamicBone);
        this.mDynamicBoneChangeTypeList.Add(EChangeType.Add);
    }
    public void OnExit(DynamicBoneJob rDynamicBone)
    {
        if (!this.bIsInitalize) return;
        this.mDynamicBoneChangeList.Add(rDynamicBone);
        this.mDynamicBoneChangeTypeList.Add(EChangeType.Remove);
    }
    public void RefreshWeight(DynamicBoneJob rDynamicBone)
    {
        if (!this.bIsInitalize) return;
        this.mDynamicBoneChangeList.Add(rDynamicBone);
        this.mDynamicBoneChangeTypeList.Add(EChangeType.RefreshWeight);
    }
    public bool Exists(DynamicBoneJob rTarget)
    {
        if (!this.bIsInitalize) return false;
        return this.mDynamicBoneList.Contains(rTarget);
    }
    private void HandleChangeList()
    {
        for (int i = 0; i < this.mDynamicBoneChangeList.Count; i++)
        {
            var rDynamicBone = this.mDynamicBoneChangeList[i];
            var rChangeType = this.mDynamicBoneChangeTypeList[i];
            var nIndex = this.mDynamicBoneList.IndexOf(rDynamicBone);
            if (rChangeType == EChangeType.Add)
            {
                if (nIndex != -1)
                {
                    Debug.LogError($"DynamicBone already existed. DynamicBone:{rDynamicBone.name}");
                    continue;
                }
                nIndex = this.mDynamicBoneList.Count;
                rDynamicBone.ResetParticlesPosition();
                this.mDynamicBoneList.Add(rDynamicBone);
                if (rDynamicBone.m_Particles.Count > SINGLE_CHUNK_SIZE)
                {
                    Debug.LogWarning($"动态骨骼子节点层次太深，超过了{SINGLE_CHUNK_SIZE}个，超过的将被忽略。 DynamicBoneRootName:{rDynamicBone.transform.root.name} Name:{rDynamicBone.transform.name}");
                }
                if (rDynamicBone.m_Colliders.Count > SINGLE_CHUNK_SIZE)
                {
                    Debug.LogWarning($"动态骨骼碰撞器太多，超过了{SINGLE_CHUNK_SIZE}个，超过的将被忽略。 DynamicBoneRootName:{rDynamicBone.transform.root.name} Name:{rDynamicBone.transform.name}");
                }
                // 设置Head信息
                var rHead = new DynamicBoneJob.Head
                {
                    m_ParticleIndex = nIndex * SINGLE_CHUNK_SIZE,
                    m_ParticleLength = math.min(SINGLE_CHUNK_SIZE, rDynamicBone.m_Particles.Count),

                    m_ColliderIndex = nIndex * SINGLE_CHUNK_SIZE,
                    m_ColliderLength = math.min(SINGLE_CHUNK_SIZE, rDynamicBone.m_Colliders.Count),

                    m_UpdateRate = rDynamicBone.m_UpdateRate,
                    m_LocalGravity = rDynamicBone.m_LocalGravity,
                    m_ObjectMove = rDynamicBone.m_ObjectMove,
                    m_ObjectPrevPosition = rDynamicBone.m_ObjectPrevPosition,
                    m_BoneTotalLength = rDynamicBone.m_BoneTotalLength,
                    m_ObjectScale = rDynamicBone.m_ObjectScale,
                    m_Time = rDynamicBone.m_Time,
                    m_Weight = rDynamicBone.m_Weight,
                    m_DistantDisable = rDynamicBone.m_DistantDisable,
                    m_DistantDisabled = rDynamicBone.m_DistantDisabled,
                    m_DistanceToObject = rDynamicBone.m_DistanceToObject,
                    m_ReferenceObjectIsNull = !rDynamicBone.m_ReferenceObject,
                    m_Gravity = rDynamicBone.m_Gravity,
                    m_Force = rDynamicBone.m_Force,
                    m_FreezeAxis = rDynamicBone.m_FreezeAxis,
                    m_InitTransformInFrame = rDynamicBone.m_InitTransformInFrame,
                };

                this.mHeadList.Add(rHead);
                this.mHeadTransformList.Add(rDynamicBone.transform);
                this.mHeadRootTransformList.Add(rDynamicBone.m_Root);
                this.mHeadReferenceObjectTransformList.Add(rDynamicBone.m_ReferenceObject);
                for (int j = 0; j < rHead.m_ParticleLength; j++)
                {
                    this.mParticleList.Add(rDynamicBone.m_Particles[j]);
                    this.mParticleTransformList.Add(rDynamicBone.m_ParticleTransforms[j]);
                }
                for (int j = 0; j < rHead.m_ColliderLength; j++)
                {
                    var rColliderMB = rDynamicBone.m_Colliders[j];
                    if (rColliderMB)
                    {
                        this.mColliderList.Add(new DynamicBoneJob.Collider()
                        {
                            m_Enabled = true,
                            m_Direction = rColliderMB.m_Direction,
                            m_Center = rColliderMB.m_Center,
                            m_Bound = rColliderMB.m_Bound,
                            m_Radius = rColliderMB.m_Radius,
                            m_Height = rColliderMB.m_Height,
                        });
                        this.mColliderTransformList.Add(rColliderMB.transform);
                    }
                    else
                    {
                        this.mColliderList.Add(new DynamicBoneJob.Collider());
                        this.mColliderTransformList.Add(null);
                    }
                }
                // 对齐 ParticleList TransformAccessArray
                for (int j = rHead.m_ParticleLength; j < SINGLE_CHUNK_SIZE; j++)
                {
                    this.mParticleList.Add(new DynamicBoneJob.Particle());
                    this.mParticleTransformList.Add(null);
                }
                // 对齐 ColliderList
                for (int j = rHead.m_ColliderLength; j < SINGLE_CHUNK_SIZE; j++)
                {
                    this.mColliderList.Add(new DynamicBoneJob.Collider());
                    this.mColliderTransformList.Add(null);
                }
                Debug.Assert(this.mHeadList.Length == this.mDynamicBoneList.Count);
                Debug.Assert(this.mHeadRootTransformList.length == this.mDynamicBoneList.Count);
                Debug.Assert(this.mHeadTransformList.length == this.mDynamicBoneList.Count);
                Debug.Assert(this.mParticleList.Length == this.mDynamicBoneList.Count * SINGLE_CHUNK_SIZE);
                Debug.Assert(this.mParticleTransformList.length == this.mDynamicBoneList.Count * SINGLE_CHUNK_SIZE);
                Debug.Assert(this.mColliderList.Length == this.mDynamicBoneList.Count * SINGLE_CHUNK_SIZE);
                Debug.Assert(this.mColliderTransformList.length == this.mDynamicBoneList.Count * SINGLE_CHUNK_SIZE);
            }
            else if (rChangeType == EChangeType.Remove)
            {
                if (nIndex == -1)
                {
                    Debug.LogError($"DynamicBone dose not existed. DynamicBone:{rDynamicBone.name}");
                    continue;
                }
                rDynamicBone.InitTransforms();
                var nSwapBackIndex = this.mDynamicBoneList.Count - 1;
                this.mDynamicBoneList[nIndex] = this.mDynamicBoneList[nSwapBackIndex];
                this.mDynamicBoneList.RemoveAt(nSwapBackIndex);
                // 修改索引
                var rHead = this.mHeadList[nSwapBackIndex];
                rHead.m_ParticleIndex = nIndex * SINGLE_CHUNK_SIZE;
                rHead.m_ColliderIndex = nIndex * SINGLE_CHUNK_SIZE;
                this.mHeadList[nSwapBackIndex] = rHead;

                this.mHeadList.RemoveAtSwapBack(nIndex);
                this.mHeadTransformList.RemoveAtSwapBack(nIndex);
                this.mHeadRootTransformList.RemoveAtSwapBack(nIndex);
                this.mHeadReferenceObjectTransformList.RemoveAtSwapBack(nIndex);
                for (int j = (nIndex + 1) * SINGLE_CHUNK_SIZE - 1; j >= nIndex * SINGLE_CHUNK_SIZE; j--)
                {
                    this.mParticleList.RemoveAtSwapBack(j);
                    this.mParticleTransformList.RemoveAtSwapBack(j);
                    this.mColliderList.RemoveAtSwapBack(j);
                    this.mColliderTransformList.RemoveAtSwapBack(j);
                }
                Debug.Assert(this.mHeadList.Length == this.mDynamicBoneList.Count);
                Debug.Assert(this.mHeadRootTransformList.length == this.mDynamicBoneList.Count);
                Debug.Assert(this.mHeadTransformList.length == this.mDynamicBoneList.Count);
                Debug.Assert(this.mParticleList.Length == this.mDynamicBoneList.Count * SINGLE_CHUNK_SIZE);
                Debug.Assert(this.mParticleTransformList.length == this.mDynamicBoneList.Count * SINGLE_CHUNK_SIZE);
                Debug.Assert(this.mColliderList.Length == this.mDynamicBoneList.Count * SINGLE_CHUNK_SIZE);
                Debug.Assert(this.mColliderTransformList.length == this.mDynamicBoneList.Count * SINGLE_CHUNK_SIZE);
            }
            else if (rChangeType == EChangeType.RefreshWeight)
            {
                if (nIndex == -1)
                {
                    Debug.LogError($"DynamicBone dose not existed. DynamicBone:{rDynamicBone.name}");
                    continue;
                }
                var rHead = this.mHeadList[nIndex];
                rHead.m_Weight = rDynamicBone.m_Weight;
                this.mHeadList[nIndex] = rHead;
            }
        }
        this.mDynamicBoneChangeList.Clear();
        this.mDynamicBoneChangeTypeList.Clear();
    }
    private void OnDrawGizmosSelected()
    {
        if (!this.enabled || this.mDynamicBoneList == null) return;
        for (int i = 0; i < this.mDynamicBoneList.Count; i++)
        {
            Gizmos.color = Color.white;
            // 绘制DynamicBoneJob
            var rHead = this.mHeadList[i];
            for (int j = 0; j < rHead.m_ParticleLength; j++)
            {
                var p = this.mParticleList[rHead.m_ParticleIndex + j];
                if (p.m_ParentIndex >= 0)
                {
                    var p0 = this.mParticleList[rHead.m_ParticleIndex + p.m_ParentIndex];
                    Gizmos.DrawLine(p.m_Position, p0.m_Position);
                }
                if (p.m_Radius > 0)
                    Gizmos.DrawWireSphere(p.m_Position, p.m_Radius * rHead.m_ObjectScale);
            }
            // 绘制DynamicBoneColliderJob
            for (int j = 0; j < rHead.m_ColliderLength; j++)
            {
                var c = this.mColliderList[rHead.m_ColliderIndex + j];
                if (c.m_Bound == DynamicBoneColliderJob.Bound.Outside)
                {
                    Gizmos.color = Color.yellow;
                }
                else
                {
                    Gizmos.color = Color.magenta;
                }

                float radius = c.m_Radius * abs(c.m_WorldScale.x);
                float h = c.m_Height * 0.5f - c.m_Radius;
                if (h <= 0)
                {
                    Gizmos.DrawWireSphere(mul(c.m_LocalToWorldMatrix, float4(c.m_Center, 1)).xyz, radius);
                }
                else
                {
                    Vector3 c0 = c.m_Center;
                    Vector3 c1 = c.m_Center;

                    switch (c.m_Direction)
                    {
                        case DynamicBoneColliderJob.Direction.X:
                            c0.x -= h;
                            c1.x += h;
                            break;
                        case DynamicBoneColliderJob.Direction.Y:
                            c0.y -= h;
                            c1.y += h;
                            break;
                        case DynamicBoneColliderJob.Direction.Z:
                            c0.z -= h;
                            c1.z += h;
                            break;
                    }
                    Gizmos.DrawWireSphere(mul(c.m_LocalToWorldMatrix, float4(c0, 1)).xyz, radius);
                    Gizmos.DrawWireSphere(mul(c.m_LocalToWorldMatrix, float4(c1, 1)).xyz, radius);
                }
            }
        }
    }
    private void Update()
    {
        if (this.mDynamicBoneList == null || this.mDynamicBoneList.Count == 0)
        {
            return;
        }
        for (int i = 0; i < this.mDynamicBoneList.Count; i++)
        {
            var rDynamicBone = this.mDynamicBoneList[i];
            if (rDynamicBone.m_UpdateMode != DynamicBoneJob.UpdateMode.AnimatePhysics)
            {
                rDynamicBone.InitTransforms();
            }
        }
    }
    private void FixedUpdate()
    {
        if (this.mDynamicBoneList == null || this.mDynamicBoneList.Count == 0)
        {
            return;
        }
        for (int i = 0; i < this.mDynamicBoneList.Count; i++)
        {
            var rDynamicBone = this.mDynamicBoneList[i];
            if (rDynamicBone.m_UpdateMode == DynamicBoneJob.UpdateMode.AnimatePhysics)
            {
                rDynamicBone.InitTransforms();
            }
        }
    }
    private void LateUpdate()
    {
        if (!this.mLateUpdateJobHandle.IsCompleted)
        {
            return;
        }
        this.mLateUpdateJobHandle.Complete();

        this.HandleChangeList();
        if (this.mDynamicBoneList == null || this.mDynamicBoneList.Count == 0)
        {
            return;
        }

        var nHeadListCount = this.mDynamicBoneList.Count;
        var nParticleListCount = this.mDynamicBoneList.Count * SINGLE_CHUNK_SIZE;

        var rPrepareHeadRootTransformJob = new PrepareHeadRootTransformJob()
        {
            HeadList = this.mHeadList,
        };
        this.mLateUpdateJobHandle = rPrepareHeadRootTransformJob.Schedule(this.mHeadRootTransformList);

        var rPrepareHeadTransformJob = new PrepareHeadTransformJob()
        {
            HeadList = this.mHeadList,
            DeltaTime = Time.deltaTime,
        };
        this.mLateUpdateJobHandle = rPrepareHeadTransformJob.Schedule(this.mHeadTransformList, this.mLateUpdateJobHandle);

        var rPrepareHeadReferenceObjectTransformJob = new PrepareHeadReferenceObjectTransformJob()
        {
            HeadList = this.mHeadList,
        };
        this.mLateUpdateJobHandle = rPrepareHeadReferenceObjectTransformJob.Schedule(this.mHeadReferenceObjectTransformList, this.mLateUpdateJobHandle);

        var rPrepareParticleTransformJob = new PrepareParticleTransformJob()
        {
            HeadList = this.mHeadList,
            ParticleList = this.mParticleList,
        };
        this.mLateUpdateJobHandle = rPrepareParticleTransformJob.Schedule(this.mParticleTransformList, this.mLateUpdateJobHandle);

        var rCheckDistanceJob = new CheckDistanceJob()
        {
            HeadList = this.mHeadList,
            ParticleList = this.mParticleList,
            CameraIsNull = Camera.main == null,
            CameraPosition = Camera.main != null ? (float3)Camera.main.transform.position : Unity.Mathematics.float3.zero,
        };
        this.mLateUpdateJobHandle = rCheckDistanceJob.Schedule(this.mHeadTransformList, this.mLateUpdateJobHandle);

        var rPrepareColliderTrasnformJob = new PrepareColliderTrasnformJob()
        {
            HeadList = this.mHeadList,
            ColliderList = this.mColliderList,
        };
        this.mLateUpdateJobHandle = rPrepareColliderTrasnformJob.Schedule(this.mColliderTransformList, this.mLateUpdateJobHandle);

        var rPrepareParticleJob = new PrepareParticleJob()
        {
            HeadList = this.mHeadList,
            ParticleList = this.mParticleList,
        };
        this.mLateUpdateJobHandle = rPrepareParticleJob.Schedule(nParticleListCount, DESIRED_JOB_SIZE, this.mLateUpdateJobHandle);

        for (int i = 0; i < 3; i++)
        {
            var rUpdateParticles1Job = new UpdateParticles1Job()
            {
                HeadList = this.mHeadList,
                ParticleList = this.mParticleList,
            };
            this.mLateUpdateJobHandle = rUpdateParticles1Job.Schedule(nParticleListCount, DESIRED_JOB_SIZE, this.mLateUpdateJobHandle);

            var rUpdateParticles2Job = new UpdateParticles2Job()
            {
                HeadList = this.mHeadList,
                ParticleList = this.mParticleList,
                ColliderList = this.mColliderList,
            };
            this.mLateUpdateJobHandle = rUpdateParticles2Job.Schedule(nParticleListCount, DESIRED_JOB_SIZE, this.mLateUpdateJobHandle);
        }

        var rSkipUpdateParticlesJob = new SkipUpdateParticlesJob()
        {
            HeadList = this.mHeadList,
            ParticleList = this.mParticleList,
        };
        this.mLateUpdateJobHandle = rSkipUpdateParticlesJob.Schedule(nParticleListCount, DESIRED_JOB_SIZE, this.mLateUpdateJobHandle);

        var rCalcParticlesTransformsJob = new CalcParticlesTransformsJob()
        {
            HeadList = this.mHeadList,
            ParticleList = this.mParticleList,
        };
        this.mLateUpdateJobHandle = rCalcParticlesTransformsJob.Schedule(nParticleListCount, DESIRED_JOB_SIZE, this.mLateUpdateJobHandle);

        var rApplyParticlesToTransformsJob = new ApplyParticlesToTransformsJob()
        {
            HeadList = this.mHeadList,
            ParticleList = this.mParticleList,
        };
        this.mLateUpdateJobHandle = rApplyParticlesToTransformsJob.Schedule(this.mParticleTransformList, this.mLateUpdateJobHandle);
        
        JobHandle.ScheduleBatchedJobs();
    }

    [BurstCompile]
    private struct PrepareHeadRootTransformJob : IJobParallelForTransform
    {
        public NativeArray<DynamicBoneJob.Head> HeadList;
        public void Execute(int nIndex, TransformAccess rTransform)
        {
            var rHead = this.HeadList[nIndex];

            rHead.m_RootLocalScale = rTransform.localScale;
            rHead.m_RootWorldPosition = rTransform.position;
            rHead.m_RootWorldRotation = rTransform.rotation;
            rHead.m_RootWorldScale = rTransform.localToWorldMatrix.lossyScale;

            this.HeadList[nIndex] = rHead;
        }
    }
    [BurstCompile]
    private struct PrepareHeadTransformJob : IJobParallelForTransform
    {
        public NativeArray<DynamicBoneJob.Head> HeadList;
        [ReadOnly]
        public float DeltaTime;
        public void Execute(int nIndex, TransformAccess rTransform)
        {
            var rHead = this.HeadList[nIndex];

            rHead.m_WorldPosition = rTransform.position;
            rHead.m_WorldScale = rTransform.localToWorldMatrix.lossyScale;

            rHead.m_ObjectScale = abs(rHead.m_WorldScale.x);
            rHead.m_ObjectMove = rHead.m_WorldPosition - rHead.m_ObjectPrevPosition;
            rHead.m_ObjectPrevPosition = rHead.m_WorldPosition;

            // calc force
            var force = rHead.m_Gravity;
            var fdir = normalizesafe(rHead.m_Gravity);

            var rf = rotate(rHead.m_RootWorldRotation, rHead.m_LocalGravity);
            var pf = fdir * max(dot(rf, fdir), 0);
            force -= pf;
            force = (force + rHead.m_Force) * rHead.m_ObjectScale;

            rHead.m_PreForce = force;

            rHead.m_Loop = 1;
            if (rHead.m_UpdateRate > 0)
            {
                float rDeltaTime = rcp(rHead.m_UpdateRate);
                rHead.m_Time += this.DeltaTime;
                rHead.m_Loop = 0;
                while (rHead.m_Time >= rDeltaTime)
                {
                    rHead.m_Time -= rDeltaTime;
                    if (++rHead.m_Loop >= 3)
                    {
                        rHead.m_Time = 0;
                        break;
                    }
                }
            }
            this.HeadList[nIndex] = rHead;
        }
    }
    [BurstCompile]
    private struct PrepareHeadReferenceObjectTransformJob : IJobParallelForTransform
    {
        public NativeArray<DynamicBoneJob.Head> HeadList;
        public void Execute(int nIndex, TransformAccess rTransform)
        {
            var rHead = this.HeadList[nIndex];

            rHead.m_CheckDistancePosition = rTransform.position;

            this.HeadList[nIndex] = rHead;
        }
    }
    [BurstCompile]
    private struct PrepareParticleTransformJob : IJobParallelForTransform
    {
        [ReadOnly]
        public NativeArray<DynamicBoneJob.Head> HeadList;
        public NativeArray<DynamicBoneJob.Particle> ParticleList;
        public void Execute(int nIndex, TransformAccess rTransform)
        {
            var rHead = this.HeadList[nIndex / SINGLE_CHUNK_SIZE];
            if (rHead.m_Weight <= 0) return;
            var p = this.ParticleList[nIndex];
            if (!p.m_TransformIsNull)
            {
                // 还原坐标
                //if (rHead.m_InitTransformInFrame)
                //{
                //    rTransform.localPosition = p.m_InitLocalPosition;
                //    rTransform.localRotation = p.m_InitLocalRotation;
                //}

                p.m_WorldPosition = rTransform.position;
                //p.m_WorldRotation = rTransform.rotation;
                p.m_WorldScale = rTransform.localToWorldMatrix.lossyScale;
                p.m_LocalPosition = rTransform.localPosition;
                p.m_LocalRotation = rTransform.localRotation;
                p.m_LocalToWorldMatrix = rTransform.localToWorldMatrix;
                p.m_LocalScale = rTransform.localScale;
                p.m_Rotation = rTransform.rotation;
            }

            this.ParticleList[nIndex] = p;
        }
    }
    [BurstCompile]
    private struct CheckDistanceJob : IJobParallelForTransform
    {
        public NativeArray<DynamicBoneJob.Head> HeadList;
        public NativeArray<DynamicBoneJob.Particle> ParticleList;
        [ReadOnly]
        public bool CameraIsNull;
        [ReadOnly]
        public float3 CameraPosition;
        public void Execute(int nIndex, TransformAccess rTransform)
        {
            var rHead = this.HeadList[nIndex];
            if (!rHead.m_DistantDisable) return;
            if (rHead.m_ReferenceObjectIsNull && this.CameraIsNull) return;
            var pos = rHead.m_CheckDistancePosition;
            if (rHead.m_ReferenceObjectIsNull && !this.CameraIsNull)
            {
                pos = this.CameraPosition;
            }
            var d = lengthsq(pos - rHead.m_WorldPosition);
            bool disable = d > rHead.m_DistanceToObject * rHead.m_DistanceToObject;
            if (disable != rHead.m_DistantDisabled)
            {
                rHead.m_DistantDisabled = disable;
                this.HeadList[nIndex] = rHead;
            }
        }
    }
    [BurstCompile]
    private struct PrepareColliderTrasnformJob : IJobParallelForTransform
    {
        [ReadOnly]
        public NativeArray<DynamicBoneJob.Head> HeadList;
        public NativeArray<DynamicBoneJob.Collider> ColliderList;
        public void Execute(int nIndex, TransformAccess rTrasnform)
        {
            var rHead = this.HeadList[nIndex / SINGLE_CHUNK_SIZE];
            if (rHead.m_Weight <= 0 || (rHead.m_DistantDisable && rHead.m_DistantDisabled)) return;
            var c = this.ColliderList[nIndex];
            c.m_WorldScale = rTrasnform.localToWorldMatrix.lossyScale;
            c.m_LocalToWorldMatrix = rTrasnform.localToWorldMatrix;
            this.ColliderList[nIndex] = c;
        }
    }
    [BurstCompile]
    private struct PrepareParticleJob : IJobParallelFor
    {
        [ReadOnly]
        public NativeArray<DynamicBoneJob.Head> HeadList;
        public NativeArray<DynamicBoneJob.Particle> ParticleList;
        public void Execute(int nIndex)
        {
            var rHead = this.HeadList[nIndex / SINGLE_CHUNK_SIZE];
            if (rHead.m_Weight <= 0) return;
            if (nIndex % SINGLE_CHUNK_SIZE >= rHead.m_ParticleLength)
            {
                return;
            }

            var p = this.ParticleList[nIndex];

            p.m_UpdateParticles1LoopCount = 0;
            p.m_UpdateParticles2LoopCount = 0;

            if (rHead.m_DistantDisabled != p.m_DistantDisabled)
            {
                if (!rHead.m_DistantDisabled)
                {
                    if (!p.m_TransformIsNull)
                    {
                        p.m_Position = p.m_PrevPosition = p.m_WorldPosition;
                    }
                    else // end bone
                    {
                        var p0 = this.ParticleList[rHead.m_ParticleIndex + p.m_ParentIndex];
                        p.m_Position = p.m_PrevPosition = mul(p0.m_LocalToWorldMatrix, float4(p0.m_EndOffset, 1)).xyz;
                    }
                }
                p.m_DistantDisabled = rHead.m_DistantDisabled;
            }

            this.ParticleList[nIndex] = p;
        }
    }
    [BurstCompile]
    private struct UpdateParticles1Job : IJobParallelFor
    {
        [ReadOnly]
        public NativeArray<DynamicBoneJob.Head> HeadList;
        public NativeArray<DynamicBoneJob.Particle> ParticleList;
        public void Execute(int nIndex)
        {
            var rHead = this.HeadList[nIndex / SINGLE_CHUNK_SIZE];
            if (rHead.m_Weight <= 0 || (rHead.m_DistantDisable && rHead.m_DistantDisabled)) return;
            if (nIndex % SINGLE_CHUNK_SIZE >= rHead.m_ParticleLength)
            {
                return;
            }

            if (rHead.m_Loop == 0) return;

            var p = this.ParticleList[nIndex];
            if (p.m_UpdateParticles1LoopCount >= rHead.m_Loop) return;
            p.m_UpdateParticles1LoopCount++;

            // UpdateParticles1
            var objectMove = rHead.m_ObjectMove;
            if (p.m_UpdateParticles1LoopCount > 1)
            {
                objectMove = float3(0);
            }
            if (p.m_ParentIndex >= 0)
            {
                var v = p.m_Position - p.m_PrevPosition;
                var rmove = objectMove * p.m_Inert;
                p.m_PrevPosition = p.m_Position + rmove;
                p.m_Position += v * (1 - p.m_Damping) + rHead.m_PreForce + rmove;
            }
            else
            {
                p.m_PrevPosition = p.m_Position;
                p.m_Position = p.m_WorldPosition;
            }

            this.ParticleList[nIndex] = p;
        }
    }
    [BurstCompile]
    private struct UpdateParticles2Job : IJobParallelFor
    {
        [ReadOnly]
        public NativeArray<DynamicBoneJob.Head> HeadList;
        public NativeArray<DynamicBoneJob.Particle> ParticleList;
        [ReadOnly]
        public NativeArray<DynamicBoneJob.Collider> ColliderList;
        public void Execute(int nIndex)
        {
            var rHead = this.HeadList[nIndex / SINGLE_CHUNK_SIZE];
            if (rHead.m_Weight <= 0 || (rHead.m_DistantDisable && rHead.m_DistantDisabled)) return;
            var nCheckIndex = nIndex % SINGLE_CHUNK_SIZE;
            if (nCheckIndex == 0 || nCheckIndex >= rHead.m_ParticleLength)
            {
                return;
            }

            if (rHead.m_Loop == 0) return;

            var p = this.ParticleList[nIndex];
            if (p.m_UpdateParticles2LoopCount >= rHead.m_Loop) return;
            p.m_UpdateParticles2LoopCount++;

            // UpdateParticles2
            var p0 = this.ParticleList[rHead.m_ParticleIndex + p.m_ParentIndex];
            float restLen;
            if (!p.m_TransformIsNull)
            {
                restLen = distance(p0.m_WorldPosition, p.m_WorldPosition);
            }
            else
            {
                restLen = length(mul(p0.m_LocalToWorldMatrix, float4(p.m_EndOffset, 0)).xyz);
            }
            // keep shape
            var stiffness = lerp(1.0f, p.m_Stiffness, rHead.m_Weight);
            if (stiffness > 0 || p.m_Elasticity > 0)
            {
                var m0 = p0.m_LocalToWorldMatrix;
                m0.c3 = float4(p0.m_Position, 0);

                float3 restPos;
                if (!p.m_TransformIsNull)
                {
                    restPos = mul(m0, float4(p.m_LocalPosition, 1)).xyz;
                }
                else
                {
                    restPos = mul(m0, float4(p.m_EndOffset, 1)).xyz;
                }
                var d = restPos - p.m_Position;
                p.m_Position += d * p.m_Elasticity;

                if (stiffness > 0)
                {
                    d = restPos - p.m_Position;
                    var len = length(d);
                    var maxLen = restLen * (1 - stiffness) * 2;
                    if (len > maxLen)
                    {
                        p.m_Position += d * ((len - maxLen) / len);
                    }
                }
                // collide
                if (rHead.m_ColliderLength > 0)
                {
                    var particleRadius = p.m_Radius * rHead.m_ObjectScale;
                    for (int i = 0; i < rHead.m_ColliderLength; i++)
                    {
                        var collider = this.ColliderList[rHead.m_ColliderIndex + i];
                        if (collider.m_Enabled)
                        {
                            p.m_Position = ColliderHelper.Collide(collider, p.m_Position, particleRadius);
                        }
                    }
                }

                // freeze axis, project to plane
                if (rHead.m_FreezeAxis != DynamicBoneJob.FreezeAxis.None)
                {
                    var planeNormal = Unity.Mathematics.float3.zero;
                    var planeDistance = 0f;
                    if (rHead.m_FreezeAxis == DynamicBoneJob.FreezeAxis.X)
                    {
                        var inNormal = mul(p0.m_Rotation, right());
                        planeNormal = normalize(inNormal);
                        planeDistance = -dot(inNormal, p0.m_Position);
                    }
                    else if (rHead.m_FreezeAxis == DynamicBoneJob.FreezeAxis.Y)
                    {
                        var inNormal = mul(p0.m_Rotation, up());
                        planeNormal = normalize(inNormal);
                        planeDistance = -dot(inNormal, p0.m_Position);
                    }
                    else if (rHead.m_FreezeAxis == DynamicBoneJob.FreezeAxis.Z)
                    {
                        var inNormal = mul(p0.m_Rotation, forward());
                        planeNormal = normalize(inNormal);
                        planeDistance = -dot(inNormal, p0.m_Position);
                    }
                    p.m_Position -= planeNormal * (dot(planeNormal, p.m_Position) + planeDistance);
                }

                // keep length
                var dd = p0.m_Position - p.m_Position;
                var leng = length(dd);
                if (leng > 0)
                {
                    p.m_Position += dd * ((leng - restLen) / leng);
                }
            }
            this.ParticleList[nIndex] = p;
        }
    }
    [BurstCompile]
    private struct SkipUpdateParticlesJob : IJobParallelFor
    {
        [ReadOnly]
        public NativeArray<DynamicBoneJob.Head> HeadList;
        public NativeArray<DynamicBoneJob.Particle> ParticleList;
        public void Execute(int nIndex)
        {
            var rHead = this.HeadList[nIndex / SINGLE_CHUNK_SIZE];
            if (rHead.m_Weight <= 0 || (rHead.m_DistantDisable && rHead.m_DistantDisabled)) return;
            if (nIndex % SINGLE_CHUNK_SIZE >= rHead.m_ParticleLength)
            {
                return;
            }

            if (rHead.m_Loop != 0) return;

            var p = this.ParticleList[nIndex];
            if (p.m_ParentIndex >= 0)
            {
                p.m_PrevPosition += rHead.m_ObjectMove;
                p.m_Position += rHead.m_ObjectMove;

                var p0 = this.ParticleList[rHead.m_ParticleIndex + p.m_ParentIndex];
                float restLen;
                if (!p.m_TransformIsNull)
                {
                    restLen = length(p0.m_WorldPosition - p.m_WorldPosition);
                }
                else
                {
                    restLen = length(mul(p0.m_LocalToWorldMatrix, float4(p.m_EndOffset, 0)).xyz);
                }
                // keep shape
                var stiffness = lerp(1.0f, p.m_Stiffness, rHead.m_Weight);
                if (stiffness > 0)
                {
                    var m0 = p0.m_LocalToWorldMatrix;
                    m0.c3 = float4(p0.m_Position, 0);
                    float3 restPos;
                    if (!p.m_TransformIsNull)
                    {
                        restPos = mul(m0, float4(p.m_LocalPosition, 1)).xyz;
                    }
                    else
                    {
                        restPos = mul(m0, float4(p.m_EndOffset, 1)).xyz;
                    }
                    var d = restPos - p.m_Position;
                    var len = length(d);
                    var maxLen = restLen * (1 - stiffness) * 2;
                    if (len > maxLen)
                    {
                        p.m_Position += d * ((len - maxLen) / len);
                    }
                }
                // keep length
                var dd = p0.m_Position - p.m_Position;
                var leng = length(dd);
                if (leng > 0)
                {
                    p.m_Position += dd * ((leng - restLen) / leng);
                }
            }
            else
            {
                p.m_PrevPosition = p.m_Position;
                p.m_Position = p.m_WorldPosition;
            }
            this.ParticleList[nIndex] = p;
        }
    }
    [BurstCompile]
    private struct CalcParticlesTransformsJob : IJobParallelFor
    {
        [ReadOnly]
        public NativeArray<DynamicBoneJob.Head> HeadList;
        public NativeArray<DynamicBoneJob.Particle> ParticleList;
        public void Execute(int nIndex)
        {
            var rHead = this.HeadList[nIndex / SINGLE_CHUNK_SIZE];
            if (rHead.m_Weight <= 0 || (rHead.m_DistantDisable && rHead.m_DistantDisabled)) return;
            var nCheckIndex = nIndex % SINGLE_CHUNK_SIZE;
            if (nCheckIndex == 0 || nCheckIndex >= rHead.m_ParticleLength)
            {
                return;
            }
            var p = this.ParticleList[nIndex];
            var p0 = this.ParticleList[rHead.m_ParticleIndex + p.m_ParentIndex];
            if (p0.m_ChildCount <= 1)
            {
                float3 v;
                if (!p.m_TransformIsNull)
                {
                    v = p.m_LocalPosition;
                }
                else
                {
                    v = p.m_EndOffset;
                }
                var v2 = p.m_Position - p0.m_Position;

                var v1 = rotate(p0.m_Rotation, normalizesafe(v));
                var rot = Quaternion.FromToRotation(v1, v2);

                p0.m_Rotation = mul(rot, p0.m_Rotation);
                p.m_Rotation = mul(rot, p.m_Rotation);
                this.ParticleList[rHead.m_ParticleIndex + p.m_ParentIndex] = p0;
                this.ParticleList[nIndex] = p;
            }
        }
    }
    [BurstCompile]
    private struct ApplyParticlesToTransformsJob : IJobParallelForTransform
    {
        [ReadOnly]
        public NativeArray<DynamicBoneJob.Head> HeadList;
        [ReadOnly]
        public NativeArray<DynamicBoneJob.Particle> ParticleList;
        public void Execute(int nIndex, TransformAccess rTransform)
        {
            var rHead = this.HeadList[nIndex / SINGLE_CHUNK_SIZE];
            if (rHead.m_Weight <= 0 || (rHead.m_DistantDisable && rHead.m_DistantDisabled)) return;
            var p = this.ParticleList[nIndex];

            rTransform.position = p.m_Position;
            rTransform.rotation = p.m_Rotation;
        }
    }

    private static class ColliderHelper
    {
        public static float3 Collide(DynamicBoneJob.Collider collider, float3 particlePosition, float particleRadius)
        {
            float3 resultPosition;
            var radius = collider.m_Radius * abs(collider.m_WorldScale.x);
            var h = collider.m_Height * 0.5f - collider.m_Radius;
            if (h <= 0)
            {
                if (collider.m_Bound == DynamicBoneColliderJob.Bound.Outside)
                {
                    resultPosition = OutsideSphere(particlePosition, particleRadius, mul(collider.m_LocalToWorldMatrix, float4(collider.m_Center, 1)).xyz, radius);
                }
                else
                {
                    resultPosition = InsideSphere(particlePosition, particleRadius, mul(collider.m_LocalToWorldMatrix, float4(collider.m_Center, 1)).xyz, radius);
                }
            }
            else
            {
                var c0 = collider.m_Center;
                var c1 = collider.m_Center;
                if (collider.m_Direction == DynamicBoneColliderJob.Direction.X)
                {
                    c0.x -= h;
                    c1.x += h;
                }
                else if (collider.m_Direction == DynamicBoneColliderJob.Direction.Y)
                {
                    c0.y -= h;
                    c1.y += h;
                }
                else if (collider.m_Direction == DynamicBoneColliderJob.Direction.Z)
                {
                    c0.z -= h;
                    c1.z += h;
                }
                if (collider.m_Bound == DynamicBoneColliderJob.Bound.Outside)
                {
                    c0 = mul(collider.m_LocalToWorldMatrix, float4(c0, 1)).xyz;
                    c1 = mul(collider.m_LocalToWorldMatrix, float4(c1, 1)).xyz;
                    resultPosition = OutsideCapsule(particlePosition, particleRadius, c0, c1, radius);
                }
                else
                {

                    c0 = mul(collider.m_LocalToWorldMatrix, float4(c0, 1)).xyz;
                    c1 = mul(collider.m_LocalToWorldMatrix, float4(c1, 1)).xyz;
                    resultPosition = InsideCapsule(particlePosition, particleRadius, c0, c1, radius);
                }
            }
            return resultPosition;
        }

        private static float3 OutsideSphere(float3 particlePosition, float particleRadius, float3 sphereCenter, float sphereRadius)
        {
            var resultPosition = particlePosition;
            var r = sphereRadius + particleRadius;
            var r2 = r * r;
            var d = particlePosition - sphereCenter;
            var len2 = lengthsq(d);
            // if is inside sphere, project onto sphere surface
            if (len2 > 0 && len2 < r2)
            {
                var len = sqrt(len2);
                resultPosition = sphereCenter + (d * (r / len));
            }
            return resultPosition;
        }

        private static float3 InsideSphere(float3 particlePosition, float particleRadius, float3 sphereCenter, float sphereRadius)
        {
            var resultPosition = particlePosition;
            var r = sphereRadius + particleRadius;
            var r2 = r * r;
            var d = particlePosition - sphereCenter;
            var len2 = lengthsq(d);
            // if is outside sphere, project onto sphere surface
            if (len2 > r2)
            {
                var len = sqrt(len2);
                resultPosition = sphereCenter + (d * (r / len));
            }
            return resultPosition;
        }

        private static float3 OutsideCapsule(float3 particlePosition, float particleRadius, float3 capsuleP0, float3 capsuleP1, float capsuleRadius)
        {
            var resultPosition = particlePosition;
            var r = capsuleRadius + particleRadius;
            var r2 = r * r;
            var dir = capsuleP1 - capsuleP0;
            var d = particlePosition - capsuleP0;
            var t = dot(d, dir);
            if (t <= 0)
            {
                // check sphere1
                var len2 = lengthsq(d);
                if (len2 > 0 && len2 < r2)
                {
                    var len = sqrt(len2);
                    resultPosition = capsuleP0 + (d * (r / len));
                }
            }
            else
            {
                var dl = lengthsq(dir);
                if (t >= dl)
                {
                    // check sphere2
                    d = particlePosition - capsuleP1;
                    var len2 = lengthsq(d);
                    if (len2 > 0 && len2 < r2)
                    {
                        var len = sqrt(len2);
                        resultPosition = capsuleP1 + (d * (r / len));
                    }
                }
                else if (dl > 0)
                {
                    // check cylinder
                    t /= dl;
                    d -= dir * t;
                    var len2 = lengthsq(d);
                    if (len2 > 0 && len2 < r2)
                    {
                        var len = sqrt(len2);
                        resultPosition += d * ((r - len) / len);
                    }
                }
            }
            return resultPosition;
        }

        private static float3 InsideCapsule(float3 particlePosition, float particleRadius, float3 capsuleP0, float3 capsuleP1, float capsuleRadius)
        {
            var resultPosition = particlePosition;
            var r = capsuleRadius - particleRadius;
            var r2 = r * r;
            var dir = capsuleP1 - capsuleP0;
            var d = particlePosition - capsuleP0;
            float t = dot(d, dir);

            if (t <= 0)
            {
                // check sphere1
                var len2 = lengthsq(d);
                if (len2 > r2)
                {
                    var len = sqrt(len2);
                    resultPosition = capsuleP0 + d * (r / len);
                }
            }
            else
            {
                float dl = lengthsq(dir);
                if (t >= dl)
                {
                    // check sphere2
                    d = particlePosition - capsuleP1;
                    var len2 = lengthsq(d);
                    if (len2 > r2)
                    {
                        var len = sqrt(len2);
                        resultPosition = capsuleP1 + d * (r / len);
                    }
                }
                else if (dl > 0)
                {
                    // check cylinder
                    t /= dl;
                    d -= dir * t;
                    var len2 = lengthsq(d);
                    if (len2 > r2)
                    {
                        var len = sqrt(len2);
                        resultPosition += d * ((r - len) / len);
                    }
                }
            }
            return resultPosition;
        }
    }

}