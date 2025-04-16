// package: logging
// file: src/app/protos/log.proto

var src_app_protos_log_pb = require("src/app/generated/log_pb");
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

var OrderAlerter = (function () {
  function OrderAlerter() {}
  OrderAlerter.serviceName = "logging.OrderAlerter";
  return OrderAlerter;
}());

OrderAlerter.Publish = {
  methodName: "Publish",
  service: OrderAlerter,
  requestStream: true,
  responseStream: true,
  requestType: src_app_protos_log_pb.PublishStatus,
  responseType: src_app_protos_log_pb.OrderMessage
};

exports.OrderAlerter = OrderAlerter;

function OrderAlerterClient(serviceHost, options) {
  this.serviceHost = serviceHost;
  this.options = options || {};
}

OrderAlerterClient.prototype.publish = function publish(metadata) {
  var listeners = {
    data: [],
    end: [],
    status: []
  };
  var client = grpc.client(OrderAlerter.Publish, {
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

exports.OrderAlerterClient = OrderAlerterClient;

var Charter = (function () {
  function Charter() {}
  Charter.serviceName = "logging.Charter";
  return Charter;
}());

Charter.DrawChart = {
  methodName: "DrawChart",
  service: Charter,
  requestStream: true,
  responseStream: true,
  requestType: src_app_protos_log_pb.CStatus,
  responseType: src_app_protos_log_pb.CData
};

exports.Charter = Charter;

function CharterClient(serviceHost, options) {
  this.serviceHost = serviceHost;
  this.options = options || {};
}

CharterClient.prototype.drawChart = function drawChart(metadata) {
  var listeners = {
    data: [],
    end: [],
    status: []
  };
  var client = grpc.client(Charter.DrawChart, {
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

exports.CharterClient = CharterClient;

