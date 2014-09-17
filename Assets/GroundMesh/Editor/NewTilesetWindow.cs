using UnityEditor;
using UnityEngine;
using System.Collections;

namespace MobilFactory
{
    class NewTilesetWindow : EditorWindow
    {
        public GroundMesh target = null;

        private Material mat = null;
        private int tileSize = 1;
        private int columnCount = 1;

        private void OnGUI()
        {
            GUILayout.BeginVertical();

            GUILayout.Label("Select Material");
            mat = EditorGUILayout.ObjectField(mat, typeof(Material), false) as Material;

            tileSize = EditorGUILayout.IntField("Tile size", tileSize);
            columnCount = EditorGUILayout.IntField("Column count", columnCount);

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Create"))
            {
                Create();
                Close();
            }

            GUILayout.EndVertical();
        }

        private void Create()
        {
            if (mat == null || tileSize < 0 || columnCount < 0)
                return;
          
            var path = AssetDatabase.GenerateUniqueAssetPath( "Assets/" + mat.name + ".prefab" );
            var go = new GameObject(mat.name);
            var prefab = PrefabUtility.CreateEmptyPrefab(path);
            var tileset = go.AddComponent<Tileset>();
            tileset.material = mat;
            tileset.tileSize = tileSize;
            tileset.columnCount = columnCount;

            PrefabUtility.ReplacePrefab(go, prefab, ReplacePrefabOptions.ConnectToPrefab);
            GameObject.DestroyImmediate(go);

            target.tileset = AssetDatabase.LoadAssetAtPath(path, typeof(Tileset)) as Tileset;
            target.renderer.sharedMaterial = mat;
        }
    }
}
