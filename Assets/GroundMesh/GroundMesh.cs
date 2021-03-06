﻿using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace MobilFactory
{
    [ExecuteInEditMode]
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public class GroundMesh : MonoBehaviour
    {
        public GameObject _tileset;

        [ContextMenu ("Do Something")]
        void DoSomething () {
            Debug.Log ("Perform operation");
        }

        public Tileset tileset {
            get
            {
                if (_tileset == null)
                    return null;

                return _tileset.GetComponent<Tileset>();
            }
        }

    #if UNITY_EDITOR
        public struct HistoryItem
        {
            public List<Vector3> vertices;
            public List<Vector2> uv;
            public List<string> terrains;
        }

        public readonly int HistoryLimit = 50;

        public int cols = 10;
        public int rows = 10;
        [HideInInspector]
        public int _cols;
        [HideInInspector]
        public int _rows;
        [HideInInspector]
        public float _brushHeight = 0f;
        [HideInInspector]
        public float _brushHeightUnit = 0.5f;
        [HideInInspector]
        public int _brushSize = 1;
        [HideInInspector]
        public int _historyIndex = 0;
        [HideInInspector]
        public List<HistoryItem> _histories = new List<HistoryItem>();
        [HideInInspector]
        public bool _isPresentHistory = true;
        [HideInInspector]
        public List<string> _terrains = new List<string>();
        
        public void UpdateMesh(List<Vector3> vertices, List<Vector2> uv, List<int> triangles)
        {
            var filter = GetComponent<MeshFilter>();
            if (filter.sharedMesh == null)
            {
                filter.sharedMesh = new Mesh();
                filter.sharedMesh.name = "GroundMesh";
            }
            var mesh = filter.sharedMesh;
            mesh.Clear();
            mesh.vertices = vertices.ToArray();
            mesh.uv = uv.ToArray();
            mesh.triangles = triangles.ToArray();
            
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mesh.Optimize();
        }
        
        public void RegisterHistory(List<Vector3> vertices, List<Vector2> uv, List<string> terrains)
        {
            var item = new HistoryItem();
            item.vertices = new List<Vector3>(vertices);
            item.uv = new List<Vector2>(uv);
            item.terrains = new List<string>(terrains);

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
        
        public HistoryItem UndoHistory(List<Vector3> vertices, List<Vector2> uv, List<string> terrains)
        {
            if (_isPresentHistory)
            {
                RegisterHistory(vertices, uv, terrains);
                _isPresentHistory = false;
            }
            
            _historyIndex++;
            if (_histories.Count <= _historyIndex)
            {
                _historyIndex = _histories.Count - 1;
                return new HistoryItem();
            }
            
            var item = _histories[_historyIndex];
            var newItem = new HistoryItem();
            newItem.vertices = new List<Vector3>(item.vertices);
            newItem.uv = new List<Vector2>(item.uv);
            newItem.terrains = new List<string>(item.terrains);

            return newItem;
        }
        
        public HistoryItem RedoHistory()
        {
            _historyIndex--;
            if (_historyIndex < 0)
            {
                _historyIndex = 0;
                return new HistoryItem();
            }
            
            var item = _histories[_historyIndex];
            var newItem = new HistoryItem();
            newItem.vertices = new List<Vector3>(item.vertices);
            newItem.uv = new List<Vector2>(item.uv);

            return newItem;
        }
    #endif
    }
}
