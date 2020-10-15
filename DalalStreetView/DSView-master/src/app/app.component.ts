import { Component, OnInit, Inject, ViewChild, ElementRef, AfterViewInit } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import {AuthService} from './auth/auth.service'
import {  ActivatedRoute } from '@angular/router';
import { filter } from 'rxjs/operators';

@Component({
  selector: 'app-root',
  templateUrl: './app.component.html',
  styleUrls: ['./app.component.scss']
})
export class AppComponent implements OnInit {
  title = 'DSView';
  _baseUrl: any;
  request_token: any;
  action: string;
  status: string;

  constructor(private http: HttpClient, @Inject('BASE_URL') baseUrl: string, private as: AuthService, private route: ActivatedRoute) {
    this._baseUrl = baseUrl;

    this.route.queryParams.pipe(
      filter(queryParams => Object.keys(queryParams).length > 0 === window.location.href.includes('?')))
      .subscribe(params => {
      this.request_token = params['request_token'];
      this.action = params['action'];
      this.status = params['status'];

    if (!as.isLoggedIn) {
      as.login(this.request_token, this.action, this.status, baseUrl);
        }

      });
  }
  ngOnInit(): void {

  }
}
