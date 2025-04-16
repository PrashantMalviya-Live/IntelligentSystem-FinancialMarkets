namespace DataAnalyzer2
{
    partial class MainForm
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
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
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.btnGreeks = new System.Windows.Forms.Button();
            this.btnST = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // btnGreeks
            // 
            this.btnGreeks.Location = new System.Drawing.Point(12, 12);
            this.btnGreeks.Name = "btnGreeks";
            this.btnGreeks.Size = new System.Drawing.Size(75, 23);
            this.btnGreeks.TabIndex = 0;
            this.btnGreeks.Text = "Greeks";
            this.btnGreeks.UseVisualStyleBackColor = true;
            this.btnGreeks.Click += new System.EventHandler(this.btnGreeks_Click);
            // 
            // btnST
            // 
            this.btnST.Location = new System.Drawing.Point(13, 60);
            this.btnST.Name = "btnST";
            this.btnST.Size = new System.Drawing.Size(75, 23);
            this.btnST.TabIndex = 1;
            this.btnST.Text = "Super Trend";
            this.btnST.UseVisualStyleBackColor = true;
            this.btnST.Click += new System.EventHandler(this.btnST_Click);
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(800, 450);
            this.Controls.Add(this.btnST);
            this.Controls.Add(this.btnGreeks);
            this.Name = "MainForm";
            this.Text = "Form1";
            this.ResumeLayout(false);

        }

        #endregion

        private Button btnGreeks;
        private Button btnST;
    }
}