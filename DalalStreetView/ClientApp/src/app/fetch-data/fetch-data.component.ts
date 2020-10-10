import { Component, Inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';

@Component({
  selector: 'app-fetch-data',
  templateUrl: './fetch-data.component.html'
})
export class FetchDataComponent {
  //public forecasts: WeatherForecast[];
  public strangles: StrangleDetails[];

  constructor(http: HttpClient, @Inject('BASE_URL') baseUrl: string) {
    http.get<StrangleDetails[]>(baseUrl + 'expirystrangle').subscribe(result => {
      this.strangles = result;
    }, error => console.error(error));
  }
}

interface WeatherForecast {
  date: string;
  temperatureC: number;
  temperatureF: number;
  summary: string;
}

interface StrangleDetails {
  peToken: number;
  ceToken: number;
  peSymbol: string;
  ceSymbol: string;
  expiry: any;
  pelowerThreshold: number;
  peUpperThreshold: number;
  celowerThreshold: number;
  ceUpperThreshold: number;
  ThresholdinPercent: boolean;
  stopLossPoints: number;
  strangleId: number;
}
