using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using UnityEngine;

#if !UNITY_5_0
using UnityEngine.Assertions;
#endif

namespace Tiled2Unity
{
    public class TileAnimator : MonoBehaviour
    {
        public float StartTime = -1;
        public float Duration = -1;
        public float TotalAnimationTime = -1;

        private float timer = 0;

        private void Start()
        {
#if !UNITY_5_0
            Assert.IsTrue(this.StartTime >= 0, "StartTime cannot be negative");
            Assert.IsTrue(this.Duration > 0, "Duration must be positive and non-zero.");
            Assert.IsTrue(this.TotalAnimationTime > 0, "Total time of animation must be positive non-zero");
#endif
            this.timer = 0.0f;
        }

        private void Update()
        {
            this.timer += Time.deltaTime;

            // Roll around the time if needed
            while (this.timer > this.TotalAnimationTime)
            {
                this.timer -= this.TotalAnimationTime;
            }

            // Should our mesh be rendered or not?
            MeshRenderer renderer = this.gameObject.GetComponent<MeshRenderer>();
            bool isEnabled = renderer.enabled;

            if (timer >= this.StartTime && timer < (this.StartTime + this.Duration))
            {
                // Our mesh should be visible at this time
                if (!isEnabled)
                {
                    renderer.enabled = true;
                }
            }
            else
            {
                // Mesh should not be visible at this time
                if (isEnabled)
                {
                    renderer.enabled = false;
                }
            }

        }

    }
}
