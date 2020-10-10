using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Algorithms.Utilities;
using AdvanceAlgos.Algorithms;
using Global;
using MarketDataTest;
using StorageAlgos;
using System.Configuration;
using ZConnectWrapper;
using KiteConnect;

namespace GUI
{
    public partial class Main : Form
    {
        ManagedStrangleDelta strangle = new ManagedStrangleDelta();
        ManageStrangleValue valueStrangle = new ManageStrangleValue();
        ManageStrangleBuyBackWithoutNodeMovement bbStrangle = new ManageStrangleBuyBackWithoutNodeMovement();
        FrequentBuySell frequentTrade = new FrequentBuySell();
        FrequentBuySellWithNodemovement frequentMovements = new FrequentBuySellWithNodemovement();
        FrequentBuySellWithNodemovementBothSideContinuous frequentMovementBothSide = new FrequentBuySellWithNodemovementBothSideContinuous();
        ManageStrangleBuyBackWithoutNodeMovement_Aggressive bbStrangleAggressive = new ManageStrangleBuyBackWithoutNodeMovement_Aggressive();
        ManageStrangleConstantWidth_v2 manageStrangleConstantWidth = new ManageStrangleConstantWidth_v2();
        ManageBox manageBox = new ManageBox();
        ActiveBuyStrangleManagerWithVariableQty activeBuyStrangleManagerWithVariableQty = new ActiveBuyStrangleManagerWithVariableQty();
        ExpiryTrade expiryTrade = new ExpiryTrade();
        public Main()
        {
            InitializeComponent();
        }

        private void grpBXIndices_Enter(object sender, EventArgs e)
        {

        }

        private void btnOrder_Click(object sender, EventArgs e)
        {
            //create a list of all CEs and PEs for the selected expiry and instrument


            decimal bValue = 11300;///TODO Opened on 27 March at 11531
            Instrument bInst = (Instrument)lstbxInstruments.SelectedItem;
            Instrument peToken = (Instrument)lstBxPuts.SelectedItem;
            Instrument ceToken = (Instrument)lstbxCalls.SelectedItem;

            int stopLossPoints = 50;

            //Dummy..change it later
            peToken.LastPrice = 288; //28800 PE for 08 Aug expiry
            ceToken.LastPrice = 218; //29500 CE for 08 Aug expiry
            bInst.LastPrice = 29217; //as on 24 July


            //peToken.LastPrice = 41m; //11500 CE for July end
            //ceToken.LastPrice = 94m; //11500 PE for July end
            //bInst.LastPrice = 11374; //as on 22 July

            if (tbctrlDeltaValue.SelectedIndex == 0)
            {
                strangle.ManageStrangleDelta(bInst, peToken, ceToken,
                    Convert.ToDouble(lPEDeltaLW.Text), Convert.ToDouble(lPEDeltaUW.Text),
                    Convert.ToDouble(lCEDeltaLW.Text), Convert.ToDouble(lCEDeltaUW.Text),
                    stopLossPoints);
            }
            else if(tbctrlDeltaValue.SelectedIndex == 1)
            {
                valueStrangle.ManageStrangle(bInst, peToken, ceToken,
                    Convert.ToDecimal(txtPEMaxProfitPoints.Text), Convert.ToDecimal(txtPEMaxLossPoints.Text), 
                    Convert.ToDecimal(txtCEMaxProfitPoints.Text), Convert.ToDecimal(txtCEMaxLossPoints.Text),
                    stopLossPoints, strangleId:0, peMaxProfitPercent: Convert.ToDecimal(txtPEMaxProfitPercent.Text), 
                    peMaxLossPercent: Convert.ToDecimal(txtPEMaxLossPercent.Text),
                      ceMaxProfitPercent: Convert.ToDecimal(txtCEMaxProfitPercent.Text), 
                      ceMaxLossPercent: Convert.ToDecimal(txtCEMaxLossPercent.Text));
            }
            else if(tbctrlDeltaValue.SelectedIndex == 2)
            {
                bbStrangleAggressive.ManageStrangleBB(bInst, peToken, ceToken,
                       Convert.ToDecimal(txtPutGainValue.Text), Convert.ToDecimal(txtPutLossValue.Text),
                       Convert.ToDecimal(txtCallGainValue.Text), Convert.ToDecimal(txtCallLossValue.Text),
                       stopLossPoints, Convert.ToDecimal(txtPutGainPercent.Text), Convert.ToDecimal(txtPutLossPercent.Text),
                       Convert.ToDecimal(txtCallGainPercent.Text), Convert.ToDecimal(txtCallLossPercent.Text));
            }
            else if (tbctrlDeltaValue.SelectedIndex == 3)
            {
                //frequentTrade.ManageFrequentStrangle(bInst, peToken, ceToken,
                //       Convert.ToDecimal(txtBxMaxProfitPoints.Text), Convert.ToDecimal(txtBxMaxLossPoints.Text),
                //       stopLossPoints);
                //frequentMovements.ManageFrequentStrangle(bInst, peToken, ceToken,
                //       Convert.ToDecimal(txtBxMaxProfitPoints.Text), Convert.ToDecimal(txtBxMaxLossPoints.Text),
                //       stopLossPoints);

                frequentMovementBothSide.ManageFrequentStrangle(bInst, peToken, ceToken,
                    Convert.ToDecimal(txtBxMaxProfitPoints.Text), Convert.ToDecimal(txtBxMaxLossPoints.Text),
                        stopLossPoints);

            }
            else if (tbctrlDeltaValue.SelectedIndex == 4)
            {
                //frequentTrade.ManageFrequentStrangle(bInst, peToken, ceToken,
                //       Convert.ToDecimal(txtBxMaxProfitPoints.Text), Convert.ToDecimal(txtBxMaxLossPoints.Text),
                //       stopLossPoints);
                //frequentMovements.ManageFrequentStrangle(bInst, peToken, ceToken,
                //       Convert.ToDecimal(txtBxMaxProfitPoints.Text), Convert.ToDecimal(txtBxMaxLossPoints.Text),
                ////       stopLossPoints);
                //txtBxMaxLossPoints.Text = "10";
                manageStrangleConstantWidth.ManageStrangleWithConstantWidth(bInst, peToken, ceToken,
                    Convert.ToDecimal(txtMaxLossPoints.Text), timeOfOrder: DateTime.Now);

            }
            else if (tbctrlDeltaValue.SelectedIndex == 5)
            {
                activeBuyStrangleManagerWithVariableQty.StoreActiveBuyStrangeTrade(bInst, peToken, ceToken, Convert.ToInt32(txtInitialQty.Text),
                    Convert.ToInt32(txtMaxQty.Text), Convert.ToInt32(txtStepQty.Text), timeOfOrder: Convert.ToDateTime("2019-08-02 09:23:00"));

            }
            else if (tbctrlDeltaValue.SelectedIndex == 6)
            {
                //pivotCPRBasics.StorePivotInstruments(bInst.InstrumentToken, Convert.ToDateTime(lstBxExpiry.SelectedItem.ToString()), Convert.ToInt32(0),
                //    Convert.ToInt32(txtMaxQuantity.Text), Convert.ToInt32(txtStepQuantity.Text), Convert.ToInt32(lstBxPivotFrequency.SelectedItem.ToString() == "Daily" ? 1 : 1), Convert.ToInt32(txtPivotWindow.Text == "" ? "0" : txtPivotWindow.Text), timeOfOrder: Convert.ToDateTime("2019-09-25 09:23:00"));

            }

            else if (tbctrlDeltaValue.SelectedIndex == 7)
            {
                decimal strikePriceIncrement = ((Instrument)lstbxCalls.Items[0]).Strike - ((Instrument)lstbxCalls.Items[1]).Strike;

                //manageStrangleWithMaxPain.StoreIndexForMainPainStrangle(bInst.InstrumentToken, Convert.ToDateTime(lstBxExpiry.SelectedItem.ToString()), Convert.ToInt32(txtTradingQty.Text),
                //    Convert.ToInt32(strikePriceIncrement), Convert.ToDecimal(txtMaxLoss.Text), Convert.ToDecimal(txtProfitTarget.Text), timeOfOrder: Convert.ToDateTime("2019-09-25 09:23:00"));

                expiryTrade.StoreIndexForExpiryTrade(bInst.InstrumentToken, Convert.ToDateTime(lstBxExpiry.SelectedItem.ToString()), Convert.ToInt32(100),
                  Convert.ToInt32(strikePriceIncrement), Convert.ToDecimal(20), Convert.ToDecimal(30), timeOfOrder: Convert.ToDateTime("2019-09-25 09:23:00"));

            }

            //    Convert.ToDecimal(txtBxPE.Text),
            //    Convert.ToDecimal(txtBxCE.Text), Convert.ToDecimal(lPEDeltaLW.Text), 
            //    Convert.ToDecimal(lPEDeltaUW.Text), Convert.ToDecimal(lCEDeltaLW.Text), Convert.ToDecimal(lCEDeltaUW.Text));



            //public void ManageStrangle(UInt32 bToken, decimal bValue, UInt32 peToken, decimal pevalue, 
            //    UInt32 ceToken, decimal ceValue, decimal peLowerLimitValue, decimal peUpperLimtValue, 
            //    decimal peUpperLimitDelta, decimal peUpperLimitDelta, decimal ceLowerLimitValue, 
            //    decimal ceUpperLimtValue, decimal ceUpperLimitDelta, decimal ceUpperLimitDelta, decimal stopLossPoints = 0);


        }

        private void trackBar1_Scroll(object sender, EventArgs e)
        {

        }

        private void Main_Load(object sender, EventArgs e)
        {
            ZConnect.ZerodhaLogin();

            uint baseInstrument = 256265; //256265; //260105
            DateTime expiry = Convert.ToDateTime("2020-04-30");
            //Retrive all options

            DataLogic dl = new DataLogic();
            List<Instrument> bInstruments = dl.RetrieveBaseInstruments();
            lstbxInstruments.DataSource = new BindingSource(bInstruments, null); ;
            lstbxInstruments.DisplayMember = "TradingSymbol";
            lstbxInstruments.ValueMember = "InstrumentToken";
            // lstbxInstruments.SelectedIndex = 1;

           //dgActiveStrangles.AutoResizeColumns(
           //    DataGridViewAutoSizeColumnsMode.AllCells);

           // AlgoIndex algoIndex = AlgoIndex.BBStrangleWithoutMovement_Aggressive;
           // //Load current Strangles
           // DataSet dsActiveStrangles = dl.RetrieveActiveStrangles(algoIndex);
           // BindingSource bindingSource = new BindingSource(dsActiveStrangles, dsActiveStrangles.Tables[0].TableName);
           // dgActiveStrangles.DataSource = bindingSource;
            // dsActiveStrangles.
        }

        private void label1_Click(object sender, EventArgs e)
        {

        }

        private void lstbxInstruments_DoubleClick(object sender, EventArgs e)
        {
            DataLogic dl = new DataLogic();

            //Retrieve expiry list ONLY here and fulllist at expiry click
            List<string> expiryList = dl.RetrieveOptionExpiries(Convert.ToUInt32(lstbxInstruments.SelectedValue));


            lstBxExpiry.DataSource = expiryList;
            //lstBxExpiry.DisplayMember = "Expiry";
            //lstBxExpiry.ValueMember = "Expiry";
            lstBxExpiry.SelectedIndex = 0;

        }
        
        private void lstBxExpiry_DoubleClick(object sender, EventArgs e)
        {

            //Retrieve expiry list ONLY here and fulllist at expiry click
            DataLogic dl = new DataLogic();
            List<Instrument> optionsList = dl.RetrieveOptions(Convert.ToUInt32(lstbxInstruments.SelectedValue),
                Convert.ToDateTime(lstBxExpiry.SelectedValue));

            List<Instrument> options = optionsList;// Global.GlobalObjects.OptionsList;

            lstbxCalls.DataSource = new BindingSource(options.Where(x => x.InstrumentType.StartsWith("CE") && x.Expiry == Convert.ToDateTime(lstBxExpiry.SelectedValue)).OrderBy(x=>x.Strike), null);
            lstbxCalls.DisplayMember = "Strike";
            lstbxCalls.FormatString = String.Format("0.");
            lstbxCalls.ValueMember = "InstrumentToken";
            lstbxCalls.SelectedIndex = 0;

            lstBxPuts.DataSource = new BindingSource(options.Where(x => x.InstrumentType.StartsWith("PE") && x.Expiry == Convert.ToDateTime(lstBxExpiry.SelectedValue)).OrderBy(x => x.Strike), null);
            lstBxPuts.DisplayMember = "Strike";
            lstBxPuts.ValueMember = "InstrumentToken";
            lstBxPuts.SelectedIndex = 0;
            lstBxPuts.FormatString = String.Format("0.");

        }

        private void lstbxCalls_DoubleClick(object sender, EventArgs e)
        {
            lblselectedcall.Text = ((Instrument)lstbxCalls.SelectedItem).TradingSymbol;
            //Call Blackschole class or Instrument delta.Need to define a way to take option price for delta
        }

        private void lstBxPuts_DoubleClick(object sender, EventArgs e)
        {
            lblselectedput.Text = ((Instrument)lstBxPuts.SelectedItem).TradingSymbol;
            //Call Blackschole class or Instrument delta.Need to define a way to take option price for delta
        }

        private void StartDBUpdate()
        {
            string appSetting = ConfigurationManager.AppSettings["PullMarketData"];
            if (appSetting == "1")
            {
                // LoginUser();

                DataLogic_Candles dataLogic = new DataLogic_Candles();
                dataLogic.StoreMarketData();
            }
        }
        private void BtnDummy_Click(object sender, EventArgs e)
        {
            btnDummy.Text = "Stop";

            //Live
            //StartDBUpdate();

            //Test
            try
            {
                
                //TickDataStreamer dataStreamer = new TickDataStreamer();

                ////ManagedStrangle ms = new ManagedStrangle();
                //// strangle.Subscribe(dataStreamer);
                ////  valueStrangle.Subscribe(dataStreamer);
                //frequentMovements.Subscribe(dataStreamer);

                //Task<long> allDone = dataStreamer.BeginStreaming();
                //if (allDone != null && allDone.IsCompleted)
                //    MessageBox.Show(allDone.ToString());

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);

            }
        }

        private void BtnLoad_Click(object sender, EventArgs e)
        {
            DataLogic dl = new DataLogic();
            dgActiveStrangles.AutoResizeColumns(
              DataGridViewAutoSizeColumnsMode.AllCells);

            AlgoIndex algoIndex = AlgoIndex.BBStrangleWithoutMovement_Aggressive;
            //Load current Strangles
            DataSet dsActiveStrangles = dl.RetrieveActiveStrangles(algoIndex);
            BindingSource bindingSource = new BindingSource(dsActiveStrangles, dsActiveStrangles.Tables[0].TableName);
            dgActiveStrangles.DataSource = bindingSource;
        }

        private void Button1_Click(object sender, EventArgs e)
        {
            double iv = BlackScholesImpliedVol(76.75, 11400, 11419.25, 0.016438, 0.07, 0, PutCallFlag.Call);
           double optionsDelta = BlackScholesDelta(11400, 11419.25, 0.016438, iv, 0.07, 0, PutCallFlag.Call);
        }

        public static double BlackScholesDelta(double strike, double underlyingPrice, double yearsToExpiry, double vol, double riskFreeRate, double dividendYield, PutCallFlag putCallFlag)
        {
            double sqrtT = Math.Sqrt(yearsToExpiry);
            double d1 = (Math.Log(underlyingPrice / strike) + (riskFreeRate - dividendYield + 0.5 * vol * vol) * yearsToExpiry) / (vol * sqrtT);
            double N1 = StandardNormalCumulativeDistributionFunction(d1);
            if (putCallFlag == PutCallFlag.Call)
                return Math.Exp(-dividendYield * yearsToExpiry) * N1;
            return Math.Exp(-dividendYield * yearsToExpiry) * (N1 - 1.0);
        }

        public static double StandardNormalCumulativeDistributionFunction(double x)
        {
            //Approimation based on Abramowitz & Stegun (1964)

            if (x < 0)
                return 1.0 - StandardNormalCumulativeDistributionFunction(-x);
            const double b0 = 0.2316419;
            const double b1 = 0.319381530;
            const double b2 = -0.356563782;
            const double b3 = 1.781477937;
            const double b4 = -1.821255978;
            const double b5 = 1.330274429;
            double pdf = StandardNormalProbabilityDensityFunction(x);
            double a = 1.0 / (1.0 + b0 * x);
            return 1.0 - pdf * (b1 * a + b2 * Math.Pow(a, 2) + b3 * Math.Pow(a, 3) + b4 * Math.Pow(a, 4) + b5 * Math.Pow(a, 5));
        }
        /// <summary>
        /// Returns the PDF of the standard normal distribution.
        /// </summary>
        /// <param name="x">Value at which the distribution is evaluated.</param>
        public static double StandardNormalProbabilityDensityFunction(double x)
        {
            const double SqrtTwoPiInv = 0.398942280401433;
            return SqrtTwoPiInv * Math.Exp(-0.5 * x * x);
        }

        public static double BlackScholesImpliedVol(double price, double strike, double underlyingPrice, double yearsToExpiry, double riskFreeRate, double dividendYield, PutCallFlag putCallFlag)
        {
            const double tolerance = 0.001;
            const int maxLoops = 100;

            double vol = Math.Sqrt(2 * Math.Abs(Math.Log(underlyingPrice / strike) / yearsToExpiry + riskFreeRate));    //Manaster and Koehler intial vol value
            vol = Math.Max(0.01, vol);
            double vega;
            double impliedPrice = BlackScholesPriceAndVega(strike, underlyingPrice, yearsToExpiry, vol, riskFreeRate, dividendYield, putCallFlag, out vega);

            int nLoops = 0;
            while (Math.Abs(impliedPrice - price) > tolerance)
            {
                nLoops++;
                if (nLoops > maxLoops)
                    throw new Exception("BlackScholesImpliedVol did not converge.");

                vol = vol - (impliedPrice - price) / vega;
                if (vol <= 0)
                    vol = 0.5 * (vol + (impliedPrice - price) / vega); //half way btwn previous estimate and zero
                impliedPrice = BlackScholesPriceAndVega(strike, underlyingPrice, yearsToExpiry, vol, riskFreeRate, dividendYield, putCallFlag, out vega);
            }
            return vol;
        }

        private static double BlackScholesPriceAndVega(double strike, double underlyingPrice, double yearsToExpiry, double vol, double riskFreeRate, double dividendYield, PutCallFlag putCallFlag, out double vega)
        {
            double sqrtT = Math.Sqrt(yearsToExpiry);
            double d1 = (Math.Log(underlyingPrice / strike) + (riskFreeRate - dividendYield + 0.5 * vol * vol) * yearsToExpiry) / (vol * sqrtT);
            double d2 = d1 - vol * sqrtT;
            if (putCallFlag == PutCallFlag.Call)
            {
                double N1 = StandardNormalCumulativeDistributionFunction(d1);
                double N2 = StandardNormalCumulativeDistributionFunction(d2);
                double nn1 = StandardNormalProbabilityDensityFunction(d1);

                vega = underlyingPrice * Math.Exp(-dividendYield * yearsToExpiry) * nn1 * sqrtT;
                return N1 * underlyingPrice * Math.Exp(-dividendYield * yearsToExpiry) - N2 * strike * Math.Exp(-riskFreeRate * yearsToExpiry);
            }
            double Nn1 = StandardNormalCumulativeDistributionFunction(-d1);
            double Nn2 = StandardNormalCumulativeDistributionFunction(-d2);
            double n1 = StandardNormalProbabilityDensityFunction(d1);

            vega = underlyingPrice * Math.Exp(-dividendYield * yearsToExpiry) * n1 * sqrtT;
            return Nn2 * strike * Math.Exp(-riskFreeRate * yearsToExpiry) - Nn1 * underlyingPrice * Math.Exp(-dividendYield * yearsToExpiry);
        }

        private void btnLoadTokens_Click(object sender, EventArgs e)
        {
            //ZConnect.ZerodhaLogin();
            List<Instrument> instruments = ZObjects.kite.GetInstruments();

            DataLogic_Candles storage = new DataLogic_Candles();
            storage.StoreInstrumentList(instruments);
        }

        private void btnZeroMQTest_Click(object sender, EventArgs e)
        {
            //Storage storage = new Storage();
            //storage.ZMQClient();
        }

        private void btnMarketService_Click(object sender, EventArgs e)
        {
            //MarketData.PublishData();
            //if (ZObjects.ticker.IsConnected)
            //{
            //    ZObjects.ticker.Close();
            //}
        }
    }
    
}
