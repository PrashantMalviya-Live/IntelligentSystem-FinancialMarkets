export class Order {
  constructor(
    public instrumentToken: number,
    public tradingSymbol: string,
    public transactionType: string,
    public price: number,
    public quantity: number,
    public triggerprice: number,
    public status: string,
    public statusmessage: string,
    public algorithm: string,
    public algoInstance: number,
    public orderTime: string,
    public orderType: string,
  ) { }
}
export class ActiveAlgo {
  constructor(
    //algoId
    public aid: number,
    //algo name
    public an: string,
    //algo instance id
    public aIns: number
  ) { }
}

//export class OrdersByInstance {
//  constructor(
//    public algoInstance: number,
//    public orders: Order[]
//  ) { }
//}
