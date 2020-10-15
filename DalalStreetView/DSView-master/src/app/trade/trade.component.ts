import { Component, OnInit, Inject, ViewChild, ElementRef, AfterViewInit } from '@angular/core';
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
import { Constants, Loglevel } from '../data/constants';

import { ErrorDialog } from './error.component';
//import { TimeToStringPipe } from './TimeToString.Pipe';

import { grpc } from "@improbable-eng/grpc-web";
import { Logger, LoggerClient } from "../generated/log_pb_service";
import { LogMessage, Status } from "../generated/log_pb";
import { OrderAlerter, OrderAlerterClient } from "../generated/log_pb_service";
import { OrderMessage, PublishStatus } from "../generated/log_pb";
import { error } from '@angular/compiler/src/util';

import { BehaviorSubject, concat, Observable } from 'rxjs';
import { MatDialog, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { Timestamp } from 'google-protobuf/google/protobuf/timestamp_pb';

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
  momentumTradeOptionsForm: FormGroup;

  constructor(private http: HttpClient, private formBuilder: FormBuilder,
    @Inject('BASE_URL') baseUrl: string, private ts: TradeService, private dialog: MatDialog) {
    this._baseUrl = baseUrl;
    
    //get Instruments
    http.get<Instrument[]>(baseUrl + 'api/momentumvolume').subscribe(result => {
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
  }

  private subj = new BehaviorSubject(this.log);
  private osubj = new BehaviorSubject(this.porder);
  returnAsObservable() {
    return this.subj.asObservable();
  }

  orderAsObservable() {
    return this.osubj.asObservable();
  }

  filterLogsOfType(type) {
    return this.logs.filter(x => x.algoInstance == type.aIns);
  }
  
  ngOnInit(): void {
    let subject = this.subj;
    let osubject = this.osubj;

    window.setInterval(this.checkhealth, 60001, this.algosHealth);

    this.onSelectInstrument(this.selectedInstrument.instrumentToken);

    this.momentumTradeOptionsForm = this.formBuilder.group({
      ctf: ['', Validators.required],
      quantity: ['', Validators.required]
    });
    this.returnAsObservable().subscribe(
      data => {
        if (data !== undefined) {
          if (data.logLevel === Loglevel.Health.toString()) {
            var selectedAlgoIndex = this.algosHealth.findIndex(x => x.aIns === data.algoInstance);

            var rs =  data.message == "1" ? true : false;
            
            if (selectedAlgoIndex != -1) {
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
          
          this.logs.push(data);
        }
      }
    );
    this.orderAsObservable().subscribe(
      data => {
        if (data !== undefined) {
           var selectedAlgoIndex = this.activeAlgos.findIndex(x => x.aIns === data.algoinstance);

          var selectedOrderIndex = this.activeAlgos[selectedAlgoIndex].orders.findIndex(function (e) { e.orderid === data.orderid });

          if (selectedOrderIndex != -1) {
            this.activeAlgos[selectedAlgoIndex].orders[selectedOrderIndex] = data;
          }
          else {
            this.activeAlgos[selectedAlgoIndex].orders = this.activeAlgos[selectedAlgoIndex].orders.concat(data);
            
          }

        }
      }
    );

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

  checkhealth(alh) {
    alh.forEach((x) => {
      if (x.ad < Date.now() - 60000) {
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
  
  getLog() {
    this.http.get(this._baseUrl + 'api/momentumvolume/dummyorder').subscribe(result => {
        this.result = result;
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
  getExpiry(token) {
    this.http.get<Expiry[]>(this._baseUrl + 'api/momentumvolume/' + token.value).subscribe(result => {
      this.expiry = result;
    }, error => console.error(error));
  }
  //get call put options
  getOption(token, expval) {
    this.http.get<Options[]>(this._baseUrl + 'api/momentumvolume/' + token.value + '/' + expval.value).subscribe(result => {
      this.options = result;
      this.call = this.options.filter(function (item) {
        return item.type.toLowerCase() === 'ce';
      });
      this.put = this.options.filter(function (item) {
        return item.type.toLowerCase() === 'pe';
      });
    }, error => console.error(error));
  }

  onSelectInstrument(instid) {
    this.selectedInst = instid;
    this.getExpiry(instid);
  }

  onSelectExpiry(expval) {
    this.selectedExpiry = expval.value;
    this.getOption(this.selectedInst, expval);
  }

  onSelectCall(e){
    this.selectedCallValue = e.value;
  }

  onSelectPut(e){
    this.selectedPutValue = e.value;
  }

  //algo panel section
  panelOpenState = false;

  executeAlgo() {
    //alert('trade button clicked');
    const data = {
      token: this.selectedInst.value,
      expiry: this.selectedExpiry,
      ctf: this.momentumTradeOptionsForm.value.ctf,
      quantity: this.momentumTradeOptionsForm.value.quantity
    }
    this.http.post<ActiveAlgo>(this._baseUrl + 'api/momentumvolume' ,data).subscribe(result => {
      //this.result = result;
      this.activeAlgos.push(result);
    }, error => console.error(error));
  }

  startdsservice() {

    //var sc = require('windows-service-controller');
    //const ServiceController = require("nodejs-windows-service-controller");

    //var sc = require('windows-service-controller');

    //sc.getDisplayName('SomeService')
    //  .catch(function (error) {
    //    console.log(error.message);
    //  })
    //  .done(function (displayName) {
    //    console.log('Display name: ' + displayName);
    //  });
    //sc.start('MyService');
  }

}
