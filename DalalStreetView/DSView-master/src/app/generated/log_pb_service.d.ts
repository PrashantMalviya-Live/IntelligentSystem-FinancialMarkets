// package: logging
// file: src/app/protos/log.proto

import * as src_app_protos_log_pb from "../generated/log_pb";
import {grpc} from "@improbable-eng/grpc-web";

type LoggerLog = {
  readonly methodName: string;
  readonly service: typeof Logger;
  readonly requestStream: true;
  readonly responseStream: true;
  readonly requestType: typeof src_app_protos_log_pb.Status;
  readonly responseType: typeof src_app_protos_log_pb.LogMessage;
};

export class Logger {
  static readonly serviceName: string;
  static readonly Log: LoggerLog;
}

export type ServiceError = { message: string, code: number; metadata: grpc.Metadata }
export type Status = { details: string, code: number; metadata: grpc.Metadata }

interface UnaryResponse {
  cancel(): void;
}
interface ResponseStream<T> {
  cancel(): void;
  on(type: 'data', handler: (message: T) => void): ResponseStream<T>;
  on(type: 'end', handler: (status?: Status) => void): ResponseStream<T>;
  on(type: 'status', handler: (status: Status) => void): ResponseStream<T>;
}
interface RequestStream<T> {
  write(message: T): RequestStream<T>;
  end(): void;
  cancel(): void;
  on(type: 'end', handler: (status?: Status) => void): RequestStream<T>;
  on(type: 'status', handler: (status: Status) => void): RequestStream<T>;
}
interface BidirectionalStream<ReqT, ResT> {
  write(message: ReqT): BidirectionalStream<ReqT, ResT>;
  end(): void;
  cancel(): void;
  on(type: 'data', handler: (message: ResT) => void): BidirectionalStream<ReqT, ResT>;
  on(type: 'end', handler: (status?: Status) => void): BidirectionalStream<ReqT, ResT>;
  on(type: 'status', handler: (status: Status) => void): BidirectionalStream<ReqT, ResT>;
}

export class LoggerClient {
  readonly serviceHost: string;

  constructor(serviceHost: string, options?: grpc.RpcOptions);
  log(metadata?: grpc.Metadata): BidirectionalStream<src_app_protos_log_pb.Status, src_app_protos_log_pb.LogMessage>;
}

