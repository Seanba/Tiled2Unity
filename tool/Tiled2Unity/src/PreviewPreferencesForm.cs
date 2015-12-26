using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Tiled2Unity
{
    public partial class PreviewPreferencesForm : Form
    {
        public delegate void OnApplyChanges();
        public event OnApplyChanges ApplyChanges;

        public PreviewPreferencesForm()
        {
            InitializeComponent();
            this.dataGridView.CellValueChanged += new DataGridViewCellEventHandler(dataGridView_CellValueChanged);
        }

        public void InitializePrefernces(TmxMap map)
        {
            this.Preferences = new List<LayerPreference>();

            Dictionary<string, Color> layers2Colors = PerLayerColorData.StringCollectionToDictionary(Properties.Settings.Default.PerLayerColors);
            Color defaultColor = System.Drawing.Color.Tomato;

            foreach (var layer in map.Layers)
            {
                if (layer.Visible)
                {
                    string name = layer.Name;
                    System.Drawing.Color color = layers2Colors.ContainsKey(name) ? layers2Colors[name] : defaultColor;
                    this.Preferences.Add(new LayerPreference() { Name = name, Previewing = true, Color = color, CanEditColor = true });
                }
            }

            // Object groups have a color setting from the map file. This is a purposeful setting so honor it and don't allow edits.
            foreach (var objects in map.ObjectGroups)
            {
                if (objects.Visible)
                {
                    string name = objects.Name;
                    Color color = objects.Color;
                    this.Preferences.Add(new LayerPreference() { Name = name, Previewing = true, Color = color, CanEditColor = false });
                }
            }

            for (int i = 0; i < this.Preferences.Count(); ++i)
            {
                var row = this.Preferences[i];
                this.dataGridView.Rows.Add(new object[3] { row.Previewing, row.Name, row.Color });

                if (row.CanEditColor == false)
                {
                    this.dataGridView.Rows[i].Cells[2].ReadOnly = true;
                }
            }
        }

        public Color GetLayerColor(string layerName)
        {
            return this.Preferences.FirstOrDefault(p => p.Name == layerName).Color;
        }

        public bool GetLayerPreviewing(string layerName)
        {
            LayerPreference layerPrefernces = this.Preferences.Find(p => p.Name == layerName);
            if (layerPrefernces != null)
            {
                return layerPrefernces.Previewing;
            }
            return false;
        }

        private class LayerPreference
        {
            public string Name { get; set; }
            public bool Previewing { get; set; }
            public Color Color { get; set; }
            public bool CanEditColor { get; set; }
        };

        private List<LayerPreference> Preferences { get; set; }


        private void dataGridView_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            // For some reason we rarely get notifications that are out of index
            if (e.RowIndex < 0 || e.RowIndex >= this.dataGridView.Rows.Count)
                return;
            if (e.ColumnIndex < 0 || e.ColumnIndex >= this.dataGridView.Columns.Count)
                return;

            LayerPreference preference = this.Preferences[e.RowIndex];
            if (e.ColumnIndex == 0)
            {
                // Preivew changed
                preference.Previewing = (bool)this.dataGridView.Rows[e.RowIndex].Cells[e.ColumnIndex].Value;
            }
            else if (e.ColumnIndex == 2)
            {
                // Color changed
                preference.Color = (Color)this.dataGridView.Rows[e.RowIndex].Cells[e.ColumnIndex].Value;
            }
        }

        private void buttonApplyChanges_Click(object sender, EventArgs e)
        {
            // Apply changes to color settings
            Dictionary<string, Color> layers2Colors = PerLayerColorData.StringCollectionToDictionary(Properties.Settings.Default.PerLayerColors);

            // Override if needed
            for (int i = 0; i < this.dataGridView.Rows.Count; ++i)
            {
                string name = (string)this.dataGridView.Rows[i].Cells[1].Value;
                System.Drawing.Color color = (System.Drawing.Color)this.dataGridView.Rows[i].Cells[2].Value;

                layers2Colors[name] = color;
            }

            // Save changes to settings back out
            Properties.Settings.Default.PerLayerColors = PerLayerColorData.DictionaryToStringCollection(layers2Colors);
            Properties.Settings.Default.Save();

            // Notify listeners that we've had a change
            if (this.ApplyChanges != null)
            {
                this.ApplyChanges();
            }
        }

        private void PreviewPreferencesForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            // Hide instead of closing
            this.Hide();
            e.Cancel = true;
        }


    }

}
