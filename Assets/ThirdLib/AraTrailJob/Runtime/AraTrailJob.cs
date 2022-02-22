using System.Collections.Generic;
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

    //[ExecuteInEditMode]
    public class AraTrailJob : MonoBehaviour
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
        public Vector3 velocity = Vector3.zero;
        public Vector3 prevPosition;
        public float speed = 0;
        public float accumTime = 0;

        public List<Vector3> vertices = new List<Vector3>();
        public List<Vector3> normals = new List<Vector3>();
        public List<Vector4> tangents = new List<Vector4>();
        public List<Vector2> uvs = new List<Vector2>();
        public List<Color> vertColors = new List<Color>();
        public List<int> tris = new List<int>();


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

            public int len_point;
            public int len_lengthCurve;
            public int len_lengthGradientColor;
            public int len_lengthGradientAlpha;
            public int len_timeCurve;
            public int len_timeGradientColor;
            public int len_timeGradientAlpha;
            public int len_vertices;
            public int len_tangents;
            public int len_vertColors;
            public int len_uvs;
            public int len_tris;
            public int len_normals;

            public int index_point;
            public int index_lengthCurve;
            public int index_lengthGradientColor;
            public int index_lengthGradientAlpha;
            public int index_timeCurve;
            public int index_timeGradientColor;
            public int index_timeGradientAlpha;
            public int index_vertices;
            public int index_tangents;
            public int index_vertColors;
            public int index_uvs;
            public int index_tris;
            public int index_normals;


        }

        public float DeltaTime
        {
            get { return timescale == Timescale.Unscaled ? Time.unscaledDeltaTime : Time.deltaTime; }
        }

        public float FixedDeltaTime
        {
            get { return timescale == Timescale.Unscaled ? Time.fixedUnscaledDeltaTime : Time.fixedDeltaTime; }
        }


        public Mesh mesh
        {
            get { return mesh_; }
        }


        private Camera mainCamera;

        #region Unity Mono

        public void Awake()
        {
            this.mainCamera = Camera.main;
            Warmup();
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



#if UNITY_EDITOR
            if (!baseOnSceneViewCamera)
            {
                if (curCamera != null)
                    tempCamera = curCamera;
                else if (this.mainCamera)
                    tempCamera = this.mainCamera;
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

            AraTrailJobManager.Instance.OnEnter(this);
        }



        void OnDisable()
        {
            points.Clear();

            AraTrailJobManager.Instance.OnExit(this);
            DestroyImmediate(mesh_);
            DestoryTrailGoc();
        }

        private void OnDestroy()
        {
            DestoryTrailGoc();
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
            //this.mUpdateJobHandle.Complete();
            //this.mLateUpdateJobHandle.Complete();
            //this.mPointList.Clear();
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
        public Camera tempCamera;

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




        #endregion
        
        public void DrawMeshData(NativeArray<Vector3> vertices, int index_vertices, int len_vertices,
            NativeArray<Vector3> normals, int index_normals, int len_normals,
            NativeArray<Vector4> tangents, int index_tangents, int len_tangents,
            NativeArray<Color> vertColors, int index_vertColors, int len_vertColors,
            NativeArray<Vector2> uvs, int index_uvs, int len_uvs,
            NativeArray<int> tris, int index_tris, int len_tris)
        {
            mesh_.Clear();
            mesh_.SetVertices(vertices, index_vertices, len_vertices);
            mesh_.SetNormals(normals, index_normals, len_normals);
            mesh_.SetTangents(tangents, index_tangents, len_tangents);
            mesh_.SetColors(vertColors, index_vertColors, len_vertColors);
            mesh_.SetUVs(0, uvs, index_uvs, len_uvs);
            mesh_.SetTriangles(tris.ToArray(), index_tris, len_tris, 0, true);
            RenderMesh(tempCamera);
        }



    }
}
