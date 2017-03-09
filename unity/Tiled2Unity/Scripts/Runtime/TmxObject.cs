using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using UnityEngine;

namespace Tiled2Unity
{
    // Represents an object within an Object Layer. Contains common object data as it is represented in the TMX file (before scaling and other transformations are applied).
    public class TmxObject : MonoBehaviour
    {
        [Header("Tmx Object Properties (Raw Data)")]

        [Tooltip("Id of object in Tiled TMX file")]
        public int TmxId;

        [Tooltip("Name of object in Tiled TMX file")]
        public string TmxName;

        [Tooltip("Type of object in Tiled TMX file")]
        public string TmxType;

        [Tooltip("Position of object in Tiled TMX file")]
        public Vector2 TmxPosition;

        [Tooltip("Size of object in Tiled TMX file")]
        public Vector2 TmxSize;

        [Tooltip("Rotation of object in Tiled TMX file")]
        public float TmxRotation;
    }
}
