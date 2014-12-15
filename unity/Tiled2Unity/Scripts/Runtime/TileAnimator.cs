using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using UnityEngine;

namespace Tiled2Unity
{
    public class TileAnimator : MonoBehaviour
    {
        [System.Serializable]
        public class Frame
        {
            public int DurationMs = 0;
            public float Vertex_z = 0;
        }

        public List<Frame> frames = new List<Frame>();
        private int currentFrameIndex = 0;

        private void Start()
        {
            this.currentFrameIndex = 0;

            if (this.frames.Count == 0)
            {
                Debug.LogError(String.Format("TileAnimation on '{0}' has no frames.", this.name));
            }
            else
            {
                StartCoroutine(AnimationRoutine());
            }
        }

        private IEnumerator AnimationRoutine()
        {
            while (true)
            {
                Frame frame = this.frames[this.currentFrameIndex];

                // Make the frame 'visible' by making negative 'z' vertex positions positive
                ModifyVertices(-frame.Vertex_z);

                // Wait until the next frame
                float timeToWait = frame.DurationMs / 1000.0f;
                yield return new WaitForSeconds(timeToWait);

                // Make the frame 'invisible' again. Make matching positive 'z' values negative
                ModifyVertices(frame.Vertex_z);

                // Go to the next frame
                this.currentFrameIndex = (this.currentFrameIndex + 1) % this.frames.Count;
            }
        }

        // Find 'z' values on vertices that match and negate them
        private void ModifyVertices(float z)
        {
            float negated = -z;

            // Because meshes may be split we have to go over all them in our tree
            MeshFilter[] meshFilters = GetComponentsInChildren<MeshFilter>();
            foreach (var mf in meshFilters)
            {

                Vector3[] vertices = mf.mesh.vertices;
                for (int i = 0; i < vertices.Length; ++i)
                {
                    if (vertices[i].z == z)
                    {
                        vertices[i].z = negated;
                    }
                }

                // Save the vertices back
                mf.mesh.vertices = vertices;
            }
        }

    }
}
