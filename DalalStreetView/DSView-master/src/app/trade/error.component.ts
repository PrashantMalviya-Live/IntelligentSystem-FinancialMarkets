import { Component, Inject } from '@angular/core';
import { MatDialog, MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { DialogData } from './trade.component';


@Component({
  selector: 'error-dialog',
  templateUrl: 'error.component.html',
})
export class ErrorDialog {
  constructor(@Inject(MAT_DIALOG_DATA) public data: DialogData, public dialogRef: MatDialogRef<ErrorDialog> ) { }
  close() {
    this.dialogRef.close(true);
  }
}
