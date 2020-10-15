import { BrowserModule } from '@angular/platform-browser';
import { NgModule } from '@angular/core';
import { FormsModule, ReactiveFormsModule } from '@angular/forms';
import { HttpClientModule } from '@angular/common/http';
import { AppRoutingModule } from './app-routing.module';
import { AppComponent } from './app.component';
import { BrowserAnimationsModule } from '@angular/platform-browser/animations';
import { AngularMaterialModule } from './material-module';
import { getBaseUrl } from './data/baseUrl';
//import { TimeToStringPipe } from './trade/TimeToString.Pipe';
import { TradeComponent } from './trade/trade.component';
import { TradeService } from './trade/trade.service';
import { ErrorDialog } from './trade/error.component';
import { AuthService } from './auth/auth.service';
import { RouterModule } from '@angular/router';

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
    HttpClientModule,
    RouterModule
  ],
  providers: [{ provide: 'BASE_URL', useFactory: getBaseUrl }, TradeService],
  bootstrap: [AppComponent]
})
export class AppModule {

}
