namespace Tiled2Unity
{
    partial class Tiled2UnityAbout
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Tiled2UnityAbout));
            this.buttonOkay = new System.Windows.Forms.Button();
            this.pictureBoxMegaDad = new System.Windows.Forms.PictureBox();
            this.labelVersion = new System.Windows.Forms.Label();
            this.richTextBoxAbout = new System.Windows.Forms.RichTextBox();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBoxMegaDad)).BeginInit();
            this.SuspendLayout();
            // 
            // buttonOkay
            // 
            this.buttonOkay.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonOkay.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.buttonOkay.Location = new System.Drawing.Point(535, 366);
            this.buttonOkay.Name = "buttonOkay";
            this.buttonOkay.Size = new System.Drawing.Size(75, 23);
            this.buttonOkay.TabIndex = 0;
            this.buttonOkay.Text = "OK";
            this.buttonOkay.UseVisualStyleBackColor = true;
            this.buttonOkay.Click += new System.EventHandler(this.buttonOkay_Click);
            // 
            // pictureBoxMegaDad
            // 
            this.pictureBoxMegaDad.Image = global::Tiled2Unity.Properties.Resources.mega_dad_stand;
            this.pictureBoxMegaDad.InitialImage = ((System.Drawing.Image)(resources.GetObject("pictureBoxMegaDad.InitialImage")));
            this.pictureBoxMegaDad.Location = new System.Drawing.Point(12, 12);
            this.pictureBoxMegaDad.Name = "pictureBoxMegaDad";
            this.pictureBoxMegaDad.Size = new System.Drawing.Size(42, 48);
            this.pictureBoxMegaDad.SizeMode = System.Windows.Forms.PictureBoxSizeMode.AutoSize;
            this.pictureBoxMegaDad.TabIndex = 1;
            this.pictureBoxMegaDad.TabStop = false;
            // 
            // labelVersion
            // 
            this.labelVersion.AutoSize = true;
            this.labelVersion.Location = new System.Drawing.Point(72, 12);
            this.labelVersion.Name = "labelVersion";
            this.labelVersion.Size = new System.Drawing.Size(96, 13);
            this.labelVersion.TabIndex = 3;
            this.labelVersion.Text = "Version Goes Here";
            // 
            // richTextBoxAbout
            // 
            this.richTextBoxAbout.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.richTextBoxAbout.BackColor = System.Drawing.SystemColors.Control;
            this.richTextBoxAbout.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.richTextBoxAbout.Location = new System.Drawing.Point(72, 41);
            this.richTextBoxAbout.Name = "richTextBoxAbout";
            this.richTextBoxAbout.ReadOnly = true;
            this.richTextBoxAbout.ScrollBars = System.Windows.Forms.RichTextBoxScrollBars.None;
            this.richTextBoxAbout.Size = new System.Drawing.Size(538, 319);
            this.richTextBoxAbout.TabIndex = 2;
            this.richTextBoxAbout.Text = "Load Rich Text here ...";
            this.richTextBoxAbout.LinkClicked += new System.Windows.Forms.LinkClickedEventHandler(this.richTextBoxAbout_LinkClicked);
            // 
            // Tiled2UnityAbout
            // 
            this.AcceptButton = this.buttonOkay;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.buttonOkay;
            this.ClientSize = new System.Drawing.Size(622, 401);
            this.Controls.Add(this.labelVersion);
            this.Controls.Add(this.richTextBoxAbout);
            this.Controls.Add(this.pictureBoxMegaDad);
            this.Controls.Add(this.buttonOkay);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "Tiled2UnityAbout";
            this.Text = "About Tiled2Unity";
            ((System.ComponentModel.ISupportInitialize)(this.pictureBoxMegaDad)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button buttonOkay;
        private System.Windows.Forms.PictureBox pictureBoxMegaDad;
        private System.Windows.Forms.RichTextBox richTextBoxAbout;
        private System.Windows.Forms.Label labelVersion;
    }
}