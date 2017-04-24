using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using UnityEngine;

namespace Tiled2Unity
{
    // Allows us to set shader properties on the Tiled mesh
    // Note: Keep default shader properties imported from Tiled to avoid breaking batching
    // For example, keeping layer opacity to 1.0 (the default) will keep layers using the same material in the same draw call
    public class TiledInitialShaderProperties : MonoBehaviour
    {
		//the desired opacity of the meshes controlled by this script. Locked to 0 through 1 float.
        [Range(0, 1)]
        public float InitialOpacity = 1.0f;
		
		//used to check if desired opacity has changed. Value lags behind InitialOpacity by one cycle.
		private float opacityCheck;
		
		//list of all MeshRenders on which to control opacity
		//(unknown why, but script only works when list is public. Even tried get{} and set{} in a public List<T> class...)
		public List<MeshRenderer> meshRenderers;
		
        private void Awake()
        {
			//catch if mesh has been split by Unity
			if(this.gameObject.transform.childCount > 0){
				//if mesh is split, iterate over child meshes
				for(int i = 0; i < this.gameObject.transform.childCount; i++){
					//and add child meshes to meshRenderers list
					meshRenderers.Add(this.gameObject.transform.GetChild(i).gameObject.GetComponent<MeshRenderer>());
				}
			}else{
				// If supported in the shader set our opacity
				// (Keep opacity at 1.0 to avoid copying the material)
				
				//if mesh is not split, add this mesh to meshRenders list
				meshRenderers.Add(this.gameObject.GetComponent<MeshRenderer>());
			}
			
			//no need to loop if there is no change in desired opacity
			if(this.InitialOpacity != 1.0f){
				//iterate over all meshes
				for(int i = 0; i < meshRenderers.Count; i++){
					//check for material compatibility this opacity changes
					if (meshRenderers[i].material.HasProperty("_Color")){
						//change material opacity
						meshRenderers[i].material.SetColor("_Color", new Color(1, 1, 1, this.InitialOpacity));
					}
				}
			}
        }
		void Update(){
			//no need to loop if there is no change in desired opacity
			if(this.InitialOpacity != this.opacityCheck){
				//iterate over all meshes
				for(int i = 0; i < meshRenderers.Count; i++){
					//check for material compatibility this opacity changes
					if (meshRenderers[i].material.HasProperty("_Color")){
						//change material opacity
						meshRenderers[i].material.SetColor("_Color", new Color(1, 1, 1, this.InitialOpacity));
					}
				}
				
				//reset change check. Prevents unnecessary opacity re-assignment
				this.opacityCheck = this.InitialOpacity;
			}
		}
    }
}
