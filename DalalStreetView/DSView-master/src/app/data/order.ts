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
    public aIns: number,
    
    //algo orders (if any)
    public orders: Order[]
  ) { }
}
