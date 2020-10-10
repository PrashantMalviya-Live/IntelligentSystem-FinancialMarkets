import { Component, OnInit, Inject, ViewChild, ElementRef, AfterViewInit } from '@angular/core';
import { Instrument } from '../data/instrument';
import { Expiry } from '../data/expiry';
import { Options } from '../data/options';
import { Order } from '../data/order';
import { LogData } from '../data/logdata';
import { TradeService } from './trade.service';
import { HttpClient } from '@angular/common/http';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { FormsModule, ReactiveFormsModule } from '@angular/forms';

import { grpc } from "@improbable-eng/grpc-web";
import { Logger, LoggerClient } from "../generated/log_pb_service";
import { LogMessage, Status } from "../generated/log_pb";
import { error } from '@angular/compiler/src/util';

import { BehaviorSubject } from 'rxjs';


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
  activeOrders: any;
  call: Options[];
  put: Options[];
  log: LogData;
  logs: LogData[] = [];
  selectedInst: any;
  client: grpc.Client<Status, LogMessage> = grpc.client(Logger.Log, {host: 'https://localhost:5001' });
  selectedExpiry: any;
  selectedCallValue: any;
  selectedPutValue: any;
  displayedColumns: string[] = ['orderTime', 'tradingSymbol', 'price', 'quantity', 'transactionType', 'orderType', 'algorithm'];
  dataSource = this.orders;
  _baseUrl: any;
  result: any;
  ctf: any;
  quantity: any;
  momentumTradeOptionsForm: FormGroup;
  
  constructor(private http: HttpClient, private formBuilder: FormBuilder, @Inject('BASE_URL') baseUrl: string, private ts:TradeService) {
    this._baseUrl = baseUrl;

    //get Instruments
    http.get<Instrument[]>(baseUrl + 'api/momentumvolume').subscribe(result => {
      this.instrument = result;
    }, error => console.error(error));

    http.get<any>(baseUrl + 'api/position').subscribe(result => {

      if (this.orders == null) {
        this.activeOrders = result;
      }
      else {
        this.activeOrders.concat(result);
      }
    }, error => console.error(error));
  }

  private subj = new BehaviorSubject(this.log);
  returnAsObservable() {
    return this.subj.asObservable();
  }

  ngOnInit(): void {
    let subject = this.subj;

    this.onSelectInstrument(this.selectedInstrument.instrumentToken);

    this.momentumTradeOptionsForm = this.formBuilder.group({
      ctf: ['', Validators.required],
      quantity: ['', Validators.required]
    });
    this.returnAsObservable().subscribe(
      data => {
        if (data !== undefined) {
          console.log(data);
          //this.logs = this.logs.filter(function (element) {
          //  return element !== undefined;
          //});
          this.logs.push(data);
        }
      }
    );


    //var client = grpc.client(Logger.Log, {
    //  host: 'https://localhost:5001',
    //});
    

    var status = new Status();
    status.setStatus(true); 

    this.client.start();
     this.client.send(status);
    //var message = new LogMessage();

    //var client = grpc.client(
    //  Logger.Log, {
    //  host: 'https://localhost:5001'
    //});

    
    //var testLog;
   
    
    this.client.onMessage(function (message){
      var results = message.toObject() as LogMessage.AsObject;
      //testLog = result;
      subject.next(results);
     // this.log = result;
    });
    
   

    //grpc.unary(Greeter.SayHello, {
    //  request: getHelloRequest,
    //  host: 'https://localhost:5001',
    //  onEnd: res => {
    //    const { status, message } = res;
    //    if (status === grpc.Code.OK && message) {
    //      var result = message.toObject() as HelloReply.AsObject;
    //    }
    //  }
    //});

  }

  getLog() {
    const data = {
      algoInstance: "1"
    }
    this.http.post(this._baseUrl + 'api/momentumvolume', data).subscribe(result => {
        this.result = result;
      }, error => console.error(error));
  }
  stopExchangeUpdates() {
    this.ts.stopExchangeUpdates();
  }


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
    this.http.post(this._baseUrl + 'api/momentumvolume' ,data).subscribe(result => {
      this.result = result;
    }, error => console.error(error));
  }
}
