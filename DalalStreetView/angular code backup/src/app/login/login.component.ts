import { Component, OnInit, Input, Output, EventEmitter, Inject } from '@angular/core';
import { FormGroup, FormControl, FormBuilder, Validators } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { Router, ActivatedRoute } from '@angular/router';
@Component({
  selector: 'app-login',
  templateUrl: './login.component.html',
  styleUrls: ['./login.component.scss']
})
export class LoginComponent implements OnInit {

  loginForm: FormGroup;
  showotp: boolean = false;
  _baseUrl: any;

  constructor(private http: HttpClient, private formBuilder: FormBuilder, private router: Router,  @Inject('BASE_URL') baseUrl: string) {
    this._baseUrl = baseUrl
  }
  ngOnInit(): void {
    this.loginForm = this.formBuilder.group({
      username: ['', Validators.required],
      password: ['', Validators.required],
      accessToken: ['', Validators.required],//,
      otp: ['' ]
    });
  }



  submit() {
    if (this.loginForm.valid) {
        const data = {
          userid: this.loginForm.value.username,
          pwd: this.loginForm.value.password,
          accessToken: this.loginForm.value.accessToken,
          otp: this.loginForm.value.otp
        }

      this.http.post<any>(this._baseUrl + 'api/kotaklogin', data).subscribe(result => {

        if (result.message == "200 OK") {

            this.router.navigateByUrl('/trade');
            //window.location.href = baseUrl;
          //this.submitEM.emit(this.loginForm.value);
          //return result.userName;
        }
        else if
          (result.message == "418 TP") {

          this.showotp = true;
          //window.location.href = baseUrl;
          //this.submitEM.emit(this.loginForm.value);
          //return result.userName;
        }

        
        }, error => console.error(error));
      
    }
  }
  @Input() error: string | null;

  @Output() submitEM = new EventEmitter();

}
