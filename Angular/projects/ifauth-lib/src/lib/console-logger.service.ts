import { Injectable } from '@angular/core';
import { environment } from '../../../deepseek-gui/src/environments/environment';

@Injectable({
  providedIn: 'root'
})
export class ConsoleLoggerService {

  private _enabled: boolean;

  constructor() {
    this._enabled = environment.consoleLog;
  }

  log(message: string): void {
    if (this._enabled) {
      console.log(message);
    }
  }

  toggleLogging(enabled: boolean): void {
    this._enabled = enabled;
  }

}