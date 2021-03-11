import { Timestamp } from "google-protobuf/google/protobuf/timestamp_pb"
export class ChartData {
  constructor(
    public algoid: string,
    public instrumenttoken: number,
    public t: Timestamp.AsObject,
    public d: number,
  ) { }
}
