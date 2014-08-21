using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[ExecuteInEditMode]
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class GroundMesh : MonoBehaviour
{
#if UNITY_EDITOR
    public struct HistoryItem
    {
        public List<Vector3> vertices;
        public List<Vector2> uv;
    }

    public readonly int HistoryLimit = 50;
    public readonly string KeyFormat = "{0},{1}";

    public int cols = 10;
    public int rows = 10;
    public int texCountPerRow = 1;
    [HideInInspector]
    public int _cols;
    [HideInInspector]
    public int _rows;
    [HideInInspector]
    public List<Vector3> vertices = new List<Vector3>();
    [HideInInspector]
    public List<Vector2> uv = new List<Vector2>();
    [HideInInspector]
    public List<int> triangles = new List<int>();
    [HideInInspector]
    public float _brushHeight = 0f;
    [HideInInspector]
    public float _brushHeightUnit = 0.5f;
    [HideInInspector]
    public int _brushSize = 1;
    [HideInInspector]
    public Dictionary <string, List<int>> _indexCache = new Dictionary<string, List<int>>();
    [HideInInspector]
    public int _historyIndex = 0;
    [HideInInspector]
    public List<HistoryItem> _histories = new List<HistoryItem>();
    [HideInInspector]
    public bool _isPresentHistory = true;

    public void NewMesh()
    {
        Clear();

        var c = (float)texCountPerRow;
        var i = 0;
        for (int y = 0; y < rows; ++y)
        {
            for (int x = 0; x < cols; ++x)
            {
                vertices.Add(new Vector3(x, 0, y));
                vertices.Add(new Vector3(x + 1, 0, y));
                vertices.Add(new Vector3(x + 1, 0, y + 1));
                vertices.Add(new Vector3(x, 0, y + 1));

                uv.Add(new Vector2(0f, 0f));
                uv.Add(new Vector2(1f / c, 0f));
                uv.Add(new Vector2(1f / c, 1f / c));
                uv.Add(new Vector2(0f, 1f / c));
                
                triangles.Add(i);
                triangles.Add(i + 3);
                triangles.Add(i + 2);
                triangles.Add(i);
                triangles.Add(i + 2);
                triangles.Add(i + 1);
                
                i += 4;
            }
        }
        
        _cols = cols;
        _rows = rows;

        UpdateMesh();
        ClearHistory();
    }

    public void ResizeMesh()
    {
        var tempVertices = new List<Vector3>(vertices);
        var tempUV = new List<Vector2>(uv);

        vertices.Clear();
        uv.Clear();
        triangles.Clear();

        var c = (float)texCountPerRow;
        var i = 0;
        for (int y = 0; y < rows; ++y)
        {
            for (int x = 0; x < cols; ++x)
            {
                vertices.Add(new Vector3(x, 0, y));
                vertices.Add(new Vector3(x + 1, 0, y));
                vertices.Add(new Vector3(x + 1, 0, y + 1));
                vertices.Add(new Vector3(x, 0, y + 1));
                
                uv.Add(new Vector2(0f, 0f));
                uv.Add(new Vector2(1f / c, 0f));
                uv.Add(new Vector2(1f / c, 1f / c));
                uv.Add(new Vector2(0f, 1f / c));
                
                triangles.Add(i);
                triangles.Add(i + 3);
                triangles.Add(i + 2);
                triangles.Add(i);
                triangles.Add(i + 2);
                triangles.Add(i + 1);
                
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
                    vertices[idx] = tempVertices[idx2];
                }
            }
        }

        for (int y = 0; y < _rows; ++y)
        {
            for (int x = 0; x < _cols; ++x)
            {
                var oldIdx = x  * 4 + (_cols * 4 * y);
                var newIdx = x  * 4 + (cols * 4 * y);
                if (uv.Count <= newIdx || tempUV.Count <= oldIdx)
                    continue;

                uv[newIdx + 0] = tempUV[oldIdx + 0];
                uv[newIdx + 1] = tempUV[oldIdx + 1];
                uv[newIdx + 2] = tempUV[oldIdx + 2];
                uv[newIdx + 3] = tempUV[oldIdx + 3];
            }
        }

        _cols = cols;
        _rows = rows;
        
        _indexCache = null;
        
        var filter = GetComponent<MeshFilter>();
        if (filter != null && filter.sharedMesh != null)
            filter.sharedMesh = null;
        
        UpdateMesh();
        ClearHistory();
    }

    public void Clean()
    {
        vertices.Clear();
        uv.Clear();
        triangles.Clear();
        _histories.Clear();
        _indexCache.Clear();
    }

    public void UpdateMesh()
    {
        if (vertices.Count == 0)
            return;

        var filter = GetComponent<MeshFilter>();
        var mesh = filter.sharedMesh;
        if (mesh == null)
            mesh = new Mesh();

        mesh.name = "GroundMesh";
        mesh.vertices = vertices.ToArray();
        mesh.uv = uv.ToArray();
        mesh.triangles = triangles.ToArray();

        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        mesh.Optimize();

        filter.sharedMesh = mesh;

        CacheIndex();
    }

    public void CacheIndex()
    {
        if (_indexCache == null
            || _indexCache.Count == 0)
            _indexCache = GetIndexDict();
    }

    public Dictionary<string, List<int>> GetIndexDict()
    {
        var dict = new Dictionary<string, List<int>>();
        var key = "";
        for (var i = 0; i < vertices.Count; ++i)
        {
            var v = vertices[i];
            key = string.Format(KeyFormat, v.x, v.z);
            if (!dict.ContainsKey(key))
                dict[key] = new List<int>();
            
            dict[key].Add(i);
        }

        return dict;
    }

    public void Clear()
    {
        vertices.Clear();
        uv.Clear();
        triangles.Clear();

        _indexCache = null;
        
        var filter = GetComponent<MeshFilter>();
        if (filter != null && filter.sharedMesh != null)
            filter.sharedMesh = null;
    }

    public void SetVertex(Vector3 mouse)
    {
        bool dirty = false;
        for (int row = 0; row < _brushSize; ++row)
        {
            for (int col = 0; col < _brushSize; ++col)
            {
                var sx = col;
                var sy = row;
                sx -= _brushSize / 2;
                sy -= _brushSize / 2;
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
                            ResisterHistory();
                    }
                    
                    var v = vertices[i];
                    v.y = _brushHeight;
                    vertices[i] = v;
                }
            }
        }

        UpdateMesh();
    }
    
    public void SetUV(Vector3 mouse, int texIdx, int texRotation)
    {
        bool dirty = false;
        for (int row = 0; row < _brushSize; ++row)
        {
            for (int col = 0; col < _brushSize; ++col)
            {
                if (row + 1 >= _brushSize || col + 1 >= _brushSize)
                    continue;
                
                var sx = col;
                var sy = row;
                sx -= _brushSize / 2;
                sy -= _brushSize / 2;
                int vx = Mathf.RoundToInt(mouse.x + sx + 0.5f);
                int vz = Mathf.RoundToInt(mouse.z + sy + 0.5f);
                
                var idx = vx  * 4 + (cols * 4 * vz);
                if (vx < 0 
                    || vx >= cols 
                    || vz < 0 
                    || vz >= rows)
                    continue;
                
                float c = (float)texCountPerRow;
                float i = (float)texIdx;
                float x = i % c;
                float y = Mathf.Floor(i / c);
                
                if (!dirty)
                {
                    dirty = true;
                    if (Event.current.type == EventType.mouseDown)
                        ResisterHistory();
                }
                
                if (texRotation == 0)
                {
                    uv[idx] = new Vector2(x / c, y / c);
                    uv[idx + 1] = new Vector2((x + 1) / c, y / c);
                    uv[idx + 2] = new Vector2((x + 1) / c, (y + 1) / c);
                    uv[idx + 3] = new Vector2(x / c, (y + 1) / c);
                }
                else if (texRotation == 90)
                {
                    uv[idx] = new Vector2((x + 1) / c, y / c);
                    uv[idx + 1] = new Vector2((x + 1) / c, (y + 1) / c);
                    uv[idx + 2] = new Vector2(x / c, (y + 1) / c);
                    uv[idx + 3] = new Vector2(x / c, y / c);
                }
                else if (texRotation == 180)
                {
                    uv[idx] = new Vector2((x + 1) / c, (y + 1) / c);
                    uv[idx + 1] = new Vector2(x / c, (y + 1) / c);
                    uv[idx + 2] = new Vector2(x / c, y / c);
                    uv[idx + 3] = new Vector2((x + 1) / c, y / c);
                }
                else
                {
                    uv[idx] = new Vector2(x / c, (y + 1) / c);
                    uv[idx + 1] = new Vector2(x / c, y / c);
                    uv[idx + 2] = new Vector2((x + 1) / c, y / c);
                    uv[idx + 3] = new Vector2((x + 1) / c, (y + 1) / c);
                }
            }
        }
        
        UpdateMesh();
    }
    
    public void ResisterHistory()
    {
        var item = new HistoryItem();
        item.vertices = new List<Vector3>(vertices);
        item.uv = new List<Vector2>(uv);
        _histories.Insert(0, item);
        if (_histories.Count > HistoryLimit)
            _histories.RemoveAt(HistoryLimit);
        
        if (!_isPresentHistory)
        {
            while (_histories.Count > 1)
            {
                _histories.RemoveAt(1);
            }
        }
        
        _isPresentHistory = true;
        _historyIndex = 0;
    }

    public void ClearHistory()
    {
        _histories.Clear();
        _isPresentHistory = true;
        _historyIndex = 0;
    }
    
    public void UndoHistory()
    {
        if (_isPresentHistory)
        {
            ResisterHistory();
            _isPresentHistory = false;
        }
        
        _historyIndex++;
        
        if (_histories.Count <= _historyIndex)
        {
            _historyIndex = _histories.Count - 1;
            return;
        }
        
        var item = _histories[_historyIndex];
        vertices = new List<Vector3>(item.vertices);
        uv = new List<Vector2>(item.uv);

        UpdateMesh();
    }
    
    public void RedoHistory()
    {
        _historyIndex--;
        if (_historyIndex < 0)
        {
            _historyIndex = 0;
            return;
        }
        
        var item = _histories[_historyIndex];
        vertices = new List<Vector3>(item.vertices);
        uv = new List<Vector2>(item.uv);

        UpdateMesh();
    }
#endif
}

