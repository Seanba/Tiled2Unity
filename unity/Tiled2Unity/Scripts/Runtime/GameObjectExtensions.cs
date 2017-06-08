using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using UnityEngine;

namespace Tiled2Unity
{
    public static class GameObjectExtensions
    {
        public static T GetOrAddComponent<T>(this GameObject gameObject) where T : UnityEngine.Component
        {
            // Get the component if it exists
            T component = gameObject.GetComponent<T>();
            if (component != null)
                return component;

            // Add the component
            return gameObject.AddComponent<T>();
        }
    }
}
