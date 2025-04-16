import { Injectable, Inject } from '@angular/core';
import { Observable, throwError, map } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { Instrument } from './data/instrument';
import { Expiry } from './data/expiry';
//import { Call } from './data/call';
//import { Put } from './data/put';
import * as moment from 'moment';
import { HttpClient } from '@angular/common/http';

@Injectable()

export class DataService {
  _baseURL = "";
  constructor(private http: HttpClient, @Inject('BASE_URL') baseUrl: string) {
    this._baseURL = baseUrl;
  }
  

  getInstruments(): Observable<any> {
    return this.http.get<Instrument[]>(this._baseURL + 'home');
    //.pipe(
      //catchError(this.error)
    //)
    //return [
    // new Instrument(1, 'NIFTY50' ),
    // new Instrument(2, 'BANKNIFTY50' ),
    // new Instrument(3, 'TATASTEEL' )
    //];
  }
  
  getExpiry() {
   return [
     new Expiry(1, 1, '9/3/2020 12:00:00 AM'),
     new Expiry(2, 1, '9/4/2020 12:00:00 AM'),
     new Expiry(3, 1, '9/5/2020 12:00:00 AM'),
     new Expiry(1, 2, '9/6/2020 12:00:00 AM'),
     new Expiry(2, 2, '9/7/2020 12:00:00 AM'),
     new Expiry(3, 2, '9/8/2020 12:00:00 AM'),
     new Expiry(1, 3, '9/9/2020 12:00:00 AM'),
     new Expiry(2, 3, '9/10/2020 12:00:00 AM'),
     new Expiry(3, 3, '9/11/2020 12:00:00 AM')
    ];
  }

  //getCall(){
  //  return [
  //      new Call(10000, 1, 1, 'NIFY20910000CE' ),
  //      new Call(10100, 1, 1, 'NIFY20910100CE' ),
  //      new Call(10200, 1, 2, 'NIFY20910200CE' ),
  //      new Call(10300, 1, 2, 'NIFY20910300CE' ),
  //      new Call(10400, 1, 3, 'NIFY20910400CE' ),
  //      new Call(10500, 2, 3, 'NIFY20910500CE' )
  //     ];
  //}

  //getPut(){
  //  return [
  //      new Put(10000, 1, 2, 'NIFY20910000PE' ),
  //      new Put(10100, 1, 1, 'NIFY20910100PE' ),
  //      new Put(10200, 1, 2, 'NIFY20910200PE' ),
  //      new Put(10300, 2, 3, 'NIFY20910300PE' ),
  //      new Put(10400, 1, 3, 'NIFY20910400PE' ),
  //      new Put(10500, 1, 1, 'NIFY20910500PE' )
  //     ];
  //}

}
