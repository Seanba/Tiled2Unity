using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

using UnityEngine;
using UnityEditor;

namespace Tiled2Unity
{
    // Class to help us manage the import status when a *.tiled2unity.xml file is (re)imported
    // Also holds onto the XML file in memory so that we don't have to keep opening it (an expensive operation) when different parts of the import process needs it.
    // This is a *temporary* behaviour we add to the hierarchy only while importing. It should not be around for runtime.
    public class ImportBehaviour : MonoBehaviour
    {
        public string ImportName;
        public XDocument XmlDocument { get; private set; }

        private int importCounter = 0;
        private int numberOfElements = 0;

        // We have many independent requests on the ImportBehaviour so we can't take for granted it has been created yet.
        // However, if it has been created then use it.
        public static ImportBehaviour FindOrCreateImportBehaviour(string xmlPath)
        {
            string importName = Path.GetFileNameWithoutExtension(xmlPath);

            // Try to find
            foreach (var status in UnityEngine.Object.FindObjectsOfType<ImportBehaviour>())
            {
                if (String.Compare(status.ImportName, importName, true) == 0)
                {
                    return status;
                }
            }

            // Couldn't find, so create.
            Debug.LogFormat("Tiled2Unity import status create for file {0}", xmlPath);
            GameObject gameObject = new GameObject("__temp_tiled2unity_import");
            gameObject.transform.SetAsFirstSibling();

            var importStatus = gameObject.AddComponent<ImportBehaviour>();
            importStatus.ImportName = Path.GetFileNameWithoutExtension(xmlPath);

            // Opening the XDocument itself can be expensive so start the progress bar just before we start
            importStatus.StartProgressBar(xmlPath);
            importStatus.XmlDocument = XDocument.Load(xmlPath);

            importStatus.numberOfElements = importStatus.XmlDocument.Element("Tiled2Unity").Elements().Count();
            importStatus.IncrementProgressBar(xmlPath);

            return importStatus;
        }

        private void StartProgressBar(string xmlPath)
        {
            string title = string.Format("Tiled2Unity Import ({0})", this.ImportName);
            UnityEditor.EditorUtility.DisplayProgressBar(title, xmlPath, 0);
        }

        public void IncrementProgressBar(string detail)
        {
            string title = string.Format("Tiled2Unity Import ({0})", this.ImportName);

            float progress = this.importCounter / (float)this.numberOfElements;
            UnityEditor.EditorUtility.DisplayProgressBar(title, detail, progress);
            this.importCounter++;
        }

        public void DestroyImportBehaviour()
        {
            UnityEditor.EditorUtility.ClearProgressBar();
            UnityEngine.Object.DestroyImmediate(this.gameObject);
        }

        private void Update()
        {
            Debug.LogErrorFormat("ImportBehaviour {0} left in scene after importing. Check if import was successful and remove this object from scene {1}", this.ImportName, this.gameObject.name);
        }

    }
}
