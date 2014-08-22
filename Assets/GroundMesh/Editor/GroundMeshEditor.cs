using UnityEngine;
using UnityEditor;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

[CustomEditor(typeof(GroundMesh))]
public class GroundMeshEditor : Editor
{
    private enum EditMode
    {
        Lock,
        Vertex,
        UV,
    }

    private readonly float CapSize = 0.2f;
    private readonly float CapSizeHalf = 0.1f;
    private readonly Quaternion CapRotationUp = Quaternion.Euler(270, 0, 0);
    private readonly Quaternion CapRotationBottom = Quaternion.Euler(90, 0, 0);
    private readonly KeyCode KeyToggleEditMode = KeyCode.Tab;
    private readonly KeyCode KeyBrushHeightUp = KeyCode.W;
    private readonly KeyCode KeyBrushHeightDown = KeyCode.S;
    private readonly KeyCode KeyBrushHeightReset = KeyCode.R;
    private readonly KeyCode KeyBrushSizeUp = KeyCode.D;
    private readonly KeyCode KeyBrushSizeDown = KeyCode.A;
    private readonly KeyCode KeyBrushRotateMinus = KeyCode.Q;
    private readonly KeyCode KeyBrushRotatePlus = KeyCode.E;
    private readonly KeyCode KeyUndo = KeyCode.Z;
    private readonly KeyCode KeyRedo = KeyCode.Y;
    
    private EditMode _editMode = EditMode.Lock;
    private Rect _winsize = new Rect(10, 30, 10, 10);
    private Vector3 _mouse;
    private Matrix4x4 _worldToLocal;
    private List<Vector3> _vertices;
    private List<Vector2> _uv;
    private List<int> _triangles;
    private Dictionary <string, List<int>> _indexCache;
    private int _selectedTexIdx = 0;
    private int _texRotation = 0;

    private SerializedProperty _serializedVertices;

    void OnEnable()
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

        _indexCache = GetIndexDict();
    }

    public override void OnInspectorGUI()
    {
        var ground = target as GroundMesh;

        base.OnInspectorGUI();

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
        else if (GUILayout.Button("Init UV"))
        {
            if (!EditorUtility.DisplayDialog("Init UV", "Are you sure?", "New", "Cancel"))
                return;
            
            InitUV();
        }
        else if (GUILayout.Button("Create MeshCollider"))
        {
            if (ground.GetComponent<MeshCollider>() != null)
                DestroyImmediate(ground.GetComponent<MeshCollider>());

            ground.gameObject.AddComponent<MeshCollider>();
        }
        else if (GUILayout.Button(string.Format("Clean History ({0}/{1})", ground._historyIndex, ground._histories.Count)))
        {
            ground.ClearHistory();
            EditorUtility.UnloadUnusedAssets();
        }
    }

    public void DestroyCollider()
    {
        var ground = target as GroundMesh;

        if (ground.GetComponent<MeshCollider>() != null)
            DestroyImmediate(ground.GetComponent<MeshCollider>());

        EditorUtility.UnloadUnusedAssets();
    }

    public void NewMesh()
    {
        DestroyCollider();

        var ground = target as GroundMesh;
        
        _vertices.Clear();
        _uv.Clear();
        _triangles.Clear();

        var c = (float)ground.texCountPerRow;
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
        
        UpdateMesh();
    }
    
    public void ResizeMesh()
    {
        DestroyCollider();

        var ground = target as GroundMesh;

        var oldVertices = new List<Vector3>(_vertices);
        var oldUV = new List<Vector2>(_uv);
        
        _vertices.Clear();
        _uv.Clear();
        _triangles.Clear();
        
        var c = (float)ground.texCountPerRow;
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
        
        UpdateMesh();
        ground.ClearHistory();
    }

    private void InitUV()
    {
        var ground = target as GroundMesh;

        _uv.Clear();
        
        var c = (float)ground.texCountPerRow;
        for (int y = 0; y < ground.rows; ++y)
        {
            for (int x = 0; x < ground.cols; ++x)
            {        
                _uv.Add(new Vector2(0f, 0f));
                _uv.Add(new Vector2(1f / c, 0f));
                _uv.Add(new Vector2(1f / c, 1f / c));
                _uv.Add(new Vector2(0f, 1f / c));
            }
        }
        
        UpdateMesh();
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
        if (_editMode != EditMode.Lock)
        {
            OnKey();
            OnMouse();
        }

        GUILayout.Window(0, _winsize, DrawWindow, "Ground Mesh Editor");

        DrawCursorIndex();

        if (_editMode == EditMode.Lock)
            return;
        else if (_editMode == EditMode.Vertex)
            DrawVertexHandles();
        else
            DrawUVHandles();
    }
    
    private void OnKey()
    {
        var e = Event.current;

        var ground = target as GroundMesh;
        if (e.type == EventType.KeyDown)
        {
            if (e.keyCode == KeyToggleEditMode)
            {
                ToggleEditMode();
                e.Use();
            }
            else if (e.keyCode == KeyBrushSizeUp)
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
            if (_editMode == EditMode.Vertex)
                SetVertex(_mouse);
            else
                SetUV(_mouse, _selectedTexIdx, _texRotation);

            e.Use();
        }
    }
    
    private void DrawWindow(int a)
    {
        GUILayout.BeginVertical();

        if (_editMode == EditMode.Lock)
        {
            GUI.color = Color.red;
            if (GUILayout.Button("Lock", GUILayout.MinWidth(320)))
            {
                _editMode = EditMode.Vertex;
            }
            GUI.color = Color.white;
            GUILayout.EndVertical();
            return;
        }

        GUILayout.Space(10);
        
        if (GUILayout.Button(string.Format("Toggle Edit Mode (Tab) : {0}", _editMode.ToString())))
        {
            ToggleEditMode();
        }

        GUILayout.Space(10);

        GUILayout.BeginHorizontal();
        if (GUILayout.Button(string.Format("Undo ({0})", KeyUndo)))
            UndoHistory();
        if (GUILayout.Button(string.Format("Redo ({0})", KeyRedo)))
            RedoHistory();
        GUILayout.EndHorizontal();

        DrawBrushGUI();
        if (_editMode == EditMode.UV)
        {
            DrawTextureGUI();
        }
        
        GUILayout.EndVertical();
    }
    
    private void DrawBrushGUI()
    {
        var ground = target as GroundMesh;

        GUILayout.Space(4);

        GUILayout.Label("Brush");

        GUILayout.BeginHorizontal();
        ground._brushHeightUnit = EditorGUILayout.FloatField("Height Unit ", ground._brushHeightUnit);
        GUILayout.EndHorizontal();

        GUILayout.Space(4);

        GUILayout.BeginHorizontal();
        ground._brushHeight = EditorGUILayout.FloatField("Height ", ground._brushHeight);
        if (GUILayout.Button(string.Format("+ ({0})", KeyBrushHeightUp)))
            ground._brushHeight += ground._brushHeightUnit;
        if (GUILayout.Button(string.Format("- ({0})", KeyBrushHeightDown)))
            ground._brushHeight -= ground._brushHeightUnit;
        if (GUILayout.Button(string.Format("Reset ({0})", KeyBrushHeightReset)))
            ground._brushHeight = 0f;
        GUILayout.EndHorizontal();

        GUILayout.Space(4);

        GUILayout.BeginHorizontal();
        ground._brushSize = EditorGUILayout.IntField("Size ", ground._brushSize);
        if (GUILayout.Button(string.Format("+ ({0})", KeyBrushSizeUp)))
            ground._brushSize = Mathf.Min(20, ground._brushSize + 1);
        if (GUILayout.Button(string.Format("- ({0})", KeyBrushSizeDown)))
            ground._brushSize = Mathf.Max(1, ground._brushSize - 1);
        GUILayout.EndHorizontal();
    }

    private void DrawTextureGUI()
    {
        var ground = target as GroundMesh;
        if (ground == null || ground.renderer == null)
            return;

        Material mat = ground.renderer.sharedMaterial;
        if (mat == null)
            return;
        var tex = mat.GetTexture(0);
        if (tex == null)
            return;

        GUILayout.Space(10);

        EditorGUILayout.ObjectField(mat, typeof(Material), false);

        GUILayout.BeginHorizontal();
        GUILayout.Label(string.Format("Tex Rotation: {0}", _texRotation));
        if (GUILayout.Button(string.Format("Rotate ({0}), ({1})", KeyBrushRotateMinus, KeyBrushRotatePlus)))
        {
            _texRotation += 90;
            if (_texRotation >= 360)
                _texRotation = 0;
        }
        GUILayout.EndHorizontal();

        int beginX = 10;
        int beginY = 260;
        int margin = 15;
        int size = 60;
        int idx = 0;
        int col = 0;
        int row = 0;
        for (int y = 0; y < ground.texCountPerRow; ++y)
        {
            for (int x = 0; x < ground.texCountPerRow; ++x)
            {
                var rect = new Rect((size + margin) * col + beginX, 
                                    (size + margin) * row + beginY, 
                                    size, size);

                var c = (float)ground.texCountPerRow;
                var texCoords = new Rect((float)x / c, (float)y / c, 1f / c, 1f / c);
                GUI.DrawTextureWithTexCoords(rect, tex, texCoords);

                if (idx == _selectedTexIdx)
                    GUI.Box(rect, "Selected", EditorStyles.toolbarButton);

                Event e = Event.current;
                if (e.type == EventType.MouseUp)
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

    private void DrawVertexHandles()
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
                var key = string.Format(ground.KeyFormat, vx, vz);
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

    private void DrawUVHandles()
    {
        var ground = target as GroundMesh;
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
                int vx = Mathf.RoundToInt(_mouse.x + sx + 0.5f);
                int vz = Mathf.RoundToInt(_mouse.z + sy + 0.5f);
                
                var key = string.Format(ground.KeyFormat, vx, vz);
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
                
                var key = string.Format(ground.KeyFormat, vx, vz);
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
    
    public void SetUV(Vector3 mouse, int texIdx, int texRotation)
    {
        var ground = target as GroundMesh;
        var brushSize = ground._brushSize;

        bool dirty = false;
        for (int row = 0; row < brushSize; ++row)
        {
            for (int col = 0; col < brushSize; ++col)
            {
                if (row + 1 >= brushSize || col + 1 >= brushSize)
                    continue;
                
                var sx = col;
                var sy = row;
                sx -= brushSize / 2;
                sy -= brushSize / 2;
                int vx = Mathf.RoundToInt(mouse.x + sx + 0.5f);
                int vz = Mathf.RoundToInt(mouse.z + sy + 0.5f);
                
                var idx = vx  * 4 + (ground._cols * 4 * vz);
                if (vx < 0 
                    || vx >= ground._cols 
                    || vz < 0 
                    || vz >= ground._rows)
                    continue;
                
                float c = (float)ground.texCountPerRow;
                float i = (float)texIdx;
                float x = i % c;
                float y = Mathf.Floor(i / c);
                
                if (!dirty)
                {
                    dirty = true;
                    if (Event.current.type == EventType.mouseDown)
                        RegisterHistory();
                }
                
                if (texRotation == 0)
                {
                    _uv[idx] = new Vector2(x / c, y / c);
                    _uv[idx + 1] = new Vector2((x + 1) / c, y / c);
                    _uv[idx + 2] = new Vector2((x + 1) / c, (y + 1) / c);
                    _uv[idx + 3] = new Vector2(x / c, (y + 1) / c);
                }
                else if (texRotation == 90)
                {
                    _uv[idx] = new Vector2((x + 1) / c, y / c);
                    _uv[idx + 1] = new Vector2((x + 1) / c, (y + 1) / c);
                    _uv[idx + 2] = new Vector2(x / c, (y + 1) / c);
                    _uv[idx + 3] = new Vector2(x / c, y / c);
                }
                else if (texRotation == 180)
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

    private void DrawCursorIndex()
    {
        var ground = target as GroundMesh;
        Handles.Label(new Vector3(_mouse.x, ground._brushHeight, _mouse.z), 
                      string.Format("{0}, {1}", (int)_mouse.x, (int)_mouse.z));
    }

    private void ToggleEditMode()
    {
        var ground = target as GroundMesh;
        if (_editMode == EditMode.Vertex)
        {
            _editMode = EditMode.UV;
            if (ground._brushSize == 1)
                ground._brushSize = 2;
        }
        else
        {
            _editMode = EditMode.Vertex;
        }
    }

    public Dictionary<string, List<int>> GetIndexDict()
    {
        var ground = target as GroundMesh;
        var dict = new Dictionary<string, List<int>>();
        var key = "";
        for (var i = 0; i < _vertices.Count; ++i)
        {
            var v = _vertices[i];
            key = string.Format(ground.KeyFormat, v.x, v.z);
            if (!dict.ContainsKey(key))
                dict[key] = new List<int>();
            
            dict[key].Add(i);
        }
        
        return dict;
    }

    private void RegisterHistory()
    {
        var ground = target as GroundMesh;
        ground.RegisterHistory(_vertices, _uv);
        EditorUtility.SetDirty(target);
    }

    private void UndoHistory()
    {
        var ground = target as GroundMesh;
        var item = ground.UndoHistory(_vertices, _uv);
        if (item.vertices != null)
        {
            _vertices = item.vertices;
            _uv = item.uv;
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
