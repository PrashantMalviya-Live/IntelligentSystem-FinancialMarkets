// package: logging
// file: src/app/protos/log.proto

import * as jspb from "google-protobuf";
import * as google_protobuf_timestamp_pb from "google-protobuf/google/protobuf/timestamp_pb";

export class LogMessage extends jspb.Message {
  getAlgoid(): number;
  setAlgoid(value: number): void;

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
    algoid: number,
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

