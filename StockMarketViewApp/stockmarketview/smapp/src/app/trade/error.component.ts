import { Component, Inject } from '@angular/core';
import { MatDialog, MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { DialogData } from './trade.component';
import { Howl, Howler } from "../../../node_modules/howler/dist/howler.js";


@Component({
  selector: 'error-dialog',
  templateUrl: 'error.component.html',
})
export class ErrorDialog {
  _sound: any;
  constructor(@Inject(MAT_DIALOG_DATA) public data: DialogData, public dialogRef: MatDialogRef<ErrorDialog>) {
    var context = new AudioContext();
    this._sound = new Howl({
      src: ['../assets/alert.mp3'],
      autoplay: true,
      loop: true,
      volume: 0.5,
      onend: function () {
       // console.log('Finished!');
      }
    });
    context.resume();
    this._sound.play();
  }
  close() {
    this._sound.stop();
    this.dialogRef.close(true);
  }
}
