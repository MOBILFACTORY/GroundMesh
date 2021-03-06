using UnityEngine;
using UnityEditor;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace MobilFactory
{
    [CustomEditor(typeof(GroundMesh))]
    public class GroundMeshEditor : Editor
    {
        private enum TilesetMode
        {
            Tile,
            Terrain,
        }

        private readonly string KeyFormat = "{0},{1}";
        private readonly float CapSize = 0.2f;
        private readonly float CapSizeHalf = 0.1f;
        private readonly Quaternion CapRotationUp = Quaternion.Euler(270, 0, 0);
        private readonly Quaternion CapRotationBottom = Quaternion.Euler(90, 0, 0);
        private readonly KeyCode KeyBrushHeightUp = KeyCode.W;
        private readonly KeyCode KeyBrushHeightDown = KeyCode.S;
        private readonly KeyCode KeyBrushHeightReset = KeyCode.R;
        private readonly KeyCode KeyBrushSizeUp = KeyCode.D;
        private readonly KeyCode KeyBrushSizeDown = KeyCode.A;
        private readonly KeyCode KeyBrushRotateMinus = KeyCode.Q;
        private readonly KeyCode KeyBrushRotatePlus = KeyCode.E;
        private readonly KeyCode KeyUndo = KeyCode.Z;
        private readonly KeyCode KeyRedo = KeyCode.Y;

        private bool _lock = true;
        private bool _drawHeight = true;
        private bool _drawTile = false;
        private Rect _winsize = new Rect(10, 30, 10, 10);
        private Vector3 _mouse;
        private Matrix4x4 _worldToLocal;
        private List<Vector3> _vertices;
        private List<Vector2> _uv;
        private List<int> _triangles;
        private Dictionary <string, List<int>> _indexCache;
        private int _selectedTexIdx = 0;
        private int _texRotation = 0;
        private TilesetMode _tilesetMode = TilesetMode.Tile;
        private Vector2 _tilesetPos = Vector2.zero;

        [MenuItem("GameObject/Create Other/GroundMesh")]
        static private void Create()
        {
            var go = new GameObject("GroundMesh");
            go.AddComponent<GroundMesh>();
        }

        private void OnEnable()
        {
            Tools.current = Tool.View;
            
            var ground = target as GroundMesh;
            var mesh = ground.GetComponent<MeshFilter>().sharedMesh;
            if (mesh != null)
            {
                _vertices = new List<Vector3>(mesh.vertices);
                _uv = new List<Vector2>(mesh.uv);
                _triangles = new List<int>(mesh.triangles);
            }
            else
            {
                _vertices = new List<Vector3>();
                _uv = new List<Vector2>();
                _triangles = new List<int>();
            }

            if (ground.tileset != null && ground.tileset.material != null)
                ground.renderer.sharedMaterial = ground.tileset.material;

            _indexCache = GetIndexDict();
        }

        public override void OnInspectorGUI()
        {
            var ground = target as GroundMesh;

            base.OnInspectorGUI();

            GUILayout.Space(10);

            if (GUILayout.Button("New Mesh"))
            {
                if (!EditorUtility.DisplayDialog("New", "Are you sure?", "New", "Cancel"))
                    return;

                NewMesh();
            }
            else if (GUILayout.Button("Resize Mesh"))
            {
                if (ground._rows > ground.rows
                    || ground._cols > ground.cols)
                {
                    if (!EditorUtility.DisplayDialog("Resize", "Are you sure?", "Resize", "Cancel"))
                        return;
                }

                ResizeMesh();
            }
            
            GUILayout.Space(10);

            if (GUILayout.Button("Create MeshCollider"))
            {
                if (ground.GetComponent<MeshCollider>() != null)
                    DestroyImmediate(ground.GetComponent<MeshCollider>());

                ground.gameObject.AddComponent<MeshCollider>();
            }

            GUILayout.Space(10);

            if (GUILayout.Button(string.Format("Clean History ({0}/{1})", ground._historyIndex, ground._histories.Count)))
            {
                ground.ClearHistory();
                EditorUtility.UnloadUnusedAssets();
            }

            GUILayout.Space(10);
            
            if (GUILayout.Button("Create Tileset"))
            {
                var win = EditorWindow.GetWindow<NewTilesetWindow>("New Tileset");
                win.target = target as GroundMesh;
                Event.current.Use();
            }
        }

        public void NewMesh()
        {
            var ground = target as GroundMesh;
            if (ground.GetComponent<MeshCollider>() != null)
                DestroyImmediate(ground.GetComponent<MeshCollider>());

            if (ground.tileset != null && ground.tileset.material != null)
                ground.renderer.sharedMaterial = ground.tileset.material;
            
            EditorUtility.UnloadUnusedAssets();
            
            _vertices.Clear();
            _uv.Clear();
            _triangles.Clear();

            var c = (float)ground.tileset.columnCount;
            var i = 0;
            for (int y = 0; y < ground.rows; ++y)
            {
                for (int x = 0; x < ground.cols; ++x)
                {
                    _vertices.Add(new Vector3(x, 0, y));
                    _vertices.Add(new Vector3(x + 1, 0, y));
                    _vertices.Add(new Vector3(x + 1, 0, y + 1));
                    _vertices.Add(new Vector3(x, 0, y + 1));
                    
                    _uv.Add(new Vector2(0f, 0f));
                    _uv.Add(new Vector2(1f / c, 0f));
                    _uv.Add(new Vector2(1f / c, 1f / c));
                    _uv.Add(new Vector2(0f, 1f / c));
                    
                    _triangles.Add(i);
                    _triangles.Add(i + 3);
                    _triangles.Add(i + 2);
                    _triangles.Add(i);
                    _triangles.Add(i + 2);
                    _triangles.Add(i + 1);
                    
                    i += 4;
                }
            }

            var defaultTerrain = "-1,-1,-1,-1";
            if (ground.tileset.terrainNames.Count > 0)
                defaultTerrain = "0,0,0,0";
            ground._terrains.Clear();
            for (i = 0; i < ground.cols * ground.rows; ++i)
            {
                ground._terrains.Add(defaultTerrain);
            }

            UpdateMesh();
        }
        
        public void ResizeMesh()
        {
            var ground = target as GroundMesh;
            if (ground.GetComponent<MeshCollider>() != null)
                DestroyImmediate(ground.GetComponent<MeshCollider>());
            
            EditorUtility.UnloadUnusedAssets();

            var oldVertices = new List<Vector3>(_vertices);
            var oldUV = new List<Vector2>(_uv);
            
            _vertices.Clear();
            _uv.Clear();
            _triangles.Clear();
            
            var c = (float)ground.tileset.columnCount;
            var i = 0;
            for (int y = 0; y < ground.rows; ++y)
            {
                for (int x = 0; x < ground.cols; ++x)
                {
                    _vertices.Add(new Vector3(x, 0, y));
                    _vertices.Add(new Vector3(x + 1, 0, y));
                    _vertices.Add(new Vector3(x + 1, 0, y + 1));
                    _vertices.Add(new Vector3(x, 0, y + 1));
                    
                    _uv.Add(new Vector2(0f, 0f));
                    _uv.Add(new Vector2(1f / c, 0f));
                    _uv.Add(new Vector2(1f / c, 1f / c));
                    _uv.Add(new Vector2(0f, 1f / c));
                    
                    _triangles.Add(i);
                    _triangles.Add(i + 3);
                    _triangles.Add(i + 2);
                    _triangles.Add(i);
                    _triangles.Add(i + 2);
                    _triangles.Add(i + 1);
                    
                    i += 4;
                }
            }
            
            var newIndexCache = GetIndexDict();
            foreach (var pair in _indexCache)
            {
                if (!newIndexCache.ContainsKey(pair.Key))
                    continue;
                
                var list = newIndexCache[pair.Key];
                foreach (var idx in list)
                {
                    foreach (var idx2 in _indexCache[pair.Key])
                    {
                        _vertices[idx] = oldVertices[idx2];
                    }
                }
            }
            
            for (int y = 0; y < ground._rows; ++y)
            {
                for (int x = 0; x < ground._cols; ++x)
                {
                    var oldIdx = x  * 4 + (ground._cols * 4 * y);
                    var newIdx = x  * 4 + (ground.cols * 4 * y);
                    if (_uv.Count <= newIdx || oldUV.Count <= oldIdx)
                        continue;
                    
                    _uv[newIdx + 0] = oldUV[oldIdx + 0];
                    _uv[newIdx + 1] = oldUV[oldIdx + 1];
                    _uv[newIdx + 2] = oldUV[oldIdx + 2];
                    _uv[newIdx + 3] = oldUV[oldIdx + 3];
                }
            }

            var oldTerrains = new List<string>(ground._terrains);
            ground._terrains.Clear();
            for (i = 0; i < ground.cols * ground.rows; ++i)
            {
                ground._terrains.Add(oldTerrains[i]);
            }

            UpdateMesh();
            ground.ClearHistory();
        }

        private void UpdateMesh()
        {
            var ground = target as GroundMesh;
            
            ground._cols = ground.cols;
            ground._rows = ground.rows;
            
            ground.UpdateMesh(_vertices, _uv, _triangles);

            _indexCache = GetIndexDict();
        }

        private void OnSceneGUI()
        {
            if (!_lock)
            {
                OnKey();
                OnMouse();
            }

            GUILayout.Window(0, _winsize, OnWindowGUI, "Ground Mesh Editor");
            DrawCursorIndex();

            if (_lock)
                return;
            if (_drawHeight)
                OnHeightHandles();
            if (_drawTile)
                OnTileHandles();
        }
        
        private void OnKey()
        {
            var e = Event.current;

            var ground = target as GroundMesh;
            if (e.type == EventType.KeyDown)
            {
                if (e.keyCode == KeyBrushSizeUp)
                {
                    ground._brushSize = Mathf.Min(20, ground._brushSize + 1);
                    e.Use();
                }
                else if (e.keyCode == KeyBrushSizeDown)
                {
                    ground._brushSize = Mathf.Max(1, ground._brushSize - 1);
                    e.Use();
                }
                else if (e.keyCode == KeyBrushHeightUp)
                {
                    ground._brushHeight += ground._brushHeightUnit;
                    e.Use();
                }
                else if (e.keyCode == KeyBrushHeightDown)
                {
                    ground._brushHeight -= ground._brushHeightUnit;
                    e.Use();
                }
                else if (e.keyCode == KeyBrushHeightReset)
                {
                    ground._brushHeight = 0f;
                    e.Use();
                }
                else if (e.keyCode == KeyBrushRotateMinus)
                {
                    _texRotation -= 90;
                    if (_texRotation < 0)
                        _texRotation = 270;
                    e.Use();
                }
                else if (e.keyCode == KeyBrushRotatePlus)
                {
                    _texRotation += 90;
                    if (_texRotation >= 360)
                        _texRotation = 0;
                    e.Use();
                }
                else if (e.keyCode == KeyUndo)
                {
                    UndoHistory();
                    e.Use();
                }
                else if (e.keyCode == KeyRedo)
                {
                    RedoHistory();
                    e.Use();
                }
                else if (e.keyCode == KeyCode.RightControl)
                {
                    e.Use();
                }
            }
        }
        
        private void OnMouse()
        {
            var ground = target as GroundMesh;
            var e = Event.current;
            
            if (e.type == EventType.mouseDown
                || e.type == EventType.mouseMove 
                || e.type == EventType.mouseDrag)
            {
                Handles.matrix = ground.transform.localToWorldMatrix;
                _worldToLocal = ground.transform.worldToLocalMatrix;
                
                var plane = new Plane(ground.transform.up, ground.transform.position);
                var x = e.mousePosition.x;
                var y = Camera.current.pixelHeight + ground._brushHeight - e.mousePosition.y;
                var ray = Camera.current.ScreenPointToRay(new Vector3(x, y));
                float hit;
                if (!plane.Raycast(ray, out hit))
                    return;

                _mouse = _worldToLocal.MultiplyPoint(ray.GetPoint(hit));
            }

            if ((e.type == EventType.mouseDown || e.type == EventType.mouseDrag) && e.button == 0)
            {
                if (_drawHeight)
                    SetVertex(_mouse);

                if (_drawTile)
                {
                    if (_tilesetMode == TilesetMode.Tile)
                        SetUV(_mouse, _selectedTexIdx);
                    else
                    {
                        SetTerrain();
                    }
                }

                e.Use();
            }
        }
        
        private void OnWindowGUI(int id)
        {
            GUILayout.BeginVertical(GUILayout.Width(320));

            if (_lock)
            {
                GUI.color = Color.red;
                if (GUILayout.Button("Unlock", GUILayout.MinWidth(320)))
                {
                    _lock = false;
                }
                GUI.color = Color.white;
                GUILayout.EndVertical();
                return;
            }
            
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(string.Format("Undo ({0})", KeyUndo)))
                UndoHistory();
            if (GUILayout.Button(string.Format("Redo ({0})", KeyRedo)))
                RedoHistory();
            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            GUILayout.BeginHorizontal();
            GUI.color = _drawHeight ? Color.green : Color.white;
            if (GUILayout.Button("Draw Height"))
            {
                _drawHeight = !_drawHeight;
            }
            GUI.color = Color.white;
            GUI.color = _drawTile ? Color.green : Color.white;
            if (GUILayout.Button("Draw Tiles"))
            {
                _drawTile = !_drawTile;
            }
            GUI.color = Color.white;
            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            OnBrushGUI();

            GUILayout.Space(10);

            if (_drawTile)
                OnTileGUI();
            
            GUILayout.EndVertical();
        }
        
        private void OnBrushGUI()
        {
            var ground = target as GroundMesh;

            if (_drawHeight)
            {
                GUILayout.BeginHorizontal();
                ground._brushHeightUnit = EditorGUILayout.FloatField("Height Unit ", ground._brushHeightUnit);
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                ground._brushHeight = EditorGUILayout.FloatField("Height ", ground._brushHeight);
                if (GUILayout.Button(string.Format("+ ({0})", KeyBrushHeightUp)))
                    ground._brushHeight += ground._brushHeightUnit;
                if (GUILayout.Button(string.Format("- ({0})", KeyBrushHeightDown)))
                    ground._brushHeight -= ground._brushHeightUnit;
                if (GUILayout.Button(string.Format("Reset ({0})", KeyBrushHeightReset)))
                    ground._brushHeight = 0f;
                GUILayout.EndHorizontal();
            }

            if (_drawHeight || _drawTile)
            {
                GUILayout.BeginHorizontal();
                ground._brushSize = EditorGUILayout.IntField("Size ", ground._brushSize);
                if (GUILayout.Button(string.Format("+ ({0})", KeyBrushSizeUp)))
                    ground._brushSize = Mathf.Min(20, ground._brushSize + 1);
                if (GUILayout.Button(string.Format("- ({0})", KeyBrushSizeDown)))
                    ground._brushSize = Mathf.Max(1, ground._brushSize - 1);
                GUILayout.EndHorizontal();
            }
        }

        private void OnTileGUI()
        {
            var ground = target as GroundMesh;
            if (ground == null || ground.renderer == null)
                return;

            var mat = ground.tileset.material;
            if (mat == null)
                return;

            var tex = ground.tileset.material.GetTexture(0);
            if (tex == null)
                return;

            GUILayout.BeginHorizontal();
            GUI.color = _tilesetMode == TilesetMode.Tile ? Color.green : Color.white;
            if (GUILayout.Button("Tile"))
            {
                _tilesetMode = TilesetMode.Tile;
                _tilesetPos = Vector2.zero;
                _selectedTexIdx = -1;
            }
            GUI.color = _tilesetMode == TilesetMode.Terrain ? Color.green : Color.white;
            if (GUILayout.Button("Terrain"))
            {
                _tilesetMode = TilesetMode.Terrain;
                _tilesetPos = Vector2.zero;
                _selectedTexIdx = -1;
                _texRotation = 0;
            }
            GUILayout.EndHorizontal();
            GUI.color = Color.white;

            if (_tilesetMode == TilesetMode.Tile)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(string.Format("Tex Rotation: {0}", _texRotation));
                if (GUILayout.Button(string.Format("Rotate Left ({0})", KeyBrushRotateMinus)))
                {
                    _texRotation -= 90;
                    if (_texRotation < 0)
                        _texRotation = 270;
                }
                if (GUILayout.Button(string.Format("Rotate Right ({0})", KeyBrushRotatePlus)))
                {
                    _texRotation += 90;
                    if (_texRotation >= 360)
                        _texRotation = 0;
                }
                GUILayout.EndHorizontal();
                GUILayout.Space(10);
            }
            
            _tilesetPos = GUILayout.BeginScrollView(_tilesetPos, GUILayout.Height(300));
            if (_tilesetMode == TilesetMode.Tile)
            {
                int beginX = 10;
                int beginY = 0;
                int margin = 15;
                int size = 60;
                int idx = 0;
                int col = 0;
                int row = 0;
                for (int y = 0; y < ground.tileset.columnCount; ++y)
                {
                    for (int x = 0; x < ground.tileset.columnCount; ++x)
                    {
                        var rect = new Rect((size + margin) * col + beginX, 
                                            (size + margin) * row + beginY, 
                                            size, size);

                        var c = (float)ground.tileset.columnCount;
                        var texCoords = new Rect((float)x / c, (float)y / c, 1f / c, 1f / c);
                        GUI.DrawTextureWithTexCoords(rect, tex, texCoords);

                        if (idx == _selectedTexIdx)
                            GUI.Box(rect, "Selected", EditorStyles.toolbarButton);

                        Event e = Event.current;
                        if (e.type == EventType.MouseDown)
                        {
                            if (rect.Contains(e.mousePosition))
                            {
                                _selectedTexIdx = idx;
                                e.Use();
                            }
                        }

                        idx++;
                        col++;
                        if (col == 4)
                        {
                            col = 0;
                            row++;
                            GUILayout.Space(size * 2);
                        }
                    }
                }
            }
            else
            {
                foreach (var key in ground.tileset.terrainNames)
                {
                    if (_selectedTexIdx == ground.tileset.terrainNames.IndexOf(key))
                        GUI.color = Color.green;
                    else
                        GUI.color = Color.white;
                    if (GUILayout.Button(key))
                    {
                        _selectedTexIdx = ground.tileset.terrainNames.IndexOf(key);
                    }
                    GUI.color = Color.white;
                }
            }
            GUILayout.EndScrollView();
        }

        private void OnHeightHandles()
        {
            var ground = target as GroundMesh;
            var brushHeight = ground._brushHeight;
            var brushSize = ground._brushSize;
            
            SceneView.RepaintAll();

            for (int row = 0; row < brushSize; ++row)
            {
                for (int col = 0; col < brushSize; ++col)
                {
                    var sx = col;
                    var sy = row;
                    sx -= brushSize / 2;
                    sy -= brushSize / 2;
                    int vx = Mathf.RoundToInt(_mouse.x + sx);
                    int vz = Mathf.RoundToInt(_mouse.z + sy);
                    var key = string.Format(KeyFormat, vx, vz);
                    var currentY = 0f;
                    if (_indexCache.ContainsKey(key))
                    {
                        var list = _indexCache[key];
                        foreach (var i in list)
                        {
                            var v = _vertices[i];
                            currentY = v.y;
                            break;
                        }
                    }

                    Handles.color = Color.yellow;
                    if (brushHeight > currentY)
                        Handles.ConeCap(0, new Vector3(vx, CapSizeHalf + brushHeight, vz), CapRotationUp, CapSize);
                    else if (brushHeight < currentY)
                        Handles.ConeCap(0, new Vector3(vx, CapSizeHalf + brushHeight, vz), CapRotationBottom, CapSize);
                    else
                        Handles.CubeCap(0, new Vector3(vx, 0f + brushHeight, vz), CapRotationUp, CapSize);
                    Handles.DrawLine(new Vector3(vx, currentY, vz), new Vector3(vx, brushHeight, vz));

                    if (row + 1 >= brushSize || col + 1 >= brushSize)
                        continue;

                    Handles.color = Color.red;
                    Handles.DrawLine(new Vector3(vx, currentY, vz), new Vector3(vx + 1, currentY, vz));
                    Handles.DrawLine(new Vector3(vx + 1, currentY, vz), new Vector3(vx + 1, currentY, vz + 1));
                    Handles.DrawLine(new Vector3(vx + 1, currentY, vz + 1), new Vector3(vx, currentY, vz + 1));
                    Handles.DrawLine(new Vector3(vx, currentY, vz + 1), new Vector3(vx, currentY, vz));

                    Handles.color = Color.yellow;
                    Handles.DrawLine(new Vector3(vx, brushHeight, vz), new Vector3(vx + 1, brushHeight, vz));
                    Handles.DrawLine(new Vector3(vx + 1, brushHeight, vz), new Vector3(vx + 1, brushHeight, vz + 1));
                    Handles.DrawLine(new Vector3(vx + 1, brushHeight, vz + 1), new Vector3(vx, brushHeight, vz + 1));
                    Handles.DrawLine(new Vector3(vx, brushHeight, vz + 1), new Vector3(vx, brushHeight, vz));
                }
            }
        }

        private void OnTileHandles()
        {
            var ground = target as GroundMesh;
            var brushSize = ground._brushSize;
            if (brushSize < 2)
                return;

            SceneView.RepaintAll();

            for (int row = 0; row < brushSize; ++row)
            {
                for (int col = 0; col < brushSize; ++col)
                {
                    var sx = col;
                    var sy = row;
                    sx -= brushSize / 2;
                    sy -= brushSize / 2;
                    int vx = Mathf.RoundToInt(_mouse.x + sx);
                    int vz = Mathf.RoundToInt(_mouse.z + sy);
                    var key = string.Format(KeyFormat, vx, vz);
                    var currentY = 0f;
                    if (_indexCache.ContainsKey(key))
                    {
                        var list = _indexCache[key];
                        foreach (var i in list)
                        {
                            var v = _vertices[i];
                            currentY = v.y;
                            break;
                        }
                    }
                    
                    if (row + 1 >= brushSize || col + 1 >= brushSize)
                        continue;
                    
                    Handles.color = Color.cyan;
                    Handles.DrawLine(new Vector3(vx, currentY, vz), new Vector3(vx + 1, currentY, vz));
                    Handles.DrawLine(new Vector3(vx + 1, currentY, vz), new Vector3(vx + 1, currentY, vz + 1));
                    Handles.DrawLine(new Vector3(vx + 1, currentY, vz + 1), new Vector3(vx, currentY, vz + 1));
                    Handles.DrawLine(new Vector3(vx, currentY, vz + 1), new Vector3(vx, currentY, vz));
                }
            }
        }
        
        public void SetVertex(Vector3 mouse)
        {
            var ground = target as GroundMesh;
            var brushSize = ground._brushSize;
            var brushHeight = ground._brushHeight;

            bool dirty = false;
            for (int row = 0; row < brushSize; ++row)
            {
                for (int col = 0; col < brushSize; ++col)
                {
                    var sx = col;
                    var sy = row;
                    sx -= brushSize / 2;
                    sy -= brushSize / 2;
                    int vx = Mathf.RoundToInt(mouse.x + sx);
                    int vz = Mathf.RoundToInt(mouse.z + sy);
                    
                    var key = string.Format(KeyFormat, vx, vz);
                    if (!_indexCache.ContainsKey(key))
                        continue;
                    
                    var list = _indexCache[key];
                    foreach (var i in list)
                    {
                        if (!dirty)
                        {
                            dirty = true;
                            if (Event.current.type == EventType.mouseDown)
                                RegisterHistory();
                        }
                        
                        var v = _vertices[i];
                        v.y = brushHeight;
                        _vertices[i] = v;
                    }
                }
            }
            
            UpdateMesh();
        }
        
        public void SetUV(Vector3 mouse, int texIndex)
        {
            var ground = target as GroundMesh;
            var brushSize = ground._brushSize;
            if (brushSize < 2)
                return;

            bool dirty = false;

            for (int row = 0; row < brushSize - 1; ++row)
            {
                for (int col = 0; col < brushSize - 1; ++col)
                {
                    var sx = col;
                    var sy = row;
                    sx -= brushSize / 2;
                    sy -= brushSize / 2;
                    int vx = Mathf.RoundToInt(_mouse.x + sx);
                    int vz = Mathf.RoundToInt(_mouse.z + sy);

                    var idx = vx  * 4 + (ground._cols * 4 * vz);
                    if (vx < 0 || vx >= ground._cols 
                        || vz < 0 || vz >= ground._rows)
                        continue;

                    float c = (float)ground.tileset.columnCount;
                    float i = (float)texIndex;
                    float x = i % c;
                    float y = Mathf.Floor(i / c);
                    
                    if (!dirty && !_drawHeight)
                    {
                        dirty = true;
                        if (Event.current.type == EventType.mouseDown)
                            RegisterHistory();
                    }
                    
                    if (_texRotation == 0)
                    {
                        _uv[idx] = new Vector2(x / c, y / c);
                        _uv[idx + 1] = new Vector2((x + 1) / c, y / c);
                        _uv[idx + 2] = new Vector2((x + 1) / c, (y + 1) / c);
                        _uv[idx + 3] = new Vector2(x / c, (y + 1) / c);
                    }
                    else if (_texRotation == 90)
                    {
                        _uv[idx] = new Vector2((x + 1) / c, y / c);
                        _uv[idx + 1] = new Vector2((x + 1) / c, (y + 1) / c);
                        _uv[idx + 2] = new Vector2(x / c, (y + 1) / c);
                        _uv[idx + 3] = new Vector2(x / c, y / c);
                    }
                    else if (_texRotation == 180)
                    {
                        _uv[idx] = new Vector2((x + 1) / c, (y + 1) / c);
                        _uv[idx + 1] = new Vector2(x / c, (y + 1) / c);
                        _uv[idx + 2] = new Vector2(x / c, y / c);
                        _uv[idx + 3] = new Vector2((x + 1) / c, y / c);
                    }
                    else
                    {
                        _uv[idx] = new Vector2(x / c, (y + 1) / c);
                        _uv[idx + 1] = new Vector2(x / c, y / c);
                        _uv[idx + 2] = new Vector2((x + 1) / c, y / c);
                        _uv[idx + 3] = new Vector2((x + 1) / c, (y + 1) / c);
                    }
                }
            }
            
            UpdateMesh();
        }

        private void SetTerrain()
        {
            var ground = target as GroundMesh;
            var brushSize = ground._brushSize;
            if (brushSize < 2)
                return;
            
            bool dirty = false;
            
            for (int row = 0; row < brushSize - 1; ++row)
            {
                for (int col = 0; col < brushSize - 1; ++col)
                {
                    for (var dirX = -1; dirX < 2; ++dirX)
                    {
                        for (var dirY = -1; dirY < 2; ++dirY)
                        {
                            var sx = col;
                            var sy = row;
                            sx -= brushSize / 2;
                            sy -= brushSize / 2;
                            int vx = Mathf.RoundToInt(_mouse.x + sx) + dirX;
                            int vz = Mathf.RoundToInt(_mouse.z + sy) + dirY;
                            var terrainIdx = vx + vz * ground.rows;
                            if (terrainIdx < 0 || terrainIdx >= ground._terrains.Count)
                                continue;
                            
                            var currentTerrain = ground._terrains[terrainIdx].Split(',');
                            var str = "";
                            
                            for (int arrIdx = 0; arrIdx < 4; ++arrIdx)
                            {
                                if (currentTerrain[arrIdx] == "-1")
                                    currentTerrain[arrIdx] = _selectedTexIdx.ToString();
                            }
                            
                            if (dirX == -1 && dirY == -1)
                            {
                                currentTerrain[1] = _selectedTexIdx.ToString();
                            }
                            if (dirX == 0 && dirY == -1)
                            {
                                currentTerrain[0] = _selectedTexIdx.ToString();
                                currentTerrain[1] = _selectedTexIdx.ToString();
                            }
                            if (dirX == 1 && dirY == -1)
                            {
                                currentTerrain[0] = _selectedTexIdx.ToString();
                            }
                            if (dirX == -1 && dirY == 0)
                            {
                                currentTerrain[1] = _selectedTexIdx.ToString();
                                currentTerrain[3] = _selectedTexIdx.ToString();
                            }
                            if (dirX == 0 && dirY == 0)
                            {
                                currentTerrain[0] = _selectedTexIdx.ToString();
                                currentTerrain[1] = _selectedTexIdx.ToString();
                                currentTerrain[2] = _selectedTexIdx.ToString();
                                currentTerrain[3] = _selectedTexIdx.ToString();
                            }
                            if (dirX == 1 && dirY == 0)
                            {
                                currentTerrain[0] = _selectedTexIdx.ToString();
                                currentTerrain[2] = _selectedTexIdx.ToString();
                            }
                            if (dirX == -1 && dirY == 1)
                            {
                                currentTerrain[3] = _selectedTexIdx.ToString();
                            }
                            if (dirX == 0 && dirY == 1)
                            {
                                currentTerrain[2] = _selectedTexIdx.ToString();
                                currentTerrain[3] = _selectedTexIdx.ToString();
                            }
                            if (dirX == 1 && dirY == 1)
                            {
                                currentTerrain[2] = _selectedTexIdx.ToString();
                            }
                            str = string.Format("{0},{1},{2},{3}", currentTerrain[0], currentTerrain[1], currentTerrain[2], currentTerrain[3]);
                            
                            var texIdx = ground.tileset.tileTerrains.IndexOf(str);
                            if (texIdx < 0)
                                continue;
                            
                            var idx = vx * 4 + (ground._cols * 4 * vz);
                            if (vx < 0 
                                || vx >= ground._cols 
                                || vz < 0 
                                || vz >= ground._rows)
                                continue;
                            
                            if (!dirty && !_drawHeight)
                            {
                                dirty = true;
                                if (Event.current.type == EventType.mouseDown)
                                    RegisterHistory();
                            }
                            
                            float c = (float)ground.tileset.columnCount;
                            float i = (float)texIdx;
                            float x = i % c;
                            float y = Mathf.Floor(i / c);
                            
                            ground._terrains[terrainIdx] = str;
                            
                            _uv[idx] = new Vector2(x / c, y / c);
                            _uv[idx + 1] = new Vector2((x + 1) / c, y / c);
                            _uv[idx + 2] = new Vector2((x + 1) / c, (y + 1) / c);
                            _uv[idx + 3] = new Vector2(x / c, (y + 1) / c);
                        }
                    }
                }
            }
            
            UpdateMesh();
            
            UpdateMesh();
        }

        private void DrawCursorIndex()
        {
            var ground = target as GroundMesh;
            Handles.Label(new Vector3(_mouse.x, ground._brushHeight, _mouse.z), 
                          string.Format("{0}, {1}", (int)_mouse.x, (int)_mouse.z));
        }

        public Dictionary<string, List<int>> GetIndexDict()
        {
            var dict = new Dictionary<string, List<int>>();
            var key = "";
            for (var i = 0; i < _vertices.Count; ++i)
            {
                var v = _vertices[i];
                key = string.Format(KeyFormat, v.x, v.z);
                if (!dict.ContainsKey(key))
                    dict[key] = new List<int>();
                
                dict[key].Add(i);
            }
            
            return dict;
        }

        private void RegisterHistory()
        {
            var ground = target as GroundMesh;
            ground.RegisterHistory(_vertices, _uv, ground._terrains);
            EditorUtility.SetDirty(target);
        }

        private void UndoHistory()
        {
            var ground = target as GroundMesh;
            var item = ground.UndoHistory(_vertices, _uv, ground._terrains);
            if (item.vertices != null)
            {
                _vertices = item.vertices;
                _uv = item.uv;
                ground._terrains = item.terrains;

                ground.UpdateMesh(_vertices, _uv, _triangles);
            }
            EditorUtility.SetDirty(target);
        }
        
        private void RedoHistory()
        {
            var ground = target as GroundMesh;
            var item = ground.RedoHistory();
            if (item.vertices != null)
            {
                _vertices = item.vertices;
                _uv = item.uv;
                ground.UpdateMesh(_vertices, _uv, _triangles);
            }
            EditorUtility.SetDirty(target);
        }
    }
}
