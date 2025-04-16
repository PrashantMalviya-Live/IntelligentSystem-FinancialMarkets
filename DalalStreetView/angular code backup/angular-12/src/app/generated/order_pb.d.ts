// package: ordering
// file: src/app/protos/order.proto

import * as jspb from "google-protobuf";
import * as google_protobuf_timestamp_pb from "google-protobuf/google/protobuf/timestamp_pb";

export class OrderMessage extends jspb.Message {
  getOrderid(): string;
  setOrderid(value: string): void;

  getInstrumenttoken(): number;
  setInstrumenttoken(value: number): void;

  getTradingsymbol(): string;
  setTradingsymbol(value: string): void;

  getTransactiontype(): string;
  setTransactiontype(value: string): void;

  getPrice(): number;
  setPrice(value: number): void;

  getQuantity(): number;
  setQuantity(value: number): void;

  getTriggerprice(): number;
  setTriggerprice(value: number): void;

  getStatus(): string;
  setStatus(value: string): void;

  getStatusmessage(): string;
  setStatusmessage(value: string): void;

  getAlgorithm(): string;
  setAlgorithm(value: string): void;

  getAlgoinstance(): number;
  setAlgoinstance(value: number): void;

  hasOrdertime(): boolean;
  clearOrdertime(): void;
  getOrdertime(): google_protobuf_timestamp_pb.Timestamp | undefined;
  setOrdertime(value?: google_protobuf_timestamp_pb.Timestamp): void;

  getOrdertype(): string;
  setOrdertype(value: string): void;

  serializeBinary(): Uint8Array;
  toObject(includeInstance?: boolean): OrderMessage.AsObject;
  static toObject(includeInstance: boolean, msg: OrderMessage): OrderMessage.AsObject;
  static extensions: {[key: number]: jspb.ExtensionFieldInfo<jspb.Message>};
  static extensionsBinary: {[key: number]: jspb.ExtensionFieldBinaryInfo<jspb.Message>};
  static serializeBinaryToWriter(message: OrderMessage, writer: jspb.BinaryWriter): void;
  static deserializeBinary(bytes: Uint8Array): OrderMessage;
  static deserializeBinaryFromReader(message: OrderMessage, reader: jspb.BinaryReader): OrderMessage;
}

export namespace OrderMessage {
  export type AsObject = {
    orderid: string,
    instrumenttoken: number,
    tradingsymbol: string,
    transactiontype: string,
    price: number,
    quantity: number,
    triggerprice: number,
    status: string,
    statusmessage: string,
    algorithm: string,
    algoinstance: number,
    ordertime?: google_protobuf_timestamp_pb.Timestamp.AsObject,
    ordertype: string,
  }
}

export class PublishStatus extends jspb.Message {
  getStatus(): boolean;
  setStatus(value: boolean): void;

  serializeBinary(): Uint8Array;
  toObject(includeInstance?: boolean): PublishStatus.AsObject;
  static toObject(includeInstance: boolean, msg: PublishStatus): PublishStatus.AsObject;
  static extensions: {[key: number]: jspb.ExtensionFieldInfo<jspb.Message>};
  static extensionsBinary: {[key: number]: jspb.ExtensionFieldBinaryInfo<jspb.Message>};
  static serializeBinaryToWriter(message: PublishStatus, writer: jspb.BinaryWriter): void;
  static deserializeBinary(bytes: Uint8Array): PublishStatus;
  static deserializeBinaryFromReader(message: PublishStatus, reader: jspb.BinaryReader): PublishStatus;
}

export namespace PublishStatus {
  export type AsObject = {
    status: boolean,
  }
}

