using UnityEngine;
using System.Collections.Generic;
using Unity.Mathematics;
using Unity.Burst;

[AddComponentMenu("Dynamic Bone/Dynamic Bone")]
public class DynamicBoneJob : MonoBehaviour
{
    public enum UpdateMode : byte
    {
        Normal,
        AnimatePhysics,
        UnscaledTime
    }
    public enum FreezeAxis : byte
    {
        None, X, Y, Z
    }

    public Transform m_Root = null;
    public float m_UpdateRate = 60.0f;
    public UpdateMode m_UpdateMode = UpdateMode.Normal;
    [Range(0, 1)]
    public float m_Damping = 0.1f;
    public AnimationCurve m_DampingDistrib = null;
    [Range(0, 1)]
    public float m_Elasticity = 0.1f;
    public AnimationCurve m_ElasticityDistrib = null;
    [Range(0, 1)]
    public float m_Stiffness = 0.1f;
    public AnimationCurve m_StiffnessDistrib = null;
    [Range(0, 1)]
    public float m_Inert = 0;
    public AnimationCurve m_InertDistrib = null;
    public float m_Radius = 0;
    public AnimationCurve m_RadiusDistrib = null;
    public float m_EndLength = 0;
    public Vector3 m_EndOffset = Vector3.zero;
    public Vector3 m_Gravity = Vector3.zero;
    public Vector3 m_Force = Vector3.zero;
    public List<DynamicBoneColliderJob> m_Colliders = null;
    public List<Transform> m_Exclusions = null;

    public FreezeAxis m_FreezeAxis = FreezeAxis.None;
    public bool m_DistantDisable = false;
    public Transform m_ReferenceObject = null;
    public float m_DistanceToObject = 20;
    public bool m_InitTransformInFrame = false;

    [HideInInspector]
    [System.NonSerialized]
    public Vector3 m_LocalGravity = Vector3.zero;
    [HideInInspector]
    [System.NonSerialized]
    public Vector3 m_ObjectMove = Vector3.zero;
    [HideInInspector]
    [System.NonSerialized]
    public Vector3 m_ObjectPrevPosition = Vector3.zero;
    [HideInInspector]
    [System.NonSerialized]
    public float m_BoneTotalLength = 0;
    [HideInInspector]
    [System.NonSerialized]
    public float m_ObjectScale = 1.0f;
    [HideInInspector]
    [System.NonSerialized]
    public float m_Time = 0;
    [HideInInspector]
    [System.NonSerialized]
    public float m_Weight = 1.0f;
    [HideInInspector]
    [System.NonSerialized]
    public bool m_DistantDisabled = false;

    public float Weight
    {
        get
        {
            return m_Weight;
        }
        set
        {
            if (m_Weight == value) return;
            DynamicBoneJobManager.Instance?.RefreshWeight(this);
            m_Weight = value;
        }
    }
    [BurstCompile]
    public struct Head
    {
        public int m_ParticleIndex;
        public int m_ParticleLength;

        public int m_ColliderIndex;
        public int m_ColliderLength;

        public float m_UpdateRate;
        public float3 m_LocalGravity;
        public float3 m_ObjectMove;
        public float3 m_ObjectPrevPosition;
        public float m_BoneTotalLength;
        public float m_ObjectScale;
        public float m_Time;
        public float m_Weight;
        public bool m_DistantDisable;
        public bool m_DistantDisabled;
        public float m_DistanceToObject;
        public bool m_ReferenceObjectIsNull;
        public float3 m_Gravity;
        public float3 m_Force;
        public FreezeAxis m_FreezeAxis;
        public bool m_InitTransformInFrame;

        public float3 m_WorldPosition;
        public float3 m_WorldScale;

        public float3 m_CheckDistancePosition;

        public float3 m_RootWorldPosition;
        public quaternion m_RootWorldRotation;
        public float3 m_RootWorldScale;
        public float3 m_RootLocalScale;

        public int m_Loop;
        public float3 m_PreForce;
    }
    [BurstCompile]
    public struct Particle
    {
        public int m_ParentIndex;
        public float m_Damping;
        public float m_Elasticity;
        public float m_Stiffness;
        public float m_Inert;
        public float m_Radius;
        public float m_BoneLength;

        public float3 m_Position;
        public float3 m_PrevPosition;
        public float3 m_EndOffset;
        public float3 m_InitLocalPosition;
        public quaternion m_InitLocalRotation;

        public int m_ChildCount;
        public bool m_TransformIsNull;
        public float3 m_WorldPosition;
        //public quaternion m_WorldRotation;
        public float3 m_WorldScale;
        public float3 m_LocalPosition;
        public quaternion m_LocalRotation;
        public float3 m_LocalScale;
        public float4x4 m_LocalToWorldMatrix;
        public quaternion m_Rotation;

        public int m_UpdateParticles1LoopCount;
        public int m_UpdateParticles2LoopCount;
        public bool m_DistantDisabled;
    }

    public struct Collider
    {
        public bool m_Enabled;
        public DynamicBoneColliderJob.Direction m_Direction;
        public float3 m_Center;
        public DynamicBoneColliderJob.Bound m_Bound;
        public float m_Radius;
        public float m_Height;

        public float3 m_WorldScale;
        public float4x4 m_LocalToWorldMatrix;
    }
    [HideInInspector]
    [System.NonSerialized]
    public List<Particle> m_Particles = new List<Particle>();
    [HideInInspector]
    [System.NonSerialized]
    public List<Transform> m_ParticleTransforms = new List<Transform>();
    void Awake()
    {
        SetupParticles();
    }
    void OnEnable()
    {
        DynamicBoneJobManager.Instance?.OnEnter(this);
    }

    void OnDisable()
    {
        InitTransforms();
        DynamicBoneJobManager.Instance?.OnExit(this);
    }
#if UNITY_EDITOR
    void OnValidate()
    {
        m_UpdateRate = Mathf.Max(m_UpdateRate, 0);
        m_Damping = Mathf.Clamp01(m_Damping);
        m_Elasticity = Mathf.Clamp01(m_Elasticity);
        m_Stiffness = Mathf.Clamp01(m_Stiffness);
        m_Inert = Mathf.Clamp01(m_Inert);
        m_Radius = Mathf.Max(m_Radius, 0);

        if (Application.isEditor && Application.isPlaying)
        {
            InitTransforms();
            SetupParticles();
            if (DynamicBoneJobManager.Instance != null && DynamicBoneJobManager.Instance.Exists(this))
            {
                DynamicBoneJobManager.Instance?.OnExit(this);
                DynamicBoneJobManager.Instance?.OnEnter(this);
            }
        }
    }
#endif

    public void SetupParticles()
    {
        m_Particles.Clear();
        m_ParticleTransforms.Clear();
        if (m_Root == null)
            return;

        m_LocalGravity = m_Root.InverseTransformDirection(m_Gravity);
        m_ObjectScale = Mathf.Abs(transform.lossyScale.x);
        m_ObjectPrevPosition = transform.position;
        m_ObjectMove = Vector3.zero;
        m_BoneTotalLength = 0;
        AppendParticles(m_Root, -1, 0);
        UpdateParameters();
    }

    void AppendParticles(Transform b, int parentIndex, float boneLength)
    {
        Particle p = new Particle();
        p.m_ParentIndex = parentIndex;
        p.m_TransformIsNull = b == null;
        if (b != null)
        {
            p.m_Position = p.m_PrevPosition = b.position;
            p.m_InitLocalPosition = b.localPosition;
            p.m_InitLocalRotation = b.localRotation;
            p.m_ChildCount = b.childCount;
        }
        else 	// end bone
        {
            Transform pb = m_ParticleTransforms[parentIndex];
            if (m_EndLength > 0)
            {
                Transform ppb = pb.parent;
                if (ppb != null)
                    p.m_EndOffset = pb.InverseTransformPoint((pb.position * 2 - ppb.position)) * m_EndLength;
                else
                    p.m_EndOffset = new Vector3(m_EndLength, 0, 0);
            }
            else
            {
                p.m_EndOffset = pb.InverseTransformPoint(transform.TransformDirection(m_EndOffset) + pb.position);
            }
            p.m_Position = p.m_PrevPosition = pb.TransformPoint(p.m_EndOffset);
        }

        if (parentIndex >= 0)
        {
            boneLength += (m_ParticleTransforms[parentIndex].position - (Vector3)p.m_Position).magnitude;
            p.m_BoneLength = boneLength;
            m_BoneTotalLength = Mathf.Max(m_BoneTotalLength, boneLength);
        }

        int index = m_Particles.Count;
        m_Particles.Add(p);
        m_ParticleTransforms.Add(b);


        if (b != null)
        {
            for (int i = 0; i < b.childCount; ++i)
            {
                bool exclude = false;
                if (m_Exclusions != null)
                {
                    for (int j = 0; j < m_Exclusions.Count; ++j)
                    {
                        Transform e = m_Exclusions[j];
                        if (e == b.GetChild(i))
                        {
                            exclude = true;
                            break;
                        }
                    }
                }
                if (!exclude)
                    AppendParticles(b.GetChild(i), index, boneLength);
                else if (m_EndLength > 0 || m_EndOffset != Vector3.zero)
                    AppendParticles(null, index, boneLength);
            }

            if (b.childCount == 0 && (m_EndLength > 0 || m_EndOffset != Vector3.zero))
                AppendParticles(null, index, boneLength);
        }
    }

    public void UpdateParameters()
    {
        if (m_Root == null)
            return;

        m_LocalGravity = m_Root.InverseTransformDirection(m_Gravity);

        for (int i = 0; i < m_Particles.Count; ++i)
        {
            Particle p = m_Particles[i];
            p.m_Damping = m_Damping;
            p.m_Elasticity = m_Elasticity;
            p.m_Stiffness = m_Stiffness;
            p.m_Inert = m_Inert;
            p.m_Radius = m_Radius;

            if (m_BoneTotalLength > 0)
            {
                float a = p.m_BoneLength / m_BoneTotalLength;
                if (m_DampingDistrib != null && m_DampingDistrib.keys.Length > 0)
                    p.m_Damping *= m_DampingDistrib.Evaluate(a);
                if (m_ElasticityDistrib != null && m_ElasticityDistrib.keys.Length > 0)
                    p.m_Elasticity *= m_ElasticityDistrib.Evaluate(a);
                if (m_StiffnessDistrib != null && m_StiffnessDistrib.keys.Length > 0)
                    p.m_Stiffness *= m_StiffnessDistrib.Evaluate(a);
                if (m_InertDistrib != null && m_InertDistrib.keys.Length > 0)
                    p.m_Inert *= m_InertDistrib.Evaluate(a);
                if (m_RadiusDistrib != null && m_RadiusDistrib.keys.Length > 0)
                    p.m_Radius *= m_RadiusDistrib.Evaluate(a);
            }

            p.m_Damping = Mathf.Clamp01(p.m_Damping);
            p.m_Elasticity = Mathf.Clamp01(p.m_Elasticity);
            p.m_Stiffness = Mathf.Clamp01(p.m_Stiffness);
            p.m_Inert = Mathf.Clamp01(p.m_Inert);
            p.m_Radius = Mathf.Max(p.m_Radius, 0);
            this.m_Particles[i] = p;
        }
    }

    public void InitTransforms()
    {
        for (int i = 0; i < m_Particles.Count; ++i)
        {
            Particle p = m_Particles[i];
            if (i < this.m_ParticleTransforms.Count)
            {
                Transform t = m_ParticleTransforms[i];
                if (t != null)
                {
                    t.localPosition = p.m_InitLocalPosition;
                    t.localRotation = p.m_InitLocalRotation;
                }
            }
        }
    }

    public void ResetParticlesPosition()
    {
        for (int i = 0; i < m_Particles.Count; ++i)
        {
            Particle p = m_Particles[i];
            Transform t = m_ParticleTransforms[i];
            if (!p.m_TransformIsNull)
            {
                p.m_Position = p.m_PrevPosition = t.position;
            }
            else	// end bone
            {
                Transform pt = m_ParticleTransforms[p.m_ParentIndex];
                p.m_Position = p.m_PrevPosition = pt.TransformPoint(p.m_EndOffset);
            }
            m_Particles[i] = p;
        }
        m_ObjectPrevPosition = transform.position;
    }
    void OnDrawGizmosSelected()
    {
        if (!enabled || m_Root == null)
            return;
        if (Application.isPlaying) return;
#if UNITY_EDITOR
        if (Application.isEditor && !Application.isPlaying && transform.hasChanged)
        {
            InitTransforms();
            SetupParticles();
        }
#endif

        Gizmos.color = Color.white;
        for (int i = 0; i < m_Particles.Count; ++i)
        {
            Particle p = m_Particles[i];
            if (p.m_ParentIndex >= 0)
            {
                Particle p0 = m_Particles[p.m_ParentIndex];
                Gizmos.DrawLine(p.m_Position, p0.m_Position);
            }
            if (p.m_Radius > 0)
                Gizmos.DrawWireSphere(p.m_Position, p.m_Radius * m_ObjectScale);
        }
    }
}
