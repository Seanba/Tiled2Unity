namespace Tiled2Unity
{
    partial class Tiled2UnityViewer
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
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Tiled2UnityViewer));
            this.panelViewer = new System.Windows.Forms.Panel();
            this.pictureBoxViewer = new System.Windows.Forms.PictureBox();
            this.previewContextMenuStrip = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.saveImageAsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.copyToClipboardToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.previewScaleToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.view18ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.view14ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.view12ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.view100ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.view200ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.view400ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.view800ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.panelViewer.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBoxViewer)).BeginInit();
            this.previewContextMenuStrip.SuspendLayout();
            this.SuspendLayout();
            // 
            // panelViewer
            // 
            this.panelViewer.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.panelViewer.AutoScroll = true;
            this.panelViewer.Controls.Add(this.pictureBoxViewer);
            this.panelViewer.Location = new System.Drawing.Point(12, 12);
            this.panelViewer.Name = "panelViewer";
            this.panelViewer.Size = new System.Drawing.Size(733, 463);
            this.panelViewer.TabIndex = 0;
            // 
            // pictureBoxViewer
            // 
            this.pictureBoxViewer.ContextMenuStrip = this.previewContextMenuStrip;
            this.pictureBoxViewer.Location = new System.Drawing.Point(3, 3);
            this.pictureBoxViewer.Name = "pictureBoxViewer";
            this.pictureBoxViewer.Size = new System.Drawing.Size(100, 50);
            this.pictureBoxViewer.SizeMode = System.Windows.Forms.PictureBoxSizeMode.AutoSize;
            this.pictureBoxViewer.TabIndex = 0;
            this.pictureBoxViewer.TabStop = false;
            // 
            // previewContextMenuStrip
            // 
            this.previewContextMenuStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.saveImageAsToolStripMenuItem,
            this.copyToClipboardToolStripMenuItem,
            this.previewScaleToolStripMenuItem});
            this.previewContextMenuStrip.Name = "contextMenuStrip1";
            this.previewContextMenuStrip.Size = new System.Drawing.Size(172, 92);
            // 
            // saveImageAsToolStripMenuItem
            // 
            this.saveImageAsToolStripMenuItem.Name = "saveImageAsToolStripMenuItem";
            this.saveImageAsToolStripMenuItem.Size = new System.Drawing.Size(171, 22);
            this.saveImageAsToolStripMenuItem.Text = "&Save Image As ...";
            this.saveImageAsToolStripMenuItem.Click += new System.EventHandler(this.saveImageAsToolStripMenuItem_Click);
            // 
            // copyToClipboardToolStripMenuItem
            // 
            this.copyToClipboardToolStripMenuItem.Name = "copyToClipboardToolStripMenuItem";
            this.copyToClipboardToolStripMenuItem.Size = new System.Drawing.Size(171, 22);
            this.copyToClipboardToolStripMenuItem.Text = "&Copy to Clipboard";
            this.copyToClipboardToolStripMenuItem.Click += new System.EventHandler(this.copyToClipboardToolStripMenuItem_Click);
            // 
            // previewScaleToolStripMenuItem
            // 
            this.previewScaleToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.view18ToolStripMenuItem,
            this.view14ToolStripMenuItem,
            this.view12ToolStripMenuItem,
            this.view100ToolStripMenuItem,
            this.view200ToolStripMenuItem,
            this.view400ToolStripMenuItem,
            this.view800ToolStripMenuItem});
            this.previewScaleToolStripMenuItem.Name = "previewScaleToolStripMenuItem";
            this.previewScaleToolStripMenuItem.Size = new System.Drawing.Size(171, 22);
            this.previewScaleToolStripMenuItem.Text = "Preview Scale";
            // 
            // view18ToolStripMenuItem
            // 
            this.view18ToolStripMenuItem.Name = "view18ToolStripMenuItem";
            this.view18ToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.D8)));
            this.view18ToolStripMenuItem.Size = new System.Drawing.Size(166, 22);
            this.view18ToolStripMenuItem.Text = "View 1/8 ";
            this.view18ToolStripMenuItem.Click += new System.EventHandler(this.view18ToolStripMenuItem_Click);
            // 
            // view14ToolStripMenuItem
            // 
            this.view14ToolStripMenuItem.Name = "view14ToolStripMenuItem";
            this.view14ToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.D4)));
            this.view14ToolStripMenuItem.Size = new System.Drawing.Size(166, 22);
            this.view14ToolStripMenuItem.Text = "View 1/4";
            this.view14ToolStripMenuItem.Click += new System.EventHandler(this.view14ToolStripMenuItem_Click);
            // 
            // view12ToolStripMenuItem
            // 
            this.view12ToolStripMenuItem.Name = "view12ToolStripMenuItem";
            this.view12ToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.D2)));
            this.view12ToolStripMenuItem.Size = new System.Drawing.Size(166, 22);
            this.view12ToolStripMenuItem.Text = "View 1/2";
            this.view12ToolStripMenuItem.Click += new System.EventHandler(this.view12ToolStripMenuItem_Click);
            // 
            // view100ToolStripMenuItem
            // 
            this.view100ToolStripMenuItem.Name = "view100ToolStripMenuItem";
            this.view100ToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Alt | System.Windows.Forms.Keys.D1)));
            this.view100ToolStripMenuItem.Size = new System.Drawing.Size(166, 22);
            this.view100ToolStripMenuItem.Text = "View 100%";
            this.view100ToolStripMenuItem.Click += new System.EventHandler(this.view100ToolStripMenuItem_Click);
            // 
            // view200ToolStripMenuItem
            // 
            this.view200ToolStripMenuItem.Name = "view200ToolStripMenuItem";
            this.view200ToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Alt | System.Windows.Forms.Keys.D2)));
            this.view200ToolStripMenuItem.Size = new System.Drawing.Size(166, 22);
            this.view200ToolStripMenuItem.Text = "View 200%";
            this.view200ToolStripMenuItem.Click += new System.EventHandler(this.view200ToolStripMenuItem_Click);
            // 
            // view400ToolStripMenuItem
            // 
            this.view400ToolStripMenuItem.Name = "view400ToolStripMenuItem";
            this.view400ToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Alt | System.Windows.Forms.Keys.D4)));
            this.view400ToolStripMenuItem.Size = new System.Drawing.Size(166, 22);
            this.view400ToolStripMenuItem.Text = "View 400%";
            this.view400ToolStripMenuItem.Click += new System.EventHandler(this.view400ToolStripMenuItem_Click);
            // 
            // view800ToolStripMenuItem
            // 
            this.view800ToolStripMenuItem.Name = "view800ToolStripMenuItem";
            this.view800ToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Alt | System.Windows.Forms.Keys.D8)));
            this.view800ToolStripMenuItem.Size = new System.Drawing.Size(166, 22);
            this.view800ToolStripMenuItem.Text = "View 800%";
            this.view800ToolStripMenuItem.Click += new System.EventHandler(this.view800ToolStripMenuItem_Click);
            // 
            // Tiled2UnityViewer
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(757, 487);
            this.Controls.Add(this.panelViewer);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "Tiled2UnityViewer";
            this.Text = "Tiled2Unity Previewer";
            this.KeyDown += new System.Windows.Forms.KeyEventHandler(this.Tiled2UnityViewer_KeyDown);
            this.panelViewer.ResumeLayout(false);
            this.panelViewer.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBoxViewer)).EndInit();
            this.previewContextMenuStrip.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Panel panelViewer;
        private System.Windows.Forms.PictureBox pictureBoxViewer;
        private System.Windows.Forms.ContextMenuStrip previewContextMenuStrip;
        private System.Windows.Forms.ToolStripMenuItem saveImageAsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem copyToClipboardToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem previewScaleToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem view18ToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem view14ToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem view12ToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem view100ToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem view200ToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem view400ToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem view800ToolStripMenuItem;
    }
}