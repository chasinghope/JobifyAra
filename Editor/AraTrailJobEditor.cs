using UnityEditor;

namespace AraJob
{
    [CustomEditor(typeof(AraTrailJob))]
    [CanEditMultipleObjects]
    internal class AraTrailEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            this.serializedObject.Update();
            Editor.DrawPropertiesExcluding(serializedObject, "m_Script");
            this.serializedObject.ApplyModifiedProperties();
        }
    }
}

