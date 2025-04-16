import { Component, OnInit, Inject, ViewChild, Injectable, ElementRef, AfterViewInit, PipeTransform, Pipe } from '@angular/core';
import { Timestamp } from 'google-protobuf/google/protobuf/timestamp_pb';
import { AlgoHealth } from '../data/health';

//@Injectable({
//  providedIn: 'root',
//})

//@Pipe({
//  name: 'getalgohealthstatus',
//  pure: true
//})
//export class GetAlgoHealthStatusPipe implements PipeTransform {
//  _algoheath
//  constructor(private algoshealth: AlgoHealth[]) {

//  }

//  transform(value: any, ): boolean {
//    return this.timeToString(value);
//  }
//  timeToString(data: any): any {
//    return new Date(data.seconds * 1000).toLocaleString();
//  }

//} 
