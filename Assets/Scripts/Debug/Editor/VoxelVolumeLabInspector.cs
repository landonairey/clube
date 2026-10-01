using UnityEditor;

namespace Clube.Debug.Editor
{
    /// <summary>Volume Lab settings plus the V11 / V12 / V14 readout.</summary>
    [CustomEditor(typeof(VoxelVolumeLab))]
    public class VoxelVolumeLabInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.Space();
            VolumeReadoutGui.Draw((VoxelVolumeLab)target);
        }
    }
}
