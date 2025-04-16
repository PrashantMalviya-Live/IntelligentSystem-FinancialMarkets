import { BrowserModule } from '@angular/platform-browser';
import { NgModule, EventEmitter } from '@angular/core';
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
import { LoginComponent } from './login/login.component';
import { MatExpansionModule } from '@angular/material/expansion';
@NgModule({
  declarations: [
    AppComponent,
    TradeComponent,
    LoginComponent
  ],
  imports: [
    BrowserModule,
    FormsModule,
    ReactiveFormsModule,
    AppRoutingModule,
    BrowserAnimationsModule,
    AngularMaterialModule,
    HttpClientModule,
    RouterModule,
    MatExpansionModule
  ],
  providers: [{ provide: 'BASE_URL', useFactory: getBaseUrl }, TradeService],
  bootstrap: [AppComponent]
})
export class AppModule {

}
