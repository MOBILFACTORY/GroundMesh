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

    private bool _lock = true;
    private Rect _winsize = new Rect(10, 30, 10, 10);
    private Vector3 _mouse;
    private Matrix4x4 _worldToLocal;
    private EditMode _editMode = EditMode.Vertex;
    private bool _init = false;
    private int _selectedTexIdx = 0;
    private int _texRotation = 0;

    [SerializeField]
    public List<Vector3> vertices = new List<Vector3>();
    [SerializeField]
    public List<Vector2> uv = new List<Vector2>();

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        var ground = target as GroundMesh;
        if (GUILayout.Button("New Mesh"))
        {
            if (!EditorUtility.DisplayDialog("New", "Are you sure?", "New", "Cancel"))
                return;

            if (ground.GetComponent<MeshCollider>() != null)
                DestroyImmediate(ground.GetComponent<MeshCollider>());

            ground.NewMesh();
            EditorUtility.UnloadUnusedAssets();
        }
        else if (GUILayout.Button("Resize Mesh"))
        {
            if (ground._rows > ground.rows
                || ground._cols > ground.cols)
            {
                if (!EditorUtility.DisplayDialog("Resize", "Are you sure?", "Resize", "Cancel"))
                    return;
            }

            if (ground.GetComponent<MeshCollider>() != null)
                DestroyImmediate(ground.GetComponent<MeshCollider>());

            ground.ResizeMesh();
            EditorUtility.UnloadUnusedAssets();
        }
        else if (GUILayout.Button("Create MeshCollider"))
        {
            if (ground.GetComponent<MeshCollider>() != null)
                DestroyImmediate(ground.GetComponent<MeshCollider>());

            ground.gameObject.AddComponent<MeshCollider>();
        }
        else if (GUILayout.Button("Clean"))
        {
            ground.Clean();
            EditorUtility.UnloadUnusedAssets();
        }
    }

    private void OnSceneGUI()
    {
        Init();

        if (!_lock)
        {
            OnKey();
            OnMouse();
        }

        GUILayout.Window(0, _winsize, DrawWindow, "Ground Mesh Editor");

        DrawCursorIndex();

        if (_lock)
            return;

        if (_editMode == EditMode.Vertex)
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
                ground.UndoHistory();
                e.Use();
            }
            else if (e.keyCode == KeyRedo)
            {
                ground.RedoHistory();
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
            {
                ground.SetVertex(_mouse);
            }
            else
                ground.SetUV(_mouse, _selectedTexIdx, _texRotation);

            e.Use();
        }
    }
    
    private void Init()
    {
        if (_init)
            return;

        _init = true;

        Tools.current = Tool.View;

        var ground = target as GroundMesh;
        ground.CacheIndex();

        var mesh = ground.GetComponent<MeshFilter>().sharedMesh;
        if (mesh != null)
        {
            ground.vertices = new List<Vector3>(mesh.vertices);
            ground.uv = new List<Vector2>(mesh.uv);
            ground.triangles = new List<int>(mesh.triangles);
        }
    }
    
    private void DrawWindow(int a)
    {
        GUILayout.BeginVertical();

        var ground = target as GroundMesh;

        if (_lock)
        {
            GUI.color = Color.red;
            if (GUILayout.Button("Lock", GUILayout.MinWidth(320)))
            {
                _lock = !_lock;
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
            ground.UndoHistory();
        if (GUILayout.Button(string.Format("Redo ({0})", KeyRedo)))
            ground.RedoHistory();
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
                if (ground._indexCache.ContainsKey(key))
                {
                    var list = ground._indexCache[key];
                    foreach (var i in list)
                    {
                        var v = ground.vertices[i];
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
                if (ground._indexCache.ContainsKey(key))
                {
                    var list = ground._indexCache[key];
                    foreach (var i in list)
                    {
                        var v = ground.vertices[i];
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
}
