﻿namespace GUI
{
    partial class Main
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
            this.grpBxIndices = new System.Windows.Forms.GroupBox();
            this.lblCurrentBNF = new System.Windows.Forms.Label();
            this.lblCurrentNifty = new System.Windows.Forms.Label();
            this.lblBNF = new System.Windows.Forms.Label();
            this.lblNifty = new System.Windows.Forms.Label();
            this.grpbxSStrangle = new System.Windows.Forms.GroupBox();
            this.lblselectedput = new System.Windows.Forms.Label();
            this.lblselectedcall = new System.Windows.Forms.Label();
            this.label8 = new System.Windows.Forms.Label();
            this.label7 = new System.Windows.Forms.Label();
            this.lstBxPuts = new System.Windows.Forms.ListBox();
            this.lstbxCalls = new System.Windows.Forms.ListBox();
            this.label1 = new System.Windows.Forms.Label();
            this.lstBxExpiry = new System.Windows.Forms.ListBox();
            this.lblInstruments = new System.Windows.Forms.Label();
            this.lstbxInstruments = new System.Windows.Forms.ListBox();
            this.btnOrder = new System.Windows.Forms.Button();
            this.lblPEDelta = new System.Windows.Forms.Label();
            this.lblCEDelta = new System.Windows.Forms.Label();
            this.lblPut = new System.Windows.Forms.Label();
            this.lblCE = new System.Windows.Forms.Label();
            this.tbctrlDeltaValue = new System.Windows.Forms.TabControl();
            this.tbDelta = new System.Windows.Forms.TabPage();
            this.lPEDeltaUW = new System.Windows.Forms.TextBox();
            this.label5 = new System.Windows.Forms.Label();
            this.label6 = new System.Windows.Forms.Label();
            this.lPEDeltaLW = new System.Windows.Forms.TextBox();
            this.label4 = new System.Windows.Forms.Label();
            this.lCEDeltaUW = new System.Windows.Forms.TextBox();
            this.lCEDeltaLW = new System.Windows.Forms.TextBox();
            this.label3 = new System.Windows.Forms.Label();
            this.tbValue = new System.Windows.Forms.TabPage();
            this.label13 = new System.Windows.Forms.Label();
            this.label12 = new System.Windows.Forms.Label();
            this.label11 = new System.Windows.Forms.Label();
            this.label10 = new System.Windows.Forms.Label();
            this.txtPEMaxLossPercent = new System.Windows.Forms.TextBox();
            this.txtCEMaxLossPercent = new System.Windows.Forms.TextBox();
            this.txtPEMaxProfitPercent = new System.Windows.Forms.TextBox();
            this.txtCEMaxProfitPercent = new System.Windows.Forms.TextBox();
            this.lblMaxLoss = new System.Windows.Forms.Label();
            this.lblMaxProfit = new System.Windows.Forms.Label();
            this.txtPEMaxLossPoints = new System.Windows.Forms.TextBox();
            this.txtCEMaxLossPoints = new System.Windows.Forms.TextBox();
            this.txtPEMaxProfitPoints = new System.Windows.Forms.TextBox();
            this.txtCEMaxProfitPoints = new System.Windows.Forms.TextBox();
            this.lblPutTitle = new System.Windows.Forms.Label();
            this.lblCallTitle = new System.Windows.Forms.Label();
            this.tbpgBuyBack = new System.Windows.Forms.TabPage();
            this.label9 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.lblMaxLossBB = new System.Windows.Forms.Label();
            this.lblGain = new System.Windows.Forms.Label();
            this.txtPutLossPercent = new System.Windows.Forms.TextBox();
            this.txtPutLossValue = new System.Windows.Forms.TextBox();
            this.txtPutGainPercent = new System.Windows.Forms.TextBox();
            this.txtPutGainValue = new System.Windows.Forms.TextBox();
            this.txtCallLossPercent = new System.Windows.Forms.TextBox();
            this.txtCallLossValue = new System.Windows.Forms.TextBox();
            this.lblPutBBTitle = new System.Windows.Forms.Label();
            this.txtCallGainPercent = new System.Windows.Forms.TextBox();
            this.txtCallGainValue = new System.Windows.Forms.TextBox();
            this.lblCallBB = new System.Windows.Forms.Label();
            this.tbFrequent = new System.Windows.Forms.TabPage();
            this.lblFrequentMaxLossPoint = new System.Windows.Forms.Label();
            this.lblFrequentMaxProfit = new System.Windows.Forms.Label();
            this.txtBxMaxLossPoints = new System.Windows.Forms.TextBox();
            this.txtBxMaxProfitPoints = new System.Windows.Forms.TextBox();
            this.tbpgConstantWidth = new System.Windows.Forms.TabPage();
            this.txtMaxLossPoints = new System.Windows.Forms.TextBox();
            this.lblMaxLossPoints = new System.Windows.Forms.Label();
            this.tbpgActiveBuyStrangle = new System.Windows.Forms.TabPage();
            this.txtStepQty = new System.Windows.Forms.TextBox();
            this.lblStepQty = new System.Windows.Forms.Label();
            this.txtMaxQty = new System.Windows.Forms.TextBox();
            this.txtInitialQty = new System.Windows.Forms.TextBox();
            this.lblMaxQty = new System.Windows.Forms.Label();
            this.lblInitialQty = new System.Windows.Forms.Label();
            this.btnDummy = new System.Windows.Forms.Button();
            this.dgActiveStrangles = new System.Windows.Forms.DataGridView();
            this.lblActiveStrangles = new System.Windows.Forms.Label();
            this.btnLoad = new System.Windows.Forms.Button();
            this.button1 = new System.Windows.Forms.Button();
            this.btnLoadTokens = new System.Windows.Forms.Button();
            this.btnZeroMQTest = new System.Windows.Forms.Button();
            this.btnMarketService = new System.Windows.Forms.Button();
            this.grpBxIndices.SuspendLayout();
            this.grpbxSStrangle.SuspendLayout();
            this.tbctrlDeltaValue.SuspendLayout();
            this.tbDelta.SuspendLayout();
            this.tbValue.SuspendLayout();
            this.tbpgBuyBack.SuspendLayout();
            this.tbFrequent.SuspendLayout();
            this.tbpgConstantWidth.SuspendLayout();
            this.tbpgActiveBuyStrangle.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgActiveStrangles)).BeginInit();
            this.SuspendLayout();
            // 
            // grpBxIndices
            // 
            this.grpBxIndices.Controls.Add(this.lblCurrentBNF);
            this.grpBxIndices.Controls.Add(this.lblCurrentNifty);
            this.grpBxIndices.Controls.Add(this.lblBNF);
            this.grpBxIndices.Controls.Add(this.lblNifty);
            this.grpBxIndices.Location = new System.Drawing.Point(590, 12);
            this.grpBxIndices.Name = "grpBxIndices";
            this.grpBxIndices.Size = new System.Drawing.Size(225, 44);
            this.grpBxIndices.TabIndex = 0;
            this.grpBxIndices.TabStop = false;
            this.grpBxIndices.Text = "Indices";
            // 
            // lblCurrentBNF
            // 
            this.lblCurrentBNF.AutoSize = true;
            this.lblCurrentBNF.Location = new System.Drawing.Point(177, 20);
            this.lblCurrentBNF.Name = "lblCurrentBNF";
            this.lblCurrentBNF.Size = new System.Drawing.Size(33, 13);
            this.lblCurrentBNF.TabIndex = 3;
            this.lblCurrentBNF.Text = "[BNF]";
            // 
            // lblCurrentNifty
            // 
            this.lblCurrentNifty.AutoSize = true;
            this.lblCurrentNifty.Location = new System.Drawing.Point(53, 20);
            this.lblCurrentNifty.Name = "lblCurrentNifty";
            this.lblCurrentNifty.Size = new System.Drawing.Size(26, 13);
            this.lblCurrentNifty.TabIndex = 2;
            this.lblCurrentNifty.Text = "[NF]";
            // 
            // lblBNF
            // 
            this.lblBNF.AutoSize = true;
            this.lblBNF.Location = new System.Drawing.Point(102, 20);
            this.lblBNF.Name = "lblBNF";
            this.lblBNF.Size = new System.Drawing.Size(69, 13);
            this.lblBNF.TabIndex = 1;
            this.lblBNF.Text = "BANK NIFTY:";
            // 
            // lblNifty
            // 
            this.lblNifty.AutoSize = true;
            this.lblNifty.Location = new System.Drawing.Point(6, 20);
            this.lblNifty.Name = "lblNifty";
            this.lblNifty.Size = new System.Drawing.Size(38, 13);
            this.lblNifty.TabIndex = 0;
            this.lblNifty.Text = "NIFTY:";
            // 
            // grpbxSStrangle
            // 
            this.grpbxSStrangle.Controls.Add(this.lblselectedput);
            this.grpbxSStrangle.Controls.Add(this.lblselectedcall);
            this.grpbxSStrangle.Controls.Add(this.label8);
            this.grpbxSStrangle.Controls.Add(this.label7);
            this.grpbxSStrangle.Controls.Add(this.lstBxPuts);
            this.grpbxSStrangle.Controls.Add(this.lstbxCalls);
            this.grpbxSStrangle.Controls.Add(this.label1);
            this.grpbxSStrangle.Controls.Add(this.lstBxExpiry);
            this.grpbxSStrangle.Controls.Add(this.lblInstruments);
            this.grpbxSStrangle.Controls.Add(this.lstbxInstruments);
            this.grpbxSStrangle.Controls.Add(this.btnOrder);
            this.grpbxSStrangle.Controls.Add(this.lblPEDelta);
            this.grpbxSStrangle.Controls.Add(this.lblCEDelta);
            this.grpbxSStrangle.Controls.Add(this.lblPut);
            this.grpbxSStrangle.Controls.Add(this.lblCE);
            this.grpbxSStrangle.Location = new System.Drawing.Point(16, 14);
            this.grpbxSStrangle.Name = "grpbxSStrangle";
            this.grpbxSStrangle.Size = new System.Drawing.Size(233, 474);
            this.grpbxSStrangle.TabIndex = 1;
            this.grpbxSStrangle.TabStop = false;
            this.grpbxSStrangle.Text = "Strangle (short)";
            this.grpbxSStrangle.Enter += new System.EventHandler(this.grpBXIndices_Enter);
            // 
            // lblselectedput
            // 
            this.lblselectedput.AutoSize = true;
            this.lblselectedput.Location = new System.Drawing.Point(49, 297);
            this.lblselectedput.Name = "lblselectedput";
            this.lblselectedput.Size = new System.Drawing.Size(29, 13);
            this.lblselectedput.TabIndex = 25;
            this.lblselectedput.Text = "[Put]";
            // 
            // lblselectedcall
            // 
            this.lblselectedcall.AutoSize = true;
            this.lblselectedcall.Location = new System.Drawing.Point(49, 274);
            this.lblselectedcall.Name = "lblselectedcall";
            this.lblselectedcall.Size = new System.Drawing.Size(30, 13);
            this.lblselectedcall.TabIndex = 24;
            this.lblselectedcall.Text = "[Call]";
            // 
            // label8
            // 
            this.label8.AutoSize = true;
            this.label8.Location = new System.Drawing.Point(140, 137);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(26, 13);
            this.label8.TabIndex = 23;
            this.label8.Text = "PUT";
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.Location = new System.Drawing.Point(10, 143);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(33, 13);
            this.label7.TabIndex = 22;
            this.label7.Text = "CALL";
            // 
            // lstBxPuts
            // 
            this.lstBxPuts.Font = new System.Drawing.Font("Microsoft Sans Serif", 7.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lstBxPuts.FormattingEnabled = true;
            this.lstBxPuts.Location = new System.Drawing.Point(143, 159);
            this.lstBxPuts.Name = "lstBxPuts";
            this.lstBxPuts.Size = new System.Drawing.Size(75, 95);
            this.lstBxPuts.TabIndex = 21;
            this.lstBxPuts.DoubleClick += new System.EventHandler(this.lstBxPuts_DoubleClick);
            // 
            // lstbxCalls
            // 
            this.lstbxCalls.Font = new System.Drawing.Font("Microsoft Sans Serif", 7.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lstbxCalls.FormattingEnabled = true;
            this.lstbxCalls.Location = new System.Drawing.Point(29, 159);
            this.lstbxCalls.Name = "lstbxCalls";
            this.lstbxCalls.Size = new System.Drawing.Size(75, 95);
            this.lstbxCalls.TabIndex = 20;
            this.lstbxCalls.DoubleClick += new System.EventHandler(this.lstbxCalls_DoubleClick);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(10, 78);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(35, 13);
            this.label1.TabIndex = 10;
            this.label1.Text = "Expiry";
            this.label1.Click += new System.EventHandler(this.label1_Click);
            // 
            // lstBxExpiry
            // 
            this.lstBxExpiry.Font = new System.Drawing.Font("Microsoft Sans Serif", 7.25F);
            this.lstBxExpiry.FormattingEnabled = true;
            this.lstBxExpiry.Location = new System.Drawing.Point(116, 78);
            this.lstBxExpiry.Name = "lstBxExpiry";
            this.lstBxExpiry.Size = new System.Drawing.Size(102, 56);
            this.lstBxExpiry.TabIndex = 9;
            this.lstBxExpiry.DoubleClick += new System.EventHandler(this.lstBxExpiry_DoubleClick);
            // 
            // lblInstruments
            // 
            this.lblInstruments.AutoSize = true;
            this.lblInstruments.Location = new System.Drawing.Point(10, 29);
            this.lblInstruments.Name = "lblInstruments";
            this.lblInstruments.Size = new System.Drawing.Size(56, 13);
            this.lblInstruments.TabIndex = 8;
            this.lblInstruments.Text = "Instrument";
            // 
            // lstbxInstruments
            // 
            this.lstbxInstruments.Font = new System.Drawing.Font("Microsoft Sans Serif", 7.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lstbxInstruments.FormattingEnabled = true;
            this.lstbxInstruments.Location = new System.Drawing.Point(116, 18);
            this.lstbxInstruments.Name = "lstbxInstruments";
            this.lstbxInstruments.Size = new System.Drawing.Size(102, 43);
            this.lstbxInstruments.TabIndex = 7;
            this.lstbxInstruments.DoubleClick += new System.EventHandler(this.lstbxInstruments_DoubleClick);
            // 
            // btnOrder
            // 
            this.btnOrder.Location = new System.Drawing.Point(152, 434);
            this.btnOrder.Name = "btnOrder";
            this.btnOrder.Size = new System.Drawing.Size(75, 23);
            this.btnOrder.TabIndex = 6;
            this.btnOrder.Text = "Sell";
            this.btnOrder.UseVisualStyleBackColor = true;
            this.btnOrder.Click += new System.EventHandler(this.btnOrder_Click);
            // 
            // lblPEDelta
            // 
            this.lblPEDelta.AutoSize = true;
            this.lblPEDelta.Location = new System.Drawing.Point(180, 297);
            this.lblPEDelta.Name = "lblPEDelta";
            this.lblPEDelta.Size = new System.Drawing.Size(38, 13);
            this.lblPEDelta.TabIndex = 5;
            this.lblPEDelta.Text = "[Delta]";
            // 
            // lblCEDelta
            // 
            this.lblCEDelta.AutoSize = true;
            this.lblCEDelta.Location = new System.Drawing.Point(180, 274);
            this.lblCEDelta.Name = "lblCEDelta";
            this.lblCEDelta.Size = new System.Drawing.Size(38, 13);
            this.lblCEDelta.TabIndex = 4;
            this.lblCEDelta.Text = "[Delta]";
            // 
            // lblPut
            // 
            this.lblPut.AutoSize = true;
            this.lblPut.Location = new System.Drawing.Point(10, 297);
            this.lblPut.Name = "lblPut";
            this.lblPut.Size = new System.Drawing.Size(26, 13);
            this.lblPut.TabIndex = 1;
            this.lblPut.Text = "PUT";
            // 
            // lblCE
            // 
            this.lblCE.AutoSize = true;
            this.lblCE.Location = new System.Drawing.Point(10, 274);
            this.lblCE.Name = "lblCE";
            this.lblCE.Size = new System.Drawing.Size(33, 13);
            this.lblCE.TabIndex = 0;
            this.lblCE.Text = "CALL";
            // 
            // tbctrlDeltaValue
            // 
            this.tbctrlDeltaValue.Controls.Add(this.tbDelta);
            this.tbctrlDeltaValue.Controls.Add(this.tbValue);
            this.tbctrlDeltaValue.Controls.Add(this.tbpgBuyBack);
            this.tbctrlDeltaValue.Controls.Add(this.tbFrequent);
            this.tbctrlDeltaValue.Controls.Add(this.tbpgConstantWidth);
            this.tbctrlDeltaValue.Controls.Add(this.tbpgActiveBuyStrangle);
            this.tbctrlDeltaValue.Location = new System.Drawing.Point(271, 187);
            this.tbctrlDeltaValue.Name = "tbctrlDeltaValue";
            this.tbctrlDeltaValue.SelectedIndex = 0;
            this.tbctrlDeltaValue.Size = new System.Drawing.Size(544, 205);
            this.tbctrlDeltaValue.TabIndex = 3;
            // 
            // tbDelta
            // 
            this.tbDelta.Controls.Add(this.lPEDeltaUW);
            this.tbDelta.Controls.Add(this.label5);
            this.tbDelta.Controls.Add(this.label6);
            this.tbDelta.Controls.Add(this.lPEDeltaLW);
            this.tbDelta.Controls.Add(this.label4);
            this.tbDelta.Controls.Add(this.lCEDeltaUW);
            this.tbDelta.Controls.Add(this.lCEDeltaLW);
            this.tbDelta.Controls.Add(this.label3);
            this.tbDelta.Location = new System.Drawing.Point(4, 22);
            this.tbDelta.Name = "tbDelta";
            this.tbDelta.Padding = new System.Windows.Forms.Padding(3);
            this.tbDelta.Size = new System.Drawing.Size(536, 179);
            this.tbDelta.TabIndex = 0;
            this.tbDelta.Text = "Delta";
            this.tbDelta.UseVisualStyleBackColor = true;
            // 
            // lPEDeltaUW
            // 
            this.lPEDeltaUW.Location = new System.Drawing.Point(150, 57);
            this.lPEDeltaUW.Name = "lPEDeltaUW";
            this.lPEDeltaUW.Size = new System.Drawing.Size(35, 18);
            this.lPEDeltaUW.TabIndex = 18;
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(6, 57);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(23, 13);
            this.label5.TabIndex = 19;
            this.label5.Text = "Put";
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(107, 60);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(18, 13);
            this.label6.TabIndex = 16;
            this.label6.Text = "To";
            // 
            // lPEDeltaLW
            // 
            this.lPEDeltaLW.Location = new System.Drawing.Point(61, 57);
            this.lPEDeltaLW.Name = "lPEDeltaLW";
            this.lPEDeltaLW.Size = new System.Drawing.Size(33, 18);
            this.lPEDeltaLW.TabIndex = 17;
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(6, 24);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(24, 13);
            this.label4.TabIndex = 15;
            this.label4.Text = "Call";
            // 
            // lCEDeltaUW
            // 
            this.lCEDeltaUW.Location = new System.Drawing.Point(150, 24);
            this.lCEDeltaUW.Name = "lCEDeltaUW";
            this.lCEDeltaUW.Size = new System.Drawing.Size(35, 18);
            this.lCEDeltaUW.TabIndex = 14;
            // 
            // lCEDeltaLW
            // 
            this.lCEDeltaLW.Location = new System.Drawing.Point(61, 24);
            this.lCEDeltaLW.Name = "lCEDeltaLW";
            this.lCEDeltaLW.Size = new System.Drawing.Size(33, 18);
            this.lCEDeltaLW.TabIndex = 13;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(107, 27);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(18, 13);
            this.label3.TabIndex = 12;
            this.label3.Text = "To";
            // 
            // tbValue
            // 
            this.tbValue.Controls.Add(this.label13);
            this.tbValue.Controls.Add(this.label12);
            this.tbValue.Controls.Add(this.label11);
            this.tbValue.Controls.Add(this.label10);
            this.tbValue.Controls.Add(this.txtPEMaxLossPercent);
            this.tbValue.Controls.Add(this.txtCEMaxLossPercent);
            this.tbValue.Controls.Add(this.txtPEMaxProfitPercent);
            this.tbValue.Controls.Add(this.txtCEMaxProfitPercent);
            this.tbValue.Controls.Add(this.lblMaxLoss);
            this.tbValue.Controls.Add(this.lblMaxProfit);
            this.tbValue.Controls.Add(this.txtPEMaxLossPoints);
            this.tbValue.Controls.Add(this.txtCEMaxLossPoints);
            this.tbValue.Controls.Add(this.txtPEMaxProfitPoints);
            this.tbValue.Controls.Add(this.txtCEMaxProfitPoints);
            this.tbValue.Controls.Add(this.lblPutTitle);
            this.tbValue.Controls.Add(this.lblCallTitle);
            this.tbValue.Location = new System.Drawing.Point(4, 22);
            this.tbValue.Name = "tbValue";
            this.tbValue.Padding = new System.Windows.Forms.Padding(3);
            this.tbValue.Size = new System.Drawing.Size(536, 179);
            this.tbValue.TabIndex = 1;
            this.tbValue.Text = "Value";
            this.tbValue.UseVisualStyleBackColor = true;
            // 
            // label13
            // 
            this.label13.AutoSize = true;
            this.label13.Location = new System.Drawing.Point(312, 38);
            this.label13.Name = "label13";
            this.label13.Size = new System.Drawing.Size(43, 13);
            this.label13.TabIndex = 15;
            this.label13.Text = "Percent";
            // 
            // label12
            // 
            this.label12.AutoSize = true;
            this.label12.Location = new System.Drawing.Point(86, 38);
            this.label12.Name = "label12";
            this.label12.Size = new System.Drawing.Size(43, 13);
            this.label12.TabIndex = 14;
            this.label12.Text = "Percent";
            // 
            // label11
            // 
            this.label11.AutoSize = true;
            this.label11.Location = new System.Drawing.Point(250, 38);
            this.label11.Name = "label11";
            this.label11.Size = new System.Drawing.Size(36, 13);
            this.label11.TabIndex = 13;
            this.label11.Text = "Points";
            // 
            // label10
            // 
            this.label10.AutoSize = true;
            this.label10.Location = new System.Drawing.Point(36, 38);
            this.label10.Name = "label10";
            this.label10.Size = new System.Drawing.Size(36, 13);
            this.label10.TabIndex = 12;
            this.label10.Text = "Points";
            // 
            // txtPEMaxLossPercent
            // 
            this.txtPEMaxLossPercent.Location = new System.Drawing.Point(315, 81);
            this.txtPEMaxLossPercent.Name = "txtPEMaxLossPercent";
            this.txtPEMaxLossPercent.Size = new System.Drawing.Size(44, 18);
            this.txtPEMaxLossPercent.TabIndex = 11;
            // 
            // txtCEMaxLossPercent
            // 
            this.txtCEMaxLossPercent.Location = new System.Drawing.Point(315, 57);
            this.txtCEMaxLossPercent.Name = "txtCEMaxLossPercent";
            this.txtCEMaxLossPercent.Size = new System.Drawing.Size(44, 18);
            this.txtCEMaxLossPercent.TabIndex = 10;
            // 
            // txtPEMaxProfitPercent
            // 
            this.txtPEMaxProfitPercent.Location = new System.Drawing.Point(89, 81);
            this.txtPEMaxProfitPercent.Name = "txtPEMaxProfitPercent";
            this.txtPEMaxProfitPercent.Size = new System.Drawing.Size(40, 18);
            this.txtPEMaxProfitPercent.TabIndex = 9;
            // 
            // txtCEMaxProfitPercent
            // 
            this.txtCEMaxProfitPercent.Location = new System.Drawing.Point(89, 57);
            this.txtCEMaxProfitPercent.Name = "txtCEMaxProfitPercent";
            this.txtCEMaxProfitPercent.Size = new System.Drawing.Size(40, 18);
            this.txtCEMaxProfitPercent.TabIndex = 8;
            // 
            // lblMaxLoss
            // 
            this.lblMaxLoss.AutoSize = true;
            this.lblMaxLoss.Location = new System.Drawing.Point(250, 3);
            this.lblMaxLoss.Name = "lblMaxLoss";
            this.lblMaxLoss.Size = new System.Drawing.Size(52, 13);
            this.lblMaxLoss.TabIndex = 7;
            this.lblMaxLoss.Text = "Max Loss";
            // 
            // lblMaxProfit
            // 
            this.lblMaxProfit.AutoSize = true;
            this.lblMaxProfit.Location = new System.Drawing.Point(44, 3);
            this.lblMaxProfit.Name = "lblMaxProfit";
            this.lblMaxProfit.Size = new System.Drawing.Size(54, 13);
            this.lblMaxProfit.TabIndex = 6;
            this.lblMaxProfit.Text = "Max Profit";
            // 
            // txtPEMaxLossPoints
            // 
            this.txtPEMaxLossPoints.Location = new System.Drawing.Point(253, 81);
            this.txtPEMaxLossPoints.Name = "txtPEMaxLossPoints";
            this.txtPEMaxLossPoints.Size = new System.Drawing.Size(46, 18);
            this.txtPEMaxLossPoints.TabIndex = 5;
            // 
            // txtCEMaxLossPoints
            // 
            this.txtCEMaxLossPoints.Location = new System.Drawing.Point(253, 57);
            this.txtCEMaxLossPoints.Name = "txtCEMaxLossPoints";
            this.txtCEMaxLossPoints.Size = new System.Drawing.Size(46, 18);
            this.txtCEMaxLossPoints.TabIndex = 4;
            // 
            // txtPEMaxProfitPoints
            // 
            this.txtPEMaxProfitPoints.Location = new System.Drawing.Point(36, 81);
            this.txtPEMaxProfitPoints.Name = "txtPEMaxProfitPoints";
            this.txtPEMaxProfitPoints.Size = new System.Drawing.Size(46, 18);
            this.txtPEMaxProfitPoints.TabIndex = 3;
            // 
            // txtCEMaxProfitPoints
            // 
            this.txtCEMaxProfitPoints.Location = new System.Drawing.Point(36, 57);
            this.txtCEMaxProfitPoints.Name = "txtCEMaxProfitPoints";
            this.txtCEMaxProfitPoints.Size = new System.Drawing.Size(46, 18);
            this.txtCEMaxProfitPoints.TabIndex = 2;
            // 
            // lblPutTitle
            // 
            this.lblPutTitle.AutoSize = true;
            this.lblPutTitle.Location = new System.Drawing.Point(6, 81);
            this.lblPutTitle.Name = "lblPutTitle";
            this.lblPutTitle.Size = new System.Drawing.Size(23, 13);
            this.lblPutTitle.TabIndex = 1;
            this.lblPutTitle.Text = "Put";
            // 
            // lblCallTitle
            // 
            this.lblCallTitle.AutoSize = true;
            this.lblCallTitle.Location = new System.Drawing.Point(3, 62);
            this.lblCallTitle.Name = "lblCallTitle";
            this.lblCallTitle.Size = new System.Drawing.Size(24, 13);
            this.lblCallTitle.TabIndex = 0;
            this.lblCallTitle.Text = "Call";
            // 
            // tbpgBuyBack
            // 
            this.tbpgBuyBack.Controls.Add(this.label9);
            this.tbpgBuyBack.Controls.Add(this.label2);
            this.tbpgBuyBack.Controls.Add(this.lblMaxLossBB);
            this.tbpgBuyBack.Controls.Add(this.lblGain);
            this.tbpgBuyBack.Controls.Add(this.txtPutLossPercent);
            this.tbpgBuyBack.Controls.Add(this.txtPutLossValue);
            this.tbpgBuyBack.Controls.Add(this.txtPutGainPercent);
            this.tbpgBuyBack.Controls.Add(this.txtPutGainValue);
            this.tbpgBuyBack.Controls.Add(this.txtCallLossPercent);
            this.tbpgBuyBack.Controls.Add(this.txtCallLossValue);
            this.tbpgBuyBack.Controls.Add(this.lblPutBBTitle);
            this.tbpgBuyBack.Controls.Add(this.txtCallGainPercent);
            this.tbpgBuyBack.Controls.Add(this.txtCallGainValue);
            this.tbpgBuyBack.Controls.Add(this.lblCallBB);
            this.tbpgBuyBack.Location = new System.Drawing.Point(4, 22);
            this.tbpgBuyBack.Name = "tbpgBuyBack";
            this.tbpgBuyBack.Size = new System.Drawing.Size(536, 179);
            this.tbpgBuyBack.TabIndex = 2;
            this.tbpgBuyBack.Text = "Buyback";
            this.tbpgBuyBack.UseVisualStyleBackColor = true;
            // 
            // label9
            // 
            this.label9.AutoSize = true;
            this.label9.Location = new System.Drawing.Point(130, 18);
            this.label9.Name = "label9";
            this.label9.Size = new System.Drawing.Size(44, 13);
            this.label9.TabIndex = 13;
            this.label9.Text = "lower of";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(36, 18);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(44, 13);
            this.label2.TabIndex = 12;
            this.label2.Text = "lower of";
            // 
            // lblMaxLossBB
            // 
            this.lblMaxLossBB.AutoSize = true;
            this.lblMaxLossBB.Location = new System.Drawing.Point(130, 4);
            this.lblMaxLossBB.Name = "lblMaxLossBB";
            this.lblMaxLossBB.Size = new System.Drawing.Size(52, 13);
            this.lblMaxLossBB.TabIndex = 11;
            this.lblMaxLossBB.Text = "Max Loss";
            // 
            // lblGain
            // 
            this.lblGain.AutoSize = true;
            this.lblGain.Location = new System.Drawing.Point(36, 3);
            this.lblGain.Name = "lblGain";
            this.lblGain.Size = new System.Drawing.Size(54, 13);
            this.lblGain.TabIndex = 10;
            this.lblGain.Text = "Max Profit";
            // 
            // txtPutLossPercent
            // 
            this.txtPutLossPercent.Location = new System.Drawing.Point(166, 66);
            this.txtPutLossPercent.Name = "txtPutLossPercent";
            this.txtPutLossPercent.Size = new System.Drawing.Size(29, 18);
            this.txtPutLossPercent.TabIndex = 9;
            // 
            // txtPutLossValue
            // 
            this.txtPutLossValue.Location = new System.Drawing.Point(126, 66);
            this.txtPutLossValue.Name = "txtPutLossValue";
            this.txtPutLossValue.Size = new System.Drawing.Size(34, 18);
            this.txtPutLossValue.TabIndex = 8;
            // 
            // txtPutGainPercent
            // 
            this.txtPutGainPercent.Location = new System.Drawing.Point(76, 66);
            this.txtPutGainPercent.Name = "txtPutGainPercent";
            this.txtPutGainPercent.Size = new System.Drawing.Size(29, 18);
            this.txtPutGainPercent.TabIndex = 7;
            // 
            // txtPutGainValue
            // 
            this.txtPutGainValue.Location = new System.Drawing.Point(36, 66);
            this.txtPutGainValue.Name = "txtPutGainValue";
            this.txtPutGainValue.Size = new System.Drawing.Size(34, 18);
            this.txtPutGainValue.TabIndex = 6;
            // 
            // txtCallLossPercent
            // 
            this.txtCallLossPercent.Location = new System.Drawing.Point(166, 38);
            this.txtCallLossPercent.Name = "txtCallLossPercent";
            this.txtCallLossPercent.Size = new System.Drawing.Size(29, 18);
            this.txtCallLossPercent.TabIndex = 5;
            // 
            // txtCallLossValue
            // 
            this.txtCallLossValue.Location = new System.Drawing.Point(126, 38);
            this.txtCallLossValue.Name = "txtCallLossValue";
            this.txtCallLossValue.Size = new System.Drawing.Size(34, 18);
            this.txtCallLossValue.TabIndex = 4;
            // 
            // lblPutBBTitle
            // 
            this.lblPutBBTitle.AutoSize = true;
            this.lblPutBBTitle.Location = new System.Drawing.Point(7, 69);
            this.lblPutBBTitle.Name = "lblPutBBTitle";
            this.lblPutBBTitle.Size = new System.Drawing.Size(23, 13);
            this.lblPutBBTitle.TabIndex = 3;
            this.lblPutBBTitle.Text = "Put";
            // 
            // txtCallGainPercent
            // 
            this.txtCallGainPercent.Location = new System.Drawing.Point(76, 38);
            this.txtCallGainPercent.Name = "txtCallGainPercent";
            this.txtCallGainPercent.Size = new System.Drawing.Size(29, 18);
            this.txtCallGainPercent.TabIndex = 2;
            // 
            // txtCallGainValue
            // 
            this.txtCallGainValue.Location = new System.Drawing.Point(36, 38);
            this.txtCallGainValue.Name = "txtCallGainValue";
            this.txtCallGainValue.Size = new System.Drawing.Size(34, 18);
            this.txtCallGainValue.TabIndex = 1;
            // 
            // lblCallBB
            // 
            this.lblCallBB.AutoSize = true;
            this.lblCallBB.Location = new System.Drawing.Point(6, 41);
            this.lblCallBB.Name = "lblCallBB";
            this.lblCallBB.Size = new System.Drawing.Size(24, 13);
            this.lblCallBB.TabIndex = 0;
            this.lblCallBB.Text = "Call";
            // 
            // tbFrequent
            // 
            this.tbFrequent.Controls.Add(this.lblFrequentMaxLossPoint);
            this.tbFrequent.Controls.Add(this.lblFrequentMaxProfit);
            this.tbFrequent.Controls.Add(this.txtBxMaxLossPoints);
            this.tbFrequent.Controls.Add(this.txtBxMaxProfitPoints);
            this.tbFrequent.Location = new System.Drawing.Point(4, 22);
            this.tbFrequent.Name = "tbFrequent";
            this.tbFrequent.Size = new System.Drawing.Size(536, 179);
            this.tbFrequent.TabIndex = 3;
            this.tbFrequent.Text = "Frequent";
            this.tbFrequent.UseVisualStyleBackColor = true;
            // 
            // lblFrequentMaxLossPoint
            // 
            this.lblFrequentMaxLossPoint.AutoSize = true;
            this.lblFrequentMaxLossPoint.Location = new System.Drawing.Point(16, 51);
            this.lblFrequentMaxLossPoint.Name = "lblFrequentMaxLossPoint";
            this.lblFrequentMaxLossPoint.Size = new System.Drawing.Size(79, 13);
            this.lblFrequentMaxLossPoint.TabIndex = 3;
            this.lblFrequentMaxLossPoint.Text = "Max Loss Point";
            // 
            // lblFrequentMaxProfit
            // 
            this.lblFrequentMaxProfit.AutoSize = true;
            this.lblFrequentMaxProfit.Location = new System.Drawing.Point(16, 15);
            this.lblFrequentMaxProfit.Name = "lblFrequentMaxProfit";
            this.lblFrequentMaxProfit.Size = new System.Drawing.Size(86, 13);
            this.lblFrequentMaxProfit.TabIndex = 2;
            this.lblFrequentMaxProfit.Text = "Max Profit Points";
            // 
            // txtBxMaxLossPoints
            // 
            this.txtBxMaxLossPoints.Location = new System.Drawing.Point(126, 48);
            this.txtBxMaxLossPoints.Name = "txtBxMaxLossPoints";
            this.txtBxMaxLossPoints.Size = new System.Drawing.Size(56, 18);
            this.txtBxMaxLossPoints.TabIndex = 1;
            // 
            // txtBxMaxProfitPoints
            // 
            this.txtBxMaxProfitPoints.Location = new System.Drawing.Point(126, 12);
            this.txtBxMaxProfitPoints.Name = "txtBxMaxProfitPoints";
            this.txtBxMaxProfitPoints.Size = new System.Drawing.Size(56, 18);
            this.txtBxMaxProfitPoints.TabIndex = 0;
            // 
            // tbpgConstantWidth
            // 
            this.tbpgConstantWidth.Controls.Add(this.txtMaxLossPoints);
            this.tbpgConstantWidth.Controls.Add(this.lblMaxLossPoints);
            this.tbpgConstantWidth.Location = new System.Drawing.Point(4, 22);
            this.tbpgConstantWidth.Name = "tbpgConstantWidth";
            this.tbpgConstantWidth.Size = new System.Drawing.Size(536, 179);
            this.tbpgConstantWidth.TabIndex = 4;
            this.tbpgConstantWidth.Text = "Constant Width";
            this.tbpgConstantWidth.UseVisualStyleBackColor = true;
            // 
            // txtMaxLossPoints
            // 
            this.txtMaxLossPoints.Location = new System.Drawing.Point(106, 22);
            this.txtMaxLossPoints.Name = "txtMaxLossPoints";
            this.txtMaxLossPoints.Size = new System.Drawing.Size(53, 18);
            this.txtMaxLossPoints.TabIndex = 1;
            // 
            // lblMaxLossPoints
            // 
            this.lblMaxLossPoints.AutoSize = true;
            this.lblMaxLossPoints.Location = new System.Drawing.Point(3, 22);
            this.lblMaxLossPoints.Name = "lblMaxLossPoints";
            this.lblMaxLossPoints.Size = new System.Drawing.Size(84, 13);
            this.lblMaxLossPoints.TabIndex = 0;
            this.lblMaxLossPoints.Text = "Max Loss Points";
            // 
            // tbpgActiveBuyStrangle
            // 
            this.tbpgActiveBuyStrangle.Controls.Add(this.txtStepQty);
            this.tbpgActiveBuyStrangle.Controls.Add(this.lblStepQty);
            this.tbpgActiveBuyStrangle.Controls.Add(this.txtMaxQty);
            this.tbpgActiveBuyStrangle.Controls.Add(this.txtInitialQty);
            this.tbpgActiveBuyStrangle.Controls.Add(this.lblMaxQty);
            this.tbpgActiveBuyStrangle.Controls.Add(this.lblInitialQty);
            this.tbpgActiveBuyStrangle.Location = new System.Drawing.Point(4, 22);
            this.tbpgActiveBuyStrangle.Name = "tbpgActiveBuyStrangle";
            this.tbpgActiveBuyStrangle.Size = new System.Drawing.Size(536, 179);
            this.tbpgActiveBuyStrangle.TabIndex = 5;
            this.tbpgActiveBuyStrangle.Text = "ActiveBuyStrangle";
            this.tbpgActiveBuyStrangle.UseVisualStyleBackColor = true;
            // 
            // txtStepQty
            // 
            this.txtStepQty.Location = new System.Drawing.Point(151, 67);
            this.txtStepQty.Name = "txtStepQty";
            this.txtStepQty.Size = new System.Drawing.Size(50, 18);
            this.txtStepQty.TabIndex = 11;
            // 
            // lblStepQty
            // 
            this.lblStepQty.AutoSize = true;
            this.lblStepQty.Location = new System.Drawing.Point(72, 70);
            this.lblStepQty.Name = "lblStepQty";
            this.lblStepQty.Size = new System.Drawing.Size(71, 13);
            this.lblStepQty.TabIndex = 10;
            this.lblStepQty.Text = "Step Quantity";
            // 
            // txtMaxQty
            // 
            this.txtMaxQty.Location = new System.Drawing.Point(415, 26);
            this.txtMaxQty.Name = "txtMaxQty";
            this.txtMaxQty.Size = new System.Drawing.Size(50, 18);
            this.txtMaxQty.TabIndex = 9;
            // 
            // txtInitialQty
            // 
            this.txtInitialQty.Location = new System.Drawing.Point(151, 26);
            this.txtInitialQty.Name = "txtInitialQty";
            this.txtInitialQty.Size = new System.Drawing.Size(50, 18);
            this.txtInitialQty.TabIndex = 8;
            // 
            // lblMaxQty
            // 
            this.lblMaxQty.AutoSize = true;
            this.lblMaxQty.Location = new System.Drawing.Point(301, 29);
            this.lblMaxQty.Name = "lblMaxQty";
            this.lblMaxQty.Size = new System.Drawing.Size(93, 13);
            this.lblMaxQty.TabIndex = 7;
            this.lblMaxQty.Text = "Maximum Quantity";
            this.lblMaxQty.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // lblInitialQty
            // 
            this.lblInitialQty.AutoSize = true;
            this.lblInitialQty.Location = new System.Drawing.Point(72, 29);
            this.lblInitialQty.Name = "lblInitialQty";
            this.lblInitialQty.Size = new System.Drawing.Size(73, 13);
            this.lblInitialQty.TabIndex = 6;
            this.lblInitialQty.Text = "Initial Quantity";
            // 
            // btnDummy
            // 
            this.btnDummy.Location = new System.Drawing.Point(695, 448);
            this.btnDummy.Name = "btnDummy";
            this.btnDummy.Size = new System.Drawing.Size(120, 23);
            this.btnDummy.TabIndex = 2;
            this.btnDummy.Text = "Start";
            this.btnDummy.UseVisualStyleBackColor = true;
            this.btnDummy.Click += new System.EventHandler(this.BtnDummy_Click);
            // 
            // dgActiveStrangles
            // 
            this.dgActiveStrangles.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgActiveStrangles.Location = new System.Drawing.Point(271, 79);
            this.dgActiveStrangles.Name = "dgActiveStrangles";
            this.dgActiveStrangles.Size = new System.Drawing.Size(544, 85);
            this.dgActiveStrangles.TabIndex = 3;
            // 
            // lblActiveStrangles
            // 
            this.lblActiveStrangles.AutoSize = true;
            this.lblActiveStrangles.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblActiveStrangles.Location = new System.Drawing.Point(268, 59);
            this.lblActiveStrangles.Name = "lblActiveStrangles";
            this.lblActiveStrangles.Size = new System.Drawing.Size(105, 16);
            this.lblActiveStrangles.TabIndex = 4;
            this.lblActiveStrangles.Text = "Active Strangles";
            // 
            // btnLoad
            // 
            this.btnLoad.Location = new System.Drawing.Point(271, 448);
            this.btnLoad.Name = "btnLoad";
            this.btnLoad.Size = new System.Drawing.Size(102, 23);
            this.btnLoad.TabIndex = 5;
            this.btnLoad.Text = "Load Strangles";
            this.btnLoad.UseVisualStyleBackColor = true;
            this.btnLoad.Click += new System.EventHandler(this.BtnLoad_Click);
            // 
            // button1
            // 
            this.button1.Location = new System.Drawing.Point(695, 477);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(120, 23);
            this.button1.TabIndex = 6;
            this.button1.Text = "Delta";
            this.button1.UseVisualStyleBackColor = true;
            this.button1.Click += new System.EventHandler(this.Button1_Click);
            // 
            // btnLoadTokens
            // 
            this.btnLoadTokens.Location = new System.Drawing.Point(271, 14);
            this.btnLoadTokens.Name = "btnLoadTokens";
            this.btnLoadTokens.Size = new System.Drawing.Size(75, 23);
            this.btnLoadTokens.TabIndex = 7;
            this.btnLoadTokens.Text = "Load Tokens";
            this.btnLoadTokens.UseVisualStyleBackColor = true;
            this.btnLoadTokens.Click += new System.EventHandler(this.btnLoadTokens_Click);
            // 
            // btnZeroMQTest
            // 
            this.btnZeroMQTest.Location = new System.Drawing.Point(546, 448);
            this.btnZeroMQTest.Name = "btnZeroMQTest";
            this.btnZeroMQTest.Size = new System.Drawing.Size(75, 23);
            this.btnZeroMQTest.TabIndex = 8;
            this.btnZeroMQTest.Text = "Store Data";
            this.btnZeroMQTest.UseVisualStyleBackColor = true;
            this.btnZeroMQTest.Click += new System.EventHandler(this.btnZeroMQTest_Click);
            // 
            // btnMarketService
            // 
            this.btnMarketService.Location = new System.Drawing.Point(425, 448);
            this.btnMarketService.Name = "btnMarketService";
            this.btnMarketService.Size = new System.Drawing.Size(115, 23);
            this.btnMarketService.TabIndex = 9;
            this.btnMarketService.Text = "Start Market Service";
            this.btnMarketService.UseVisualStyleBackColor = true;
            this.btnMarketService.Click += new System.EventHandler(this.btnMarketService_Click);
            // 
            // Main
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(833, 500);
            this.Controls.Add(this.btnMarketService);
            this.Controls.Add(this.btnZeroMQTest);
            this.Controls.Add(this.btnLoadTokens);
            this.Controls.Add(this.tbctrlDeltaValue);
            this.Controls.Add(this.button1);
            this.Controls.Add(this.btnLoad);
            this.Controls.Add(this.lblActiveStrangles);
            this.Controls.Add(this.dgActiveStrangles);
            this.Controls.Add(this.btnDummy);
            this.Controls.Add(this.grpbxSStrangle);
            this.Controls.Add(this.grpBxIndices);
            this.Font = new System.Drawing.Font("Microsoft Sans Serif", 7.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Name = "Main";
            this.Text = "Form1";
            this.Load += new System.EventHandler(this.Main_Load);
            this.grpBxIndices.ResumeLayout(false);
            this.grpBxIndices.PerformLayout();
            this.grpbxSStrangle.ResumeLayout(false);
            this.grpbxSStrangle.PerformLayout();
            this.tbctrlDeltaValue.ResumeLayout(false);
            this.tbDelta.ResumeLayout(false);
            this.tbDelta.PerformLayout();
            this.tbValue.ResumeLayout(false);
            this.tbValue.PerformLayout();
            this.tbpgBuyBack.ResumeLayout(false);
            this.tbpgBuyBack.PerformLayout();
            this.tbFrequent.ResumeLayout(false);
            this.tbFrequent.PerformLayout();
            this.tbpgConstantWidth.ResumeLayout(false);
            this.tbpgConstantWidth.PerformLayout();
            this.tbpgActiveBuyStrangle.ResumeLayout(false);
            this.tbpgActiveBuyStrangle.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgActiveStrangles)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.GroupBox grpBxIndices;
        private System.Windows.Forms.Label lblCurrentBNF;
        private System.Windows.Forms.Label lblCurrentNifty;
        private System.Windows.Forms.Label lblBNF;
        private System.Windows.Forms.Label lblNifty;
        private System.Windows.Forms.GroupBox grpbxSStrangle;
        private System.Windows.Forms.Label lblPut;
        private System.Windows.Forms.Label lblCE;
        private System.Windows.Forms.Label lblPEDelta;
        private System.Windows.Forms.Label lblCEDelta;
        private System.Windows.Forms.Button btnOrder;
        private System.Windows.Forms.ListBox lstbxInstruments;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.ListBox lstBxExpiry;
        private System.Windows.Forms.Label lblInstruments;
        private System.Windows.Forms.TextBox lCEDeltaUW;
        private System.Windows.Forms.TextBox lCEDeltaLW;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.TextBox lPEDeltaUW;
        private System.Windows.Forms.TextBox lPEDeltaLW;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Label lblselectedput;
        private System.Windows.Forms.Label lblselectedcall;
        private System.Windows.Forms.Label label8;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.ListBox lstBxPuts;
        private System.Windows.Forms.ListBox lstbxCalls;
        private System.Windows.Forms.Button btnDummy;
        private System.Windows.Forms.TabControl tbctrlDeltaValue;
        private System.Windows.Forms.TabPage tbDelta;
        private System.Windows.Forms.TabPage tbValue;
        private System.Windows.Forms.TextBox txtPEMaxLossPoints;
        private System.Windows.Forms.TextBox txtCEMaxLossPoints;
        private System.Windows.Forms.TextBox txtPEMaxProfitPoints;
        private System.Windows.Forms.TextBox txtCEMaxProfitPoints;
        private System.Windows.Forms.Label lblPutTitle;
        private System.Windows.Forms.Label lblCallTitle;
        private System.Windows.Forms.Label lblMaxLoss;
        private System.Windows.Forms.Label lblMaxProfit;
        private System.Windows.Forms.DataGridView dgActiveStrangles;
        private System.Windows.Forms.Label lblActiveStrangles;
        private System.Windows.Forms.TabPage tbpgBuyBack;
        private System.Windows.Forms.Label lblMaxLossBB;
        private System.Windows.Forms.Label lblGain;
        private System.Windows.Forms.TextBox txtPutLossPercent;
        private System.Windows.Forms.TextBox txtPutLossValue;
        private System.Windows.Forms.TextBox txtPutGainPercent;
        private System.Windows.Forms.TextBox txtPutGainValue;
        private System.Windows.Forms.TextBox txtCallLossPercent;
        private System.Windows.Forms.TextBox txtCallLossValue;
        private System.Windows.Forms.Label lblPutBBTitle;
        private System.Windows.Forms.TextBox txtCallGainPercent;
        private System.Windows.Forms.TextBox txtCallGainValue;
        private System.Windows.Forms.Label lblCallBB;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label9;
        private System.Windows.Forms.TabPage tbFrequent;
        private System.Windows.Forms.Label lblFrequentMaxLossPoint;
        private System.Windows.Forms.Label lblFrequentMaxProfit;
        private System.Windows.Forms.TextBox txtBxMaxLossPoints;
        private System.Windows.Forms.TextBox txtBxMaxProfitPoints;
        private System.Windows.Forms.Button btnLoad;
        private System.Windows.Forms.Button button1;
        private System.Windows.Forms.TabPage tbpgConstantWidth;
        private System.Windows.Forms.TextBox txtPEMaxLossPercent;
        private System.Windows.Forms.TextBox txtCEMaxLossPercent;
        private System.Windows.Forms.TextBox txtPEMaxProfitPercent;
        private System.Windows.Forms.TextBox txtCEMaxProfitPercent;
        private System.Windows.Forms.Label label13;
        private System.Windows.Forms.Label label12;
        private System.Windows.Forms.Label label11;
        private System.Windows.Forms.Label label10;
        private System.Windows.Forms.TextBox txtMaxLossPoints;
        private System.Windows.Forms.Label lblMaxLossPoints;
        private System.Windows.Forms.TabPage tbpgActiveBuyStrangle;
        private System.Windows.Forms.TextBox txtStepQty;
        private System.Windows.Forms.Label lblStepQty;
        private System.Windows.Forms.TextBox txtMaxQty;
        private System.Windows.Forms.TextBox txtInitialQty;
        private System.Windows.Forms.Label lblMaxQty;
        private System.Windows.Forms.Label lblInitialQty;
        private System.Windows.Forms.Button btnLoadTokens;
        private System.Windows.Forms.Button btnZeroMQTest;
        private System.Windows.Forms.Button btnMarketService;
    }
}

