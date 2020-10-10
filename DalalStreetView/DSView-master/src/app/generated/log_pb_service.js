// package: logging
// file: src/app/protos/log.proto

var src_app_protos_log_pb = require("../generated/log_pb");
var grpc = require("@improbable-eng/grpc-web").grpc;

var Logger = (function () {
  function Logger() {}
  Logger.serviceName = "logging.Logger";
  return Logger;
}());

Logger.Log = {
  methodName: "Log",
  service: Logger,
  requestStream: true,
  responseStream: true,
  requestType: src_app_protos_log_pb.Status,
  responseType: src_app_protos_log_pb.LogMessage
};

exports.Logger = Logger;

function LoggerClient(serviceHost, options) {
  this.serviceHost = serviceHost;
  this.options = options || {};
}

LoggerClient.prototype.log = function log(metadata) {
  var listeners = {
    data: [],
    end: [],
    status: []
  };
  var client = grpc.client(Logger.Log, {
    host: this.serviceHost,
    metadata: metadata,
    transport: this.options.transport
  });
  client.onEnd(function (status, statusMessage, trailers) {
    listeners.status.forEach(function (handler) {
      handler({ code: status, details: statusMessage, metadata: trailers });
    });
    listeners.end.forEach(function (handler) {
      handler({ code: status, details: statusMessage, metadata: trailers });
    });
    listeners = null;
  });
  client.onMessage(function (message) {
    listeners.data.forEach(function (handler) {
      handler(message);
    })
  });
  client.start(metadata);
  return {
    on: function (type, handler) {
      listeners[type].push(handler);
      return this;
    },
    write: function (requestMessage) {
      client.send(requestMessage);
      return this;
    },
    end: function () {
      client.finishSend();
    },
    cancel: function () {
      listeners = null;
      client.close();
    }
  };
};

exports.LoggerClient = LoggerClient;

