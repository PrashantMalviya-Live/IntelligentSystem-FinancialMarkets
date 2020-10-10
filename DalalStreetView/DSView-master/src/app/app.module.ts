import { BrowserModule } from '@angular/platform-browser';
import { NgModule } from '@angular/core';
import { FormsModule, ReactiveFormsModule } from '@angular/forms';
import { HttpClientModule } from '@angular/common/http';
import { AppRoutingModule } from './app-routing.module';
import { AppComponent } from './app.component';
import { BrowserAnimationsModule } from '@angular/platform-browser/animations';
import { AngularMaterialModule } from './material-module';
import { getBaseUrl } from './data/baseUrl';
//import { DataService } from './data.service';
import { TradeComponent } from './trade/trade.component';
import { TradeService } from './trade/trade.service';

@NgModule({
  declarations: [
    AppComponent,
    TradeComponent
  ],
  imports: [
    BrowserModule,
    FormsModule,
    ReactiveFormsModule,
    AppRoutingModule,
    BrowserAnimationsModule,
    AngularMaterialModule,
    HttpClientModule
  ],
  providers: [{ provide: 'BASE_URL', useFactory: getBaseUrl }, TradeService],
  bootstrap: [AppComponent]
})
export class AppModule { }
