// Based on code provided by: Nick Gravelyn
// from: https://gist.github.com/nickgravelyn/7460288
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

using UnityEngine;
using UnityEditor;
using Tiled2Unity;

[CustomEditor(typeof(Tiled2Unity.SortingLayerExposed))]
public class SortingLayerExposedEditor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        // Get the renderer from the target object
        var renderer = (target as SortingLayerExposed).gameObject.renderer;

        // If there is no renderer, we can't do anything
        if (!renderer)
        {
            return;
        }

        // Expose the sorting layer name
        //string newSortingLayerName = EditorGUILayout.TextField("Sorting Layer", renderer.sortingLayerName);
        //if (newSortingLayerName != renderer.sortingLayerName)
        //{
        //    Undo.RecordObject(renderer, "Edit Sorting Layer Name");
        //    renderer.sortingLayerName = newSortingLayerName;
        //    EditorUtility.SetDirty(renderer);
        //}

        // Expose the sorting layer ID
        //int newSortingLayerId = EditorGUILayout.IntField("Sorting Layer ID", renderer.sortingLayerID);
        //if (newSortingLayerId != renderer.sortingLayerID)
        //{
        //    Undo.RecordObject(renderer, "Edit Sorting Layer ID");
        //    renderer.sortingLayerID = newSortingLayerId;
        //    EditorUtility.SetDirty(renderer);
        //}

        // Seanba: Use a popup that is populated with the acceptable sorting layers for the renderer
        // Also allow the player to bring up the Tag/Layers inspector if they choose so
        string[] sortLayerNames = GetSortingLayerNames();
        //int[] sortLayerIds = GetSortingLayerUniqueIDs();
        //{
        //    StringBuilder builder = new StringBuilder("Sorting Layers = ");
        //    for (int i = 0; i < sortLayerNames.Length; ++i)
        //    {
        //        builder.AppendFormat("({0} = {1},{2}) ", i, sortLayerIds[i], sortLayerNames[i]);
        //    }
        //    Debug.Log(builder.ToString());
        //}

        int sortLayerSelection = GetSortingLayerIndex(renderer, sortLayerNames);

        GUIContent[] sortingLayerContexts = GetSortingLayerContexts();
        int newSortingLayerIndex = EditorGUILayout.Popup(new GUIContent("Sorting Layer"), sortLayerSelection, sortingLayerContexts);
        if (newSortingLayerIndex == sortingLayerContexts.Length - 1)
        {
            EditorApplication.ExecuteMenuItem("Edit/Project Settings/Tags and Layers");
        }
        else if (newSortingLayerIndex != sortLayerSelection)
        {
            //int newSortingLayerId = sortLayerIds[newSortingLayerIndex];
            string newSortingLayerName = sortLayerNames[newSortingLayerIndex];

            Undo.RecordObject(renderer, "Edit Sorting Layer ID");
            renderer.sortingLayerName = newSortingLayerName;
            //renderer.sortingLayerID = newSortingLayerId;

            EditorUtility.SetDirty(renderer);
        }

        // Expose the manual sorting order within a sort layer
        int newSortingLayerOrder = EditorGUILayout.IntField("Order in Layer", renderer.sortingOrder);
        if (newSortingLayerOrder != renderer.sortingOrder)
        {
            Undo.RecordObject(renderer, "Edit Sorting Order");
            renderer.sortingOrder = newSortingLayerOrder;
            EditorUtility.SetDirty(renderer);
        }
    }

    public static GUIContent[] GetSortingLayerContexts()
    {
        List<GUIContent> contexts = new List<GUIContent>();

        foreach (string layerName in GetSortingLayerNames())
        {
            contexts.Add(new GUIContent(layerName));
        }

        contexts.Add(GUIContent.none);
        contexts.Add(new GUIContent("Edit Layers..."));

        return contexts.ToArray();
    }

    // Get the sorting layer names
    public static string[] GetSortingLayerNames()
    {
        Type internalEditorUtilityType = typeof(UnityEditorInternal.InternalEditorUtility);
        PropertyInfo sortingLayersProperty = internalEditorUtilityType.GetProperty("sortingLayerNames", BindingFlags.Static | BindingFlags.NonPublic);
        return (string[])sortingLayersProperty.GetValue(null, new object[0]);
    }

    // Get the unique sorting layer IDs -- tossed this in for good measure
    public int[] GetSortingLayerUniqueIDs()
    {
        Type internalEditorUtilityType = typeof(UnityEditorInternal.InternalEditorUtility);
        PropertyInfo sortingLayerUniqueIDsProperty = internalEditorUtilityType.GetProperty("sortingLayerUniqueIDs", BindingFlags.Static | BindingFlags.NonPublic);
        return (int[])sortingLayerUniqueIDsProperty.GetValue(null, new object[0]);
    }

    public static int GetSortingLayerIndex(Renderer renderer, string[] layerNames)
    {
        for (int i = 0; i < layerNames.Length; ++i)
        {
            if (layerNames[i] == renderer.sortingLayerName)
                return i;

            // Special case for Default, goddammit
            if (layerNames[i] == "Default" && String.IsNullOrEmpty(renderer.sortingLayerName))
                return i;
        }

        return 0;
    }

    public static int GetSortingLayerIdIndex(Renderer renderer, int[] layerIds)
    {
        for (int i = 0; i < layerIds.Length; ++i)
        {
            if (layerIds[i] == renderer.sortingLayerID)
                return i;
        }

        return 0;
    }

}