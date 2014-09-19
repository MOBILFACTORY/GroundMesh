using UnityEngine;
using UnityEditor;
using System.Collections;
using System.Collections.Generic;

namespace MobilFactory
{
    public class EditTerrainWindow : EditorWindow
    {
        public Tileset target;

        private const int ToolbarSize = 17;
        private const int LeftbarSize = 150;

        private bool erase = false;
        private int[] zooms = new int[] {400, 300, 200, 100, 75, 50, 33, 25, 12, 6};
        private int zoomIndex = 3;
        private string[] zoomOptions;
        private Vector2 listPosition = Vector2.zero;

        private int selectedIndex = -1;

        public void SetTarget(Tileset t)
        {
            target = t;

            var i = target.tileTerrains.Count;
            for (; i < target.columnCount * target.columnCount; ++i)
                target.tileTerrains.Add("-1,-1,-1,-1");
        }

        private void OnEnable()
        {
            zoomOptions = new List<int>(zooms).ConvertAll(x => (x.ToString() + "%")).ToArray();
        }

        private void Update()
        {
            Repaint();
        }

        private void OnGUI()
        {
            OnToolbarGUI();
            OnListGUI();
            OnEditorGUI();
            OnCursorGUI();
        }

        private void OnToolbarGUI()
        {
            GUILayout.BeginHorizontal(EditorStyles.toolbar, GUILayout.ExpandWidth(true));

            if (GUILayout.Button("+", EditorStyles.toolbarButton, GUILayout.Width(LeftbarSize / 2)))
            {
                AddTerrain();
            }
            else if (GUILayout.Button("-", EditorStyles.toolbarButton, GUILayout.Width(LeftbarSize / 2)))
            {
                RemoveTerrain();
            }

            GUILayout.Space(10);

            erase = GUILayout.Toggle(erase, "Erase", EditorStyles.toolbarButton);
            
            GUILayout.FlexibleSpace();

            GUILayout.Label("Zoom");
            zoomIndex = EditorGUILayout.Popup(zoomIndex, zoomOptions, EditorStyles.toolbarPopup, GUILayout.Width(50));

            GUILayout.Space(20);

            if (GUILayout.Button("Close", EditorStyles.toolbarButton))
            {
                Close();
            }
            
            GUILayout.EndHorizontal();
        }

        private void OnListGUI()
        {
            GUILayout.BeginHorizontal(GUILayout.Width(LeftbarSize));

            GUILayout.BeginVertical(GUILayout.ExpandHeight(true));

            listPosition = GUILayout.BeginScrollView(listPosition);

            for (var i = 0; i < target.terrainNames.Count; ++i)
            {
                var name = target.terrainNames[i];
                if (name == null)
                {
                    GUILayout.Button("null");
                    continue;
                }
                if (i == selectedIndex)
                {
                    target.terrainNames[i] = GUILayout.TextField(name);
                }
                else
                {
                    if (GUILayout.Button(name))
                    {
                        selectedIndex = i;
                        erase = false;
                    }
                }
            }

            GUILayout.EndScrollView();

            GUILayout.EndVertical();

            GUILayout.EndHorizontal();
        }

        private void OnEditorGUI()
        {
            var mat = target.material;
            if (mat == null)
                return;
            var tex = mat.GetTexture(0);
            if (tex == null)
                return;

            var zoom = ((float)zooms[zoomIndex]) * 0.01f;
//            var rect = new Rect(LeftbarSize, 
//                                ToolbarSize, 
//                                tex.width * zoom, 
//                                tex.height * zoom);
//            GUI.DrawTexture(rect, tex);

            int beginX = LeftbarSize;
            int beginY = ToolbarSize + 1;
            int margin = 0;
            int size = (int)(target.tileSize * zoom);
            int idx = 0;
            int col = 0;
            int row = 0;
            for (int y = 0; y < target.columnCount; ++y)
            {
                for (int x = 0; x < target.columnCount; ++x)
                {
                    var rect = new Rect((size + margin) * col + beginX, 
                                        (size + margin) * row + beginY, 
                                        size, size);
                    
                    var c = (float)target.columnCount;
                    var texCoords = new Rect((float)x / c, (float)y / c, 1f / c, 1f / c);
                    GUI.DrawTextureWithTexCoords(rect, tex, texCoords);
                    
                    idx++;
                    col++;
                    if (col == target.columnCount)
                    {
                        col = 0;
                        row++;
                    }
                }
            }
        }

        private void OnCursorGUI()
        {
            var mat = target.material;
            if (mat == null)
                return;

            var tex = mat.GetTexture(0);
            if (tex == null)
                return;

            var zoom = ((float)zooms[zoomIndex]) * 0.01f;
            var e = Event.current;
            var m = e.mousePosition;
            var tileSize = target.tileSize * zoom;
            var terrainSize = tileSize / 2;

            if (m.x > LeftbarSize && m.y > ToolbarSize
                && m.x - LeftbarSize < (float)tex.width * zoom
                && m.y - ToolbarSize < (float)tex.height * zoom)
            {
                var tileX = Mathf.RoundToInt((m.x - LeftbarSize - tileSize / 2) / tileSize);
                var tileY = Mathf.RoundToInt((m.y - ToolbarSize - tileSize / 2) / tileSize);
                var tileIndex = tileX + tileY * target.columnCount;
//                var tileRect = new Rect(tileX * tileSize + LeftbarSize, 
//                                        tileY * tileSize + ToolbarSize, 
//                                        tileSize, tileSize);

//                GUI.color = new Color(1f, 0f, 0f, 0.7f);
//                GUI.Box(tileRect, tileIndex.ToString(), EditorStyles.miniButtonMid);

                var brushX = Mathf.RoundToInt((m.x - LeftbarSize - terrainSize / 2) / terrainSize);
                var brushY = Mathf.RoundToInt((m.y - ToolbarSize - terrainSize / 2) / terrainSize);
                var terrainX = Mathf.RoundToInt((m.x - LeftbarSize - terrainSize / 2 - tileX * tileSize) / terrainSize);
                var terrainY = Mathf.RoundToInt((m.y - ToolbarSize - terrainSize / 2 - tileY * tileSize) / terrainSize);
                var terrainIndex = terrainX + terrainY * 2;
                var brushRect = new Rect(brushX * terrainSize + LeftbarSize, 
                                         brushY * terrainSize + ToolbarSize, 
                                         terrainSize, terrainSize);
                if (terrainIndex > -1 && terrainIndex < 4)
                {
                    GUI.color = new Color(0f, 1f, 0f, 0.7f);
                    GUI.Box(brushRect, "", EditorStyles.miniButtonMid);
                }

                if (e.type == EventType.MouseDown)
                {
                    OnClick(tileIndex, terrainIndex);
                }
            }
            
            if (selectedIndex >= 0)
            {
                for (var i = 0; i < target.tileTerrains.Count; ++i)
                {
                    var arr = target.tileTerrains[i].Split(',');
                    var curr = selectedIndex.ToString();
                    for (int j = 0; j < arr.Length; ++j)
                    {
                        var x = i % target.columnCount * tileSize + j % 2 * terrainSize;
                        var y = i / target.columnCount * tileSize + j / 2 * terrainSize;
                        var rect = new Rect(x + LeftbarSize, 
                                            y + ToolbarSize, 
                                            terrainSize, terrainSize);
                        if (arr[j] == curr)
                        {
                            GUI.color = new Color(0f, 1f, 1f, 0.6f);
                            GUI.Box(rect, "", EditorStyles.miniButtonMid);
                        }
                        else if (arr[j] != "-1")
                        {
                            GUI.color = new Color(1f, 1f, 1f, 0.4f);
                            GUI.Box(rect, "", EditorStyles.miniButtonMid);
                        }
                    }
                }
            }
        }

        private void OnClick(int tileIndex, int terrainIndex)
        {
            var arr = target.tileTerrains[tileIndex].Split(',');
            if (erase)
            {
                if (selectedIndex.ToString() == arr[terrainIndex])
                    arr[terrainIndex] = "-1";
            }
            else
            {
                arr[terrainIndex] = selectedIndex.ToString();
            }
            target.tileTerrains[tileIndex] = string.Join(",", arr);
        }

        private void AddTerrain()
        {
            target.terrainNames.Add("New Terrain");
        }

        private void RemoveTerrain()
        {
            if (selectedIndex < 0)
                return;

            target.terrainNames.RemoveAt(selectedIndex);
        }
    }
}
