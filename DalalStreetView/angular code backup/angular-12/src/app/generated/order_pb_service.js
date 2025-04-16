// package: ordering
// file: src/app/protos/order.proto

var src_app_protos_order_pb = require("../generated/order_pb");
var grpc = require("@improbable-eng/grpc-web").grpc;

var OrderAlerter = (function () {
  function OrderAlerter() {}
  OrderAlerter.serviceName = "ordering.OrderAlerter";
  return OrderAlerter;
}());

OrderAlerter.Publish = {
  methodName: "Publish",
  service: OrderAlerter,
  requestStream: true,
  responseStream: true,
  requestType: src_app_protos_order_pb.PublishStatus,
  responseType: src_app_protos_order_pb.OrderMessage
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

