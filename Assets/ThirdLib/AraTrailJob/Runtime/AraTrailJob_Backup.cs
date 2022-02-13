﻿using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using Unity.Burst;
using UnityEngine.Jobs;

namespace AraJob
{

    [ExecuteInEditMode]
    public class AraTrailJob_Backup : MonoBehaviour
    {

        public const float epsilon = 0.00001f;

        public enum TrailAlignment
        {
            View,
            Velocity,
            Local
        }

        public enum Timescale
        {
            Normal,
            Unscaled
        }

        public enum TextureMode
        {
            Stretch,
            Tile
        }



        /// <summary>
        /// Spatial frame, consisting of a point an three axis. This is used to implement the parallel transport method 
        /// along the curve defined by the trail points.Using this instead of a Frenet-esque method avoids flipped frames
        /// at points where the curvature changes.
        /// </summary>
        [BurstCompile]
        public struct CurveFrame
        {

            public Vector3 position;
            public Vector3 normal;
            public Vector3 bitangent;
            public Vector3 tangent;

            public CurveFrame(Vector3 position, Vector3 normal, Vector3 bitangent, Vector3 tangent)
            {
                this.position = position;
                this.normal = normal;
                this.bitangent = bitangent;
                this.tangent = tangent;
            }

            public Vector3 Transport(Vector3 newTangent, Vector3 newPosition)
            {

                // double-reflection rotation-minimizing frame transport:
                Vector3 v1 = newPosition - position;
                float c1 = Vector3.Dot(v1, v1);

                Vector3 rL = normal - 2 / (c1 + 0.00001f) * Vector3.Dot(v1, normal) * v1;
                Vector3 tL = tangent - 2 / (c1 + 0.00001f) * Vector3.Dot(v1, tangent) * v1;

                Vector3 v2 = newTangent - tL;
                float c2 = Vector3.Dot(v2, v2);

                Vector3 r1 = rL - 2 / (c2 + 0.00001f) * Vector3.Dot(v2, rL) * v2;
                Vector3 s1 = Vector3.Cross(newTangent, r1);

                normal = r1;
                bitangent = s1;
                tangent = newTangent;
                position = newPosition;

                return normal;
            }
        }

        /// <summary>
        /// Holds information for each point in a trail: position, velocity and remaining lifetime. Points
        /// can be added or subtracted, and interpolated using Catmull-Rom spline interpolation.
        /// </summary>
        [System.Serializable]
        [BurstCompile]
        public struct Point
        {

            public Vector3 position;
            public Vector3 velocity;
            public Vector3 tangent;
            public Vector3 normal;
            public Color color;
            public float thickness;
            public float life;
            public bool discontinuous;

            public Point(Vector3 position, Vector3 velocity, Vector3 tangent, Vector3 normal, Color color, float thickness, float lifetime)
            {
                this.position = position;
                this.velocity = velocity;
                this.tangent = tangent;
                this.normal = normal;
                this.color = color;
                this.thickness = thickness;
                this.life = lifetime;
                this.discontinuous = false;
            }

            private static float CatmullRom(float p0, float p1, float p2, float p3, float t)
            {
                float t2 = t * t;
                return 0.5f * ((2 * p1) +
                              (-p0 + p2) * t +
                              (2 * p0 - 5 * p1 + 4 * p2 - p3) * t2 +
                              (-p0 + 3 * p1 - 3 * p2 + p3) * t2 * t);
            }

            private static Color CatmullRomColor(Color p0, Color p1, Color p2, Color p3, float t)
            {
                return new Color(CatmullRom(p0[0], p1[0], p2[0], p3[0], t),
                                 CatmullRom(p0[1], p1[1], p2[1], p3[1], t),
                                 CatmullRom(p0[2], p1[2], p2[2], p3[2], t),
                                 CatmullRom(p0[3], p1[3], p2[3], p3[3], t));
            }

            private static Vector3 CatmullRom3D(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
            {
                return new Vector3(CatmullRom(p0[0], p1[0], p2[0], p3[0], t),
                                   CatmullRom(p0[1], p1[1], p2[1], p3[1], t),
                                   CatmullRom(p0[2], p1[2], p2[2], p3[2], t));
            }

            public static Point Interpolate(Point a, Point b, Point c, Point d, float t)
            {

                return new Point(CatmullRom3D(a.position,
                                               b.position,
                                               c.position,
                                               d.position, t),

                                   CatmullRom3D(a.velocity,
                                                b.velocity,
                                                c.velocity,
                                                d.velocity, t),

                                    CatmullRom3D(a.tangent,
                                                 b.tangent,
                                                 c.tangent,
                                                 d.tangent, t),

                                    CatmullRom3D(a.normal,
                                                 b.normal,
                                                 c.normal,
                                                 d.normal, t),

                                   CatmullRomColor(a.color,
                                                   b.color,
                                                   c.color,
                                                   d.color, t),

                                   CatmullRom(a.thickness,
                                              b.thickness,
                                              c.thickness,
                                              d.thickness, t),

                                   CatmullRom(a.life,
                                              b.life,
                                              c.life,
                                              d.life, t)
                                );
            }

            public static Point operator +(Point p1, Point p2)
            {
                return new Point(p1.position + p2.position,
                                 p1.velocity + p2.velocity,
                    p1.tangent + p2.tangent,
                    p1.normal + p2.normal,
                                 p1.color + p2.color,
                                 p1.thickness + p2.thickness,
                                 p1.life + p2.life);
            }
            public static Point operator -(Point p1, Point p2)
            {
                return new Point(p1.position - p2.position,
                                 p1.velocity - p2.velocity,
                    p1.tangent - p2.tangent,
                    p1.normal - p2.normal,
                                 p1.color - p2.color,
                                 p1.thickness - p2.thickness,
                                 p1.life - p2.life);
            }
        }


        #region Properties

        [Header("Overall")]

        [Tooltip("是否以sceneview的camera基础构建")]
        public bool baseOnSceneViewCamera = false;

        [Tooltip("Whether to use world or local space to generate and simulate the trail.")]
        public Space space = Space.World;
        [Tooltip("Whether to use regular time.")]
        public Timescale timescale = Timescale.Normal;
        [Tooltip("How to align the trail geometry: facing the camera (view) of using the transform's rotation (local).")]
        public TrailAlignment alignment = TrailAlignment.View;
        [Tooltip("Thickness multiplier, in meters.")]
        public float thickness = 0.1f;
        [Tooltip("Amount of smoothing iterations applied to the trail shape.")]
        [Range(1, 8)]
        public int smoothness = 1;
        [Tooltip("Calculate accurate thickness at sharp corners.")]
        public bool highQualityCorners = false;
        [Range(0, 12)]
        public int cornerRoundness = 5;

        [Header("Lenght")]

        [Tooltip("How should the thickness of the curve evolve over its lenght. The horizontal axis is normalized lenght (in the [0,1] range) and the vertical axis is a thickness multiplier.")]
        public AnimationCurve thicknessOverLenght = AnimationCurve.Linear(0, 1, 0, 1);    /**< maps trail length to thickness.*/
        [Tooltip("How should vertex color evolve over the trail's length.")]
        public Gradient colorOverLenght = new Gradient();

        [Header("Time")]

        [Tooltip("How should the thickness of the curve evolve with its lifetime. The horizontal axis is normalized lifetime (in the [0,1] range) and the vertical axis is a thickness multiplier.")]
        public AnimationCurve thicknessOverTime = AnimationCurve.Linear(0, 1, 0, 1);  /**< maps trail lifetime to thickness.*/
        [Tooltip("How should vertex color evolve over the trail's lifetime.")]
        public Gradient colorOverTime = new Gradient();

        [Header("Emission")]

        public bool emit = true;
        [Tooltip("Initial thickness of trail points when they are first spawned.")]
        public float initialThickness = 1; /**< initial speed of trail, in world space. */
        [Tooltip("Initial color of trail points when they are first spawned.")]
        public Color initialColor = Color.white; /**< initial color of trail, in world space. */
        [Tooltip("Initial velocity of trail points when they are first spawned.")]
        public Vector3 initialVelocity = Vector3.zero; /**< initial speed of trail, in world space. */
        [Tooltip("Minimum amount of time (in seconds) that must pass before spawning a new point.")]
        public float timeInterval = 0.025f;
        [Tooltip("Minimum distance (in meters) that must be left between consecutive points in the trail.")]
        public float minDistance = 0.025f;
        [Tooltip("Duration of the trail (in seconds).")]
        public float time = 2f;

        [Header("Physics")]

        [Tooltip("Toggles trail physics.")]
        public bool enablePhysics = false;
        [Tooltip("Amount of seconds pre-simulated before the trail appears. Useful when you want a trail to be already simulating when the game starts.")]
        public float warmup = 0;               /**< simulation warmup seconds.*/
        [Tooltip("Gravity affecting the trail.")]
        public Vector3 gravity = Vector3.zero;  /**< gravity applied to the trail, in world space. */
        [Tooltip("Amount of speed transferred from the transform to the trail. 0 means no velocity is transferred, 1 means 100% of the velocity is transferred.")]
        [Range(0, 1)]
        public float inertia = 0;               /**< amount of GameObject velocity transferred to the trail.*/
        [Tooltip("Amount of temporal smoothing applied to the velocity transferred from the transform to the trail.")]
        [Range(0, 1)]
        public float velocitySmoothing = 0.75f;     /**< velocity smoothing amount.*/
        [Tooltip("Amount of damping applied to the trail's velocity. Larger values will slow down the trail more as time passes.")]
        [Range(0, 1)]
        public float damping = 0.75f;               /**< velocity damping amount.*/

        [Header("Rendering")]

        public Material[] materials = new Material[] { null };
        public UnityEngine.Rendering.ShadowCastingMode castShadows = UnityEngine.Rendering.ShadowCastingMode.On;
        public bool receiveShadows = true;
        public bool useLightProbes = true;

        [Header("Texture")]

        [Tooltip("How to apply the texture over the trail: stretch it all over its lenght, or tile it.")]
        public TextureMode textureMode = TextureMode.Stretch;
        [Tooltip("When the texture mode is set to 'Tile', defines the width of each tile.")]
        public float uvFactor = 1;
        [Tooltip("When the texture mode is set to 'Tile', defines where to begin tiling from: 0 means the start of the trail, 1 means the end.")]
        [Range(0, 1)]
        public float tileAnchor = 1;

        [Header("UVFlow")]

        public float uvFlowX = 0;
        public float uvFlowY = 0;

        public event System.Action onUpdatePoints;

        public Camera curCamera;
        [HideInInspector]
        public List<Point> points = new List<Point>();



        #endregion


        private List<Point> renderablePoints = new List<Point>();
        private List<int> discontinuities = new List<int>();

        private Mesh mesh_;
        private Vector3 velocity = Vector3.zero;
        private Vector3 prevPosition;
        private float speed = 0;
        private float accumTime = 0;

        private List<Vector3> vertices = new List<Vector3>();
        private List<Vector3> normals = new List<Vector3>();
        private List<Vector4> tangents = new List<Vector4>();
        private List<Vector2> uvs = new List<Vector2>();
        private List<Color> vertColors = new List<Color>();
        private List<int> tris = new List<int>();

        private float DeltaTime
        {
            get { return timescale == Timescale.Unscaled ? Time.unscaledDeltaTime : Time.deltaTime; }
        }

        private float FixedDeltaTime
        {
            get { return timescale == Timescale.Unscaled ? Time.fixedUnscaledDeltaTime : Time.fixedDeltaTime; }
        }


        public Mesh mesh
        {
            get { return mesh_; }
        }




        #region Unity Mono

        public void Awake()
        {
            Warmup();
            this.InitJobifyVariables();
        }

        void OnEnable()
        {
            points.Clear();
            // initialize previous position, for correct velocity estimation in the first frame:
            prevPosition = transform.position;
            velocity = Vector3.zero;

            // create a new mesh for the trail:
            mesh_ = new Mesh();
            mesh_.name = "ara_trail_mesh";
            mesh_.MarkDynamic();
        }


        private void Update()
        {
            #region 源代码

            //UpdateVelocity();

            //EmissionStep(DeltaTime);

            //SnapLastPointToTransform();

            //UpdatePointsLifecycle();

            //if (onUpdatePoints != null)
            //    onUpdatePoints();
            #endregion

            if (!this.mUpdateJobHandle.IsCompleted)
                return;

            FillJobifyVariables();

            UpdateVelocityJob updateVelocityJob = new UpdateVelocityJob
            {
                mHeadArray = this.mHeadArray,
                //mTranArray = this.mTransfromArray
            };

            this.mUpdateJobHandle = updateVelocityJob.Schedule(this.mUpdateJobHandle);

            EmissionStepJob emissionStepJob = new EmissionStepJob
            {
                mHeadArray = this.mHeadArray,
                //mTranArray = this.mTransfromArray,
                mPoints = this.mPointList
            };

            this.mUpdateJobHandle = emissionStepJob.Schedule(this.mUpdateJobHandle);

            SnapLastPointToTransformJob snapLastPointJob = new SnapLastPointToTransformJob
            {
                mHeadArray = this.mHeadArray,
                //mTranArray = this.mTransfromArray,
                mPoints = this.mPointList
            };

            this.mUpdateJobHandle = snapLastPointJob.Schedule(this.mUpdateJobHandle);

            UpdatePointsLifecycleJob updatePointsLifecycle = new UpdatePointsLifecycleJob
            {
                PointList = this.mPointList,
                mHeadArray = this.mHeadArray
            };

            this.mUpdateJobHandle = updatePointsLifecycle.Schedule(this.mUpdateJobHandle);

           
            this.mUpdateJobHandle.Complete();
            OutputJobResult();


            #region abandoned
            /*
NativeList<Point> rPoint = new NativeList<Point>(1, Allocator.TempJob);
for (int i = 0; i < this.points.Count; i++)
{
    rPoint.Add(this.points[i]);
}

EmissionStepJob emissionStepJob = new EmissionStepJob
{
    accumTime = this.accumTime,
    time = this.time,
    timeInterval = this.timeInterval,
    emit = this.emit,
    space = this.space,
    localPosition = this.transform.localPosition,
    position = this.transform.position,
    minDistance = this.minDistance,
    points = rPoint,
    initialVelocity = this.initialVelocity,
    inertia = this.inertia,
    tangent = this.transform.right,
    normal = this.transform.forward,
    initialColor = this.initialColor,
    initialThickness = this.initialThickness,
    DeltaTime = this.DeltaTime,
    smoothness = this.smoothness
};
emissionStepJob.Schedule().Complete();

this.points.Clear();
for (int i = 0; i < emissionStepJob.points.Length; i++)
{
    this.points.Add(emissionStepJob.points[i]);
}
rPoint.Dispose();


NativeList<Point> pointList = new NativeList<Point>(1, Allocator.TempJob);

for (int i = 0; i < this.points.Count; i++)
{
    pointList.Add(this.points[i]);
}


UpdatePointsLifecycleJob physicsStepJob = new UpdatePointsLifecycleJob
{
    PointList = pointList,
    DeltaTime = this.DeltaTime,
    smoothness = this.smoothness
};

this.mUpdateJobHandle = physicsStepJob.Schedule(pointList.Length, DESIRED_JOB_SIZE);
this.mUpdateJobHandle.Complete();

for (int i = 0; i < this.points.Count; i++)
{
    this.points[i] = pointList[i];
}
pointList.Dispose();

*/


            #endregion
        }


        private void FixedUpdate()
        {
            if (!this.mLateUpdateJobHandle.IsCompleted)
                return;
            if (!enablePhysics)
                return;

            //PhysicsStep(FixedDeltaTime);

            //if (!this.mFixUpdateJobHandle.IsCompleted)
            //{
            //    return;
            //}
            //this.mFixUpdateJobHandle.Complete();

            if(this.mPointList.Length > 0)
            {

                //NativeList<Point> pointList = new NativeList<Point>(1, Allocator.TempJob);

                //for (int i = 0; i < this.points.Count; i++)
                //{
                //    pointList.Add(this.points[i]);
                //}

                //PhysicsStepJob physicsStepJob = new PhysicsStepJob
                //{
                //    PointList = pointList,
                //    gravity = this.gravity,
                //    velocity_scale = Mathf.Pow(1 - Mathf.Clamp01(damping), FixedDeltaTime),
                //    timestep = FixedDeltaTime
                //};

                //this.mFixUpdateJobHandle = physicsStepJob.Schedule(pointList.Length, DESIRED_JOB_SIZE);
                //this.mFixUpdateJobHandle.Complete();

                PhysicsStepIJob physicsStepIJob = new PhysicsStepIJob
                {
                    PointList = mPointList,
                    mHeadArray = this.mHeadArray
                };

                physicsStepIJob.Schedule().Complete();
            }


        }

        private void LateUpdate()
        {

//#if UNITY_EDITOR
//            if (!baseOnSceneViewCamera)
//            {
//                if (curCamera != null)
//                    UpdateTrailMesh(curCamera);
//                else if (Camera.main != null)
//                    UpdateTrailMesh(Camera.main);
//            }
//            else
//            {
//                if (SceneView.lastActiveSceneView != null && SceneView.lastActiveSceneView.camera != null)
//                    UpdateTrailMesh(SceneView.lastActiveSceneView.camera);
//            }
//#else
//            if(curCamera != null)
//                UpdateTrailMesh(curCamera);
//            else if(Camera.main != null)
//                UpdateTrailMesh(Camera.main);
//#endif

#if UNITY_EDITOR
            if (!baseOnSceneViewCamera)
            {
                if (curCamera != null)
                    LateUpdateJobify();
                else if (Camera.main != null)
                    LateUpdateJobify();
            }
            else
            {
                if (SceneView.lastActiveSceneView != null && SceneView.lastActiveSceneView.camera != null)
                    LateUpdateJobify();
            }
#else
            if(curCamera != null)
                LateUpdateJobify();
            else if(Camera.main != null)
                LateUpdateJobify();
#endif



        }


        void OnDisable()
        {
            points.Clear();
            this.mPointList.Clear();
            // destroy both the trail mesh and the hidden renderer object:
            DestroyImmediate(mesh_);
            DestoryTrailGoc();
        }

        private void OnDestroy()
        {
            DestoryTrailGoc();
            this.ClearJobifyVariables();
        }


        public void OnValidate()
        {
            time = Mathf.Max(time, epsilon);
            warmup = Mathf.Max(0, warmup);
        }


        #endregion




        #region Origin Methods

        /// <summary>
        /// Removes all points in the trail, effectively removing any rendered trail segments.
        /// </summary>
        public void Clear()
        {
            points.Clear();
        }

        private void UpdateVelocity()
        {

            if (DeltaTime > 0)
            {
                velocity = Vector3.Lerp((transform.position - prevPosition) / DeltaTime, velocity, velocitySmoothing);
                speed = velocity.magnitude;
            }
            prevPosition = transform.position;

        }


        private void EmissionStep(float time)
        {
            // Acumulate the amount of time passed:
            accumTime += time;

            // If enough time has passed since the last emission (>= timeInterval), consider emitting new points.
            if (accumTime >= timeInterval)
            {
                if (emit)
                {
                    // Select the emission position, depending on the simulation space:
                    Vector3 position = space == Space.Self ? transform.localPosition : transform.position;
                    // If there's at least 1 point and it is not far enough from the current position, don't spawn any new points this frame.
                    if (points.Count <= 1 || Vector3.Distance(position, points[points.Count - 2].position) >= minDistance)
                    {
                        EmitPoint(position);
                        accumTime = 0;
                    }
                }
            }

        }

        private void Warmup()
        {
            if (!Application.isPlaying || !enablePhysics)
                return;

            float simulatedTime = warmup;

            while (simulatedTime > FixedDeltaTime)
            {

                PhysicsStep(FixedDeltaTime);

                EmissionStep(FixedDeltaTime);

                SnapLastPointToTransform();

                UpdatePointsLifecycle();

                if (onUpdatePoints != null)
                    onUpdatePoints();

                simulatedTime -= FixedDeltaTime;
            }
        }

        private void PhysicsStep(float timestep)
        {

            float velocity_scale = Mathf.Pow(1 - Mathf.Clamp01(damping), timestep);

            for (int i = 0; i < points.Count; ++i)
            {

                Point point = points[i];

                // apply gravity and external forces:
                point.velocity += gravity * timestep;
                point.velocity *= velocity_scale;

                // integrate velocity:
                point.position += point.velocity * timestep;

                points[i] = point;
            }
        }




        /// <summary>
        /// Spawns a new point in the trail.
        /// </summary>
        /// <param name="position"></param>
        public void EmitPoint(Vector3 position)
        {
            points.Add(new Point(position, initialVelocity + velocity * inertia, transform.right, transform.forward, initialColor, initialThickness, time));
        }


        /// <summary>
        /// Makes sure the first point is always at the transform's center, and that its orientation matches it.
        /// </summary>
        private void SnapLastPointToTransform()
        {

            // Last point always coincides with transform:
            if (points.Count > 0)
            {

                Point lastPoint = points[points.Count - 1];

                // if we are not emitting, the last point is a discontinuity.
                if (!emit)
                    lastPoint.discontinuous = true;

                // if the point is discontinuous, move and orient it according to the transform.
                if (!lastPoint.discontinuous)
                {
                    lastPoint.position = space == Space.Self ? transform.localPosition : transform.position;
                    lastPoint.normal = transform.forward;
                    lastPoint.tangent = transform.right;
                }

                points[points.Count - 1] = lastPoint;
            }
        }

        /// <summary>
        /// Updated trail lifetime and removes dead points.
        /// </summary>
        private void UpdatePointsLifecycle()
        {

            for (int i = points.Count - 1; i >= 0; --i)
            {

                Point point = points[i];
                point.life -= DeltaTime;
                points[i] = point;

                if (point.life <= 0)
                {
                    // Unsmoothed trails delete points as soon as they die.
                    if (smoothness <= 1)
                    {
                        points.RemoveAt(i);
                    }
                    // Smoothed trails however, should wait until the next 2 points are dead too. This ensures spline continuity.
                    else
                    {
                        if (points[Mathf.Min(i + 1, points.Count - 1)].life <= 0 &&
                            points[Mathf.Min(i + 2, points.Count - 1)].life <= 0)
                            points.RemoveAt(i);
                    }

                }
            }
        }

        /// <summary>
        /// Clears all mesh data: vertices, normals, tangents, etc. This is called at the beginning of UpdateTrailMesh().
        /// </summary>
        private void ClearMeshData()
        {

            mesh_.Clear();
            vertices.Clear();
            normals.Clear();
            tangents.Clear();
            uvs.Clear();
            vertColors.Clear();
            tris.Clear();

        }

        /// <summary>
        /// Applies vertex, normal, tangent, etc. data to the mesh. Called at the end of UpdateTrailMesh()
        /// </summary>
        private void CommitMeshData()
        {

            mesh_.SetVertices(vertices);
            mesh_.SetNormals(normals);
            mesh_.SetTangents(tangents);
            mesh_.SetColors(vertColors);
            mesh_.SetUVs(0, uvs);
            mesh_.SetTriangles(tris, 0, true);

        }

        protected bool IsSameMaterial(Material m1, Material m2)
        {
            if (m1.shader.name != m2.shader.name)
                return false;
            else if (m1.GetTexture("_BaseMap") != m2.GetTexture("_BaseMap"))
                return false;
            else
                return true;
        }
         
        protected GameObject m_TrailGoc = null;

        /// <summary>
        /// Asks Unity to render the trail mesh.
        /// </summary>
        /// <param name="cam"></param>
        private void RenderMesh(Camera cam)
        {
            for (int i = 0; i < materials.Length; ++i)
            {
                if (materials[i] == null)
                    continue;

                materials[i].EnableKeyword("_UV_FLOW_ON");
                materials[i].SetVector("_UVFlow", new Vector4(uvFlowX, uvFlowY, 0, 0));

                Graphics.DrawMesh(mesh_, space == Space.Self && transform.parent != null ? transform.parent.localToWorldMatrix : Matrix4x4.identity,
                                  materials[i], gameObject.layer, cam, 0, null, castShadows, receiveShadows, null, useLightProbes);
            }
        }

        protected void DestoryTrailGoc()
        {
            if (m_TrailGoc != null)
            {
                DestroyImmediate(m_TrailGoc);
                m_TrailGoc = null;
            }
        }

        /// <summary>
        /// Calculates the lenght of a trail segment.
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public float GetLenght(List<Point> input)
        {

            float lenght = 0;
            for (int i = 0; i < input.Count - 1; ++i)
                lenght += Vector3.Distance(input[i].position, input[i + 1].position);
            return lenght;

        }

        private List<Point> GetRenderablePoints(List<Point> input, int start, int end)
        {

            renderablePoints.Clear();

            if (smoothness <= 1)
            {
                for (int i = start; i <= end; ++i)
                    renderablePoints.Add(points[i]);
                return renderablePoints;
            }

            // calculate sample size in normalized coordinates:
            float samplesize = 1.0f / smoothness;

            for (int i = start; i < end; ++i)
            {

                // Extrapolate first and last curve control points:
                Point firstPoint = i == start ? points[start] + (points[start] - points[i + 1]) : points[i - 1];
                Point lastPoint = i == end - 1 ? points[end] + (points[end] - points[end - 1]) : points[i + 2];

                for (int j = 0; j < smoothness; ++j)
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


        /// <summary>
        /// Initializes the frame used to generate the locally aligned trail mesh.
        /// </summary>
        /// <param name="point"></param>
        /// <param name="nextPoint"></param>
        /// <returns></returns>
        private CurveFrame InitializeCurveFrame(Vector3 point, Vector3 nextPoint)
        {

            Vector3 tangent = nextPoint - point;

            // Calculate tangent proximity to the normal vector of the frame (transform.forward).
            float tangentProximity = Mathf.Abs(Vector3.Dot(tangent.normalized, transform.forward));

            // If both vectors are dangerously close, skew the tangent a bit so that a proper frame can be formed:
            if (Mathf.Approximately(tangentProximity, 1))
                tangent += transform.right * 0.01f;

            // Generate and return the frame:
            return new CurveFrame(point, transform.forward, transform.up, tangent);
        }

        /// <summary>
        ///  Updates the trail mesh to be seen from the camera passed to the function.
        /// </summary>
        /// <param name="cam"></param>
        private void UpdateTrailMesh(Camera cam)
        {

            ClearMeshData();

            // We need at least two points to create a trail mesh.
            if (points.Count > 1)
            {

                Vector3 localCamPosition = space == Space.Self && transform.parent != null ? transform.parent.InverseTransformPoint(cam.transform.position) : cam.transform.position;

                // get discontinuous point indices:
                discontinuities.Clear();
                for (int i = 0; i < points.Count; ++i)
                    if (points[i].discontinuous || i == points.Count - 1) discontinuities.Add(i);

                // generate mesh for each trail segment:
                int start = 0;
                for (int i = 0; i < discontinuities.Count; ++i)
                {
                    UpdateSegmentMesh(points, start, discontinuities[i], localCamPosition);
                    start = discontinuities[i] + 1;
                }

                CommitMeshData();

                RenderMesh(cam);
            }
        }

        /// <summary>
        /// Updates mesh for one trail segment:
        /// </summary>
        /// <param name="input"></param>
        /// <param name="start"></param>
        /// <param name="end"></param>
        /// <param name="localCamPosition"></param>
        private void UpdateSegmentMesh(List<Point> input, int start, int end, Vector3 localCamPosition)
        {

            // Get a list of the actual points to render: either the original, unsmoothed points or the smoothed curve.
            List<Point> trail = GetRenderablePoints(input, start, end);

            if (trail.Count > 1)
            {

                float lenght = Mathf.Max(GetLenght(trail), epsilon);
                float partialLenght = 0;
                float vCoord = textureMode == TextureMode.Stretch ? 0 : -uvFactor * lenght * tileAnchor;
                Vector4 texTangent = Vector4.zero;
                Vector2 uv = Vector2.zero;
                Color vertexColor;

                bool hqCorners = highQualityCorners && alignment != TrailAlignment.Local;

                // Initialize curve frame using the first two points to calculate the first tangent vector:
                CurveFrame frame = InitializeCurveFrame(trail[trail.Count - 1].position,
                                                        trail[trail.Count - 2].position);

                int va = 1;
                int vb = 0;

                int nextIndex;
                int prevIndex;
                Vector3 nextV;
                Vector3 prevV;
                Point curPoint;
                for (int i = trail.Count - 1; i >= 0; --i)
                {

                    curPoint = trail[i];
                    // Calculate next and previous point indices:
                    nextIndex = Mathf.Max(i - 1, 0);
                    prevIndex = Mathf.Min(i + 1, trail.Count - 1);

                    // Calculate next and previous trail vectors:
                    nextV = trail[nextIndex].position - curPoint.position;
                    prevV = curPoint.position - trail[prevIndex].position;
                    float sectionLength = nextV.magnitude;

                    nextV.Normalize();
                    prevV.Normalize();

                    // Calculate tangent vector:
                    Vector3 tangent = alignment == TrailAlignment.Local ? curPoint.tangent : (nextV + prevV);
                    tangent.Normalize();

                    // Calculate normal vector:
                    Vector3 normal = curPoint.normal;
                    if (alignment != TrailAlignment.Local)
                        normal = alignment == TrailAlignment.View ? localCamPosition - curPoint.position : frame.Transport(tangent, curPoint.position);
                    normal.Normalize();

                    // Calculate bitangent vector:
                    Vector3 bitangent = alignment == TrailAlignment.Velocity ? frame.bitangent : Vector3.Cross(tangent, normal);
                    bitangent.Normalize();

                    // Calculate this point's normalized (0,1) lenght and life.
                    float normalizedLength = partialLenght / lenght;
                    float normalizedLife = Mathf.Clamp01(1 - curPoint.life / time);
                    partialLenght += sectionLength;

                    // Calulate vertex color:
                    vertexColor = curPoint.color *
                                  colorOverTime.Evaluate(normalizedLife) *
                                  colorOverLenght.Evaluate(normalizedLength);

                    // Update vcoord:
                    vCoord += uvFactor * (textureMode == TextureMode.Stretch ? sectionLength / lenght : sectionLength);

                    // Calulate final thickness:
                    float sectionThickness = thickness * curPoint.thickness * thicknessOverTime.Evaluate(normalizedLife) * thicknessOverLenght.Evaluate(normalizedLength);

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
                        if (cornerRoundness > 0)
                        {

                            prevSectionBitangent = i == trail.Count - 1 ? -bitangent : Vector3.Cross(prevV, Vector3.Cross(bitangent, tangent)).normalized;

                            // Calculate "elbow" angle:
                            curvatureSign = (i == 0 || i == trail.Count - 1) ? 1 : Mathf.Sign(Vector3.Dot(nextV, -prevSectionBitangent));
                            float angle = (i == 0 || i == trail.Count - 1) ? Mathf.PI : Mathf.Acos(Mathf.Clamp(Vector3.Dot(nextSectionBitangent, prevSectionBitangent), -1, 1));

                            // Prepare a quaternion for incremental rotation of the corner vector:
                            q = Quaternion.AngleAxis(Mathf.Rad2Deg * angle / cornerRoundness, normal * curvatureSign);
                            corner = prevSectionBitangent * sectionThickness * curvatureSign;
                        }

                        // Calculate correct thickness by projecting corner bitangent onto the next section bitangent. This prevents "squeezing"
                        if (nextSectionBitangent.sqrMagnitude > 0.1f)
                            correctedThickness = sectionThickness / Mathf.Max(Vector3.Dot(bitangent, nextSectionBitangent), 0.15f);

                    }


                    // Append straight section mesh data:

                    if (hqCorners && cornerRoundness > 0)
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

                    if (i < trail.Count - 1)
                    {

                        int vc = vertices.Count - 1;

                        tris.Add(vc);
                        tris.Add(va);
                        tris.Add(vb);

                        tris.Add(vb);
                        tris.Add(vc - 1);
                        tris.Add(vc);
                    }

                    va = vertices.Count - 1;
                    vb = vertices.Count - 2;

                    // Append smooth corner mesh data:
                    if (hqCorners && cornerRoundness > 0)
                    {

                        for (int p = 0; p <= cornerRoundness; ++p)
                        {

                            vertices.Add(curPoint.position + corner);
                            normals.Add(-normal);
                            tangents.Add(texTangent);
                            vertColors.Add(vertexColor);
                            uv.Set(vCoord, curvatureSign > 0 ? 0 : 1);
                            uvs.Add(uv);

                            int vc = vertices.Count - 1;

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
            }

        }


        #endregion


        #region Jobify Part

        public const int DESIRED_JOB_SIZE = 16;

        private JobHandle mFixUpdateJobHandle;
        private JobHandle mUpdateJobHandle;
        private JobHandle mLateUpdateJobHandle;


        private NativeArray<Head> mHeadArray;
        private NativeList<Point> mPointList;
        private TransformAccessArray mTransfromArray;    // Length = 1   this.transform

        public NativeList<int> discontinuitiesNative;
        public NativeList<Vector3> verticesNative;
        public NativeList<Vector3> tangentsNative;
        public NativeList<Color> vertColorsNative;
        public NativeList<Vector3> uvsNative;
        public NativeList<int> trisNative;
        public NativeList<Vector3> normalsNative;

        //public NativeArray<Keyframe> mLengthThickCurve;
        //public NativeArray<GradientColorKey> mLengthThickColorKeys;
        //public NativeArray<GradientAlphaKey> mLengthThickAlphaKeys;
        //public NativeArray<Keyframe> mTimeThickCurve;
        //public NativeArray<GradientColorKey> mTimeThickColorKeys;
        //public NativeArray<GradientAlphaKey> mTimeThickAlphaKeys;


        private void InitJobifyVariables()
        {
            this.mHeadArray = new NativeArray<Head>(1, Allocator.Persistent);
            this.mPointList = new NativeList<Point>(Allocator.Persistent);
            this.mTransfromArray = new TransformAccessArray();

            this.discontinuitiesNative = new NativeList<int>(Allocator.Persistent);
            this.verticesNative = new NativeList<Vector3>(Allocator.Persistent);
            this.tangentsNative = new NativeList<Vector3>(Allocator.Persistent);
            this.vertColorsNative = new NativeList<Color>(Allocator.Persistent);
            this.uvsNative = new NativeList<Vector3>(Allocator.Persistent);
            this.trisNative = new NativeList<int>(Allocator.Persistent);
            this.normalsNative = new NativeList<Vector3>(Allocator.Persistent);


            //this.mLengthThickCurve = new NativeArray<Keyframe>(this.thicknessOverLenght.keys, Allocator.Persistent);
            //this.mLengthThickColorKeys = new NativeArray<GradientColorKey>(this.colorOverLenght.colorKeys, Allocator.Persistent);
            //this.mLengthThickAlphaKeys = new NativeArray<GradientAlphaKey>(this.colorOverLenght.alphaKeys, Allocator.Persistent);

            //this.mTimeThickCurve = new NativeArray<Keyframe>(this.thicknessOverTime.keys, Allocator.Persistent);
            //this.mTimeThickColorKeys = new NativeArray<GradientColorKey>(this.colorOverTime.colorKeys, Allocator.Persistent);
            //this.mTimeThickAlphaKeys = new NativeArray<GradientAlphaKey>(this.colorOverTime.alphaKeys, Allocator.Persistent);

        }


        private void FillJobifyVariables()
        {

            Camera tempCamera = null;

#if UNITY_EDITOR
            if (!baseOnSceneViewCamera)
            {
                if (curCamera != null)
                    tempCamera = curCamera;
                else if (Camera.main != null)
                    tempCamera = Camera.main;
            }
            else
            {
                if (SceneView.lastActiveSceneView != null && SceneView.lastActiveSceneView.camera != null)
                    tempCamera = SceneView.lastActiveSceneView.camera;
            }
#else
            if(curCamera != null)
                tempCamera = curCamera;
            else if(Camera.main != null)
                tempCamera = Camera.main;
#endif


            this.mHeadArray[0] = new Head
            {
                localPosition = this.transform.localPosition,
                position = this.transform.position,
                tangent = this.transform.right,
                normal = this.transform.forward,
                up = this.transform.up,

                prevPosition = this.prevPosition,
                DeltaTime = this.DeltaTime,
                velocity = this.velocity,
                speed = this.speed,
                velocitySmoothing = this.velocitySmoothing,
                accumTime = this.accumTime,
                time = this.time,
                timeInterval = this.timeInterval,
                emit = this.emit,
                space = this.space,
                minDistance = this.minDistance,
                initialVelocity = this.initialVelocity,
                inertia = this.inertia,
                initialColor = this.initialColor,
                initialThickness = this.initialThickness,
                smoothness = this.smoothness,
                gravity = this.gravity,
                damping = this.damping,
                timestep = this.FixedDeltaTime,

                localCamPosition = space == Space.Self && transform.parent != null ? transform.parent.InverseTransformPoint(tempCamera.transform.position) : tempCamera.transform.position,
                textureMode = this.textureMode,
                uvFactor = this.uvFactor,
                tileAnchor = this.tileAnchor,
                alignment = this.alignment,
                cornerRoundness = this.cornerRoundness,
                thickness = this.thickness
            };

        }


        private void OutputJobResult()
        {
            Head rHead = this.mHeadArray[0];

            this.prevPosition = rHead.prevPosition;
            this.velocity = rHead.velocity;
            this.speed = rHead.speed;
            this.accumTime = rHead.accumTime;
        }


        private void ClearJobifyVariables()
        {
            this.mFixUpdateJobHandle.Complete();
            this.mUpdateJobHandle.Complete();

            if (this.mHeadArray.IsCreated)
                this.mHeadArray.Dispose();
            if (this.mPointList.IsCreated)
                this.mPointList.Dispose();
            if (this.mTransfromArray.isCreated)
                this.mTransfromArray.Dispose();

            if (this.discontinuitiesNative.IsCreated)
                this.discontinuitiesNative.Dispose();
            if (this.verticesNative.IsCreated)
                this.verticesNative.Dispose();
            if (this.tangentsNative.IsCreated)
                this.tangentsNative.Dispose();
            if (this.vertColorsNative.IsCreated)
                this.vertColorsNative.Dispose();
            if (this.uvsNative.IsCreated)
                this.uvsNative.Dispose();
            if (this.trisNative.IsCreated)
                this.trisNative.Dispose();
            if (this.normalsNative.IsCreated)
                this.normalsNative.Dispose();

            //if (this.mLengthThickCurve.IsCreated)
            //{
            //    this.mLengthThickCurve.Dispose();
            //}
            //if (this.mLengthThickColorKeys.IsCreated)
            //{
            //    this.mLengthThickColorKeys.Dispose();
            //}
            //if (this.mLengthThickAlphaKeys.IsCreated)
            //{
            //    this.mLengthThickAlphaKeys.Dispose();
            //}


            //if (this.mTimeThickCurve.IsCreated)
            //{
            //    this.mTimeThickCurve.Dispose();
            //}
            //if (this.mTimeThickColorKeys.IsCreated)
            //{
            //    this.mTimeThickColorKeys.Dispose();
            //}
            //if (this.mTimeThickAlphaKeys.IsCreated)
            //{
            //    this.mTimeThickAlphaKeys.Dispose();
            //}




        }

        private void LateUpdateJobify()
        {



            /*
            UpdateTrailMeshJob updateTrailMeshJob = new UpdateTrailMeshJob
            {
                mPoints = this.mPointList,
                mHeadArray = this.mHeadArray,
                discontinuities = this.discontinuitiesNative,
                vertices = this.verticesNative,
                tangents = this.tangentsNative,
                vertColors = this.vertColorsNative,
                uvs = this.uvsNative,
                tris = this.trisNative,
                normals = this.normalsNative,

                mLengthThickCurve = this.mLengthThickCurve,
                mLengthThickColorKeys = this.mLengthThickColorKeys,
                mLengthThickAlphaKeys = this.mLengthThickAlphaKeys,

                mTimeThickCurve = this.mTimeThickCurve,
                mTimeThickColorKeys = this.mTimeThickColorKeys,
                mTimeThickAlphaKeys = this.mTimeThickAlphaKeys

            };

            updateTrailMeshJob.Schedule().Complete();
            */


            NativeList<float> normalizedLengthList = new NativeList<float>(Allocator.TempJob);
            NativeList<float> normalizedLifeList = new NativeList<float>(Allocator.TempJob);

            UpdateTrailMeshJob_PartA updateTrailMeshJobA = new UpdateTrailMeshJob_PartA
            {
                mPoints = this.mPointList,
                mHeadArray = this.mHeadArray,
                discontinuities = this.discontinuitiesNative,
                normalizedLengthList = normalizedLengthList,
                normalizedLifeList = normalizedLifeList,
            };

            updateTrailMeshJobA.Schedule().Complete();


            NativeList<Color> lengthThickColor = new NativeList<Color>(Allocator.TempJob);
            NativeList<Color> timeThickColor = new NativeList<Color>(Allocator.TempJob);
            NativeList<float> lengthThickCurve = new NativeList<float>(Allocator.TempJob);
            NativeList<float> timeThickCurve = new NativeList<float>(Allocator.TempJob);

            for (int i = 0; i < normalizedLifeList.Length; i++)
            {
                Color timeColor = this.colorOverTime.Evaluate(normalizedLifeList[i]);
                timeThickColor.Add(timeColor);
                float timeCurveValue = this.thicknessOverTime.Evaluate(normalizedLifeList[i]);
                timeThickCurve.Add(timeCurveValue);
            }

            for (int i = 0; i < normalizedLengthList.Length; i++)
            {
                Color lengthColor = this.colorOverLenght.Evaluate(normalizedLengthList[i]);
                lengthThickColor.Add(lengthColor);
                float lengthCurveValue = this.thicknessOverLenght.Evaluate(normalizedLengthList[i]);
                lengthThickCurve.Add(lengthCurveValue);
            }


            normalizedLengthList.Dispose();
            normalizedLifeList.Dispose();


            UpdateTrailMeshJob_PartB updateTrailMeshJobB = new UpdateTrailMeshJob_PartB
            {
                mPoints = this.mPointList,
                mHeadArray = this.mHeadArray,
                discontinuities = this.discontinuitiesNative,

                vertices = this.verticesNative,
                tangents = this.tangentsNative,
                vertColors = this.vertColorsNative,
                uvs = this.uvsNative,
                tris = this.trisNative,
                normals = this.normalsNative,

                lengthThickColor = lengthThickColor,
                timeThickColor = timeThickColor,
                lengthThickCurve = lengthThickCurve,
                timeThickCurve = timeThickCurve
            };

            updateTrailMeshJobB.Schedule().Complete();



            lengthThickColor.Dispose();
            lengthThickCurve.Dispose();
            timeThickColor.Dispose();
            timeThickCurve.Dispose();



            this.ClearMeshData();

            for (int i = 0; i < verticesNative.Length; i++)
            {
                vertices.Add(verticesNative[i]);
            }

            for (int i = 0; i < tangentsNative.Length; i++)
            {
                tangents.Add(tangentsNative[i]);
            }


            for (int i = 0; i < normalsNative.Length; i++)
            {
                normals.Add(normalsNative[i]);
            }

            for (int i = 0; i < vertColorsNative.Length; i++)
            {
                vertColors.Add(vertColorsNative[i]);
            }

            for (int i = 0; i < uvsNative.Length; i++)
            {
                uvs.Add(uvsNative[i]);
            }

            for (int i = 0; i < trisNative.Length; i++)
            {
                tris.Add(trisNative[i]);
            }

    

            mesh_.SetVertices(vertices);
            mesh_.SetNormals(normals);
            mesh_.SetTangents(tangents);
            mesh_.SetColors(vertColors);
            mesh_.SetUVs(0, uvs);
            mesh_.SetTriangles(tris, 0, true);


            RenderMesh(curCamera);


        }

        [BurstCompile]
        public struct Head
        {
            public Vector3 localPosition;
            public Vector3 position;
            public Vector3 tangent;
            public Vector3 normal;
            public Vector3 up;

            //UpdateVelocityJob
            public Vector3 prevPosition;
            public float DeltaTime;
            public Vector3 velocity;
            public float speed;
            public float velocitySmoothing;

            //EmissionStepJob
            public float accumTime;
            public float time;
            public float timeInterval;
            public bool emit;
            public Space space;
            public float minDistance;
            public Vector3 initialVelocity;
            //public Vector3 velocity;
            public float inertia;
            //public Vector3 normal;      //transform.forward
            //public Vector3 tangent;     //transform.right
            public Color initialColor;
            public float initialThickness;

            //SnapLastPointToTransform

            //UpdatePointsLifecycle
            public float smoothness;

            //PhysicsStepJob
            public Vector3 gravity;
            public float damping;
            public float timestep;

            //LateUpdate
            public Vector3 localCamPosition;
            public TextureMode textureMode;
            public float uvFactor;
            public float tileAnchor;
            public bool highQualityCorners;
            public TrailAlignment alignment;
            public int cornerRoundness;
            public float thickness;

        }
        




        [BurstCompile]
        public struct PhysicsStepIJob : IJob
        {
            public NativeList<Point> PointList;
            public NativeArray<Head> mHeadArray;
            //public Vector3 gravity;
            //public float velocity_scale;
            //public float timestep;

            public void Execute()
            {
                float velocity_scale = Mathf.Pow(1 - Mathf.Clamp01(mHeadArray[0].damping), mHeadArray[0].timestep);
                for (int i = 0; i < PointList.Length; ++i)
                {

                    Point point = PointList[i];

                    // apply gravity and external forces:
                    point.velocity += mHeadArray[0].gravity * mHeadArray[0].timestep;
                    point.velocity *= velocity_scale;

                    // integrate velocity:
                    point.position += point.velocity * mHeadArray[0].timestep;

                    PointList[i] = point;
                }
            }
        }

        [BurstCompile]
        public struct UpdateVelocityJob : IJob
        {
            //public Vector3 position;  // transform.position
            //public Vector3 prevPosition;
            //public float DeltaTime;
            //public Vector3 velocity;
            //public float speed;
            //public float velocitySmoothing;

            public NativeArray<Head> mHeadArray;
            public void Execute()
            {
                Head rHead = mHeadArray[0];
                if (rHead.DeltaTime > 0)
                {
                    rHead.velocity = Vector3.Lerp((rHead.position - rHead.prevPosition) / rHead.DeltaTime, rHead.velocity, rHead.velocitySmoothing);
                    rHead.speed = rHead.velocity.magnitude;
                }
                rHead.prevPosition = rHead.position;
                mHeadArray[0] = rHead;
            }
        }

        [BurstCompile]
        public struct EmissionStepJob : IJob
        {
            public NativeArray<Head> mHeadArray;
            public NativeList<Point> mPoints;

            public void Execute()
            {
                Head rHead = mHeadArray[0];

                // Acumulate the amount of time passed:
                rHead.accumTime += rHead.time;
                //Debug.Log($"joB accumTIme {rHead.accumTime}  {rHead.time}   {rHead.timeInterval}");
                // If enough time has passed since the last emission (>= timeInterval), consider emitting new points.
                if (rHead.accumTime >= rHead.timeInterval)
                {
                    if (rHead.emit)
                    {
                        // Select the emission position, depending on the simulation space:
                        Vector3 position = rHead.space == Space.Self ? rHead.localPosition : rHead.position;
                        // If there's at least 1 point and it is not far enough from the current position, don't spawn any new points this frame.
                        if (mPoints.Length <= 1 || Vector3.Distance(position, mPoints[mPoints.Length - 2].position) >= rHead.minDistance)
                        {
                            mPoints.Add(new Point(position, rHead.initialVelocity + rHead.velocity * rHead.inertia, rHead.tangent, rHead.normal, rHead.initialColor, rHead.initialThickness, rHead.time));
                            rHead.accumTime = 0;
                            //Debug.Log($"Tag {mPoints.Length}");
                        }
                    }
                }
                mHeadArray[0] = rHead;
                //Debug.Log($"JOb mHeadArray[0] { mHeadArray[0].accumTime}  { mHeadArray[0].time}   { mHeadArray[0].timeInterval}");
            }
        }

        [BurstCompile]
        public struct SnapLastPointToTransformJob : IJob
        {
            public NativeArray<Head> mHeadArray;
            public NativeList<Point> mPoints;
            public void Execute()
            {
                if (mPoints.Length > 0)
                {

                    Point lastPoint = mPoints[mPoints.Length - 1];

                    // if we are not emitting, the last point is a discontinuity.
                    if (!mHeadArray[0].emit)
                        lastPoint.discontinuous = true;

                    // if the point is discontinuous, move and orient it according to the transform.
                    if (!lastPoint.discontinuous)
                    {
                        lastPoint.position = mHeadArray[0].space == Space.Self ? mHeadArray[0].localPosition : mHeadArray[0].position;
                        lastPoint.normal = mHeadArray[0].normal;
                        lastPoint.tangent = mHeadArray[0].tangent;
                    }

                    mPoints[mPoints.Length - 1] = lastPoint;
                }
            }
        }

        [BurstCompile]
        public struct UpdatePointsLifecycleJob : IJob
        {
            public NativeList<Point> PointList;
            public NativeArray<Head> mHeadArray;


            //public void Execute(int index)
            //{
            //    Head rHead = mHeadArray[0];
            //    Point point = PointList[index];
            //    point.life -= rHead.DeltaTime;
            //    PointList[index] = point;

            //    if (point.life <= 0)
            //    {
            //        // Unsmoothed trails delete points as soon as they die.
            //        if (rHead.smoothness <= 1)
            //        {
            //            PointList.RemoveAt(index);
            //        }
            //        // Smoothed trails however, should wait until the next 2 points are dead too. This ensures spline continuity.
            //        else
            //        {
            //            if (PointList[Mathf.Min(index + 1, PointList.Length - 1)].life <= 0 &&
            //                PointList[Mathf.Min(index + 2, PointList.Length - 1)].life <= 0)
            //                PointList.RemoveAt(index);
            //        }
            //    }
            //}

            public void Execute()
            {
                for (int i = PointList.Length - 1; i >= 0; --i)
                {

                    Point point = PointList[i];
                    point.life -= mHeadArray[0].DeltaTime;
                    PointList[i] = point;

                    if (point.life <= 0)
                    {

                        // Unsmoothed trails delete points as soon as they die.
                        if (mHeadArray[0].smoothness <= 1)
                        {
                            PointList.RemoveAt(i);
                        }
                        // Smoothed trails however, should wait until the next 2 points are dead too. This ensures spline continuity.
                        else
                        {
                            if (PointList[Mathf.Min(i + 1, PointList.Length - 1)].life <= 0 &&
                                PointList[Mathf.Min(i + 2, PointList.Length - 1)].life <= 0)
                                PointList.RemoveAt(i);
                        }

                    }
                }
            }
        }

        //[BurstCompile]
        //public struct UpdateTrailMeshJob : IJob
        //{

        //    //public TransformAccessArray mCurCameraArray;
        //    public NativeList<Point> mPoints;
        //    public NativeArray<Head> mHeadArray;
        //    public NativeList<int> discontinuities;

        //    public NativeList<Vector3> vertices;
        //    public NativeList<Vector3> tangents;
        //    public NativeList<Color> vertColors;
        //    public NativeList<Vector3> uvs;
        //    public NativeList<int> tris;
        //    public NativeList<Vector3> normals;


        //    //[ReadOnly] public NativeArray<Keyframe> mLengthThickCurve;
        //    //[ReadOnly] public NativeArray<GradientColorKey> mLengthThickColorKeys;
        //    //[ReadOnly] public NativeArray<GradientAlphaKey> mLengthThickAlphaKeys;
        //    //[ReadOnly] public NativeArray<Keyframe> mTimeThickCurve;
        //    //[ReadOnly] public NativeArray<GradientColorKey> mTimeThickColorKeys;
        //    //[ReadOnly] public NativeArray<GradientAlphaKey> mTimeThickAlphaKeys;


        //    public void Execute()
        //    {
        //        ClearMeshData();

        //        // We need at least two points to create a trail mesh.
        //        if (mPoints.Length > 1)
        //        {

        //            //Vector3 localCamPosition = rHead.space == Space.Self && transform.parent != null ? transform.parent.InverseTransformPoint(cam.transform.position) : cam.transform.position;

        //            // get discontinuous point indices:
        //            discontinuities.Clear();
        //            for (int i = 0; i < mPoints.Length; ++i)
        //                if (mPoints[i].discontinuous || i == mPoints.Length - 1) discontinuities.Add(i);

        //            // generate mesh for each trail segment:
        //            int start = 0;
        //            for (int i = 0; i < discontinuities.Length; ++i)
        //            {
        //                UpdateSegmentMesh(mPoints, start, discontinuities[i], mHeadArray[0].localCamPosition);
        //                start = discontinuities[i] + 1;
        //            }

        //            //CommitMeshData();

        //            //RenderMesh(cam);
        //        }
        //    }


        //    private void UpdateSegmentMesh(NativeList<Point> input, int start, int end, Vector3 localCamPosition)
        //    {
        //        Head rHead = mHeadArray[0];
        //        // Get a list of the actual points to render: either the original, unsmoothed points or the smoothed curve.
        //        NativeList<Point> trail = GetRenderablePoints(input, start, end);

        //        if (trail.Length > 1)
        //        {

        //            float lenght = Mathf.Max(GetLenght(trail), 0.00001f);
        //            float partialLenght = 0;
        //            float vCoord = rHead.textureMode == TextureMode.Stretch ? 0 : -rHead.uvFactor * lenght * rHead.tileAnchor;
        //            Vector4 texTangent = Vector4.zero;
        //            Vector2 uv = Vector2.zero;
        //            Color vertexColor;

        //            bool hqCorners = rHead.highQualityCorners && rHead.alignment != TrailAlignment.Local;

        //            // Initialize curve frame using the first two points to calculate the first tangent vector:
        //            CurveFrame frame = InitializeCurveFrame(trail[trail.Length - 1].position,
        //                                                    trail[trail.Length - 2].position);

        //            int va = 1;
        //            int vb = 0;

        //            int nextIndex;
        //            int prevIndex;
        //            Vector3 nextV;
        //            Vector3 prevV;
        //            Point curPoint;
        //            for (int i = trail.Length - 1; i >= 0; --i)
        //            {

        //                curPoint = trail[i];
        //                // Calculate next and previous point indices:
        //                nextIndex = Mathf.Max(i - 1, 0);
        //                prevIndex = Mathf.Min(i + 1, trail.Length - 1);

        //                // Calculate next and previous trail vectors:
        //                nextV = trail[nextIndex].position - curPoint.position;
        //                prevV = curPoint.position - trail[prevIndex].position;
        //                float sectionLength = nextV.magnitude;

        //                nextV.Normalize();
        //                prevV.Normalize();

        //                // Calculate tangent vector:
        //                Vector3 tangent = rHead.alignment == TrailAlignment.Local ? curPoint.tangent : (nextV + prevV);
        //                tangent.Normalize();

        //                // Calculate normal vector:
        //                Vector3 normal = curPoint.normal;
        //                if (rHead.alignment != TrailAlignment.Local)
        //                    normal = rHead.alignment == TrailAlignment.View ? localCamPosition - curPoint.position : frame.Transport(tangent, curPoint.position);
        //                normal.Normalize();

        //                // Calculate bitangent vector:
        //                Vector3 bitangent = rHead.alignment == TrailAlignment.Velocity ? frame.bitangent : Vector3.Cross(tangent, normal);
        //                bitangent.Normalize();

        //                // Calculate this point's normalized (0,1) lenght and life.
        //                float normalizedLength = partialLenght / lenght;
        //                float normalizedLife = Mathf.Clamp01(1 - curPoint.life / rHead.time);
        //                partialLenght += sectionLength;

        //                // Calulate vertex color:
        //                //vertexColor = curPoint.color *
        //                //              colorOverTime.Evaluate(normalizedLife) *
        //                //              colorOverLenght.Evaluate(normalizedLength);
        //                //vertexColor = curPoint.color *
        //                //             UnityJobifyHelper.Gradient_Evaluate(mTimeThickColorKeys, mTimeThickAlphaKeys, GradientMode.Blend, normalizedLife) *
        //                //             UnityJobifyHelper.Gradient_Evaluate(mLengthThickColorKeys, mLengthThickAlphaKeys, GradientMode.Blend, normalizedLength);

        //                // Update vcoord:
        //                vCoord += rHead.uvFactor * (rHead.textureMode == TextureMode.Stretch ? sectionLength / lenght : sectionLength);

        //                // Calulate final thickness:
        //                //float sectionThickness = rHead.thickness * curPoint.thickness * thicknessOverTime.Evaluate(normalizedLife) * thicknessOverLenght.Evaluate(normalizedLength);
        //                //float sectionThickness = rHead.thickness * curPoint.thickness * UnityJobifyHelper.AnimationCurve_Evaluate(mTimeThickCurve, normalizedLife) * UnityJobifyHelper.AnimationCurve_Evaluate(mLengthThickCurve, normalizedLength);

        //                Quaternion q = Quaternion.identity;
        //                Vector3 corner = Vector3.zero;
        //                float curvatureSign = 0;
        //                float correctedThickness = sectionThickness;
        //                Vector3 prevSectionBitangent = bitangent;

        //                // High-quality corners: 
        //                if (hqCorners)
        //                {

        //                    Vector3 nextSectionBitangent = i == 0 ? bitangent : Vector3.Cross(nextV, Vector3.Cross(bitangent, tangent)).normalized;

        //                    // If round corners are enabled:
        //                    if (rHead.cornerRoundness > 0)
        //                    {

        //                        prevSectionBitangent = i == trail.Length - 1 ? -bitangent : Vector3.Cross(prevV, Vector3.Cross(bitangent, tangent)).normalized;

        //                        // Calculate "elbow" angle:
        //                        curvatureSign = (i == 0 || i == trail.Length - 1) ? 1 : Mathf.Sign(Vector3.Dot(nextV, -prevSectionBitangent));
        //                        float angle = (i == 0 || i == trail.Length - 1) ? Mathf.PI : Mathf.Acos(Mathf.Clamp(Vector3.Dot(nextSectionBitangent, prevSectionBitangent), -1, 1));

        //                        // Prepare a quaternion for incremental rotation of the corner vector:
        //                        q = Quaternion.AngleAxis(Mathf.Rad2Deg * angle / rHead.cornerRoundness, normal * curvatureSign);
        //                        corner = prevSectionBitangent * sectionThickness * curvatureSign;
        //                    }

        //                    // Calculate correct thickness by projecting corner bitangent onto the next section bitangent. This prevents "squeezing"
        //                    if (nextSectionBitangent.sqrMagnitude > 0.1f)
        //                        correctedThickness = sectionThickness / Mathf.Max(Vector3.Dot(bitangent, nextSectionBitangent), 0.15f);

        //                }


        //                // Append straight section mesh data:

        //                if (hqCorners && rHead.cornerRoundness > 0)
        //                {

        //                    // bitangents are slightly asymmetrical in case of high-quality round or sharp corners:
        //                    if (curvatureSign > 0)
        //                    {
        //                        vertices.Add(curPoint.position + prevSectionBitangent * sectionThickness);
        //                        vertices.Add(curPoint.position - bitangent * correctedThickness);
        //                    }
        //                    else
        //                    {
        //                        vertices.Add(curPoint.position + bitangent * correctedThickness);
        //                        vertices.Add(curPoint.position - prevSectionBitangent * sectionThickness);
        //                    }

        //                }
        //                else
        //                {
        //                    vertices.Add(curPoint.position + bitangent * correctedThickness);
        //                    vertices.Add(curPoint.position - bitangent * correctedThickness);
        //                }

        //                normals.Add(-normal);
        //                normals.Add(-normal);

        //                texTangent = -bitangent;
        //                texTangent.w = 1;
        //                tangents.Add(texTangent);
        //                tangents.Add(texTangent);

        //                vertColors.Add(vertexColor);
        //                vertColors.Add(vertexColor);

        //                uv.Set(vCoord, 0);
        //                uvs.Add(uv);
        //                uv.Set(vCoord, 1);
        //                uvs.Add(uv);

        //                if (i < trail.Length - 1)
        //                {

        //                    int vc = vertices.Length - 1;

        //                    tris.Add(vc);
        //                    tris.Add(va);
        //                    tris.Add(vb);

        //                    tris.Add(vb);
        //                    tris.Add(vc - 1);
        //                    tris.Add(vc);
        //                }

        //                va = vertices.Length - 1;
        //                vb = vertices.Length - 2;

        //                // Append smooth corner mesh data:
        //                if (hqCorners && rHead.cornerRoundness > 0)
        //                {

        //                    for (int p = 0; p <= rHead.cornerRoundness; ++p)
        //                    {

        //                        vertices.Add(curPoint.position + corner);
        //                        normals.Add(-normal);
        //                        tangents.Add(texTangent);
        //                        vertColors.Add(vertexColor);
        //                        uv.Set(vCoord, curvatureSign > 0 ? 0 : 1);
        //                        uvs.Add(uv);

        //                        int vc = vertices.Length - 1;

        //                        tris.Add(vc);
        //                        tris.Add(va);
        //                        tris.Add(vb);

        //                        if (curvatureSign > 0)
        //                            vb = vc;
        //                        else va = vc;

        //                        // rotate corner point:
        //                        corner = q * corner;
        //                    }

        //                }

        //            }
        //        }

        //    }



        //    private NativeList<Point> GetRenderablePoints(NativeList<Point> input, int start, int end)
        //    {
        //        Head rHead = mHeadArray[0];
        //        NativeList<Point> points = mPoints;
        //        //renderablePoints.Clear();

        //        NativeList<Point> renderablePoints = new NativeList<Point>(Allocator.Temp);

        //        if (rHead.smoothness <= 1)
        //        {
        //            for (int i = start; i <= end; ++i)
        //                renderablePoints.Add(points[i]);
        //            return renderablePoints;
        //        }

        //        // calculate sample size in normalized coordinates:
        //        float samplesize = 1.0f / rHead.smoothness;

        //        for (int i = start; i < end; ++i)
        //        {

        //            // Extrapolate first and last curve control points:
        //            Point firstPoint = i == start ? points[start] + (points[start] - points[i + 1]) : points[i - 1];
        //            Point lastPoint = i == end - 1 ? points[end] + (points[end] - points[end - 1]) : points[i + 2];

        //            for (int j = 0; j < rHead.smoothness; ++j)
        //            {

        //                float t = j * samplesize;
        //                Point interpolated = Point.Interpolate(firstPoint,
        //                                                       points[i],
        //                                                       points[i + 1],
        //                                                       lastPoint, t);

        //                // only if the interpolated point is alive, we add it to the list of points to render.
        //                if (interpolated.life > 0)
        //                    renderablePoints.Add(interpolated);
        //            }

        //        }

        //        if (points[end].life > 0)
        //            renderablePoints.Add(points[end]);

        //        return renderablePoints;
        //    }



        //    private float GetLenght(NativeList<Point> input)
        //    {

        //        float lenght = 0;
        //        for (int i = 0; i < input.Length - 1; ++i)
        //            lenght += Vector3.Distance(input[i].position, input[i + 1].position);
        //        return lenght;

        //    }

        //    private CurveFrame InitializeCurveFrame(Vector3 point, Vector3 nextPoint)
        //    {
        //        Head rHead = mHeadArray[0];
        //        Vector3 tangent = nextPoint - point;

        //        // Calculate tangent proximity to the normal vector of the frame (transform.forward).
        //        float tangentProximity = Mathf.Abs(Vector3.Dot(tangent.normalized, rHead.normal));

        //        // If both vectors are dangerously close, skew the tangent a bit so that a proper frame can be formed:
        //        //if (Mathf.Approximately(tangentProximity, 1))
        //        if(Mathf.Abs(tangentProximity - 1) < 0.0001f)
        //            tangent += rHead.tangent * 0.01f;

        //        // Generate and return the frame:
        //        return new CurveFrame(point, rHead.normal, rHead.up, tangent);
        //    }
        
        //    private void ClearMeshData()
        //    {
        //        vertices.Clear();
        //        normals.Clear();
        //        tangents.Clear();
        //        uvs.Clear();
        //        vertColors.Clear();
        //        tris.Clear();
        //    }
        //}

        [BurstCompile]
        public struct UpdateTrailMeshJob_PartA : IJob
        {
            public NativeList<Point> mPoints;
            public NativeArray<Head> mHeadArray;
            public NativeList<int> discontinuities;
            public NativeList<float> normalizedLengthList;
            public NativeList<float> normalizedLifeList;

            //public NativeList<Vector3> vertices;
            //public NativeList<Vector3> tangents;
            //public NativeList<Color> vertColors;
            //public NativeList<Vector3> uvs;
            //public NativeList<int> tris;
            //public NativeList<Vector3> normals;



            public void Execute()
            {
                normalizedLengthList.Clear();
                normalizedLifeList.Clear();

                // We need at least two points to create a trail mesh.
                if (mPoints.Length > 1)
                {

                    //Vector3 localCamPosition = rHead.space == Space.Self && transform.parent != null ? transform.parent.InverseTransformPoint(cam.transform.position) : cam.transform.position;

                    // get discontinuous point indices:
                    discontinuities.Clear();
                    for (int i = 0; i < mPoints.Length; ++i)
                        if (mPoints[i].discontinuous || i == mPoints.Length - 1) discontinuities.Add(i);

                    // generate mesh for each trail segment:
                    int start = 0;
                    for (int i = 0; i < discontinuities.Length; ++i)
                    {
                        UpdateSegmentMesh(mPoints, start, discontinuities[i], mHeadArray[0].localCamPosition);
                        start = discontinuities[i] + 1;
                    }

                    //CommitMeshData();

                    //RenderMesh(cam);
                }
            }


            private void UpdateSegmentMesh(NativeList<Point> input, int start, int end, Vector3 localCamPosition)
            {
                Head rHead = mHeadArray[0];
                // Get a list of the actual points to render: either the original, unsmoothed points or the smoothed curve.
                NativeList<Point> trail = GetRenderablePoints(input, start, end);

                if (trail.Length > 1)
                {

                    float lenght = Mathf.Max(GetLenght(trail), 0.00001f);
                    float partialLenght = 0;
                    Color vertexColor;

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

                        // Calculate this point's normalized (0,1) lenght and life.
                        float normalizedLength = partialLenght / lenght;
                        float normalizedLife = Mathf.Clamp01(1 - curPoint.life / rHead.time);
                        partialLenght += sectionLength;

                        //TODO
                        normalizedLengthList.Add(normalizedLength);
                        normalizedLifeList.Add(normalizedLife);

                    }
                }

            }



            private NativeList<Point> GetRenderablePoints(NativeList<Point> input, int start, int end)
            {
                Head rHead = mHeadArray[0];
                NativeList<Point> points = mPoints;
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



            private float GetLenght(NativeList<Point> input)
            {

                float lenght = 0;
                for (int i = 0; i < input.Length - 1; ++i)
                    lenght += Vector3.Distance(input[i].position, input[i + 1].position);
                return lenght;

            }

        }


        [BurstCompile]
        public struct UpdateTrailMeshJob_PartB : IJob
        {

            //public TransformAccessArray mCurCameraArray;
            public NativeList<Point> mPoints;
            public NativeArray<Head> mHeadArray;
            public NativeList<int> discontinuities;

            public NativeList<Vector3> vertices;
            public NativeList<Vector3> tangents;
            public NativeList<Color> vertColors;
            public NativeList<Vector3> uvs;
            public NativeList<int> tris;
            public NativeList<Vector3> normals;

            [ReadOnly] public NativeList<Color> lengthThickColor;
            [ReadOnly] public NativeList<Color> timeThickColor;
            [ReadOnly] public NativeList<float> lengthThickCurve;
            [ReadOnly] public NativeList<float> timeThickCurve;


            //[ReadOnly] public NativeArray<Keyframe> mLengthThickCurve;
            //[ReadOnly] public NativeArray<GradientColorKey> mLengthThickColorKeys;
            //[ReadOnly] public NativeArray<GradientAlphaKey> mLengthThickAlphaKeys;
            //[ReadOnly] public NativeArray<Keyframe> mTimeThickCurve;
            //[ReadOnly] public NativeArray<GradientColorKey> mTimeThickColorKeys;
            //[ReadOnly] public NativeArray<GradientAlphaKey> mTimeThickAlphaKeys;


            public void Execute()
            {
                ClearMeshData();

                // We need at least two points to create a trail mesh.
                if (mPoints.Length > 1)
                {

                    //Vector3 localCamPosition = rHead.space == Space.Self && transform.parent != null ? transform.parent.InverseTransformPoint(cam.transform.position) : cam.transform.position;

                    // get discontinuous point indices:
                    discontinuities.Clear();
                    for (int i = 0; i < mPoints.Length; ++i)
                        if (mPoints[i].discontinuous || i == mPoints.Length - 1) discontinuities.Add(i);

                    // generate mesh for each trail segment:
                    int start = 0;
                    for (int i = 0; i < discontinuities.Length; ++i)
                    {
                        UpdateSegmentMesh(mPoints, start, discontinuities[i], mHeadArray[0].localCamPosition);
                        start = discontinuities[i] + 1;
                    }

                    //CommitMeshData();

                    //RenderMesh(cam);
                }
            }


            private void UpdateSegmentMesh(NativeList<Point> input, int start, int end, Vector3 localCamPosition)
            {
                Head rHead = mHeadArray[0];
                // Get a list of the actual points to render: either the original, unsmoothed points or the smoothed curve.
                //NativeList<Point> trail = GetRenderablePoints(input, start, end);
                NativeList<Point>  trail = input;

                if (trail.Length > 1)
                {

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
                        vertexColor = curPoint.color * timeThickColor[i] * lengthThickColor[i];



                        // Update vcoord:
                        vCoord += rHead.uvFactor * (rHead.textureMode == TextureMode.Stretch ? sectionLength / lenght : sectionLength);

                        // Calulate final thickness:
                        //float sectionThickness = rHead.thickness * curPoint.thickness * thicknessOverTime.Evaluate(normalizedLife) * thicknessOverLenght.Evaluate(normalizedLength);
                        float sectionThickness = rHead.thickness * curPoint.thickness * timeThickCurve[i] * lengthThickCurve[i];

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
                }

            }

            private float GetLenght(NativeList<Point> input)
            {

                float lenght = 0;
                for (int i = 0; i < input.Length - 1; ++i)
                    lenght += Vector3.Distance(input[i].position, input[i + 1].position);
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

            private void ClearMeshData()
            {
                vertices.Clear();
                normals.Clear();
                tangents.Clear();
                uvs.Clear();
                vertColors.Clear();
                tris.Clear();
            }
        }


    }

        #endregion





   
}