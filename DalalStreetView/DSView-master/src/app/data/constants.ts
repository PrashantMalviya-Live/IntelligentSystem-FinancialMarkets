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
  deltastrangle,
  activebuystrangle,
  momentumvolume,
  rsicross,
  expirystrangle,
  stranglewithlevels,
  rsistrangle,
  rsitrade,
  emacross,
  straddle,
  straddleindexrange,
  referencestraddle,
  premiumcross,
  momentumstraddle,
  ivtrade,
  calendarspread,
  directionaloptionsell,
  optionoptimizer,
  chart,
  alert,
  priceaction,
  candlewickscalping,
  emascalpingkb,
  initialbreakout,
  stockmomentum,
  marketalerts,
  tj3,
  tj4,
  tj5,
  bc,
  bc2,
  sar,
  optionsellonht,
  straddleexpirytrade,
  multitimeframesellonht,
  optionsellonema,
  pricedirectionalfutureoption,
  activestranglebuy
}
export class RunningAlgos {
  public _algoCtrl: string;
}
