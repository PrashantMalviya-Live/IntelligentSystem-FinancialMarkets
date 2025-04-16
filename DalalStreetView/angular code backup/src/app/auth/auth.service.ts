import { Inject, Injectable } from '@angular/core';
import { HttpClient, HttpResponse } from '@angular/common/http';
import { Router, ActivatedRoute  } from '@angular/router';
import { Observable } from 'rxjs';

@Injectable({
  providedIn: 'root',
})
export class AuthService {
  isLoggedIn: boolean = false;
  public _baseUrl: string;


  constructor(private http: HttpClient, private router: Router, @Inject('BASE_URL') baseUrl: string) {
    this._baseUrl = baseUrl;
  }

  login(request_token, action, status, baseUrl): any {
    const data = {
      request_token: request_token,
      action: action,
      status: status
    }

    this.http.post<any>(this._baseUrl + 'api/login', data).subscribe(result => {
      if (result.message == "200 OK") {
        this.isLoggedIn = true;
        return result.userName;
        //this.router.navigate([baseUrl]);
        //window.location.href = baseUrl;
      }
      else if (result.message == "401 Unauthorized") {
       // this.router.navigate([result.url]);
        window.location.href = result.url;
      }
    }, error => console.error(error));
  }

  logout(): void {
    this.isLoggedIn = false;
  }
}
