import { Timestamp } from "google-protobuf/google/protobuf/timestamp_pb"
export class Order {
  constructor(
    public orderid: string,
    public instrumenttoken: number,
    public tradingsymbol: string,
    public transactiontype: string,
    public price: number,
    public quantity: number,
    public triggerprice: number,
    public status: string,
    public statusmessage: string,
    public algorithm: string,
    public algoinstance: number,
    public ordertype: string,
    public ordertime?: Timestamp.AsObject
  ) { }
}
export class ActiveAlgo {
  constructor(
    //algoId
    public aid: number,
    //algo name
    public an: string,
    //algo instance id
    public ains: number,
    //expiry
    public expiry: string,
    //candle time frame (in mins)
    public mins: number,
    //base instrument
    public binstrument: string,
    //Lot size
    public lotsize: number,
    //algo start date
    public algodate: string,
    //algo orders (if any)
    public orders: Order[] = []
  ) { }
}
