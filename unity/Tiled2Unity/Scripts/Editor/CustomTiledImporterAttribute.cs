using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using UnityEditor;
using UnityEngine;

namespace Tiled2Unity
{
    [AttributeUsage(System.AttributeTargets.Class, AllowMultiple = false)]
    class CustomTiledImporterAttribute : Attribute
    {
        public int Order { get; set; }
    }
}
