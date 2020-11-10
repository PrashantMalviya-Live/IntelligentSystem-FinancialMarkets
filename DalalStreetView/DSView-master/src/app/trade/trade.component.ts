import { Component, OnInit, Inject, ViewChild, ElementRef, AfterViewInit, EventEmitter } from '@angular/core';
import { Instrument } from '../data/instrument';
import { Expiry } from '../data/expiry';
import { Options } from '../data/options';
import { Order, ActiveAlgo } from '../data/order';
import { LogData } from '../data/logdata';
import { AlgoHealth } from '../data/health';
import { TradeService } from './trade.service';
import { HttpClient } from '@angular/common/http';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { FormsModule, ReactiveFormsModule } from '@angular/forms';
import { Constants, Loglevel, AlgoControllers, RunningAlgos } from '../data/constants';
import { ErrorDialog } from './error.component';
//import { TimeToStringPipe } from './TimeToString.Pipe';
import { grpc } from "@improbable-eng/grpc-web";
import { Logger, LoggerClient } from "../generated/log_pb_service";
import { LogMessage, Status } from "../generated/log_pb";
import { OrderAlerter, OrderAlerterClient } from "../generated/log_pb_service";
import { OrderMessage, PublishStatus } from "../generated/log_pb";
import { error } from '@angular/compiler/src/util';

import { BehaviorSubject, concat, Observable} from 'rxjs';
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
export class TradeComponent implements OnInit{
  instrument: Instrument[];
  selectedInstrument: Instrument = new Instrument(0, '--Select--');
  expiry: Expiry[];
  options: Options[];
  orders: Order[];
  activeAlgos: ActiveAlgo[];
  activeAlgo: ActiveAlgo;
  call: Options[];
  put: Options[];
  log: LogData;
  logs: LogData[] = [];
  algosHealth: AlgoHealth[] = [];
  porder: Order;
  selectedInst: any;
  client: grpc.Client<Status, LogMessage> = grpc.client(Logger.Log, { host: 'https://localhost:5001' });
  oclient: grpc.Client<PublishStatus, OrderMessage> = grpc.client(OrderAlerter.Publish, { host: 'https://localhost:5001' });
  selectedExpiry: any;
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
  momentumTradeOptionsForm: FormGroup;
  rsiCrossOptionsForm: FormGroup;
  expiryTradeOptionsForm: FormGroup;
  sellwithRSIOptionsForm: FormGroup;
  buywithRSIOptionsForm: FormGroup;
  _sound: any;
  _ctrl: string;
  Algos: any;
  _algoCtrls: string;
  _ras: RunningAlgos[]=[];
  _ra: RunningAlgos;
  constructor(private http: HttpClient, private formBuilder: FormBuilder,
    @Inject('BASE_URL') baseUrl: string, private ts: TradeService, private dialog: MatDialog, private _snackBar: MatSnackBar) {
    this._baseUrl = baseUrl;

    //get Instruments
    http.get<Instrument[]>(baseUrl + 'api/momentumvolume').subscribe(result => {
      this.instrument = result;
    }, error => console.error(error));

    //get Instruments
    http.get<Instrument[]>(baseUrl + 'api/rsicross').subscribe(result => {
      this.instrument = result;
    }, error => console.error(error));
    //get Instruments
    http.get<Instrument[]>(baseUrl + 'api/expirystrangle').subscribe(result => {
      this.instrument = result;
    }, error => console.error(error));

    //get Instruments
    http.get<Instrument[]>(baseUrl + 'api/rsistrangle').subscribe(result => {
      this.instrument = result;
    }, error => console.error(error));

    http.get<Instrument[]>(baseUrl + 'api/rsitrade').subscribe(result => {
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

  returnAsObservable() {
    return this.subj.asObservable();
  }

  orderAsObservable() {
    return this.osubj.asObservable();
  }
  algoAsObservable() {
    return this.alsubj.asObservable();
  }

  filterLogsOfType(type) {
    return this.logs.filter(x => x.algoInstance == type.ains);
  }
  filterbyname(ctrl)
  {
    //return this._ras.filter(x => x._algoCrtl == ctrl);
    //this._ras.findIndex(x => x._algoCtrl == '171')
    
    if (this._ras !== undefined && this._ras.findIndex(x => x!== undefined && x._algoCtrl == ctrl) >=0)
    {
      return true;
    }
    else return false;
  }
  
  ngOnInit(): void {
    let subject = this.subj;
    let osubject = this.osubj;
    let asubject = this.alsubj;

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
      ,quantity: ['', Validators.required]
      ,mlpt: ['', Validators.required]
      //,ps: ['', Validators.required]
    });
    this.rsiCrossOptionsForm = this.formBuilder.group({
      ctf: ['', Validators.required]
      , qty: ['', Validators.required]
      , rmx: ['', Validators.required]
      , maxdfbi: ['', Validators.required]
      , mindfbi: ['', Validators.required]
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
      mindfbi: ['', Validators.required],
      maxdfbi: ['', Validators.required],
      ema: ['', Validators.required]
    });
    this.buywithRSIOptionsForm = this.formBuilder.group({
      qty: ['', Validators.required],
      ctf: ['', Validators.required],
      mindfbi: ['', Validators.required],
      maxdfbi: ['', Validators.required],
      ema: ['', Validators.required],
      tp: ['', Validators.required],
      rulx: ['', Validators.required],
      rlle: ['', Validators.required]
      //,ps: ['', Validators.required]
    });
    this.returnAsObservable().subscribe(
      data => {
        if (data !== undefined) {
          if (data.logLevel === Loglevel.Health.toString()) {
            var selectedAlgoIndex = this.algosHealth.findIndex(x => x.aIns === data.algoInstance);

            var rs =  data.message == "1" ? true : false;
            
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

    //Code for GRPS Logger Service
    var status = new Status();
    status.setStatus(true); 

    this.client.start();
    this.client.send(status);
    this.client.onMessage(function (message){
      var results = message.toObject() as LogMessage.AsObject;
      subject.next(results);
    });
  }

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
        this.openSnackBar(result.toString(),"Tokens Loaded");
     }, error => console.error(error));
  }

  openDialog(ain, algo, message, source, time) {
    this.dialog.open(ErrorDialog, { data: {ain:ain , algo:algo, message:message, source:source, time: new Date(time.seconds*1000).toLocaleString()}});
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

  onSelectCall(e){
    this.selectedCallValue = e.value;
  }
  
  onSelectPut(e){
    this.selectedPutValue = e.value;
  }
  onPSChange(e) {
    this.ps = e.checked;
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
    this.http.post<ActiveAlgo>(this._baseUrl + 'api/momentumvolume' ,data).subscribe(result => {
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
      maxdfbi: this.rsiCrossOptionsForm.value.maxdfbi,
    }
    this.http.post<ActiveAlgo>(this._baseUrl + 'api/rsicross', data).subscribe(result => {
      this.activeAlgos.push(result);
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
      maxdfbi: this.sellwithRSIOptionsForm.value.maxdfbi
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
      rulx: this.buywithRSIOptionsForm.value.rulx,
      rlle: this.buywithRSIOptionsForm.value.rlle,
      mindfbi: this.buywithRSIOptionsForm.value.mindfbi,
      maxdfbi: this.buywithRSIOptionsForm.value.maxdfbi
    }
    this.http.post<ActiveAlgo>(this._baseUrl + 'api/rsitrade', data).subscribe(result => {
      if (this.activeAlgos == undefined) {
        this.activeAlgos = [];
      }
      this.activeAlgos.push(result);
    }, error => console.error(error));
  }

  startstopalgo(ain, aid, s) {
    var ctrl = "";
    if (aid == "20") {
      ctrl = "rsitrade";
    }
    if (aid == "18") {
      ctrl = "rsicross";
    }
    if (aid == "19") {
      ctrl = "rsistrangle";
    }
    if (aid == "17") {
      ctrl = "momentumvolume";
    }

    const start = s ;
    this.http.put<boolean>(this._baseUrl + 'api/' + ctrl + '/' + ain, start).subscribe(result => {
      var message: string;
      if (start == 1) { message = "The algo has started. Wait for the light to turn green!" } else { message = "The algo has stopped. Wait for the light to turn red!" };
      this.openSnackBar(message,"");
    }, error => console.error(error));
  }

  startdsservice() {
    const data = { start: true};
    this.http.post<any>(this._baseUrl + 'api/ws', data).subscribe(result => {
      console.log(result);
    }, error => console.error(error));
  }

}
