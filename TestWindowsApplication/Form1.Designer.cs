namespace TestWindowsApplication
{
    partial class Form1
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
            this.button1 = new System.Windows.Forms.Button();
            this.button2 = new System.Windows.Forms.Button();
            this.btnJsonCompare = new System.Windows.Forms.Button();
            this.btnRedis = new System.Windows.Forms.Button();
            this.btnRedisServer = new System.Windows.Forms.Button();
            this.btnMemSQL = new System.Windows.Forms.Button();
            this.btnIgnite = new System.Windows.Forms.Button();
            this.btnTIBCO = new System.Windows.Forms.Button();
            this.btnRSI = new System.Windows.Forms.Button();
            this.button3 = new System.Windows.Forms.Button();
            this.btnGrpc = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // button1
            // 
            this.button1.Location = new System.Drawing.Point(327, 12);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(169, 23);
            this.button1.TabIndex = 0;
            this.button1.Text = "ConstantWidthStrangle";
            this.button1.UseVisualStyleBackColor = true;
            this.button1.Click += new System.EventHandler(this.button1_Click);
            // 
            // button2
            // 
            this.button2.Location = new System.Drawing.Point(327, 58);
            this.button2.Name = "button2";
            this.button2.Size = new System.Drawing.Size(75, 23);
            this.button2.TabIndex = 1;
            this.button2.Text = "Delta";
            this.button2.UseVisualStyleBackColor = true;
            this.button2.Click += new System.EventHandler(this.Button2_Click);
            // 
            // btnJsonCompare
            // 
            this.btnJsonCompare.Location = new System.Drawing.Point(327, 122);
            this.btnJsonCompare.Name = "btnJsonCompare";
            this.btnJsonCompare.Size = new System.Drawing.Size(102, 23);
            this.btnJsonCompare.TabIndex = 2;
            this.btnJsonCompare.Text = "Compare Json";
            this.btnJsonCompare.UseVisualStyleBackColor = true;
            this.btnJsonCompare.Click += new System.EventHandler(this.btnJsonCompare_Click);
            // 
            // btnRedis
            // 
            this.btnRedis.Location = new System.Drawing.Point(327, 201);
            this.btnRedis.Name = "btnRedis";
            this.btnRedis.Size = new System.Drawing.Size(148, 23);
            this.btnRedis.TabIndex = 3;
            this.btnRedis.Text = "Redis Subscriber";
            this.btnRedis.UseVisualStyleBackColor = true;
            this.btnRedis.Click += new System.EventHandler(this.btnRedis_Click);
            this.btnRedis.MouseClick += new System.Windows.Forms.MouseEventHandler(this.btnRedis_MouseClick);
            // 
            // btnRedisServer
            // 
            this.btnRedisServer.Location = new System.Drawing.Point(327, 278);
            this.btnRedisServer.Name = "btnRedisServer";
            this.btnRedisServer.Size = new System.Drawing.Size(148, 23);
            this.btnRedisServer.TabIndex = 4;
            this.btnRedisServer.Text = "Redis Publisher";
            this.btnRedisServer.UseVisualStyleBackColor = true;
            this.btnRedisServer.Click += new System.EventHandler(this.btnRedisServer_Click);
            // 
            // btnMemSQL
            // 
            this.btnMemSQL.Location = new System.Drawing.Point(588, 201);
            this.btnMemSQL.Name = "btnMemSQL";
            this.btnMemSQL.Size = new System.Drawing.Size(75, 23);
            this.btnMemSQL.TabIndex = 5;
            this.btnMemSQL.Text = "MemSQL";
            this.btnMemSQL.UseVisualStyleBackColor = true;
            this.btnMemSQL.Click += new System.EventHandler(this.btnMemSQL_Click);
            // 
            // btnIgnite
            // 
            this.btnIgnite.Location = new System.Drawing.Point(327, 342);
            this.btnIgnite.Name = "btnIgnite";
            this.btnIgnite.Size = new System.Drawing.Size(75, 23);
            this.btnIgnite.TabIndex = 6;
            this.btnIgnite.Text = "Apache ignite";
            this.btnIgnite.UseVisualStyleBackColor = true;
            this.btnIgnite.Click += new System.EventHandler(this.btnIgnite_Click);
            // 
            // btnTIBCO
            // 
            this.btnTIBCO.Location = new System.Drawing.Point(588, 333);
            this.btnTIBCO.Name = "btnTIBCO";
            this.btnTIBCO.Size = new System.Drawing.Size(75, 23);
            this.btnTIBCO.TabIndex = 7;
            this.btnTIBCO.Text = "Tibco FTL";
            this.btnTIBCO.UseVisualStyleBackColor = true;
            this.btnTIBCO.Click += new System.EventHandler(this.btnTIBCO_Click);
            // 
            // btnRSI
            // 
            this.btnRSI.Location = new System.Drawing.Point(575, 92);
            this.btnRSI.Name = "btnRSI";
            this.btnRSI.Size = new System.Drawing.Size(75, 23);
            this.btnRSI.TabIndex = 8;
            this.btnRSI.Text = "RSI";
            this.btnRSI.UseVisualStyleBackColor = true;
            this.btnRSI.Click += new System.EventHandler(this.btnRSI_Click);
            // 
            // button3
            // 
            this.button3.Location = new System.Drawing.Point(575, 142);
            this.button3.Name = "button3";
            this.button3.Size = new System.Drawing.Size(75, 23);
            this.button3.TabIndex = 9;
            this.button3.Text = "Round";
            this.button3.UseVisualStyleBackColor = true;
            this.button3.Click += new System.EventHandler(this.button3_Click);
            // 
            // btnGrpc
            // 
            this.btnGrpc.Location = new System.Drawing.Point(575, 11);
            this.btnGrpc.Name = "btnGrpc";
            this.btnGrpc.RightToLeft = System.Windows.Forms.RightToLeft.Yes;
            this.btnGrpc.Size = new System.Drawing.Size(75, 23);
            this.btnGrpc.TabIndex = 10;
            this.btnGrpc.Text = "GRPC";
            this.btnGrpc.UseVisualStyleBackColor = true;
            this.btnGrpc.Click += new System.EventHandler(this.btnGrpc_Click);
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(800, 450);
            this.Controls.Add(this.btnGrpc);
            this.Controls.Add(this.button3);
            this.Controls.Add(this.btnRSI);
            this.Controls.Add(this.btnTIBCO);
            this.Controls.Add(this.btnIgnite);
            this.Controls.Add(this.btnMemSQL);
            this.Controls.Add(this.btnRedisServer);
            this.Controls.Add(this.btnRedis);
            this.Controls.Add(this.btnJsonCompare);
            this.Controls.Add(this.button2);
            this.Controls.Add(this.button1);
            this.Name = "Form1";
            this.Text = "Form1";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.Form1_FormClosing);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Button button1;
        private System.Windows.Forms.Button button2;
        private System.Windows.Forms.Button btnJsonCompare;
        private System.Windows.Forms.Button btnRedis;
        private System.Windows.Forms.Button btnRedisServer;
        private System.Windows.Forms.Button btnMemSQL;
        private System.Windows.Forms.Button btnIgnite;
        private System.Windows.Forms.Button btnTIBCO;
        private System.Windows.Forms.Button btnRSI;
        private System.Windows.Forms.Button button3;
        private System.Windows.Forms.Button btnGrpc;
    }
}

