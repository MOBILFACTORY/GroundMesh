using UnityEngine;
using UnityEditor;
using System.Collections;

namespace MobilFactory
{
    [CustomEditor(typeof(Tileset))]
    public class TilesetEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            if (GUILayout.Button("Edit terrain"))
            {
                var win = EditorWindow.GetWindow<EditTerrainWindow>("Edit Terrain");
                win.SetTarget(target as Tileset);
            }
        }
    }
}
