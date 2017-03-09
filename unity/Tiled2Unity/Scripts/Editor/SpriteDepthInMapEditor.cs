using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using UnityEditor;
using UnityEngine;

namespace Tiled2Unity
{
    [CustomEditor(typeof(SpriteDepthInMap))]
    public class SpriteDepthInMapEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            SpriteDepthInMap depthSprite = (SpriteDepthInMap)target;
            if (GUILayout.Button("Set Depth (Changes Transform Z Position)"))
            {
                if (depthSprite.AttachedMap == null)
                {
                    Debug.LogError("Cannot set sprite depth without an Attached Map");
                }
                else
                {
                    depthSprite.UpdateSpriteDepth();
                }
            }
        }

    }
}
