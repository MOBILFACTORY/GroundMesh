using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace MobilFactory
{
    [System.Serializable]
    public class TileData : ScriptableObject
    {
        public int id;
        public List<int> terrains;
    }
}
