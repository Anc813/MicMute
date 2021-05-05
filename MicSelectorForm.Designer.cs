
namespace MicMute
{
    partial class MicSelectorForm
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
            this.cbMics = new System.Windows.Forms.ComboBox();
            this.label1 = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // cbMics
            // 
            this.cbMics.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cbMics.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
            this.cbMics.FormattingEnabled = true;
            this.cbMics.Location = new System.Drawing.Point(26, 80);
            this.cbMics.Name = "cbMics";
            this.cbMics.Size = new System.Drawing.Size(1139, 45);
            this.cbMics.TabIndex = 0;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
            this.label1.Location = new System.Drawing.Point(21, 18);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(478, 37);
            this.label1.TabIndex = 1;
            this.label1.Text = "Select mic (auto saved on close)";
            this.label1.Click += new System.EventHandler(this.label1_Click);
            // 
            // MicSelectorForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(12F, 25F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1177, 159);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.cbMics);
            this.Name = "MicSelectorForm";
            this.Text = "MicSelector";
            this.Load += new System.EventHandler(this.MicSelectorForm_Load);
            this.Shown += new System.EventHandler(this.MicSelectorForm_Shown);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        public System.Windows.Forms.ComboBox cbMics;
        private System.Windows.Forms.Label label1;
    }
}