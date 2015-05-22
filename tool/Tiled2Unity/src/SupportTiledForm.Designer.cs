namespace Tiled2Unity
{
    partial class SupportTiledForm
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
            this.buttonClose = new System.Windows.Forms.Button();
            this.pleaRichTextBox = new System.Windows.Forms.RichTextBox();
            this.patreonLinkLabel = new System.Windows.Forms.LinkLabel();
            this.SuspendLayout();
            // 
            // buttonClose
            // 
            this.buttonClose.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonClose.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.buttonClose.Location = new System.Drawing.Point(463, 363);
            this.buttonClose.Name = "buttonClose";
            this.buttonClose.Size = new System.Drawing.Size(75, 23);
            this.buttonClose.TabIndex = 0;
            this.buttonClose.Text = "Close";
            this.buttonClose.UseVisualStyleBackColor = true;
            // 
            // pleaRichTextBox
            // 
            this.pleaRichTextBox.BackColor = System.Drawing.SystemColors.ControlLightLight;
            this.pleaRichTextBox.Location = new System.Drawing.Point(12, 12);
            this.pleaRichTextBox.Name = "pleaRichTextBox";
            this.pleaRichTextBox.ReadOnly = true;
            this.pleaRichTextBox.Size = new System.Drawing.Size(526, 337);
            this.pleaRichTextBox.TabIndex = 1;
            this.pleaRichTextBox.Text = "";
            // 
            // patreonLinkLabel
            // 
            this.patreonLinkLabel.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.patreonLinkLabel.AutoSize = true;
            this.patreonLinkLabel.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.patreonLinkLabel.Location = new System.Drawing.Point(12, 352);
            this.patreonLinkLabel.Name = "patreonLinkLabel";
            this.patreonLinkLabel.Size = new System.Drawing.Size(222, 20);
            this.patreonLinkLabel.TabIndex = 2;
            this.patreonLinkLabel.TabStop = true;
            this.patreonLinkLabel.Text = "https://www.patreon.com/bjorn";
            this.patreonLinkLabel.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.patreonLinkLabel_LinkClicked);
            // 
            // SupportTiledForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.buttonClose;
            this.ClientSize = new System.Drawing.Size(550, 392);
            this.Controls.Add(this.patreonLinkLabel);
            this.Controls.Add(this.pleaRichTextBox);
            this.Controls.Add(this.buttonClose);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.Name = "SupportTiledForm";
            this.Text = "Suport Tiled On Patreon!";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button buttonClose;
        private System.Windows.Forms.RichTextBox pleaRichTextBox;
        private System.Windows.Forms.LinkLabel patreonLinkLabel;
    }
}