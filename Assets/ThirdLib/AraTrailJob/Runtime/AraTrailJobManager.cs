using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Jobs;
using static Unity.Mathematics.math;
using static AraJob.AraTrailJob;
using UnityEngine.Rendering;

namespace AraJob
{
    public class AraTrailJobManager : MonoBehaviour
    {
        private static AraTrailJobManager mInstance;
        public static AraTrailJobManager Instance => mInstance;

        public const int HEAD_SIZE = 100;
        public const int POINT_CHUNK_SIZE = 400;
        public const int VERTICES_SIZE = 526;
        public const int TRIANGLE_MUL = 3;
        public const int GRADIENT_COUNT = 8;
        public const int KEYFRAME_COUNT = 16;

        public class GraphicNumber
        {
            public Mesh mesh;
            public Material[] materials;
            public Camera cam;
            public Transform parent;
            public Matrix4x4 matrix;
            public int gameObjectLayer;
            public ShadowCastingMode castShadows;
            public bool receiveShadows;
            public bool useLightProbes;
            public float uvFlowX;
            public float uvFlowY;
        }


        private enum EChangeType
        {
            None,
            Add,
            Remove,
            //RefreshWeight,
        }

        public static void CheckCreate()
        {
            if (mInstance || !Application.isPlaying) return;
            var rGo = new GameObject("AraTrailJobManager");
            mInstance = rGo.AddComponent<AraTrailJobManager>();
            GameObject.DontDestroyOnLoad(rGo);
        }

        private List<GraphicNumber> mGraphicNumbers;
        public List<AraTrailJob> mAraJobList;
        private List<AraTrailJob> mChangeAraJobList;
        private List<EChangeType> mEChangeTypeList;
        //private NativeList<AraTrailJob.AraHead> mHeadList;

        private NativeList<AraTrailJob.Head> mHeadList;                 
        private NativeList<AraTrailJob.Point> mPoints;

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
        private NativeList<Vector2> uvs;
        private NativeList<int> tris;
        private NativeList<Vector3> normals;


        private JobHandle mLateUpdateJobHandle;

        private void Awake()
        {
            mInstance = this;
            this.Initialize();
        }

        private void LateUpdate()
        {
            this.HandleChangeList();
            this.UpdateJobsData();

            UpdateTrailMeshJob job = new UpdateTrailMeshJob
            {
                mHeadArray = this.mHeadList,
                mPoints = this.mPoints,
                mLengthThickCurve = this.mLengthCurve,
                mLengthThickColorKeys = this.mLengthGradientColor,
                mLengthThickAlphaKeys = this.mLengthGradientAlpha,
                mLengthModel = this.mLengthGradientMode,
                mTimeThickCurve = this.mTimeCurve,
                mTimeThickColorKeys = this.mTimeGradientColor,
                mTimeThickAlphaKeys = this.mTimeGradientAlpha,
                mTimeModel = this.mTimeGradientMode,

                Vertices = this.vertices,
                Tangents = this.tangents,
                VertColors = this.vertColors,
                Uvs = this.uvs,
                Tris = this.tris,
                Normals = this.normals
            };

            this.mLateUpdateJobHandle = job.Schedule(this.mHeadList.Length, 16, this.mLateUpdateJobHandle);
            this.mLateUpdateJobHandle.Complete();
            this.DrawUpdateMeshData();

            
        }

        private void OnDestroy()
        {
            this.mLateUpdateJobHandle.Complete();

            if (this.mHeadList.IsCreated)
            {
                this.mHeadList.Dispose();
            }
            if(this.mPoints.IsCreated)
            {
                this.mPoints.Dispose();
            }

            if (this.mLengthCurve.IsCreated)
            {
                this.mLengthCurve.Dispose();
            }
            if (this.mLengthGradientColor.IsCreated)
            {
                this.mLengthGradientColor.Dispose();
            }
            if (this.mLengthGradientAlpha.IsCreated)
            {
                this.mLengthGradientAlpha.Dispose();
            }
            if (this.mLengthGradientMode.IsCreated)
            {
                this.mLengthGradientMode.Dispose();
            }

            if (this.mTimeCurve.IsCreated)
            {
                this.mTimeCurve.Dispose();
            }
            if (this.mTimeGradientColor.IsCreated)
            {
                this.mTimeGradientColor.Dispose();
            }
            if (this.mTimeGradientAlpha.IsCreated)
            {
                this.mTimeGradientAlpha.Dispose();
            }
            if (this.mTimeGradientMode.IsCreated)
            {
                this.mTimeGradientMode.Dispose();
            }

            if (this.vertices.IsCreated)
            {
                this.vertices.Dispose();
            }
            if (this.tangents.IsCreated)
            {
                this.tangents.Dispose();
            }
            if (this.vertColors.IsCreated)
            {
                this.vertColors.Dispose();
            }
            if (this.uvs.IsCreated)
            {
                this.uvs.Dispose();
            }
            if (this.tris.IsCreated)
            {
                this.tris.Dispose();
            }
            if (this.normals.IsCreated)
            {
                this.normals.Dispose();
            }

        }



        private void Initialize()
        {
            this.mGraphicNumbers = new List<GraphicNumber>();
            this.mAraJobList = new List<AraTrailJob>();
            this.mChangeAraJobList = new List<AraTrailJob>();
            this.mEChangeTypeList = new List<EChangeType>();
            

            this.mHeadList = new NativeList<AraTrailJob.Head>(HEAD_SIZE, Allocator.Persistent);
            this.mPoints = new NativeList<AraTrailJob.Point>(HEAD_SIZE * POINT_CHUNK_SIZE, Allocator.Persistent);

            this.mLengthCurve = new NativeList<Keyframe>(HEAD_SIZE * KEYFRAME_COUNT, Allocator.Persistent);
            this.mLengthGradientColor = new NativeList<GradientColorKey>(HEAD_SIZE * GRADIENT_COUNT, Allocator.Persistent);
            this.mLengthGradientAlpha = new NativeList<GradientAlphaKey>(HEAD_SIZE * GRADIENT_COUNT, Allocator.Persistent);
            this.mLengthGradientMode = new NativeList<GradientMode>(HEAD_SIZE, Allocator.Persistent);

            this.mTimeCurve = new NativeList<Keyframe>(HEAD_SIZE * KEYFRAME_COUNT, Allocator.Persistent);
            this.mTimeGradientColor = new NativeList<GradientColorKey>(HEAD_SIZE * GRADIENT_COUNT, Allocator.Persistent);
            this.mTimeGradientAlpha = new NativeList<GradientAlphaKey>(HEAD_SIZE * GRADIENT_COUNT, Allocator.Persistent);
            this.mTimeGradientMode = new NativeList<GradientMode>(HEAD_SIZE, Allocator.Persistent);

            this.vertices = new NativeList<Vector3>(HEAD_SIZE * POINT_CHUNK_SIZE * VERTICES_SIZE, Allocator.Persistent);
            this.tangents = new NativeList<Vector4>(HEAD_SIZE * POINT_CHUNK_SIZE * VERTICES_SIZE, Allocator.Persistent);
            this.vertColors = new NativeList<Color>(HEAD_SIZE * POINT_CHUNK_SIZE * VERTICES_SIZE, Allocator.Persistent);
            this.uvs = new NativeList<Vector2>(HEAD_SIZE * POINT_CHUNK_SIZE * VERTICES_SIZE, Allocator.Persistent);
            this.tris = new NativeList<int>(HEAD_SIZE * POINT_CHUNK_SIZE * VERTICES_SIZE * TRIANGLE_MUL, Allocator.Persistent);
            this.normals = new NativeList<Vector3>(HEAD_SIZE * POINT_CHUNK_SIZE * VERTICES_SIZE, Allocator.Persistent);
        }

        public void OnEnter(AraTrailJob rAraTrailJob)
        {
            this.mChangeAraJobList.Add(rAraTrailJob);
            this.mEChangeTypeList.Add(EChangeType.Add);
        }
        public void OnExit(AraTrailJob rAraTrailJob)
        {
            this.mChangeAraJobList.Add(rAraTrailJob);
            this.mEChangeTypeList.Add(EChangeType.Remove);
        }

        private void HandleChangeList()
        {
            for (int i = 0; i < this.mChangeAraJobList.Count; i++)
            {
                AraTrailJob rAraTrail = this.mChangeAraJobList[i];
                EChangeType rEChangeType = this.mEChangeTypeList[i];
                int nIndex = this.mAraJobList.IndexOf(rAraTrail);
                if(rEChangeType == EChangeType.Add)
                {
                    if(nIndex != -1)
                    {
                        Debug.LogError($"AraTrial already existed. AraTrial:{rAraTrail.name}");
                        continue;
                    }
                    nIndex = this.mAraJobList.Count;
                    this.mAraJobList.Add(rAraTrail);

                    //setting head info
                    AraTrailJob.Head rHead = new AraTrailJob.Head
                    {
                        localPosition = rAraTrail.transform.localPosition,
                        position = rAraTrail.transform.position,
                        tangent = rAraTrail.transform.right,
                        normal = rAraTrail.transform.forward,
                        up = rAraTrail.transform.up,

                        prevPosition = rAraTrail.prevPosition,
                        DeltaTime = rAraTrail.DeltaTime,
                        velocity = rAraTrail.velocity,
                        speed = rAraTrail.speed,
                        velocitySmoothing = rAraTrail.velocitySmoothing,
                        accumTime = rAraTrail.accumTime,
                        time = rAraTrail.time,
                        timeInterval = rAraTrail.timeInterval,
                        emit = rAraTrail.emit,
                        space = rAraTrail.space,
                        minDistance = rAraTrail.minDistance,
                        initialVelocity = rAraTrail.initialVelocity,
                        inertia = rAraTrail.inertia,
                        initialColor = rAraTrail.initialColor,
                        initialThickness = rAraTrail.initialThickness,
                        smoothness = rAraTrail.smoothness,
                        gravity = rAraTrail.gravity,
                        damping = rAraTrail.damping,
                        timestep = rAraTrail.FixedDeltaTime,

                        localCamPosition = rAraTrail.space == Space.Self && rAraTrail.transform.parent != null ? rAraTrail.transform.parent.InverseTransformPoint(rAraTrail.tempCamera.transform.position) : rAraTrail.tempCamera.transform.position,
                        textureMode = rAraTrail.textureMode,
                        uvFactor = rAraTrail.uvFactor,
                        tileAnchor = rAraTrail.tileAnchor,
                        alignment = rAraTrail.alignment,
                        cornerRoundness = rAraTrail.cornerRoundness,
                        thickness = rAraTrail.thickness,

                        //index record.
                        index_point = nIndex * POINT_CHUNK_SIZE,
                        index_lengthCurve = nIndex * KEYFRAME_COUNT,
                        index_lengthGradientAlpha = nIndex * GRADIENT_COUNT,
                        index_lengthGradientColor = nIndex * GRADIENT_COUNT,
                        index_timeCurve = nIndex * KEYFRAME_COUNT,
                        index_timeGradientAlpha = nIndex * GRADIENT_COUNT,
                        index_timeGradientColor = nIndex * GRADIENT_COUNT,
                        index_vertices = nIndex * GRADIENT_COUNT * VERTICES_SIZE,
                        index_tangents = nIndex * GRADIENT_COUNT * VERTICES_SIZE,
                        index_vertColors = nIndex * GRADIENT_COUNT * VERTICES_SIZE,
                        index_uvs = nIndex * GRADIENT_COUNT * VERTICES_SIZE,
                        index_tris = nIndex * GRADIENT_COUNT * VERTICES_SIZE * TRIANGLE_MUL,
                        index_normals = nIndex * GRADIENT_COUNT * VERTICES_SIZE,
                        //length record.
                        len_point = math.min(POINT_CHUNK_SIZE, rAraTrail.points.Count),
                        len_lengthCurve = math.min(KEYFRAME_COUNT, rAraTrail.thicknessOverLenght.keys.Length),
                        len_lengthGradientAlpha = math.min(GRADIENT_COUNT, rAraTrail.colorOverLenght.alphaKeys.Length),
                        len_lengthGradientColor = math.min(GRADIENT_COUNT, rAraTrail.colorOverLenght.colorKeys.Length),
                        len_timeCurve = math.min(KEYFRAME_COUNT, rAraTrail.thicknessOverTime.keys.Length),
                        len_timeGradientAlpha = math.min(GRADIENT_COUNT, rAraTrail.colorOverTime.alphaKeys.Length),
                        len_timeGradientColor = math.min(GRADIENT_COUNT, rAraTrail.colorOverTime.colorKeys.Length)

                    };
                    this.mHeadList.Add(rHead);

                    GraphicNumber rGraphicNumber = new GraphicNumber();
                    rGraphicNumber.mesh = rAraTrail.mesh;
                    rGraphicNumber.materials = new Material[rAraTrail.materials.Length];
                    for (int j = 0; j < rAraTrail.materials.Length; j++)
                    {
                        rGraphicNumber.materials[j] = rAraTrail.materials[j];
                    }
                    rGraphicNumber.cam = rAraTrail.tempCamera;
                    rGraphicNumber.matrix = rAraTrail.space == Space.Self && rAraTrail.transform.parent != null ? rAraTrail.transform.parent.localToWorldMatrix : Matrix4x4.identity;
                    rGraphicNumber.gameObjectLayer = rAraTrail.gameObject.layer;
                    rGraphicNumber.castShadows = rAraTrail.castShadows;
                    rGraphicNumber.receiveShadows = rAraTrail.receiveShadows;
                    rGraphicNumber.useLightProbes = rAraTrail.useLightProbes;
                    rGraphicNumber.uvFlowX = rAraTrail.uvFlowX;
                    rGraphicNumber.uvFlowY = rAraTrail.uvFlowY;
                    this.mGraphicNumbers.Add(rGraphicNumber);


                    if (rAraTrail.thicknessOverLenght.keys.Length >= KEYFRAME_COUNT)
                    {
                        Debug.LogError($"AnimationCurve 顶点数不要超过{KEYFRAME_COUNT}个，{rAraTrail.name}配置了{rAraTrail.thicknessOverLenght.keys.Length}个");
                        return;
                    }
                    if(rAraTrail.thicknessOverTime.keys.Length >= KEYFRAME_COUNT)
                    {
                        Debug.LogError($"AnimationCurve 顶点数不要超过{KEYFRAME_COUNT}个，{rAraTrail.name}配置了{rAraTrail.thicknessOverTime.keys.Length}个");
                        return;
                    }

                    this.mLengthGradientMode.Add(rAraTrail.colorOverLenght.mode);
                    this.mTimeGradientMode.Add(rAraTrail.colorOverTime.mode);

                    for (int j = 0; j < KEYFRAME_COUNT; j++)
                    {
                        if(j < rHead.len_lengthCurve)
                        {
                            this.mLengthCurve.Add(rAraTrail.thicknessOverLenght.keys[j]);
                        }
                        else
                        {
                            this.mLengthCurve.Add(new Keyframe());
                        }

                        if (j < rHead.len_timeCurve)
                        {
                            this.mTimeCurve.Add(rAraTrail.thicknessOverTime.keys[j]);
                        }
                        else
                        {
                            this.mTimeCurve.Add(new Keyframe());
                        }
                    }

                    for (int j = 0; j < GRADIENT_COUNT; j++)
                    {

                        //Length config color
                        if (j < rHead.len_lengthGradientAlpha)
                        {
                            this.mLengthGradientAlpha.Add(rAraTrail.colorOverLenght.alphaKeys[j]);
                        }
                        else
                        {
                            this.mLengthGradientAlpha.Add(new GradientAlphaKey());
                        }
                        if (j < rHead.len_lengthGradientColor)
                        {
                            this.mLengthGradientColor.Add(rAraTrail.colorOverLenght.colorKeys[j]);
                        }
                        else
                        {
                            this.mLengthGradientColor.Add(new GradientColorKey());
                        }



                        //Time config color
                        if (j < rHead.len_timeGradientAlpha)
                        {
                            this.mTimeGradientAlpha.Add(rAraTrail.colorOverTime.alphaKeys[j]);
                        }
                        else
                        {
                            this.mTimeGradientAlpha.Add(new GradientAlphaKey());
                        }
                        if (j < rHead.len_timeGradientColor)
                        {
                            this.mTimeGradientColor.Add(rAraTrail.colorOverTime.colorKeys[j]);
                        }
                        else
                        {
                            this.mTimeGradientColor.Add(new GradientColorKey());
                        }
                    }

                    
                    for (int j = 0; j < POINT_CHUNK_SIZE; j++)
                    {
                        this.mPoints.Add(new Point());
                    }

                    for(int j = 0; j < POINT_CHUNK_SIZE * VERTICES_SIZE; j++)
                    {
                        this.vertices.Add(new Vector3());
                        this.tangents.Add(new Vector4());
                        this.vertColors.Add(new Color());
                        this.uvs.Add(new Vector3());
                        for (int k = 0; k < TRIANGLE_MUL; k++)
                        {
                            this.tris.Add(0);
                        }
                        this.normals.Add(new Vector3());
                    }
                }

                if(rEChangeType == EChangeType.Remove)
                {
                    if (nIndex != -1)
                    {
                        Debug.LogError($"AraTrail dose not existed. AraTrail:{rAraTrail.name}");
                        continue;
                    }
                    int nSwapBackIndex = this.mAraJobList.Count - 1;
                    this.mAraJobList[nIndex] = this.mAraJobList[nSwapBackIndex];
                    this.mAraJobList.RemoveAt(nSwapBackIndex);
                    this.mGraphicNumbers.RemoveAt(nSwapBackIndex);
                    //修改索引
                    AraTrailJob.Head rHead = this.mHeadList[nSwapBackIndex];
                    rHead.index_point = nIndex * POINT_CHUNK_SIZE;
                    rHead.index_lengthCurve = nIndex * KEYFRAME_COUNT;
                    rHead.index_lengthGradientAlpha = nIndex * GRADIENT_COUNT;
                    rHead.index_lengthGradientColor = nIndex * GRADIENT_COUNT;
                    rHead.index_timeCurve = nIndex * KEYFRAME_COUNT;
                    rHead.index_timeGradientAlpha = nIndex * GRADIENT_COUNT;
                    rHead.index_timeGradientColor = nIndex * GRADIENT_COUNT;
                    this.mHeadList[nSwapBackIndex] = rHead;
                    
                    //删除NativeContainers元素
                    this.mHeadList.RemoveAtSwapBack(nIndex);
                    this.mLengthGradientMode.RemoveAtSwapBack(nIndex);
                    this.mTimeGradientMode.RemoveAtSwapBack(nIndex);
                    for (int j = (nIndex + 1) * KEYFRAME_COUNT - 1; j >= nIndex * POINT_CHUNK_SIZE; j--)
                    {
                        this.mTimeCurve.RemoveAtSwapBack(j);
                        this.mLengthCurve.RemoveAtSwapBack(j);
                    }

                    for (int j = (nIndex + 1) * GRADIENT_COUNT - 1; j >= nIndex * POINT_CHUNK_SIZE; j--)
                    {
                        this.mLengthGradientAlpha.RemoveAtSwapBack(j);
                        this.mLengthGradientColor.RemoveAtSwapBack(j);
                        this.mTimeGradientAlpha.RemoveAtSwapBack(j);
                        this.mTimeGradientColor.RemoveAtSwapBack(j);
                    }

                    for (int j = (nIndex + 1) * POINT_CHUNK_SIZE - 1; j >= nIndex * POINT_CHUNK_SIZE; j--)
                    {
                        this.mPoints.RemoveAtSwapBack(j);
                    }

                    for (int j = (nIndex + 1) * POINT_CHUNK_SIZE * VERTICES_SIZE - 1; j >= nIndex * POINT_CHUNK_SIZE * VERTICES_SIZE; j--)
                    {
                        this.vertices.RemoveAtSwapBack(j);
                        this.vertColors.RemoveAtSwapBack(j);
                        this.tangents.RemoveAtSwapBack(j);
                        this.uvs.RemoveAtSwapBack(j);
                        this.normals.RemoveAtSwapBack(j);
                    }

                    for (int j = (nIndex + 1) * POINT_CHUNK_SIZE * VERTICES_SIZE * TRIANGLE_MUL - 1; j >= nIndex * POINT_CHUNK_SIZE * VERTICES_SIZE * TRIANGLE_MUL; j--)
                    {
                        this.tris.RemoveAtSwapBack(j);
                    }

                    Debug.Assert(this.mHeadList.Length == this.mAraJobList.Count);
                    Debug.Assert(this.mLengthGradientMode.Length == this.mAraJobList.Count);
                    Debug.Assert(this.mTimeGradientMode.Length == this.mAraJobList.Count);
                    Debug.Assert(this.mLengthGradientAlpha.Length == this.mAraJobList.Count * GRADIENT_COUNT);
                    Debug.Assert(this.mLengthGradientColor.Length == this.mAraJobList.Count * GRADIENT_COUNT);
                    Debug.Assert(this.mTimeGradientAlpha.Length == this.mAraJobList.Count * GRADIENT_COUNT);
                    Debug.Assert(this.mTimeGradientColor.Length == this.mAraJobList.Count * GRADIENT_COUNT);
                    Debug.Assert(this.mTimeCurve.Length == this.mAraJobList.Count * KEYFRAME_COUNT);
                    Debug.Assert(this.mLengthCurve.Length == this.mAraJobList.Count * KEYFRAME_COUNT);


                }

            }
            this.mChangeAraJobList.Clear();
            this.mEChangeTypeList.Clear();
        }

        private void UpdateJobsData()
        {
            for (int i = 0; i < this.mAraJobList.Count; i++)
            {
                var rHead = this.mHeadList[i];
                rHead.localPosition = this.mAraJobList[i].transform.localPosition;
                rHead.position = this.mAraJobList[i].transform.position;
                rHead.tangent = this.mAraJobList[i].transform.right;
                rHead.normal = this.mAraJobList[i].transform.forward;
                rHead.up = this.mAraJobList[i].transform.up;
                this.mHeadList[i] = rHead;
            }
        }

        private void DrawUpdateMeshData()
        {
            //Manager统一画
            //for (int i = 0; i < this.mAraJobList.Count; i++)
            //{
            //    GraphicNumber rGraphicNumber = this.mGraphicNumbers[i];
            //    Head rhead = this.mHeadList[i];
            //    Mesh mesh = rGraphicNumber.mesh;
            //    mesh.Clear();
            //    mesh.SetVertices(this.vertices.ToArray(), rhead.index_vertices, rhead.len_vertices);
            //    mesh.SetNormals(this.normals.ToArray(), rhead.index_normals, rhead.len_normals);
            //    mesh.SetTangents(this.tangents.ToArray(), rhead.index_tangents, rhead.len_tangents);
            //    mesh.SetColors(this.vertColors.ToArray(), rhead.index_vertColors, rhead.len_vertColors);
            //    mesh.SetUVs(0, this.uvs.ToArray(), rhead.index_uvs, rhead.len_uvs);
            //    mesh.SetTriangles(this.tris.ToArray(), rhead.index_tris, rhead.len_tris, 0, true);
            //    for (int j = 0; j < rGraphicNumber.materials.Length; j++)
            //    {
            //        if (rGraphicNumber.materials[j] == null)
            //            continue;

            //        rGraphicNumber.materials[j].EnableKeyword("_UV_FLOW_ON");
            //        rGraphicNumber.materials[j].SetVector("_UVFlow", new Vector4(rGraphicNumber.uvFlowX, rGraphicNumber.uvFlowY, 0, 0));

            //        Graphics.DrawMesh(mesh, rGraphicNumber.matrix,
            //                          rGraphicNumber.materials[j], rGraphicNumber.gameObjectLayer, rGraphicNumber.cam, 0, null, rGraphicNumber.castShadows, rGraphicNumber.receiveShadows, null, rGraphicNumber.useLightProbes);
            //    }
            //}






            //顶点数据返回给AraTrail，由他们自己画
            for (int i = 0; i < this.mAraJobList.Count; i++)
            {
                AraTrailJob araTrail = this.mAraJobList[i];
                Head rhead = this.mHeadList[i];


                araTrail.DrawMeshData(this.vertices.ToArray(), rhead.index_vertices, rhead.len_vertices,
                    this.normals.ToArray(), rhead.index_normals, rhead.len_normals,
                    this.tangents.ToArray(), rhead.index_tangents, rhead.len_tangents,
                    this.vertColors.ToArray(), rhead.index_vertColors, rhead.len_vertColors,
                    this.uvs.ToArray(), rhead.index_uvs, rhead.len_uvs,
                    this.tris.ToArray(), rhead.index_tris, rhead.len_tris
                    );
            }

        }


        [ContextMenu("打印空间占用")]
        public void ShowMemoryCost()
        {
            int num = 0;
            num += HEAD_SIZE * POINT_CHUNK_SIZE;

            Debug.Log($"{num}");
        }

    }


    [BurstCompile]
    public struct UpdateTrailMeshJob : IJobParallelFor
    {
        public NativeArray<Head> mHeadArray;
        [NativeDisableParallelForRestriction]
        public NativeArray<Point> mPoints;

        //public NativeList<int> discontinuities;

        [ReadOnly] public NativeList<Keyframe> mLengthThickCurve;
        [ReadOnly] public NativeList<GradientColorKey> mLengthThickColorKeys;
        [ReadOnly] public NativeList<GradientAlphaKey> mLengthThickAlphaKeys;
        [ReadOnly] public NativeList<GradientMode> mLengthModel;

        [ReadOnly] public NativeList<Keyframe> mTimeThickCurve;
        [ReadOnly] public NativeList<GradientColorKey> mTimeThickColorKeys;
        [ReadOnly] public NativeList<GradientAlphaKey> mTimeThickAlphaKeys;
        [ReadOnly] public NativeList<GradientMode> mTimeModel;


        [NativeDisableParallelForRestriction] public NativeArray<Vector3> Vertices;
        [NativeDisableParallelForRestriction] public NativeArray<Vector4> Tangents;
        [NativeDisableParallelForRestriction] public NativeArray<Color> VertColors;
        [NativeDisableParallelForRestriction] public NativeArray<Vector2> Uvs;
        [NativeDisableParallelForRestriction] public NativeArray<int> Tris;
        [NativeDisableParallelForRestriction] public NativeArray<Vector3> Normals;


        //vertices;
        //tangents;
        //NativeList<Color> vertColors;
        //NativeList<Vector3> uvs;
        //NativeList<int> tris;
        //NativeList<Vector3> normals;



        public void Execute(int index)
        {

            ClearMeshData(index);

            Head rHead = mHeadArray[index];
            if (rHead.DeltaTime > 0)
            {
                rHead.velocity = Vector3.Lerp((rHead.position - rHead.prevPosition) / rHead.DeltaTime, rHead.velocity, rHead.velocitySmoothing);
                rHead.speed = rHead.velocity.magnitude;
            }
            rHead.prevPosition = rHead.position;


            // Acumulate the amount of time passed:
            rHead.accumTime += rHead.DeltaTime;
            // If enough time has passed since the last emission (>= timeInterval), consider emitting new points.
            if (rHead.accumTime >= rHead.timeInterval)
            {
                if (rHead.emit)
                {
                    // Select the emission position, depending on the simulation space:
                    Vector3 position = rHead.space == Space.Self ? rHead.localPosition : rHead.position;
                    // If there's at least 1 point and it is not far enough from the current position, don't spawn any new points this frame.
                    if (rHead.len_point <= 1 || Vector3.Distance(position, mPoints[(rHead.index_point + rHead.len_point) - 2].position) >= rHead.minDistance)
                    {
                        //mPoints.Add(new Point(position, rHead.initialVelocity + rHead.velocity * rHead.inertia, rHead.tangent, rHead.normal, rHead.initialColor, rHead.initialThickness, rHead.time));
                        //Debug.LogError($"{mPoints.Length}   {rHead.index_point + rHead.len_point}");
                        mPoints[rHead.index_point + rHead.len_point] = new Point(position, rHead.initialVelocity + rHead.velocity * rHead.inertia, rHead.tangent, rHead.normal, rHead.initialColor, rHead.initialThickness, rHead.time);
                        rHead.len_point++;
                        rHead.accumTime = 0;
                        //Debug.Log($"rHead.accumTime: {rHead.accumTime}  mPoints: {mPoints.Length}");
                    }
                }
            }


            if (rHead.len_point > 0)
            {

                Point lastPoint = mPoints[(rHead.index_point + rHead.len_point) - 1];

                // if we are not emitting, the last point is a discontinuity.
                if (!rHead.emit)
                    lastPoint.discontinuous = true;

                // if the point is discontinuous, move and orient it according to the transform.
                if (!lastPoint.discontinuous)
                {
                    lastPoint.position = rHead.space == Space.Self ? rHead.localPosition : rHead.position;
                    lastPoint.normal = rHead.normal;
                    lastPoint.tangent = rHead.tangent;
                }

                mPoints[(rHead.index_point + rHead.len_point) - 1] = lastPoint;
            }


            for (int i = (rHead.index_point + rHead.len_point) - 1; i >= rHead.index_point; --i)
            {

                Point point = mPoints[i];
                point.life -= rHead.DeltaTime;
                mPoints[i] = point;

                if (point.life <= 0)
                {

                    // Unsmoothed trails delete points as soon as they die.
                    if (rHead.smoothness <= 1)
                    {
                        //mPoints.RemoveAt(i);
                        for (int k = i; k < (rHead.index_point + rHead.len_point) - 1; k++)
                        {
                            mPoints[k] = mPoints[k + 1];
                        }

                        rHead.len_point--; 
                    }
                    // Smoothed trails however, should wait until the next 2 points are dead too. This ensures spline continuity.
                    else
                    {
                        if (mPoints[Mathf.Min(i + 1, (rHead.index_point + rHead.len_point) - 1)].life <= 0 &&
                            mPoints[Mathf.Min(i + 2, (rHead.index_point + rHead.len_point) - 1)].life <= 0)
                        {
                            //mPoints.RemoveAt(i);
                            for (int k = i; k < (rHead.index_point + rHead.len_point) - 1; k++)
                            {
                                mPoints[k] = mPoints[k + 1];
                            }
                            rHead.len_point--;
                        }
                            
                            
                    }

                }
            }

            // We need at least two points to create a trail mesh.
            if (rHead.len_point > 1)
            {

                //Vector3 localCamPosition = rHead.space == Space.Self && transform.parent != null ? transform.parent.InverseTransformPoint(cam.transform.position) : cam.transform.position;

                // get discontinuous point indices:

                //discontinuities.Clear();
                //public NativeList<int> discontinuities = new NativeList<int>(Allocator.Temp);
                NativeList<int> discontinuities = new NativeList<int>(Allocator.Temp);
                for (int i = rHead.index_point; i < (rHead.index_point + rHead.len_point); ++i)
                    if (mPoints[i].discontinuous || i == (rHead.index_point + rHead.len_point) - 1) discontinuities.Add(i);

                // generate mesh for each trail segment:
                int start = rHead.index_point;
                mHeadArray[index] = rHead;
                for (int i = 0; i < discontinuities.Length; ++i)
                {
                    UpdateSegmentMesh(mPoints, start, discontinuities[i], rHead.localCamPosition, index);
                    start = discontinuities[i] + 1;
                }




            }
            else
            {
                mHeadArray[index] = rHead;
            }

        }

        private void UpdateSegmentMesh(NativeArray<Point> input, int start, int end, Vector3 localCamPosition, int nIndex)
        {
            Head rHead = mHeadArray[nIndex];
            // Get a list of the actual points to render: either the original, unsmoothed points or the smoothed curve.
            NativeArray<Point> trail = new NativeArray<Point>(GetRenderablePoints(input, start, end, nIndex), Allocator.Temp);
            //NativeArray <Point> trail = GetRenderablePoints(input, start, end, nIndex);
            //NativeList<Point> trail = input;


            NativeList<Vector3> vertices = new NativeList<Vector3>(Allocator.Temp);
            NativeList<Vector4> tangents = new NativeList<Vector4>(Allocator.Temp);
            NativeList<Color> vertColors = new NativeList<Color>(Allocator.Temp);
            NativeList<Vector3> uvs = new NativeList<Vector3>(Allocator.Temp);
            NativeList<int> tris = new NativeList<int>(Allocator.Temp);
            NativeList<Vector3> normals = new NativeList<Vector3>(Allocator.Temp);




            Debug.Log($"Trail Length: {trail.Length}");
            if (trail.Length > 1)
            {

                NativeList<GradientColorKey> lColorKeys = new NativeList<GradientColorKey>(Allocator.Temp);
                NativeList<GradientAlphaKey> lAlphaKeys = new NativeList<GradientAlphaKey>(Allocator.Temp);
                NativeList<GradientColorKey> tColorKeys = new NativeList<GradientColorKey>(Allocator.Temp);
                NativeList<GradientAlphaKey> tAlphaKeys = new NativeList<GradientAlphaKey>(Allocator.Temp);
                NativeList<Keyframe> lKeyFrames = new NativeList<Keyframe>(Allocator.Temp);
                NativeList<Keyframe> tKeyFrames = new NativeList<Keyframe>(Allocator.Temp);


                for (int j = rHead.index_lengthCurve; j < (rHead.index_lengthCurve + rHead.len_lengthCurve); j++)
                {
                    lKeyFrames.Add(mLengthThickCurve[j]);
                }
                for (int j = rHead.index_lengthGradientColor; j < (rHead.index_lengthGradientColor + rHead.len_lengthGradientColor); j++)
                {
                    lColorKeys.Add(mLengthThickColorKeys[j]);
                }
                for (int j = rHead.index_lengthGradientAlpha; j < (rHead.len_lengthGradientAlpha + rHead.index_lengthGradientAlpha); j++)
                {
                    lAlphaKeys.Add(mLengthThickAlphaKeys[j]);
                }

                for (int j = rHead.index_timeCurve; j < (rHead.len_timeCurve + rHead.index_timeCurve); j++)
                {
                    tKeyFrames.Add(mTimeThickCurve[j]);
                }
                for (int j = rHead.index_timeGradientColor; j < (rHead.len_timeGradientColor + rHead.index_timeGradientColor); j++)
                {
                    tColorKeys.Add(mTimeThickColorKeys[j]);
                }
                for (int j = rHead.index_timeGradientAlpha; j < (rHead.len_timeGradientAlpha + rHead.index_timeGradientAlpha); j++)
                {
                    tAlphaKeys.Add(mTimeThickAlphaKeys[j]);
                }
                //Debug.LogError($"index_timeGradientColor : {rHead.index_timeGradientColor}  rHead.len_timeGradientColor: {rHead.len_timeGradientColor}  tColorKeys: {tColorKeys.Length} tAlphaKeys: {tAlphaKeys.Length}");

                float lenght = Mathf.Max(GetLenght(trail), 0.00001f);
                float partialLenght = 0;
                float vCoord = rHead.textureMode == TextureMode.Stretch ? 0 : -rHead.uvFactor * lenght * rHead.tileAnchor;
                Vector4 texTangent = Vector4.zero;
                Vector2 uv = Vector2.zero;
                Color vertexColor;

                bool hqCorners = rHead.highQualityCorners && rHead.alignment != TrailAlignment.Local;

                // Initialize curve frame using the first two points to calculate the first tangent vector:
                CurveFrame frame = InitializeCurveFrame(trail[trail.Length - 1].position,
                                                        trail[trail.Length - 2].position);

                int va = 1;
                int vb = 0;

                int nextIndex;
                int prevIndex;
                Vector3 nextV;
                Vector3 prevV;
                Point curPoint;
                for (int i = trail.Length - 1; i >= 0; --i)
                {

                    curPoint = trail[i];
                    // Calculate next and previous point indices:
                    nextIndex = Mathf.Max(i - 1, 0);
                    prevIndex = Mathf.Min(i + 1, trail.Length - 1);

                    // Calculate next and previous trail vectors:
                    nextV = trail[nextIndex].position - curPoint.position;
                    prevV = curPoint.position - trail[prevIndex].position;
                    float sectionLength = nextV.magnitude;

                    nextV.Normalize();
                    prevV.Normalize();

                    // Calculate tangent vector:
                    Vector3 tangent = rHead.alignment == TrailAlignment.Local ? curPoint.tangent : (nextV + prevV);
                    tangent.Normalize();

                    // Calculate normal vector:
                    Vector3 normal = curPoint.normal;
                    if (rHead.alignment != TrailAlignment.Local)
                        normal = rHead.alignment == TrailAlignment.View ? localCamPosition - curPoint.position : frame.Transport(tangent, curPoint.position);
                    normal.Normalize();

                    // Calculate bitangent vector:
                    Vector3 bitangent = rHead.alignment == TrailAlignment.Velocity ? frame.bitangent : Vector3.Cross(tangent, normal);
                    bitangent.Normalize();

                    // Calculate this point's normalized (0,1) lenght and life.
                    float normalizedLength = partialLenght / lenght;
                    float normalizedLife = Mathf.Clamp01(1 - curPoint.life / rHead.time);
                    partialLenght += sectionLength;

                    // Calulate vertex color:
                    //vertexColor = curPoint.color *
                    //              colorOverTime.Evaluate(normalizedLife) *
                    //              colorOverLenght.Evaluate(normalizedLength);
                    //vertexColor = curPoint.color * timeThickColor[i] * lengthThickColor[i];

                    vertexColor = curPoint.color * UnitySrcAssist.GradientEvaluate(tColorKeys, tAlphaKeys, mTimeModel[nIndex], normalizedLife)
                                                 * UnitySrcAssist.GradientEvaluate(lColorKeys, lAlphaKeys, mLengthModel[nIndex], normalizedLength);


                    // Update vcoord:
                    vCoord += rHead.uvFactor * (rHead.textureMode == TextureMode.Stretch ? sectionLength / lenght : sectionLength);

                    // Calulate final thickness:
                    //float sectionThickness = rHead.thickness * curPoint.thickness * thicknessOverTime.Evaluate(normalizedLife) * thicknessOverLenght.Evaluate(normalizedLength);
                    //float sectionThickness = rHead.thickness * curPoint.thickness * timeThickCurve[i] * lengthThickCurve[i];
                    float sectionThickness = rHead.thickness * curPoint.thickness * UnitySrcAssist.AnimationCurveEvaluate(tKeyFrames, normalizedLife)
                                                                                  * UnitySrcAssist.AnimationCurveEvaluate(lKeyFrames, normalizedLength);


                    Quaternion q = Quaternion.identity;
                    Vector3 corner = Vector3.zero;
                    float curvatureSign = 0;
                    float correctedThickness = sectionThickness;
                    Vector3 prevSectionBitangent = bitangent;

                    // High-quality corners: 
                    if (hqCorners)
                    {

                        Vector3 nextSectionBitangent = i == 0 ? bitangent : Vector3.Cross(nextV, Vector3.Cross(bitangent, tangent)).normalized;

                        // If round corners are enabled:
                        if (rHead.cornerRoundness > 0)
                        {

                            prevSectionBitangent = i == trail.Length - 1 ? -bitangent : Vector3.Cross(prevV, Vector3.Cross(bitangent, tangent)).normalized;

                            // Calculate "elbow" angle:
                            curvatureSign = (i == 0 || i == trail.Length - 1) ? 1 : Mathf.Sign(Vector3.Dot(nextV, -prevSectionBitangent));
                            float angle = (i == 0 || i == trail.Length - 1) ? Mathf.PI : Mathf.Acos(Mathf.Clamp(Vector3.Dot(nextSectionBitangent, prevSectionBitangent), -1, 1));

                            // Prepare a quaternion for incremental rotation of the corner vector:
                            q = Quaternion.AngleAxis(Mathf.Rad2Deg * angle / rHead.cornerRoundness, normal * curvatureSign);
                            corner = prevSectionBitangent * sectionThickness * curvatureSign;
                        }

                        // Calculate correct thickness by projecting corner bitangent onto the next section bitangent. This prevents "squeezing"
                        if (nextSectionBitangent.sqrMagnitude > 0.1f)
                            correctedThickness = sectionThickness / Mathf.Max(Vector3.Dot(bitangent, nextSectionBitangent), 0.15f);

                    }


                    // Append straight section mesh data:

                    if (hqCorners && rHead.cornerRoundness > 0)
                    {

                        // bitangents are slightly asymmetrical in case of high-quality round or sharp corners:
                        if (curvatureSign > 0)
                        {
                            vertices.Add(curPoint.position + prevSectionBitangent * sectionThickness);
                            vertices.Add(curPoint.position - bitangent * correctedThickness);
                        }
                        else
                        {
                            vertices.Add(curPoint.position + bitangent * correctedThickness);
                            vertices.Add(curPoint.position - prevSectionBitangent * sectionThickness);
                        }

                    }
                    else
                    {
                        vertices.Add(curPoint.position + bitangent * correctedThickness);
                        vertices.Add(curPoint.position - bitangent * correctedThickness);
                    }

                    normals.Add(-normal);
                    normals.Add(-normal);

                    texTangent = -bitangent;
                    texTangent.w = 1;
                    tangents.Add(texTangent);
                    tangents.Add(texTangent);

                    vertColors.Add(vertexColor);
                    vertColors.Add(vertexColor);

                    uv.Set(vCoord, 0);
                    uvs.Add(uv);
                    uv.Set(vCoord, 1);
                    uvs.Add(uv);

                    if (i < trail.Length - 1)
                    {

                        int vc = vertices.Length - 1;

                        tris.Add(vc);
                        tris.Add(va);
                        tris.Add(vb);

                        tris.Add(vb);
                        tris.Add(vc - 1);
                        tris.Add(vc);
                    }

                    va = vertices.Length - 1;
                    vb = vertices.Length - 2;

                    // Append smooth corner mesh data:
                    if (hqCorners && rHead.cornerRoundness > 0)
                    {

                        for (int p = 0; p <= rHead.cornerRoundness; ++p)
                        {

                            vertices.Add(curPoint.position + corner);
                            normals.Add(-normal);
                            tangents.Add(texTangent);
                            vertColors.Add(vertexColor);
                            uv.Set(vCoord, curvatureSign > 0 ? 0 : 1);
                            uvs.Add(uv);

                            int vc = vertices.Length - 1;

                            tris.Add(vc);
                            tris.Add(va);
                            tris.Add(vb);

                            if (curvatureSign > 0)
                                vb = vc;
                            else va = vc;

                            // rotate corner point:
                            corner = q * corner;
                        }

                    }

                }

                lColorKeys.Dispose();
                lAlphaKeys.Dispose();
                tColorKeys.Dispose();
                lKeyFrames.Dispose();
                tAlphaKeys.Dispose();
                tKeyFrames.Dispose();
            }





            rHead.len_vertices = vertices.Length;
            rHead.len_tangents = tangents.Length;
            rHead.len_vertColors = vertColors.Length;
            rHead.len_uvs = uvs.Length;
            rHead.len_tris = tris.Length;
            rHead.len_normals = normals.Length;

            for (int j = rHead.index_vertices, k = 0; j < (rHead.index_vertices + rHead.len_vertices); j++, k++)
            {
                Vertices[j] = vertices[k];
            }

            for (int j = rHead.index_tangents, k = 0; j < (rHead.index_tangents + rHead.len_tangents); j++, k++)
            {
                //Debug.LogError($"rHead.index_tangents: {rHead.index_tangents}  j: {j}  k: {k}");
                Tangents[j] = tangents[k];
            }

            for (int j = rHead.index_vertColors, k = 0; j < (rHead.index_vertColors + rHead.len_vertColors); j++, k++)
            {
                //Debug.LogError($"rHead.len_vertColors: {rHead.len_vertColors}  j: {j}  k: {k}");
                VertColors[j] = vertColors[k];
            }

            for (int j = rHead.index_uvs, k = 0; j < (rHead.index_uvs + rHead.len_uvs); j++, k++)
            {
                Uvs[j] = uvs[k];
            }

            for (int j = rHead.index_tris, k = 0; j < (rHead.index_tris + rHead.len_tris); j++, k++)
            {
                Tris[j] = tris[k];
            }

            for (int j = rHead.index_normals, k = 0; j < (rHead.index_normals + rHead.len_normals); j++, k++)
            {
                Normals[j] = normals[k];
            }


            vertices.Dispose();
            tangents.Dispose();
            vertColors.Dispose();
            uvs.Dispose();
            tris.Dispose();
            normals.Dispose();


            mHeadArray[nIndex] = rHead;

        }

        private NativeArray<Point> GetRenderablePoints(NativeArray<Point> input, int start, int end, int index)
        {
            Head rHead = mHeadArray[index];
            NativeArray<Point> points = input;
            //renderablePoints.Clear();

            NativeList<Point> renderablePoints = new NativeList<Point>(Allocator.Temp);

            if (rHead.smoothness <= 1)
            {
                for (int i = start; i <= end; ++i)
                    renderablePoints.Add(points[i]);
                return renderablePoints;
            }

            // calculate sample size in normalized coordinates:
            float samplesize = 1.0f / rHead.smoothness;

            for (int i = start; i < end; ++i)
            {

                // Extrapolate first and last curve control points:
                Point firstPoint = i == start ? points[start] + (points[start] - points[i + 1]) : points[i - 1];
                Point lastPoint = i == end - 1 ? points[end] + (points[end] - points[end - 1]) : points[i + 2];

                for (int j = 0; j < rHead.smoothness; ++j)
                {

                    float t = j * samplesize;
                    Point interpolated = Point.Interpolate(firstPoint,
                                                           points[i],
                                                           points[i + 1],
                                                           lastPoint, t);

                    // only if the interpolated point is alive, we add it to the list of points to render.
                    if (interpolated.life > 0)
                        renderablePoints.Add(interpolated);
                }

            }

            if (points[end].life > 0)
                renderablePoints.Add(points[end]);

            return renderablePoints;
        }

        private float GetLenght(NativeArray<Point> input)
        {

            float lenght = 0;
            for (int i = 0; i < input.Length - 1; ++i)
            {
                //var temp = input[i];
                //var temp1 = input[i+1];
                lenght += Vector3.Distance(input[i].position, input[i + 1].position);
            }
            return lenght;

        }

        private CurveFrame InitializeCurveFrame(Vector3 point, Vector3 nextPoint)
        {
            Head rHead = mHeadArray[0];
            Vector3 tangent = nextPoint - point;

            // Calculate tangent proximity to the normal vector of the frame (transform.forward).
            float tangentProximity = Mathf.Abs(Vector3.Dot(tangent.normalized, rHead.normal));

            // If both vectors are dangerously close, skew the tangent a bit so that a proper frame can be formed:
            //if (Mathf.Approximately(tangentProximity, 1))
            if (Mathf.Abs(tangentProximity - 1) < 0.0001f)
                tangent += rHead.tangent * 0.01f;

            // Generate and return the frame:
            return new CurveFrame(point, rHead.normal, rHead.up, tangent);
        }

        private void ClearMeshData(int nIndex)
        {
            //vertices.Clear();
            //normals.Clear();
            //tangents.Clear();
            //uvs.Clear();
            //vertColors.Clear();
            //tris.Clear();
            Head rHead = mHeadArray[nIndex];
            rHead.len_vertices = 0;
            rHead.len_tangents = 0;
            rHead.len_vertColors = 0;
            rHead.len_uvs = 0;
            rHead.len_tris = 0;
            rHead.len_normals = 0;
            mHeadArray[nIndex] = rHead;
        }

    }

}