import { Timestamp } from "google-protobuf/google/protobuf/timestamp_pb"
export class ChartData {
  public t?: Timestamp.AsObject
  public algoid: string;
  public instrumenttoken: number;
  public d: number;
  public algoInstance: number;
  public chartid: number;
  public chartdataid: number;
  public arg: string;

  constructor(aid, tkn, data, ains, cid, cdid, arg)
  {
    this.algoid = aid;
    this.instrumenttoken = tkn;
    this.d = data;
    this.chartid = cid;
    this.chartdataid = cdid;
    this.algoInstance = ains;
    this.arg = arg;
  }

  //constructor(
  //  public algoid: string,
  //  public instrumenttoken: number,
  //  public d: number
  ////  public t: Timestamp.AsObject = undefined
  //) { }
}
