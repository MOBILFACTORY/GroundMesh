using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace MobilFactory
{
    public class Tileset : MonoBehaviour
    {
        public Material material = null;
        public int tileSize = 1;
        public int columnCount = 1;
        //[HideInInspector]
        public List<string> terrainNames = new List<string>();
        //[HideInInspector]
        public List<string> tileTerrains = new List<string>();
    }
}
