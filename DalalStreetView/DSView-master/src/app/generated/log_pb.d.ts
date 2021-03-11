// package: logging
// file: src/app/protos/log.proto

import * as jspb from "google-protobuf";
import * as google_protobuf_timestamp_pb from "google-protobuf/google/protobuf/timestamp_pb";

export class LogMessage extends jspb.Message {
  getAlgoid(): string;
  setAlgoid(value: string): void;

  getAlgoInstance(): number;
  setAlgoInstance(value: number): void;

  hasLogTime(): boolean;
  clearLogTime(): void;
  getLogTime(): google_protobuf_timestamp_pb.Timestamp | undefined;
  setLogTime(value?: google_protobuf_timestamp_pb.Timestamp): void;

  getLogLevel(): string;
  setLogLevel(value: string): void;

  getMessage(): string;
  setMessage(value: string): void;

  getMessengerMethod(): string;
  setMessengerMethod(value: string): void;

  serializeBinary(): Uint8Array;
  toObject(includeInstance?: boolean): LogMessage.AsObject;
  static toObject(includeInstance: boolean, msg: LogMessage): LogMessage.AsObject;
  static extensions: {[key: number]: jspb.ExtensionFieldInfo<jspb.Message>};
  static extensionsBinary: {[key: number]: jspb.ExtensionFieldBinaryInfo<jspb.Message>};
  static serializeBinaryToWriter(message: LogMessage, writer: jspb.BinaryWriter): void;
  static deserializeBinary(bytes: Uint8Array): LogMessage;
  static deserializeBinaryFromReader(message: LogMessage, reader: jspb.BinaryReader): LogMessage;
}

export namespace LogMessage {
  export type AsObject = {
    algoid: string,
    algoInstance: number,
    logTime?: google_protobuf_timestamp_pb.Timestamp.AsObject,
    logLevel: string,
    message: string,
    messengerMethod: string,
  }
}

export class Status extends jspb.Message {
  getStatus(): boolean;
  setStatus(value: boolean): void;

  serializeBinary(): Uint8Array;
  toObject(includeInstance?: boolean): Status.AsObject;
  static toObject(includeInstance: boolean, msg: Status): Status.AsObject;
  static extensions: {[key: number]: jspb.ExtensionFieldInfo<jspb.Message>};
  static extensionsBinary: {[key: number]: jspb.ExtensionFieldBinaryInfo<jspb.Message>};
  static serializeBinaryToWriter(message: Status, writer: jspb.BinaryWriter): void;
  static deserializeBinary(bytes: Uint8Array): Status;
  static deserializeBinaryFromReader(message: Status, reader: jspb.BinaryReader): Status;
}

export namespace Status {
  export type AsObject = {
    status: boolean,
  }
}

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

export class CData extends jspb.Message {
  getAlgoid(): string;
  setAlgoid(value: string): void;

  getAlgoInstance(): number;
  setAlgoInstance(value: number): void;

  hasT(): boolean;
  clearT(): void;
  getT(): google_protobuf_timestamp_pb.Timestamp | undefined;
  setT(value?: google_protobuf_timestamp_pb.Timestamp): void;

  getInstrumenttoken(): number;
  setInstrumenttoken(value: number): void;

  getD(): number;
  setD(value: number): void;

  getXlabel(): string;
  setXlabel(value: string): void;

  getYlabel(): string;
  setYlabel(value: string): void;

  getArg(): string;
  setArg(value: string): void;

  serializeBinary(): Uint8Array;
  toObject(includeInstance?: boolean): CData.AsObject;
  static toObject(includeInstance: boolean, msg: CData): CData.AsObject;
  static extensions: {[key: number]: jspb.ExtensionFieldInfo<jspb.Message>};
  static extensionsBinary: {[key: number]: jspb.ExtensionFieldBinaryInfo<jspb.Message>};
  static serializeBinaryToWriter(message: CData, writer: jspb.BinaryWriter): void;
  static deserializeBinary(bytes: Uint8Array): CData;
  static deserializeBinaryFromReader(message: CData, reader: jspb.BinaryReader): CData;
}

export namespace CData {
  export type AsObject = {
    algoid: string,
    algoInstance: number,
    t?: google_protobuf_timestamp_pb.Timestamp.AsObject,
    instrumenttoken: number,
    d: number,
    xlabel: string,
    ylabel: string,
    arg: string,
  }
}

export class CStatus extends jspb.Message {
  getStatus(): boolean;
  setStatus(value: boolean): void;

  serializeBinary(): Uint8Array;
  toObject(includeInstance?: boolean): CStatus.AsObject;
  static toObject(includeInstance: boolean, msg: CStatus): CStatus.AsObject;
  static extensions: {[key: number]: jspb.ExtensionFieldInfo<jspb.Message>};
  static extensionsBinary: {[key: number]: jspb.ExtensionFieldBinaryInfo<jspb.Message>};
  static serializeBinaryToWriter(message: CStatus, writer: jspb.BinaryWriter): void;
  static deserializeBinary(bytes: Uint8Array): CStatus;
  static deserializeBinaryFromReader(message: CStatus, reader: jspb.BinaryReader): CStatus;
}

export namespace CStatus {
  export type AsObject = {
    status: boolean,
  }
}

