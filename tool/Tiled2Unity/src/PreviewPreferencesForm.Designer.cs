namespace Tiled2Unity
{
    partial class PreviewPreferencesForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.dataGridView = new System.Windows.Forms.DataGridView();
            this.Preview = new System.Windows.Forms.DataGridViewCheckBoxColumn();
            this.LayerName = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Color = new Tiled2Unity.DataGridViewColumn_ColorPicker();
            this.dataGridViewColumn_ColorPicker1 = new Tiled2Unity.DataGridViewColumn_ColorPicker();
            this.buttonApplyChanges = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView)).BeginInit();
            this.SuspendLayout();
            // 
            // dataGridView
            // 
            this.dataGridView.AllowUserToAddRows = false;
            this.dataGridView.AllowUserToDeleteRows = false;
            this.dataGridView.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.dataGridView.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.dataGridView.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridView.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.Preview,
            this.LayerName,
            this.Color});
            this.dataGridView.Location = new System.Drawing.Point(0, 0);
            this.dataGridView.Name = "dataGridView";
            this.dataGridView.Size = new System.Drawing.Size(400, 309);
            this.dataGridView.TabIndex = 0;
            // 
            // Preview
            // 
            this.Preview.FillWeight = 63.84653F;
            this.Preview.HeaderText = "Preview";
            this.Preview.Name = "Preview";
            this.Preview.Resizable = System.Windows.Forms.DataGridViewTriState.False;
            // 
            // LayerName
            // 
            this.LayerName.FillWeight = 72.89146F;
            this.LayerName.HeaderText = "Layer Name";
            this.LayerName.Name = "LayerName";
            this.LayerName.ReadOnly = true;
            // 
            // Color
            // 
            this.Color.FillWeight = 72.89146F;
            this.Color.HeaderText = "Preview Color";
            this.Color.Name = "Color";
            // 
            // dataGridViewColumn_ColorPicker1
            // 
            this.dataGridViewColumn_ColorPicker1.HeaderText = "Preview Color";
            this.dataGridViewColumn_ColorPicker1.Name = "dataGridViewColumn_ColorPicker1";
            this.dataGridViewColumn_ColorPicker1.Width = 119;
            // 
            // buttonApplyChanges
            // 
            this.buttonApplyChanges.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonApplyChanges.Location = new System.Drawing.Point(270, 321);
            this.buttonApplyChanges.Name = "buttonApplyChanges";
            this.buttonApplyChanges.Size = new System.Drawing.Size(118, 23);
            this.buttonApplyChanges.TabIndex = 1;
            this.buttonApplyChanges.Text = "Apply Changes";
            this.buttonApplyChanges.UseVisualStyleBackColor = true;
            this.buttonApplyChanges.Click += new System.EventHandler(this.buttonApplyChanges_Click);
            // 
            // PreviewPreferencesForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(400, 356);
            this.Controls.Add(this.buttonApplyChanges);
            this.Controls.Add(this.dataGridView);
            this.Name = "PreviewPreferencesForm";
            this.Text = "Preview Options";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.PreviewPreferencesForm_FormClosing);
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.DataGridView dataGridView;
        private System.Windows.Forms.DataGridViewCheckBoxColumn Preview;
        private System.Windows.Forms.DataGridViewTextBoxColumn LayerName;
        private DataGridViewColumn_ColorPicker Color;
        private DataGridViewColumn_ColorPicker dataGridViewColumn_ColorPicker1;
        private System.Windows.Forms.Button buttonApplyChanges;


    }
}