export class Constants {
  public static HEALTH_CHECK_LOG_LEVEL = "Stop";
  public static MARKET_DATA_SERVICE_INSTANCE = -99;
}
export enum Loglevel {
  Trace = 0,
  Debug = 1,
  Info = 2,
  Warn = 3,
  Error = 4,
  Stop = 5,
  Health = 6
}
export enum AlgoControllers {
  momentumvolume,
  rsicross,
  expirystrangle,
  rsistrangle,
  rsitrade,
  emacrossvolume,
  straddle,
  referencestraddle,
  premiumcross,
  momentumstraddle,
  ivtrade,
  chart
}
export class RunningAlgos {
  public _algoCtrl: string;
}
