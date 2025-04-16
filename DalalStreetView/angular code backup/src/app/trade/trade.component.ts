import { Component, OnInit, Inject, ViewChild, ElementRef, AfterViewInit, EventEmitter } from '@angular/core';
import { Instrument } from '../data/instrument';
import { Expiry } from '../data/expiry';
import { Options } from '../data/options';
import { Order, ActiveAlgo } from '../data/order';
import { ChartData } from '../data/cdata';
import { LogData } from '../data/logdata';
import { AlgoHealth } from '../data/health';
import { TradeService } from './trade.service';
import { HttpClient } from '@angular/common/http';
import { FormBuilder, FormGroup, Validators, Form } from '@angular/forms';
import { FormsModule, ReactiveFormsModule } from '@angular/forms';
import { Constants, Loglevel, AlgoControllers, RunningAlgos } from '../data/constants';
import { ErrorDialog } from './error.component';
//import { TimeToStringPipe } from './TimeToString.Pipe';
import { grpc } from "@improbable-eng/grpc-web";
import { Logger, LoggerClient, Charter } from "../generated/log_pb_service";
import { LogMessage, Status, CData, CStatus } from "../generated/log_pb";
import { OrderAlerter, OrderAlerterClient } from "../generated/log_pb_service";
import { OrderMessage, PublishStatus } from "../generated/log_pb";
import { error } from '@angular/compiler/src/util';
import { Chart } from 'chart.js'
import * as moment from 'moment';
import { BehaviorSubject, concat, Observable } from 'rxjs';
import { MatDialog, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { Timestamp } from 'google-protobuf/google/protobuf/timestamp_pb';
import { Howl, Howler } from "../../../node_modules/howler/dist/howler.js";
import { MatSnackBar } from '@angular/material/snack-bar';

declare var require: any;
export interface DialogData {
  ain: number
  algo: string,
  message: string,
  source: string,
  time: string
}

@Component({
  selector: 'app-trade',
  templateUrl: './trade.component.html',
  styleUrls: ['./trade.component.scss']
})
export class TradeComponent implements OnInit, AfterViewInit {
  instrument: Instrument[];
  selectedInstrument: Instrument = new Instrument(0, '--Select--');
  expiry: Expiry[];
  options: Options[];
  orders: Order[];
  activeAlgos: ActiveAlgo[];
  activeAlgo: ActiveAlgo;
  call: Options[];
  put: Options[];
  cdata: ChartData;
  cds: ChartData[] = [];
  charts: Chart[] = [];
  //chart;
  log: LogData;
  logs: LogData[] = [];
  algosHealth: AlgoHealth[] = [];
  porder: Order;
  selectedInst: any;
  client: grpc.Client<Status, LogMessage> = grpc.client(Logger.Log, { host: 'https://localhost:5001' });
  oclient: grpc.Client<PublishStatus, OrderMessage> = grpc.client(OrderAlerter.Publish, { host: 'https://localhost:5001' });
  cclient: grpc.Client<CStatus, CData> = grpc.client(Charter.DrawChart, { host: 'https://localhost:5001' });
  selectedExpiry: any;
  selectedExpiry2: any;
  selectedCallValue: any;
  selectedPutValue: any;
  displayedColumns: string[] = ['orderTime', 'tradingSymbol', 'price', 'quantity', 'transactionType', 'orderType', 'status'];
  dataSource = this.orders;
  _baseUrl: any;
  result: any;
  ctf: any;
  quantity: any;
  mlpt: any;
  ps: any;
  ss: any;
  intd: any;
  straddle: any;
  eac: any;
  fut: any;
  cmb: any;
  ivc: any;
  oic: any;
  momentumTradeOptionsForm: FormGroup;
  rsiCrossOptionsForm: FormGroup;
  emaCrossOptionsForm: FormGroup;
  expiryTradeOptionsForm: FormGroup;
  sellwithRSIOptionsForm: FormGroup;
  buywithRSIOptionsForm: FormGroup;
  straddleMomentumOptionsForm: FormGroup;
  sarForm: FormGroup;
  straddleOnIndexRangeForm: FormGroup;
  straddleForm: FormGroup;
  deltaStrangleForm: FormGroup;
  activeBuyStrangleForm: FormGroup;
  generateAlertForm: FormGroup;
  premiumCrossForm: FormGroup;
  expiryStrangleForm: FormGroup;
  levelsStrangleForm: FormGroup;
  optionOptimizerForm: FormGroup;
  ivSpreadForm: FormGroup;
  calendarSpreadForm: FormGroup;
  directiononhistoricalaverageForm: FormGroup;
  strangleChartForm: FormGroup;
  paWithLevelsForm: FormGroup;
  optionsellonhtForm: FormGroup;
  multitimeframesellonhtForm: FormGroup;
  tj3Form: FormGroup;
  tj4Form: FormGroup;
  tj5Form: FormGroup;
  bcForm: FormGroup;
  bc2Form: FormGroup;
  estForm: FormGroup;
  paScalpingForm: FormGroup;
  simForm: FormGroup;
  maForm: FormGroup;
  ibForm: FormGroup;
  _sound: any;
  _ctrl: string;
  Algos: any;
  _algoCtrls: string;
  _ras: RunningAlgos[] = [];
  _ra: RunningAlgos;
  constructor(private http: HttpClient, private formBuilder: FormBuilder,
    @Inject('BASE_URL') baseUrl: string, private ts: TradeService, private dialog: MatDialog, private _snackBar: MatSnackBar) {
    this._baseUrl = baseUrl; //+ 'trade/';

    //get Instruments
    http.get<Instrument[]>(baseUrl + 'api/momentumvolume').subscribe(result => {
      this.instrument = result;
    }, error => console.error(error));

    //get Instruments
    http.get<Instrument[]>(baseUrl + 'api/rsicross').subscribe(result => {
      this.instrument = result;
    }, error => console.error(error));

    http.get<Instrument[]>(baseUrl + 'api/emacross').subscribe(result => {
      this.instrument = result;
    }, error => console.error(error));

    http.get<Instrument[]>(baseUrl + 'api/straddle').subscribe(result => {
      this.instrument = result;
    }, error => console.error(error));

    http.get<Instrument[]>(baseUrl + 'api/deltastrangle').subscribe(result => {
      this.instrument = result;
    }, error => console.error(error));

    http.get<Instrument[]>(baseUrl + 'api/activebuystrangle').subscribe(result => {
      this.instrument = result;
    }, error => console.error(error));

    http.get<Instrument[]>(baseUrl + 'api/alert').subscribe(result => {
      this.instrument = result;
    }, error => console.error(error));

    http.get<Instrument[]>(baseUrl + 'api/momentumstraddle').subscribe(result => {
      this.instrument = result;
    }, error => console.error(error));
    //get Instruments
    http.get<Instrument[]>(baseUrl + 'api/expirystrangle').subscribe(result => {
      this.instrument = result;
    }, error => console.error(error));
    //get Instruments
    http.get<Instrument[]>(baseUrl + 'api/stranglewithlevels').subscribe(result => {
      this.instrument = result;
    }, error => console.error(error));

    //get Instruments
    http.get<Instrument[]>(baseUrl + 'api/straddleexpirytrade').subscribe(result => {
      this.instrument = result;
    }, error => console.error(error));

    //get Instruments
    http.get<Instrument[]>(baseUrl + 'api/optionoptimizer').subscribe(result => {
      this.instrument = result;
    }, error => console.error(error));

    //get Instruments
    http.get<Instrument[]>(baseUrl + 'api/rsistrangle').subscribe(result => {
      this.instrument = result;
    }, error => console.error(error));

    http.get<Instrument[]>(baseUrl + 'api/rsitrade').subscribe(result => {
      this.instrument = result;
    }, error => console.error(error));

    http.get<Instrument[]>(baseUrl + 'api/premiumcross').subscribe(result => {
      this.instrument = result;
    }, error => console.error(error));

    http.get<Instrument[]>(baseUrl + 'api/priceaction').subscribe(result => {
      this.instrument = result;
    }, error => console.error(error));

    http.get<Instrument[]>(baseUrl + 'api/initialbreakout').subscribe(result => {
      this.instrument = result;
    }, error => console.error(error));

    http.get<Instrument[]>(baseUrl + 'api/ivtrade').subscribe(result => {
      this.instrument = result;
    }, error => console.error(error));
    http.get<Instrument[]>(baseUrl + 'api/calendarspread').subscribe(result => {
      this.instrument = result;
    }, error => console.error(error));
    http.get<Instrument[]>(baseUrl + 'api/directionaloptionsell').subscribe(result => {
      this.instrument = result;
    }, error => console.error(error));
    http.get<Instrument[]>(baseUrl + 'api/chart').subscribe(result => {
      this.instrument = result;
    }, error => console.error(error));

    http.get<any>(baseUrl + 'api/momentumvolume/activealgos').subscribe(result => {

      if (this.orders == null) {
        this.activeAlgos = result;
      }
      else {
        this.activeAlgos.push(result);
      }
    }, error => console.error(error));

    http.get<any>(baseUrl + 'api/rsicross/activealgos').subscribe(result => {

      if (this.orders == null) {
        this.activeAlgos = result;
      }
      else {
        this.activeAlgos.push(result);
      }
    }, error => console.error(error));
    http.get<any>(baseUrl + 'api/emacross/activealgos').subscribe(result => {

      if (this.orders == null) {
        this.activeAlgos = result;
      }
      else {
        this.activeAlgos.push(result);
      }
    }, error => console.error(error));

    http.get<any>(baseUrl + 'api/rsistrangle/activealgos').subscribe(result => {

      if (this.orders == null) {
        this.activeAlgos = result;
      }
      else {
        this.activeAlgos.push(result);
      }
    }, error => console.error(error));

    http.get<any>(baseUrl + 'api/rsitrade/activealgos').subscribe(result => {

      if (this.orders == null) {
        this.activeAlgos = result;
      }
      else {
        this.activeAlgos.push(result);
      }
    }, error => console.error(error));
    http.get<any>(baseUrl + 'api/optionsellonht/activealgos').subscribe(result => {

      if (this.orders == null) {
        this.activeAlgos = result;
      }
      else {
        this.activeAlgos.push(result);
      }
    }, error => console.error(error));
    http.get<any>(baseUrl + 'api/activebuystrangle/activealgos').subscribe(result => {

      if (this.orders == null) {
        this.activeAlgos = result;
      }
      else {
        this.activeAlgos.push(result);
      }
    }, error => console.error(error));

    http.get<any>(baseUrl + 'api/stranglewithlevels/activealgos').subscribe(result => {

      if (this.orders == null) {
        this.activeAlgos = result;
      }
      else {
        this.activeAlgos.push(result);
      }
    }, error => console.error(error));

    http.get<any>(baseUrl + 'api/expirystrangle/activealgos').subscribe(result => {

      if (this.orders == null) {
        this.activeAlgos = result;
      }
      else {
        this.activeAlgos.push(result);
      }
    }, error => console.error(error));

    http.get<any>(baseUrl + 'api/priceaction/activealgos').subscribe(result => {

      if (this.orders == null) {
        this.activeAlgos = result;
      }
      else {
        this.activeAlgos.push(result);
      }
    }, error => console.error(error));

    http.get<any>(baseUrl + 'api/optionoptimizer/activealgos').subscribe(result => {
      if (this.orders == null) {
        this.activeAlgos = result;
      }
      else {
        this.activeAlgos.push(result);
      }
    }, error => console.error(error));

    http.get<any>(baseUrl + 'api/straddleexpirytrade/activealgos').subscribe(result => {
      if (this.orders == null) {
        this.activeAlgos = result;
      }
      else {
        this.activeAlgos.push(result);
      }
    }, error => console.error(error));

    var context = new AudioContext();
    this._sound = new Howl({
      src: ['../assets/stop.mp3'],
      autoplay: false,
      loop: false,
      volume: 0.5,
      onend: function () {
        console.log('Finished!');
      }
    });
    context.resume();
  }


  private subj = new BehaviorSubject(this.log);
  private osubj = new BehaviorSubject(this.porder);
  private alsubj = new BehaviorSubject(this._ra);
  private csubj = new BehaviorSubject(this.cdata);

  returnAsObservable() {
    return this.subj.asObservable();
  }

  orderAsObservable() {
    return this.osubj.asObservable();
  }
  algoAsObservable() {
    return this.alsubj.asObservable();
  }
  chartAsObservable() {
    return this.csubj.asObservable();
  }

  filterLogsOfType(type) {
    return this.logs.filter(x => x.algoInstance == type.ains);
  }
  //filterChartsOfType(type) {
  //  var cd = this.cds.filter(x => x.algoInstance == type.ains);
  //  cd.forEach((cdi) => { 
  //    this.loadchartdata(cdi);
  //  });
  //}

  filterbyname(ctrl) {
    //return this._ras.filter(x => x._algoCrtl == ctrl);
    //this._ras.findIndex(x => x._algoCtrl == '171')

    if (this._ras !== undefined && this._ras.findIndex(x => x !== undefined && x._algoCtrl == ctrl) >= 0) {
      return true;
    }
    else return false;
  }

  ngOnInit(): void {
    let subject = this.subj;
    let osubject = this.osubj;
    let asubject = this.alsubj;
    let csubject = this.csubj;


    //this.selectedInst.value = 260105;
    for (var algo in AlgoControllers) {
      this.http.get<any>(this._baseUrl + 'api/' + algo + '/healthy').subscribe(result => {

        let ra = new RunningAlgos();
        // this._ra = new RunningAlgos();
        // this._ra._isRunning = result;
        // this._ra._algoCrtl = algo;
        //// this._ras.push(this._ra);
        // asubject.next(this._ra);

        //ra._isRunning = result;
        ra._algoCtrl = result;
        this._ras.push(ra);
        asubject.next(ra);
      }, error => {

        //let ra = new RunningAlgos();
        //// this._ra = new RunningAlgos();
        //// this._ra._isRunning = result;
        //// this._ra._algoCrtl = algo;
        ////// this._ras.push(this._ra);
        //// asubject.next(this._ra);

        //ra._isRunning = false;
        //ra._algoCrtl = algo;
        //// this._ras.push(this._ra);
        //asubject.next(ra);

      });

    }



    window.setInterval(this.checkhealth, 60001, this.algosHealth);

    this.onSelectInstrument(this.selectedInstrument.instrumentToken, this._ctrl);

    this.momentumTradeOptionsForm = this.formBuilder.group({
      ctf: ['', Validators.required]
      , quantity: ['', Validators.required]
      , mlpt: ['', Validators.required]
      //,ps: ['', Validators.required]
    });
    this.rsiCrossOptionsForm = this.formBuilder.group({
      ctf: ['', Validators.required]
      , qty: ['', Validators.required]
      , rmx: ['', Validators.required]
      , maxdfbi: ['', Validators.required]
      , mindfbi: ['', Validators.required]
    });
    this.emaCrossOptionsForm = this.formBuilder.group({
      ctf: ['', Validators.required]
      , qty: ['', Validators.required]
      , sema: ['', Validators.required]
      , lema: ['', Validators.required]
      , sl: ['', Validators.required]
      , tp: ['', Validators.required]
    });
    this.straddleMomentumOptionsForm = this.formBuilder.group({
      ctf: [5, Validators.required]
      , qty: [1, Validators.required]
      , sl: [50, Validators.required]
      , tr: [100, Validators.required]
      , ss: [false, Validators.required]
    });
    this.estForm = this.formBuilder.group({
      ctf: [15, Validators.required]
      , qty: [6, Validators.required]
      , sl: [50, Validators.required]
      , tp: [100, Validators.required]
      , tr: [2, Validators.required]
      , spi: [100, Validators.required]
      , uid: ['', Validators.required]
      , intd: [true, Validators.required]
    });
    this.sarForm = this.formBuilder.group({
      qty: ['1', Validators.required]
      , sl: [20, Validators.required]
      , tpr: [10, Validators.required]
      , th: [30, Validators.required]
      , vt: [300, Validators.required]
      , fo: ['', Validators.required]
    });
    this.straddleOnIndexRangeForm = this.formBuilder.group({
      qty: ['', Validators.required]
      , sl: ['', Validators.required]
      , tr: ['', Validators.required]
      , uid: ['', Validators.required]
    });
    this.straddleForm = this.formBuilder.group({
      qty: ['', Validators.required]
      , uid: ['', Validators.required]
    });
    this.deltaStrangleForm = this.formBuilder.group({
      iqty: [2, Validators.required]
      , stepqty: [1, Validators.required]
      , maxqty: [4, Validators.required]
      , idelta: [0.35, Validators.required]
      , nx: [1, Validators.required]
      , ridelta: [0.29, Validators.required]
      , maxdelta: [0.39, Validators.required]
      , mindelta: [0.25, Validators.required]
      , sl: [5000, Validators.required]
      , tp: [100000, Validators.required]
      , l1: [0, Validators.required]
      , l2: [0, Validators.required]
      , u1: [0, Validators.required]
      , u2: [0, Validators.required]
    });
    this.paWithLevelsForm = this.formBuilder.group({
      ctf: [5, Validators.required]
      , uid: ['', Validators.required]
      , pd_h: [0, Validators.required]
      , pd_l: [0, Validators.required]
      , pd_c: [0, Validators.required]
      , pw_h: [0, Validators.required]
      , pw_l: [0, Validators.required]
      , pw_c: [0, Validators.required]
      , ps_h: [0, Validators.required]
      , ps_l: [0, Validators.required]
      , sl: [5000, Validators.required]
      , tp: [100000, Validators.required]
      , qty: [1, Validators.required]
      , pd_bh: ['', Validators.required]
      , pd_bl: ['', Validators.required]
    });
    this.optionsellonhtForm = this.formBuilder.group({
      ctf: [5, Validators.required]
      , uid: ['', Validators.required]
      , tp: [100000, Validators.required]
      , qty: [1, Validators.required]
      , intd: [true, Validators.required]
    });
    this.multitimeframesellonhtForm = this.formBuilder.group({
      ctf: [5, Validators.required]
      , uid: ['', Validators.required]
      , tp: [100000, Validators.required]
      , qty: [1, Validators.required]
    });

    this.tj3Form = this.formBuilder.group({
      ctf: [5, Validators.required]
      , uid: ['', Validators.required]
      , pd_h: [0, Validators.required]
      , pd_l: [0, Validators.required]
      , pd_c: [0, Validators.required]
      , pw_h: [0, Validators.required]
      , pw_l: [0, Validators.required]
      , pw_c: [0, Validators.required]
      , ps_h: [0, Validators.required]
      , ps_l: [0, Validators.required]
      , sl: [5000, Validators.required]
      , tp: [100000, Validators.required]
      , qty: [1, Validators.required]
      , pd_bh: ['', Validators.required]
      , pd_bl: ['', Validators.required]
    });
    this.tj4Form = this.formBuilder.group({
      ctf: [5, Validators.required]
      , uid: ['', Validators.required]
      , pd_h: [0, Validators.required]
      , pd_l: [0, Validators.required]
      , pd_c: [0, Validators.required]
      , pw_h: [0, Validators.required]
      , pw_l: [0, Validators.required]
      , pw_c: [0, Validators.required]
      , ps_h: [0, Validators.required]
      , ps_l: [0, Validators.required]
      , sl: [5000, Validators.required]
      , tp: [100000, Validators.required]
      , qty: [1, Validators.required]
      , pd_bh: ['', Validators.required]
      , pd_bl: ['', Validators.required]
    });
    this.tj5Form = this.formBuilder.group({
      ctf: [5, Validators.required]
      , uid: ['', Validators.required]
      , sl: [5000, Validators.required]
      , tp: [100000, Validators.required]
      , qty: [1, Validators.required]
    });
    this.bcForm = this.formBuilder.group({
      ctf: [15, Validators.required]
      , uid: ['', Validators.required]
      , tp: [40, Validators.required]
      //, sl: [5000, Validators.required]
      , qty: [3, Validators.required]
    });
    this.bc2Form = this.formBuilder.group({
      ctf: [5, Validators.required]
      , uid: ['', Validators.required]
      , tp: [100000, Validators.required]
      //, sl: [5000, Validators.required]
      , qty: [1, Validators.required]
    });
    this.paScalpingForm = this.formBuilder.group({
      uid: ['', Validators.required]
      , ctf: [3, Validators.required]
      , qty: [1, Validators.required]
    });
    this.simForm = this.formBuilder.group({
      uid: ['', Validators.required]
      , ctf: [15, Validators.required]
      , qty: [1, Validators.required]
      , mns: [1, Validators.required]
      , tp: [10000, Validators.required]
      , slpt: [0.5, Validators.required]
    });
    this.maForm = this.formBuilder.group({
      uid: ['', Validators.required]
      , ctf: [5, Validators.required]
      , qty: [1, Validators.required]
      , mns: [1, Validators.required]
      , tp: [10000, Validators.required]
      , slpt: [0.5, Validators.required]
    });
    this.ibForm = this.formBuilder.group({
      uid: ['', Validators.required]
      , ctf: [15, Validators.required]
      , qty: [1, Validators.required]
    });
    this.activeBuyStrangleForm = this.formBuilder.group({
      iqty: [1, Validators.required]
      , stepqty: [1, Validators.required]
      , maxqty: [10, Validators.required]
      , idelta: [0.35, Validators.required]
      , maxdelta: [0.39, Validators.required]
      , mindelta: [0.25, Validators.required]
      , sl: [5000, Validators.required]
      , tp: [100000, Validators.required]
    });

    this.generateAlertForm = this.formBuilder.group({
      ctf: [15, Validators.required]
      , csp: [0.5, Validators.required]
    });

    this.premiumCrossForm = this.formBuilder.group({
      qty: ['', Validators.required]
      , tp: ['', Validators.required]
    });
    this.expiryStrangleForm = this.formBuilder.group({
      iqty: [4, Validators.required]
      , sqty: [1, Validators.required]
      , mqty: [8, Validators.required]
      , tp: [5000, Validators.required]
      , sl: [2000, Validators.required]
      , idfbi: [100, Validators.required]
      , mdfbi: [-100, Validators.required]
      , mptt: [15, Validators.required]
    });
    this.levelsStrangleForm = this.formBuilder.group({
      iqty: [4, Validators.required]
      , sqty: [2, Validators.required]
      , mqty: [8, Validators.required]
      , tp: [5000, Validators.required]
      , sl: [2000, Validators.required]
      , idelta: [0.2, Validators.required]
      , mindelta: [0.1, Validators.required]
      , maxdelta: [0.3, Validators.required]
      , l1: ['', Validators.required]
      , l2: ['', Validators.required]
      , u1: ['', Validators.required]
      , u2: ['', Validators.required]
      , iday: [1, Validators.required]
      , ctf: [120, Validators.required]
    });
    this.ivSpreadForm = this.formBuilder.group({
      qty: ['', Validators.required]
      , tp: ['', Validators.required]
    });
    this.calendarSpreadForm = this.formBuilder.group({
      ovar: ['', Validators.required]
      , cvar: ['', Validators.required]
      , sqty: ['', Validators.required]
      , maxqty: ['', Validators.required]
      , sl: ['', Validators.required]
      , tp: ['', Validators.required]
      , straddle: [false, Validators.required]
    });
    this.directiononhistoricalaverageForm = this.formBuilder.group({
      ovar: ['', Validators.required]
      , cvar: ['', Validators.required]
      , sqty: ['', Validators.required]
      , maxqty: ['', Validators.required]
      , sl: ['', Validators.required]
      , tp: ['', Validators.required]
    });

    this.strangleChartForm = this.formBuilder.group({
      oic: ['', Validators.required]
      , cmb: ['', Validators.required]
      , ivc: ['', Validators.required]
      , s1: ['', Validators.required]
      , s2: ['', Validators.required]
      , it1: ['', Validators.required]
      , it2: ['', Validators.required]
    });
    this.expiryTradeOptionsForm = this.formBuilder.group({
      iqty: ['', Validators.required],
      sqty: ['', Validators.required],
      mqty: ['', Validators.required],
      sl: ['', Validators.required],
      mdfbi: ['', Validators.required],
      mptt: ['', Validators.required]
      //,ps: ['', Validators.required]
    });
    this.sellwithRSIOptionsForm = this.formBuilder.group({
      qty: ['', Validators.required],
      ctf: ['', Validators.required],
      rlle: ['', Validators.required],
      rule: ['', Validators.required],
      rlx: ['', Validators.required],
      rmx: ['', Validators.required],
      maxdfbi: ['', Validators.required],
      mindfbi: ['', Validators.required],
      ema: ['', Validators.required]
    });

    this.buywithRSIOptionsForm = this.formBuilder.group({
      qty: ['', Validators.required],
      ctf: [5, Validators.required],
      maxdfbi: [225, Validators.required],
      edchl: [0, Validators.required],
      xdchl: [80, Validators.required],
      ema: [13, Validators.required],
      tp: [200, Validators.required],
      sl: [0, Validators.required],
      rulx: [80, Validators.required],
      rlle: [20, Validators.required],
      cell: ['', Validators.required],
      peul: ['', Validators.required],
      eac: [true, Validators.required],
      fut: [false, Validators.required]
    });
    this.returnAsObservable().subscribe(
      data => {
        if (data !== undefined) {
          if (data.logLevel === Loglevel.Health.toString()) {
            var selectedAlgoIndex = this.algosHealth.findIndex(x => x.aIns === data.algoInstance);

            var rs = data.message == "1" ? true : false;

            if (selectedAlgoIndex != -1) {
              if (this.algosHealth[selectedAlgoIndex].ah === true && rs === false) {
                this._sound.play();
              }
              this.algosHealth[selectedAlgoIndex].ah = rs;
              this.algosHealth[selectedAlgoIndex].ad = Date.now();
            }
            else {
              this.algosHealth.push(new AlgoHealth(data.algoInstance, rs, Date.now()));
            }
          }
          else if (data.logLevel === Loglevel.Error.toString()) {
            this.openDialog(data.algoInstance, data.algoid, data.message, data.messengerMethod, data.logTime);
          }

          //Do not add health pulse in to the log
          if (data.logLevel !== Loglevel.Health.toString()) {
            //this.logs.push(data);
            this.logs.unshift(data);
          }

        }
      }
    );
    this.chartAsObservable().subscribe(
      data => {
        if (data !== undefined) {
          //this.cds.unshift(data);
          this.UpdateChart(data);

        }
      }
    );


    this.orderAsObservable().subscribe(
      data => {
        if (data !== undefined) {
          var selectedAlgoIndex = this.activeAlgos.findIndex(x => x.ains === data.algoinstance);

          if (selectedAlgoIndex != -1) {
            var selectedOrderIndex = this.activeAlgos[selectedAlgoIndex].orders.findIndex(function (e) { e.orderid === data.orderid });

            if (selectedOrderIndex != -1) {
              this.activeAlgos[selectedAlgoIndex].orders[selectedOrderIndex] = data;
            }
            else {
              this.activeAlgos[selectedAlgoIndex].orders = this.activeAlgos[selectedAlgoIndex].orders.concat(data);
            }

            this.playordersound();
          }
        }
      }
    );

    this.algoAsObservable().subscribe(
      data => {
        if (data !== undefined)
          this._ras.push(this._ra);
      });

    //Code for GRPS Order Service
    var pstatus = new PublishStatus();
    pstatus.setStatus(true);

    this.oclient.start();
    this.oclient.send(pstatus);

    this.oclient.onMessage(function (message) {
      var results = message.toObject() as OrderMessage.AsObject;
      osubject.next(results);
    });

    //Code for GRPS Order Service
    var cstatus = new CStatus();
    cstatus.setStatus(true);

    this.cclient.start();
    this.cclient.send(cstatus);

    this.cclient.onMessage(function (message) {
      var results = message.toObject() as CData.AsObject;
      csubject.next(results);
    });

    //Code for GRPS Logger Service
    var status = new Status();
    status.setStatus(true);

    this.client.start();
    this.client.send(status);
    this.client.onMessage(function (message) {
      var results = message.toObject() as LogMessage.AsObject;
      subject.next(results);
    });

    //this.cds.forEach((cd) => {
    //  this.loadchartdata(cd)
    //});
  }

  ngAfterViewInit(): void {

    //this.cds.forEach((cd) => {
    //  this.loadchartdata(cd)
    //}); 

  }
  //UpdateChart() {
  //  chart.
  //}

  openSnackBar(message: string, action: string) {
    this._snackBar.open(message, action, {
      duration: 2000,
    });
  }


  playordersound() {
    var context = new AudioContext();
    this._sound = new Howl({
      src: ['../assets/order.mp3'],
      autoplay: false,
      loop: false,
      volume: 1.0,
      onend: function () {
        //console.log('Finished!');
      }
    });
    context.resume();
    this._sound.play();
  }

  checkhealth(alh) {
    alh.forEach((x) => {
      if (x.ad < Date.now() - 60000) {
        if (x.ah === true) {
          var context = new AudioContext();
          this._sound = new Howl({
            src: ['../assets/stop.mp3'],
            autoplay: false,
            loop: false,
            volume: 0.5,
            onend: function () {
              //console.log('Finished!');
            }
          });
          context.resume();
          this._sound.play();
        }
        x.ah = false;
      }
    });
  }

  UpdateChart(cd) {
    var chart = this.charts.filter(x => x.id == cd.chartid)[0];
    if (chart == undefined) {

      //var canvas = <HTMLCanvasElement>document.getElementById("canvas");
      //var ctx = canvas.getContext("2d");

      //const ctx = document.getElementById('chartx').getContext('2d');
      chart = new Chart(cd.chartid.toString(), {
        type: 'line',
        data: {
          labels: [],
          datasets: [
            //{
            //  type: 'line',
            //  data: [] //this can be both x and y
            //}
          ]
        },
        options: {
          //animation: {
          //  duration: 0 // general animation time
          //},
          //hover: {
          //  animationDuration: 0 // duration of animations when hovering an item
          //},
          //responsiveAnimationDuration: 0, // animation duration after a resize
          responsive: true,
          title: {
            display: true,
            text: 'Real Time Charts'
          },
          //elements: {
          //  line: {
          //    tension: 0 // disables bezier curves
          //  }
          //},
          legend: {
            display: false
          },
          scales: {
            xAxes: [{
              type: 'time',
              distribution: 'series',
              time: { tooltipFormat: 'LTS' }, ticks: {
                callback: function (value, index, values) {
                  if (!values[index]) { return }
                  return moment.utc(values[index]['value']).format('LTS');
                }
              }
            }],
            //xAxes: [{
            //  display: true,
            //  type: 'time',
            //  distribution: 'series',
            //  time: {
            //    displayFormats: {
            //      millisecond : 'HH:mm:ss'
            //    }
            //  }}],
            yAxes: [{
              id: '1',
              type: 'linear',
              position: 'left'
            }, {
              id: '2',
              type: 'linear',
              position: 'right'
            }],
          }
        }
      });
      chart.id = cd.chartid;
      this.charts.push(chart);
    }

    //chart.data.labels.filter(x => new Date(x).getSeconds() - (cd.t.seconds) > 500).remove();
    if (chart.data.labels.length >= 1000) {
      chart.data.labels.shift();
      var mint = chart.data.labels[0];

      chart.data.datasets.forEach((ds) => {
        if (ds.data[0] != undefined && ds.data[0].x.valueOf() < mint.valueOf()) {
          ds.data.shift();
        }
      });

      //chart.data.datasets.forEach((ds) => {
      //  ds.data.shift();
      //});
    }

    //var datetimestring = new Date(cd.t.seconds * 1000).toLocaleTimeString('en-US',
    //  { hour: 'numeric', minute: 'numeric', second: 'numeric', hour12: false, timeZone: 'UTC' });


    //var n = chart.data.labels.includes(datetimestring);
    //if (!n) {
    //  chart.data.labels.push(new Date(cd.t.seconds * 1000).toLocaleTimeString('en',
    //    { hour: 'numeric', minute: 'numeric', second: 'numeric', hour12: false, timeZone: 'UTC' }));

    //}

    var n = chart.data.labels;
    var nl = chart.data.labels.length;

    if (nl == 0) {
      chart.data.labels.push(new Date(cd.t.seconds * 1000));
    }
    else if (n[nl - 1].valueOf() != cd.t.seconds * 1000) {
      chart.data.labels.push(new Date(cd.t.seconds * 1000));
    }


    //if (!n) {
    //  chart.data.labels.push(new Date(cd.t.seconds * 1000));
    //}
    if (chart.data.datasets.length == 0) {
      chart.data.datasets = [];
    }
    //var ds = chart.data.datasets.filter(x => x.label == cd.instrumenttoken.toString());
    var ds = chart.data.datasets.find(x => x.label == cd.instrumenttoken.toString());

    //if (ds.length == 0) {
    if (ds == undefined) {
      //ds = [];
      //var obj = {
      //  type: "line", label: cd.instrumenttoken.toString(), data: [], borderColor: "#3cba9f", backgroundColor: "#ffff00", fill: false
      //};
      var ds = {
        //borderColor: "#3cba9f"
        type: "line", label: cd.instrumenttoken.toString(), data: [], borderColor: cd.arg, backgroundColor: "#ffff00", fill: false, yAxisID: cd.chartdataid.toString()
      };
      //var jsonobj = JSON.parse(obj)
      //chart.data.datasets.push(obj);
      //ds.push(obj);
      chart.data.datasets.push(ds);
    }
    //ds.data.push(cd.d);
    //chart.data.datasets.push(ds);
    var xy = { x: new Date(cd.t.seconds * 1000), y: cd.d };
    //chart.data.datasets.find(x => x.label == cd.instrumenttoken.toString()).data.push(cd.d);

    var m = chart.data.datasets.find(x => x.label == cd.instrumenttoken.toString()).data;
    var ml = m.length;

    if (ml == 0) {
      chart.data.datasets.find(x => x.label == cd.instrumenttoken.toString()).data.push(xy);
    }
    else if (m[ml - 1].x.valueOf() != cd.t.seconds * 1000) {
      chart.data.datasets.find(x => x.label == cd.instrumenttoken.toString()).data.push(xy);
    }
    //chart.data.datasets[cd.chartdataid - 1].yAxisID = cd.chartdataid.toString();
    //chart.data.datasets.find(x => x.label == cd.instrumenttoken.toString()).yAxisID = cd.chartdataid.toString();
    //chart.data.datasets.filter(x => x.label == cd.instrumenttoken.toString())[0].data.push(cd.d);
    //ds[0].data.push(cd.d);

    //if (chart.data.datasets.length < cd.chartdataid) {
    //  if (chart.data.datasets.length == 0) {
    //    chart.data.datasets = [];
    //  }
    //  var obj = {
    //    type: "line", label: cd.instrumenttoken.toString(), data: [], borderColor: "#3cba9f", backgroundColor: "#ffff00", fill: false};
    //    //var jsonobj = JSON.parse(obj)
    //  chart.data.datasets.push(obj);
    //  //chart.data.datasets[cd.chartdataid - 1].data = [];
    //  //chart.data.datasets[cd.chartdataid - 1].type = 'line';
    //}
    //chart.data.datasets[cd.chartdataid - 1].data.push(cd.d);
    chart.update();
  }

  //loadchartdata(cd) {
  //  var chart = this.charts.filter(x => x.id == cd.cid)[0];
  //  if (chart == undefined) {

  //      var canvas = document.createElement("canvas");
  //      canvas.id = cd.chartid;
  //      document.getElementById('div1').appendChild(canvas);

  //      // ----- more code ------ 

  //      setTimeout(function () {
  //        chart = new Chart(cd.chartid, { type: 'line' });
  //        this.charts.push(chart);
  //      }, 1);
  //    }

  //    chart.data.labels = cd.map(x => x.t);

  //    if (chart.data.datasets.count < cd.chartdataid) {
  //      chart.data.datasets = [];
  //    }
  //  chart.data.datasets[cd.chartdataid - 1].data = cd.map(x => x.d);
  //  chart.data.datasets[cd.chartdataid - 1].type = 'line';
  //    chart.update();

  //  //this.charts = new Chart('canvas', {
  //  //  type: 'line'
  //  //  //,
  //  //  //data: {
  //  //  //  labels: ['Jan', 'Feb', 'Mar', 'Apr'],
  //  //  //  datasets: [
  //  //  //    {
  //  //  //      type: 'line'
  //  //  //      //data: [10,20,30,40]
  //  //  //    }
  //  //  //  ]
  //  //  //}
  //  //});
  //}

  gethealthstatus(an) {
    var selectedAlgoIndex = this.algosHealth.findIndex(x => x.aIns === an);

    if (selectedAlgoIndex != -1) {
      return this.algosHealth[selectedAlgoIndex].ah;
    }
    else {
      return false;
    }
  }

  getTokens() {
    this.http.get(this._baseUrl + 'api/home').subscribe(result => {
      this.openSnackBar(result.toString(), "Tokens Loaded");
    }, error => console.error(error));
  }

  getKotakTokens() {
    this.http.get(this._baseUrl + 'api/kotaklogin').subscribe(result => {
      this.openSnackBar(result.toString(), "Tokens Loaded");
    }, error => console.error(error));
  }

  openDialog(ain, algo, message, source, time) {
    this.dialog.open(ErrorDialog, { data: { ain: ain, algo: algo, message: message, source: source, time: new Date(time.seconds * 1000).toLocaleString() } });
  }

  stopExchangeUpdates() {
    this.ts.stopExchangeUpdates();
  }

  //openDialog(): void {
  //  const dialogRef = this.dialog.open(DialogOverviewExampleDialog, {
  //    width: '250px',
  //    data: {name: this.name, animal: this.animal}
  //  });


  //get Expiry
  getExpiry(token, ctrl) {
    this.http.get<Expiry[]>(this._baseUrl + 'api/' + ctrl + '/' + token.value).subscribe(result => {
      this.expiry = result;
    }, error => console.error(error));
  }
  //get call put options
  getOption(token, expval, ctrl) {
    this.http.get<Options[]>(this._baseUrl + 'api/' + ctrl + '/' + token.value + '/' + expval.value).subscribe(result => {
      this.options = result;
      this.call = this.options.filter(function (item) {
        return item.type.toLowerCase() === 'ce';
      });
      this.put = this.options.filter(function (item) {
        return item.type.toLowerCase() === 'pe';
      });
    }, error => console.error(error));
  }

  onSelectInstrument(instid, ctrl) {
    this.selectedInst = instid;
    this.getExpiry(instid, ctrl);
  }

  onSelectExpiry(expval, ctrl) {
    this.selectedExpiry = expval.value;
    this.getOption(this.selectedInst, expval, ctrl);
  }
  onSelectExpiries(expval, ctrl, num) {
    if (num == "1") {
      this.selectedExpiry = expval.value;
      this.getOption(this.selectedInst, expval, ctrl);
    }
    if (num == "2") {
      this.selectedExpiry2 = expval.value;
      this.getOption(this.selectedInst, expval, ctrl);
    }
  }

  onSelectCall(e) {
    this.selectedCallValue = e.value;
  }

  onSelectPut(e) {
    this.selectedPutValue = e.value;
  }
  onPSChange(e) {
    this.ps = e.checked;
  }
  onSSChange(e) {
    this.ss = e.checked;
  }
  onIntDChange(e) {
    this.intd = e.checked;
  }
  onFOChange(e) {
    this.ss = e.checked;
  }
  onStraddleChange(e) {
    this.straddle = e.checked;
  }
  onCmbChange(e) {
    this.cmb = e.checked;
  }
  onIVChange(e) {
    this.ivc = e.checked;
  }
  onOIChange(e) {
    this.oic = e.checked;
  }
  onEACChange(e) {
    this.eac = e.checked;
  }
  onFutChange(e) {
    this.fut = e.checked;
  }
  //algo panel section
  panelOpenState = false;

  executeAlgo() {
    const data = {
      token: this.selectedInst.value,
      expiry: this.selectedExpiry,
      ctf: this.momentumTradeOptionsForm.value.ctf,
      quantity: this.momentumTradeOptionsForm.value.quantity,
      ps: this.ps,
      mlpt: this.momentumTradeOptionsForm.value.mlpt
    }
    this.http.post<ActiveAlgo>(this._baseUrl + 'api/momentumvolume', data).subscribe(result => {
      this.activeAlgos.push(result);
    }, error => console.error(error));
  }

  executeRsiCross() {
    const data = {
      btoken: this.selectedInst.value,
      expiry: this.selectedExpiry,
      ctf: this.rsiCrossOptionsForm.value.ctf,
      qty: this.rsiCrossOptionsForm.value.qty,
      //rmx: this.rsiCrossOptionsForm.value.rmx,
      mindfbi: this.rsiCrossOptionsForm.value.mindfbi,
      dchl: this.rsiCrossOptionsForm.value.dchl,
    }
    this.http.post<ActiveAlgo>(this._baseUrl + 'api/rsicross', data).subscribe(result => {
      this.activeAlgos.push(result);
    }, error => console.error(error));
  }
  executeEMACross() {
    const data = {
      btoken: this.selectedInst.value,
      expiry: this.selectedExpiry,
      ctf: this.emaCrossOptionsForm.value.ctf,
      qty: this.emaCrossOptionsForm.value.qty,
      sema: this.emaCrossOptionsForm.value.sema,
      lema: this.emaCrossOptionsForm.value.lema,
      sl: this.emaCrossOptionsForm.value.sl,
      tp: this.emaCrossOptionsForm.value.tp,
    }
    this.http.post<ActiveAlgo>(this._baseUrl + 'api/emacross', data).subscribe(result => {
      this.activeAlgos.push(result);
    }, error => console.error(error));
  }
  executeStraddleMomentum() {
    const data = {
      btoken: this.selectedInst.value,
      expiry: this.selectedExpiry,
      ctf: this.straddleMomentumOptionsForm.value.ctf,
      qty: this.straddleMomentumOptionsForm.value.qty,
      sl: this.straddleMomentumOptionsForm.value.sl,
      tr: this.straddleMomentumOptionsForm.value.tr,
      ss: this.straddleMomentumOptionsForm.value.ss
    }
    if (data.ss == "") { data.ss = false }
    this.http.post<ActiveAlgo>(this._baseUrl + 'api/momentumstraddle', data).subscribe(result => {
      this.activeAlgos.push(result);
    }, error => console.error(error));
  }
  executeSARScalping() {
    const data = {
      btoken: this.selectedInst.value,
      expiry: this.selectedExpiry,
      qty: this.sarForm.value.qty,
      sl: this.sarForm.value.sl,
      tp: this.sarForm.value.tp,
      vt: this.sarForm.value.vt,
      th: this.sarForm.value.th,
      uid: this.sarForm.value.uid,
      fo: this.sarForm.value.fo
    }
    if (data.fo == "") { data.fo = false }
    this.http.post<ActiveAlgo>(this._baseUrl + 'api/sar', data).subscribe(result => {
      this.activeAlgos.push(result);
    }, error => console.error(error));
  }
  executeStraddleOnIndexRange() {
    const data = {
      btoken: this.selectedInst.value,
      expiry: this.selectedExpiry,
      qty: this.straddleOnIndexRangeForm.value.qty,
      sl: this.straddleOnIndexRangeForm.value.sl,
      tr: this.straddleOnIndexRangeForm.value.tp,
      uid: this.straddleOnIndexRangeForm.value.uid
    }
    this.http.post<ActiveAlgo>(this._baseUrl + 'api/straddleindexrange', data).subscribe(result => {
      this.activeAlgos.push(result);
    }, error => console.error(error));
  }
  executeStraddleLegsCutoff() {
    const data = {
      btoken: this.selectedInst.value,
      qty: this.straddleForm.value.qty,
      uid: this.straddleForm.value.uid
    }
    this.http.post<ActiveAlgo>(this._baseUrl + 'api/straddle', data).subscribe(result => {
      this.activeAlgos.push(result);
    }, error => console.error(error));
  }
  executeInitialStraddle() {
    const data = {
      btoken: this.selectedInst.value,
      expiry: this.selectedExpiry,
      ctf: this.straddleMomentumOptionsForm.value.ctf,
      qty: this.straddleMomentumOptionsForm.value.qty,
      sl: this.straddleMomentumOptionsForm.value.sl,
      tr: this.straddleMomentumOptionsForm.value.tr

    }

    this.http.post<ActiveAlgo>(this._baseUrl + 'api/referencestraddle', data).subscribe(result => {
      this.activeAlgos.push(result);
    }, error => console.error(error));
  }

  executeDeltaStrangle() {
    const data = {
      btoken: this.selectedInst.value,
      expiry: this.selectedExpiry,
      iqty: this.deltaStrangleForm.value.iqty,
      stepqty: this.deltaStrangleForm.value.stepqty,
      maxqty: this.deltaStrangleForm.value.maxqty,
      idelta: this.deltaStrangleForm.value.idelta,
      i2delta: this.deltaStrangleForm.value.ridelta,
      maxdelta: this.deltaStrangleForm.value.maxdelta,
      mindelta: this.deltaStrangleForm.value.mindelta,
      sl: this.deltaStrangleForm.value.sl,
      tp: this.deltaStrangleForm.value.tp,
      l1: this.deltaStrangleForm.value.l1,
      l2: this.deltaStrangleForm.value.l2,
      u1: this.deltaStrangleForm.value.u1,
      u2: this.deltaStrangleForm.value.u2

    }

    this.http.post<ActiveAlgo>(this._baseUrl + 'api/deltastrangle', data).subscribe(result => {
      this.activeAlgos.push(result);
    }, error => console.error(error));
  }


  executePAWithLevels() {
    const data = {
      btoken: this.selectedInst.value,
      expiry: this.selectedExpiry,
      ctf: this.paWithLevelsForm.value.ctf,
      pd_h: this.paWithLevelsForm.value.pd_h,
      pd_l: this.paWithLevelsForm.value.pd_l,
      pd_c: this.paWithLevelsForm.value.pd_c,
      pd_bh: this.paWithLevelsForm.value.pd_bh,
      pd_bl: this.paWithLevelsForm.value.pd_bl,
      pw_h: this.paWithLevelsForm.value.pw_h,
      pw_l: this.paWithLevelsForm.value.pw_l,
      pw_c: this.paWithLevelsForm.value.pw_c,
      ps_h: this.paWithLevelsForm.value.ps_h,
      //ps_l: this.paWithLevelsForm.value.ps_l,
      uid: this.paWithLevelsForm.value.uid,
      tp: this.paWithLevelsForm.value.tp,
      sl: this.paWithLevelsForm.value.sl,
      qty: this.paWithLevelsForm.value.qty,
    }

    this.http.post<ActiveAlgo>(this._baseUrl + 'api/priceaction', data).subscribe(result => {
      this.activeAlgos.push(result);
    }, error => console.error(error));
  }
  executeOptionSellOnHT() {
    const data = {
      btoken: this.selectedInst.value,
      expiry: this.selectedExpiry,
      ctf: this.optionsellonhtForm.value.ctf,
      uid: this.optionsellonhtForm.value.uid,
      tp: this.optionsellonhtForm.value.tp,
      qty: this.optionsellonhtForm.value.qty,
      intd: this.optionsellonhtForm.value.intd
    }
    if (data.intd == "") { data.intd = false }
    this.http.post<ActiveAlgo>(this._baseUrl + 'api/optionsellonht', data).subscribe(result => {
      this.activeAlgos.push(result);
    }, error => console.error(error));
  }
  executeMultiTimeSellOnHT() {
    const data = {
      btoken: this.selectedInst.value,
      expiry: this.selectedExpiry,
      ctf: this.multitimeframesellonhtForm.value.ctf,
      uid: this.multitimeframesellonhtForm.value.uid,
      tp: this.multitimeframesellonhtForm.value.tp,
      qty: this.multitimeframesellonhtForm.value.qty,
    }

    this.http.post<ActiveAlgo>(this._baseUrl + 'api/multitimeframesellonht', data).subscribe(result => {
      this.activeAlgos.push(result);
    }, error => console.error(error));
  }
  executetj3() {
    const data = {
      btoken: this.selectedInst.value,
      expiry: this.selectedExpiry,
      ctf: this.tj3Form.value.ctf,
      pd_h: this.tj3Form.value.pd_h,
      pd_l: this.tj3Form.value.pd_l,
      pd_c: this.tj3Form.value.pd_c,
      pd_bh: this.tj3Form.value.pd_bh,
      pd_bl: this.tj3Form.value.pd_bl,
      pw_h: this.tj3Form.value.pw_h,
      pw_l: this.tj3Form.value.pw_l,
      pw_c: this.tj3Form.value.pw_c,
      ps_h: this.tj3Form.value.ps_h,
      //ps_l: this.tj3Form.value.ps_l,
      uid: this.tj3Form.value.uid,
      tp: this.tj3Form.value.tp,
      sl: this.tj3Form.value.sl,
      qty: this.tj3Form.value.qty,
    }

    this.http.post<ActiveAlgo>(this._baseUrl + 'api/tj3', data).subscribe(result => {
      this.activeAlgos.push(result);
    }, error => console.error(error));
  }
  executetj4() {
    const data = {
      btoken: this.selectedInst.value,
      expiry: this.selectedExpiry,
      ctf: this.tj4Form.value.ctf,
      pd_h: this.tj4Form.value.pd_h,
      pd_l: this.tj4Form.value.pd_l,
      pd_c: this.tj4Form.value.pd_c,
      pd_bh: this.tj4Form.value.pd_bh,
      pd_bl: this.tj4Form.value.pd_bl,
      pw_h: this.tj4Form.value.pw_h,
      pw_l: this.tj4Form.value.pw_l,
      pw_c: this.tj4Form.value.pw_c,
      ps_h: this.tj4Form.value.ps_h,
      //ps_l: this.tj4Form.value.ps_l,
      uid: this.tj4Form.value.uid,
      tp: this.tj4Form.value.tp,
      sl: this.tj4Form.value.sl,
      qty: this.tj4Form.value.qty,
    }

    this.http.post<ActiveAlgo>(this._baseUrl + 'api/tj4', data).subscribe(result => {
      this.activeAlgos.push(result);
    }, error => console.error(error));
  }
  executetj5() {
    const data = {
      btoken: this.selectedInst.value,
      expiry: this.selectedExpiry,
      ctf: this.tj5Form.value.ctf,
      uid: this.tj5Form.value.uid,
      tp: this.tj5Form.value.tp,
      sl: this.tj5Form.value.sl,
      qty: this.tj5Form.value.qty,
    }

    this.http.post<ActiveAlgo>(this._baseUrl + 'api/tj5', data).subscribe(result => {
      this.activeAlgos.push(result);
    }, error => console.error(error));
  }
  executebc() {
    const data = {
      btoken: this.selectedInst.value,
      expiry: this.selectedExpiry,
      ctf: this.bcForm.value.ctf,
      uid: this.bcForm.value.uid,
      tp: this.bcForm.value.tp,
      qty: this.bcForm.value.qty,
    }

    this.http.post<ActiveAlgo>(this._baseUrl + 'api/bc', data).subscribe(result => {
      this.activeAlgos.push(result);
    }, error => console.error(error));
  }
  executebc2() {
    const data = {
      btoken: this.selectedInst.value,
      expiry: this.selectedExpiry,
      ctf: this.bcForm.value.ctf,
      uid: this.bcForm.value.uid,
      tp: this.bcForm.value.tp,
      qty: this.bcForm.value.qty,
    }

    this.http.post<ActiveAlgo>(this._baseUrl + 'api/bc2', data).subscribe(result => {
      this.activeAlgos.push(result);
    }, error => console.error(error));
  }
  executePAScalping() {
    const data = {
      btoken: this.selectedInst.value,
      uid: this.paScalpingForm.value.uid,
      ctf: this.paScalpingForm.value.ctf,
      qty: this.paScalpingForm.value.qty,
    }

    this.http.post<ActiveAlgo>(this._baseUrl + 'api/candlewickscalping', data).subscribe(result => {
      this.activeAlgos.push(result);
    }, error => console.error(error));
  }
  executeEMAScalpingKB() {
    const data = {
      btoken: this.selectedInst.value,
      uid: this.paScalpingForm.value.uid,
      ctf: this.paScalpingForm.value.ctf,
      qty: this.paScalpingForm.value.qty,
    }

    this.http.post<ActiveAlgo>(this._baseUrl + 'api/emascalpingkb', data).subscribe(result => {
      this.activeAlgos.push(result);
    }, error => console.error(error));
  }
  executeSIM() {
    const data = {
      btoken: this.selectedInst.value,
      uid: this.simForm.value.uid,
      ctf: this.simForm.value.ctf,
      qty: this.simForm.value.qty,
      slpt: this.simForm.value.slpt,
      tp: this.simForm.value.tp,
      mns: this.simForm.value.mns,
    }

    this.http.post<ActiveAlgo>(this._baseUrl + 'api/stockmomentum', data).subscribe(result => {
      this.activeAlgos.push(result);
    }, error => console.error(error));
  }
  executeMA() {
    const data = {
      btoken: this.selectedInst.value,
      uid: this.maForm.value.uid,
      ctf: this.maForm.value.ctf,
      qty: this.maForm.value.qty,
      slpt: this.maForm.value.slpt,
      tp: this.maForm.value.tp,
      mns: this.maForm.value.mns,
    }

    this.http.post<ActiveAlgo>(this._baseUrl + 'api/marketalerts', data).subscribe(result => {
      this.activeAlgos.push(result);
    }, error => console.error(error));
  }
  executeIBScalping() {
    const data = {
      btoken: this.selectedInst.value,
      uid: this.ibForm.value.uid,
      ctf: this.ibForm.value.ctf,
      qty: this.ibForm.value.qty,
    }

    this.http.post<ActiveAlgo>(this._baseUrl + 'api/initialbreakout', data).subscribe(result => {
      this.activeAlgos.push(result);
    }, error => console.error(error));
  }

  executeActiveBuyStrangle() {
    const data = {
      btoken: this.selectedInst.value,
      expiry: this.selectedExpiry,
      iqty: this.deltaStrangleForm.value.iqty,
      stepqty: this.deltaStrangleForm.value.stepqty,
      maxqty: this.deltaStrangleForm.value.maxqty,
      idelta: this.deltaStrangleForm.value.idelta,
      maxdelta: this.deltaStrangleForm.value.maxdelta,
      mindelta: this.deltaStrangleForm.value.mindelta,
      sl: this.deltaStrangleForm.value.sl,
      tp: this.deltaStrangleForm.value.tp,
    }

    this.http.post<ActiveAlgo>(this._baseUrl + 'api/activebuystrangle', data).subscribe(result => {
      this.activeAlgos.push(result);
    }, error => console.error(error));
  }

  executeAlerts() {
    const data = {
      btoken: this.selectedInst.value,
      expiry: this.selectedExpiry,
      ctf: this.generateAlertForm.value.ctf,
      csp: this.generateAlertForm.value.csp,
    }

    this.http.post<ActiveAlgo>(this._baseUrl + 'api/alert', data).subscribe(result => {
      this.activeAlgos.push(result);
    }, error => console.error(error));
  }

  executeOptionBuyWithStraddle() {
    const data = {
      btoken: this.selectedInst.value,
      expiry: this.selectedExpiry,
      ctf: this.straddleMomentumOptionsForm.value.ctf,
      qty: this.straddleMomentumOptionsForm.value.qty,
      sl: this.straddleMomentumOptionsForm.value.sl,
      tr: this.straddleMomentumOptionsForm.value.tr,
      ss: this.straddleMomentumOptionsForm.value.ss
    }
    if (data.ss == "") { data.ss = false }
    this.http.post<ActiveAlgo>(this._baseUrl + 'api/momentumstraddle', data).subscribe(result => {
      this.activeAlgos.push(result);
    }, error => console.error(error));
  }

  executeExpiryStraddleTrade() {
    const data = {
      btoken: this.selectedInst.value,
      expiry: this.selectedExpiry,
      ctf: this.estForm.value.ctf,
      qty: this.estForm.value.qty,
      sl: this.estForm.value.sl,
      tp: this.estForm.value.tp,
      tr: this.estForm.value.tr,
      uid: this.estForm.value.uid,
      spi: this.estForm.value.spi,
      intraday: this.estForm.value.intd
    }
    if (data.intraday == "") { data.intraday = false }
    this.http.post<ActiveAlgo>(this._baseUrl + 'api/straddleexpirytrade', data).subscribe(result => {
      this.activeAlgos.push(result);
    }, error => console.error(error));
  }

  executePremiumCross() {
    const data = {
      btoken: this.selectedInst.value,
      expiry: this.selectedExpiry,
      qty: this.premiumCrossForm.value.qty,
      tp: this.premiumCrossForm.value.tp
    }
    this.http.post<ActiveAlgo>(this._baseUrl + 'api/premiumcross', data).subscribe(result => {
      this.activeAlgos.push(result);
    }, error => console.error(error));
  }

  executeOptionOptimizer() {
    const data = {
      btoken: this.selectedInst.value,
      expiry: this.selectedExpiry
    }
    this.http.post<ActiveAlgo>(this._baseUrl + 'api/optionoptimizer', data).subscribe(result => {
      this.activeAlgos.push(result);
    }, error => console.error(error));
  }


  executeExpiryStrangle() {
    const data = {
      btoken: this.selectedInst.value,
      expiry: this.selectedExpiry,
      iqty: this.expiryStrangleForm.value.iqty,
      mqty: this.expiryStrangleForm.value.mqty,
      sqty: this.expiryStrangleForm.value.sqty,
      tp: this.expiryStrangleForm.value.tp,
      sl: this.expiryStrangleForm.value.sl,
      idfbi: this.expiryStrangleForm.value.idfbi,
      mdfbi: this.expiryStrangleForm.value.mdfbi,
      mptt: this.expiryStrangleForm.value.mptt
    }
    this.http.post<ActiveAlgo>(this._baseUrl + 'api/expirystrangle', data).subscribe(result => {
      if (this.activeAlgos == undefined) {
        this.activeAlgos = [];
      }
      this.activeAlgos.push(result);
    }, error => console.error(error));
  }

  executeStrangleWithLevels() {
    const data = {
      btoken: this.selectedInst.value,
      expiry: this.selectedExpiry,
      idelta: this.levelsStrangleForm.value.idelta,
      mindelta: this.levelsStrangleForm.value.mindelta,
      maxdelta: this.levelsStrangleForm.value.maxdelta,
      iqty: this.levelsStrangleForm.value.iqty,
      sqty: this.levelsStrangleForm.value.sqty,
      mqty: this.levelsStrangleForm.value.mqty,
      tp: this.levelsStrangleForm.value.tp,
      sl: this.levelsStrangleForm.value.sl,
      l1: this.levelsStrangleForm.value.l1,
      l2: this.levelsStrangleForm.value.l2,
      u1: this.levelsStrangleForm.value.u1,
      u2: this.levelsStrangleForm.value.u2,
      intraday: this.levelsStrangleForm.value.intd,
      ctf: this.levelsStrangleForm.value.ctf
    }
    if (data.intraday == "") { data.intraday = false }
    this.http.post<ActiveAlgo>(this._baseUrl + 'api/stranglewithlevels', data).subscribe(result => {
      if (this.activeAlgos == undefined) {
        this.activeAlgos = [];
      }
      this.activeAlgos.push(result);
    }, error => console.error(error));
  }

  executeIVSpread() {
    const data = {
      btoken: this.selectedInst.value,
      expiry1: this.selectedExpiry,
      expiry2: this.selectedExpiry2,
      qty: this.ivSpreadForm.value.qty,
      tp: this.ivSpreadForm.value.tp
    }
    this.http.post<ActiveAlgo>(this._baseUrl + 'api/ivtrade', data).subscribe(result => {
      this.activeAlgos.push(result);
    }, error => console.error(error));
  }
  executeCalendarSpread() {
    const data = {
      btoken: this.selectedInst.value,
      expiry1: this.selectedExpiry,
      expiry2: this.selectedExpiry2,
      openvar: this.calendarSpreadForm.value.ovar,
      closevar: this.calendarSpreadForm.value.cvar,
      stepqty: this.calendarSpreadForm.value.sqty,
      maxqty: this.calendarSpreadForm.value.maxqty,
      to: this.calendarSpreadForm.value.sl,
      sl: this.calendarSpreadForm.value.sl,
      tp: this.calendarSpreadForm.value.tp,
      straddle: this.calendarSpreadForm.value.straddle
    }
    this.http.post<ActiveAlgo>(this._baseUrl + 'api/calendarspread', data).subscribe(result => {
      this.activeAlgos.push(result);
    }, error => console.error(error));
  }
  executedirectiononhistoricalaverage() {
    const data = {
      btoken: this.selectedInst.value,
      expiry1: this.selectedExpiry,
      openvar: this.directiononhistoricalaverageForm.value.ovar,
      closevar: this.directiononhistoricalaverageForm.value.cvar,
      stepqty: this.directiononhistoricalaverageForm.value.sqty,
      maxqty: this.directiononhistoricalaverageForm.value.maxqty,
      to: this.directiononhistoricalaverageForm.value.sl,
      sl: this.directiononhistoricalaverageForm.value.sl,
      tp: this.directiononhistoricalaverageForm.value.tp
    }
    this.http.post<ActiveAlgo>(this._baseUrl + 'api/directionaloptionsell', data).subscribe(result => {
      this.activeAlgos.push(result);
    }, error => console.error(error));
  }
  executeStrangleChart() {
    const data = {
      btoken: this.selectedInst.value,
      expiry1: this.selectedExpiry,
      expiry2: this.selectedExpiry2,
      it1: this.strangleChartForm.value.it1,
      it2: this.strangleChartForm.value.it2,
      s1: this.strangleChartForm.value.s1,
      s2: this.strangleChartForm.value.s2,
      cmb: this.strangleChartForm.value.cmb,
      oic: this.strangleChartForm.value.oic,
      ivc: this.strangleChartForm.value.ivc
    }
    if (data.cmb == "") { data.cmb = false }
    if (data.oic == "") { data.oic = false }
    if (data.ivc == "") { data.ivc = false }

    this.http.post<ActiveAlgo>(this._baseUrl + 'api/chart', data).subscribe(result => {
      //this.activeAlgos.push(result);
      //this.cd
    }, error => console.error(error));
  }

  executeExpiryTrade() {
    const data = {
      btoken: this.selectedInst.value,
      expiry: this.selectedExpiry,
      iqty: this.expiryTradeOptionsForm.value.iqty,
      sqty: this.expiryTradeOptionsForm.value.sqty,
      mqty: this.expiryTradeOptionsForm.value.mqty,
      sl: this.expiryTradeOptionsForm.value.sl,
      mdfbi: this.expiryTradeOptionsForm.value.mdfbi,
      mptt: this.expiryTradeOptionsForm.value.mptt,
    }
    this.http.post<ActiveAlgo>(this._baseUrl + 'api/expirystrangle', data).subscribe(result => {
      if (this.activeAlgos == undefined) {
        this.activeAlgos = [];
      }
      this.activeAlgos.push(result);
    }, error => console.error(error));
  }



  executeSellOnRsiTrade() {
    const data = {
      btoken: this.selectedInst.value,
      expiry: this.selectedExpiry,
      qty: this.sellwithRSIOptionsForm.value.qty,
      ctf: this.sellwithRSIOptionsForm.value.ctf,
      ema: this.sellwithRSIOptionsForm.value.ema,
      rlle: this.sellwithRSIOptionsForm.value.rlle,
      rule: this.sellwithRSIOptionsForm.value.rule,
      rmx: this.sellwithRSIOptionsForm.value.rmx,
      rlx: this.sellwithRSIOptionsForm.value.rlx,
      mindfbi: this.sellwithRSIOptionsForm.value.mindfbi,
      dchl: this.sellwithRSIOptionsForm.value.dchl
    }
    this.http.post<ActiveAlgo>(this._baseUrl + 'api/rsistrangle', data).subscribe(result => {
      if (this.activeAlgos == undefined) {
        this.activeAlgos = [];
      }
      this.activeAlgos.push(result);
    }, error => console.error(error));
  }

  executeBuyOnRsiTrade() {
    const data = {
      btoken: this.selectedInst.value,
      expiry: this.selectedExpiry,
      qty: this.buywithRSIOptionsForm.value.qty,
      ctf: this.buywithRSIOptionsForm.value.ctf,
      ema: this.buywithRSIOptionsForm.value.ema,
      tp: this.buywithRSIOptionsForm.value.tp,
      sl: this.buywithRSIOptionsForm.value.sl,
      rulx: this.buywithRSIOptionsForm.value.rulx,
      rlle: this.buywithRSIOptionsForm.value.rlle,
      cell: this.buywithRSIOptionsForm.value.cell,
      peul: this.buywithRSIOptionsForm.value.peul,
      maxdfbi: this.buywithRSIOptionsForm.value.maxdfbi,
      edchl: this.buywithRSIOptionsForm.value.edchl,
      xdchl: this.buywithRSIOptionsForm.value.xdchl,
      eac: this.buywithRSIOptionsForm.value.eac,
      fut: this.buywithRSIOptionsForm.value.fut
    }
    if (data.eac == "") { data.eac = false }
    if (data.fut == "") { data.fut = false }
    this.http.post<ActiveAlgo>(this._baseUrl + 'api/rsitrade', data).subscribe(result => {
      if (this.activeAlgos == undefined) {
        this.activeAlgos = [];
      }
      this.activeAlgos.push(result);
    }, error => console.error(error));
  }

  startstopalgo(ain, aid, s) {
    var ctrl = "";

    //switch (aid) {
    //  case "1":
    //    ctrl = "deltastrangle";
    //    break;
    //  case "10":
    //    ctrl = "activebuystrangle";
    //    break;
    //  case "20":
    //    ctrl = "rsitrade";
    //    break;
    //  case "18":
    //    ctrl = "rsicross";
    //    break;
    //  case "19":
    //    ctrl = "rsistrangle";
    //    break;
    //  case "17":
    //    ctrl = "momentumvolume";
    //    break;
    //  case "15":
    //    ctrl = "emacross";
    //    break;
    //  case "14":
    //    ctrl = "expirystrangle";
    //    break;
    //  case "21":
    //    ctrl = "straddle";
    //    break;
    //  case "22":
    //    ctrl = "premiumcross";
    //    break;
    //  case "23":
    //    ctrl = "momentumstraddle";
    //    break;
    //  case "24":
    //    ctrl = "referencestraddle";
    //    break;
    //  case "25":
    //    ctrl = "ivtrade";
    //    break;
    //  case "26":
    //    ctrl = "chart";
    //    break;
    //  case "28":
    //    ctrl = "calendarspread";
    //    break;
    //  case "29":
    //    ctrl = "stranglewithlevels";
    //    break;
    //  case "30":
    //    ctrl = "alert";
    //    break;
    //  case "31":
    //    ctrl = "directionaloptionsell";
    //    break;
    //  case "32":
    //    ctrl = "priceaction";
    //    break;
    //  case "33":
    //    ctrl = "straddleindexrange";
    //    break;
    //  case "35":
    //    ctrl = "candlewickscalping";
    //    break;
    //  case "36":
    //    ctrl = "initialbreakout";
    //    break;
    //  case "37":
    //    ctrl = "stockmomentum";
    //    break;
    //  case "38":
    //    ctrl = "straddle";
    //    break;
    //  case "39":
    //    ctrl = "emascalpingkb";
    //    break;
    //  case "43":
    //    ctrl = "marketalerts";
    //    break;
    //  case "44":
    //    ctrl = "tj3";
    //    break;
    //  case "45":
    //    ctrl = "tj4";
    //    break;
    //  case "46":
    //    ctrl = "sar";
    //    break;
    //  case "47":
    //    ctrl = "optionsellonht";
    //    break;
    //  case "48":
    //    ctrl = "multitimeframesellonht";
    //    break;
    //  default:
    //    ctrl = "home";
    //}

    if (aid == "1") {
      ctrl = "deltastrangle";
    }
    else if (aid == "10") {
      ctrl = "activebuystrangle";
    }
    else if (aid == "20") {
      ctrl = "rsitrade";
    }
    else if (aid == "18") {
      ctrl = "rsicross";
    }
    else if (aid == "19") {
      ctrl = "rsistrangle";
    }
    else if (aid == "17") {
      ctrl = "momentumvolume";
    }
    else if (aid == "15") {
      ctrl = "emacross";
    }
    else if (aid == "14") {
      ctrl = "expirystrangle";
    }
    else if (aid == "21") {
      ctrl = "straddle";
    }
    else if (aid == "22") {
      ctrl = "premiumcross";
    }
    else if (aid == "23") {
      ctrl = "momentumstraddle";
    }
    else if (aid == "24") {
      ctrl = "referencestraddle";
    }
    else if (aid == "25") {
      ctrl = "ivtrade";
    }
    else if (aid == "26") {
      ctrl = "chart";
    }
    else if (aid == "28") {
      ctrl = "calendarspread";
    }
    else if (aid == "29") {
      ctrl = "stranglewithlevels";
    }
    else if (aid == "30") {
      ctrl = "alert";
    }
    else if (aid == "31") {
      ctrl = "directionaloptionsell";
    }
    else if (aid == "32") {
      ctrl = "priceaction";
    }
    else if (aid == "33") {
      ctrl = "straddleindexrange";
    }
    if (aid == "35") {
      ctrl = "candlewickscalping";
    }
    if (aid == "36") {
      ctrl = "initialbreakout";
    }
    if (aid == "37") {
      ctrl = "stockmomentum";
    }
    if (aid == "38") {
      ctrl = "straddle";
    }
    if (aid == "39") {
      ctrl = "emascalpingkb";
    }
    if (aid == "43") {
      ctrl = "marketalerts";
    }
    if (aid == "44") {
      ctrl = "tj3";
    }
    if (aid == "45") {
      ctrl = "tj4";
    }
    if (aid == "46") {
      ctrl = "sar";
    }
    if (aid == "47") {
      ctrl = "optionsellonht";
    }
    if (aid == "48") {
      ctrl = "multitimeframesellonht";
    }
    if (aid == "50") {
      ctrl = "tj5";
    }
    if (aid == "51") {
      ctrl = "bc";
    }
    if (aid == "52") {
      ctrl = "bc2";
    }
    else if (aid == "53") {
      ctrl = "straddleexpirytrade";
    }
    const start = s;
    this.http.put<boolean>(this._baseUrl + 'api/' + ctrl + '/' + ain, start).subscribe(result => {
      var message: string;
      if (start == 1) { message = "The algo has started. Wait for the light to turn green!" } else { message = "The algo has stopped. Wait for the light to turn red!" };
      this.openSnackBar(message, "");
    }, error => console.error(error));
  }

  startdsservice() {
    const data = { start: true };
    this.http.post<any>(this._baseUrl + 'api/ws', data).subscribe(result => {
      console.log(result);
    }, error => console.error(error));
  }

  generatevolcdls() {
    const start = 1;
    this.http.post(this._baseUrl + 'api/candles', start).subscribe(result => {
      var message: string;
      message = "Candles Started";
      this.openSnackBar(message, "");
    }, error => console.error(error));
  }
}
