import Blob "mo:base/Blob";
import Cycles "mo:base/ExperimentalCycles";
import IC "ic:aaaaa-aa";

actor {
  public func http_outcall() : async Blob {
    let url = "https://example.com/api";
    let request_headers = [
      { name = "Test"; value = "test" },
    ];

    let http_request : IC.http_request_args = {
      url = url;
      max_response_bytes = null; // optional
      headers = request_headers;
      body = null; // for GET request, use ?request_body for POST
      method = #get; // or #post for POST request
      transform = null;
    };
    Cycles.add<system>(230_850_258_000);

    let http_response : IC.http_request_result = await IC.http_request(http_request);

    http_response.body;
  };

};
