import { Inject, OnInit, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { BehaviorSubject } from 'rxjs';

@Injectable({
  providedIn: 'root',
})
export class TradeService {
  _baseUrl: any;


  constructor(private http: HttpClient, @Inject('BASE_URL') baseUrl: string) {
    this._baseUrl = baseUrl;
  }

  evs: EventSource;

  private subj = new BehaviorSubject([]);

  returnAsObservable() {
    return this.subj.asObservable();
  }

  GetExchangeData() {
    let subject = this.subj;
    if (typeof(EventSource) !== 'undefined') {
      this.evs = new EventSource(this._baseUrl + 'api/position/1');

      this.evs.onopen = function (e) {
        console.log("Opening connection.Ready State is " + this.readyState);
      }

      this.evs.onmessage = function (e) {
        console.log("Message Received.Ready State is " + this.readyState);
        subject.next(e.data);
      }

      this.evs.addEventListener("timestamp", function (e) {
        console.log("Timestamp event Received.Ready State is " + this.readyState);
        subject.next(e["data"]);
      })

      this.evs.onerror = function (e) {
        console.log(e);
        if (this.readyState == 0) {
          console.log("Reconnecting…");
        }
      }
    }
  }
  stopExchangeUpdates() {
    this.evs.close();
  }
}
