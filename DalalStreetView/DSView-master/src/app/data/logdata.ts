import { Timestamp } from "google-protobuf/google/protobuf/timestamp_pb"

export class LogData {
  constructor(
    //algoId
    public algoid: string,
    //algo name
    public algoInstance: number,
    //algo instance id
    public logLevel: string,
    //algo name
    public message: string,
    //algo instance id
    public messengerMethod: string,

    public logTime?: Timestamp.AsObject
  ) { }
}
