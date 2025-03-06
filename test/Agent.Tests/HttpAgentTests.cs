using EdjCase.ICP.Agent;
using EdjCase.ICP.Agent.Agents;
using EdjCase.ICP.Agent.Agents.Http;
using EdjCase.ICP.Agent.Identities;
using EdjCase.ICP.Agent.Models;
using EdjCase.ICP.Agent.Requests;
using EdjCase.ICP.Agent.Responses;
using EdjCase.ICP.Candid;
using EdjCase.ICP.Candid.Models;
using EdjCase.ICP.Candid.Utilities;
using Moq;
using System;
using System.Formats.Cbor;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Agent.Tests
{
	public class HttpAgentTests
	{
		private static readonly Principal canisterId = Principal.FromText("j4n55-giaaa-aaaap-qb3wq-cai");
		private static readonly IIdentity identity = IdentityUtil.GenerateEd25519Identity();
		private static readonly Uri boundaryNodeUrl = new Uri("https://icp-api.io/");
		private static readonly SubjectPublicKeyInfo rootPublicKey = SubjectPublicKeyInfo.MainNetRootPublicKey;

		[Fact]
		public async Task CallAsync_SuccessfulResponse_ReturnsReply()
		{
			// Arrange
			var httpClientMock = new Mock<IHttpClient>();
			var agent = new HttpAgent(httpClientMock.Object, skipCertificateValidation: true);

			var request = new CallRequest(canisterId, "greet", CandidArg.FromCandid());
			var signedContent = SignedContent<CallRequest>.CreateAndSign(request, identity);

			var certificate = CreateCertificate(signedContent.RequestId, RequestStatus.CreateReplied(CandidArg.FromCandid("DIDL\0\01\01\x68\x65\x6c\x6c\x6f")));
			var response = new V3CallResponse(certificate);

			var responseBytes = SerializeV3CallResponse(response);
			var httpResponse = CreateHttpResponse(HttpStatusCode.OK, responseBytes);

			httpClientMock.Setup(c => c.PostAsync(
				It.Is<string>(url => url.Contains("/api/v3/canister/")),
				It.IsAny<byte[]>(),
				It.IsAny<CancellationToken>()))
				.ReturnsAsync(httpResponse);

			// Act
			var result = await agent.CallAsync(signedContent);

			// Assert
			Assert.NotNull(result);
			Assert.Equal("hello", result.Decode<string>());
		}

		[Fact]
		public async Task CallAsync_NotFoundResponse_FallsBackToV2()
		{
			// Arrange
			var httpClientMock = new Mock<IHttpClient>();
			var agent = new HttpAgent(httpClientMock.Object, skipCertificateValidation: true);

			var request = new CallRequest(canisterId, "greet", CandidArg.FromCandid());
			var signedContent = SignedContent<CallRequest>.CreateAndSign(request, identity);

			// V3 returns 404
			httpClientMock.Setup(c => c.PostAsync(
				It.Is<string>(url => url.Contains("/api/v3/canister/")),
				It.IsAny<byte[]>(),
				It.IsAny<CancellationToken>()))
				.ReturnsAsync(CreateHttpResponse(HttpStatusCode.NotFound, Array.Empty<byte>()));

			// V2 Call setup (returns 202 Accepted)
			httpClientMock.Setup(c => c.PostAsync(
				It.Is<string>(url => url.Contains("/api/v2/canister/") && url.Contains("/call")),
				It.IsAny<byte[]>(),
				It.IsAny<CancellationToken>()))
				.ReturnsAsync(CreateHttpResponse(HttpStatusCode.Accepted, Array.Empty<byte>()));

			// Read state setup for polling
			var certificate = CreateCertificate(signedContent.RequestId, RequestStatus.CreateReplied(CandidArg.FromCandid("DIDL\0\01\01\x68\x65\x6c\x6c\x6f")));
			var readStateResponse = new ReadStateResponse(certificate);
			var readStateResponseBytes = SerializeReadStateResponse(readStateResponse);

			httpClientMock.Setup(c => c.PostAsync(
				It.Is<string>(url => url.Contains("/api/v2/canister/") && url.Contains("/read_state")),
				It.IsAny<byte[]>(),
				It.IsAny<CancellationToken>()))
				.ReturnsAsync(CreateHttpResponse(HttpStatusCode.OK, readStateResponseBytes));

			// Act
			var result = await agent.CallAsync(signedContent);

			// Assert
			Assert.NotNull(result);
			Assert.Equal("hello", result.Decode<string>());
		}

		[Fact]
		public async Task CallAsync_AcceptedResponse_PollsUntilComplete()
		{
			// Arrange
			var httpClientMock = new Mock<IHttpClient>();
			var agent = new HttpAgent(httpClientMock.Object, skipCertificateValidation: true);

			var request = new CallRequest(canisterId, "greet", CandidArg.FromCandid());
			var signedContent = SignedContent<CallRequest>.CreateAndSign(request, identity);

			// V3 returns 202 Accepted (requires polling)
			httpClientMock.Setup(c => c.PostAsync(
				It.Is<string>(url => url.Contains("/api/v3/canister/")),
				It.IsAny<byte[]>(),
				It.IsAny<CancellationToken>()))
				.ReturnsAsync(CreateHttpResponse(HttpStatusCode.Accepted, Array.Empty<byte>()));

			// Read state setup for polling
			var certificate = CreateCertificate(signedContent.RequestId, RequestStatus.CreateReplied(CandidArg.FromCandid("DIDL\0\01\01\x68\x65\x6c\x6c\x6f")));
			var readStateResponse = new ReadStateResponse(certificate);
			var readStateResponseBytes = SerializeReadStateResponse(readStateResponse);

			httpClientMock.Setup(c => c.PostAsync(
				It.Is<string>(url => url.Contains("/api/v2/canister/") && url.Contains("/read_state")),
				It.IsAny<byte[]>(),
				It.IsAny<CancellationToken>()))
				.ReturnsAsync(CreateHttpResponse(HttpStatusCode.OK, readStateResponseBytes));

			// Act
			var result = await agent.CallAsync(signedContent);

			// Assert
			Assert.NotNull(result);
			Assert.Equal("hello", result.Decode<string>());
		}

		[Fact]
		public async Task CallAsync_RejectedResponse_ThrowsCallRejectedException()
		{
			// Arrange
			var httpClientMock = new Mock<IHttpClient>();
			var agent = new HttpAgent(httpClientMock.Object, skipCertificateValidation: true);

			var request = new CallRequest(canisterId, "greet", CandidArg.FromCandid());
			var signedContent = SignedContent<CallRequest>.CreateAndSign(request, identity);

			var certificate = CreateCertificate(signedContent.RequestId,
				RequestStatus.CreateRejected(RejectCode.CanisterError, "Test error message", "TEST_ERROR"));
			var response = new V3CallResponse(certificate);

			var responseBytes = SerializeV3CallResponse(response);
			var httpResponse = CreateHttpResponse(HttpStatusCode.OK, responseBytes);

			httpClientMock.Setup(c => c.PostAsync(
				It.Is<string>(url => url.Contains("/api/v3/canister/")),
				It.IsAny<byte[]>(),
				It.IsAny<CancellationToken>()))
				.ReturnsAsync(httpResponse);

			// Act & Assert
			var exception = await Assert.ThrowsAsync<CallRejectedException>(
				async () => await agent.CallAsync(signedContent));

			Assert.Equal(RejectCode.CanisterError, exception.Code);
			Assert.Equal("Test error message", exception.Message);
			Assert.Equal("TEST_ERROR", exception.ErrorCode);
		}

		[Fact]
		public async Task QueryAsync_SuccessfulResponse_ReturnsReply()
		{
			// Arrange
			var httpClientMock = new Mock<IHttpClient>();
			var agent = new HttpAgent(httpClientMock.Object);

			var request = new QueryRequest(canisterId, "greet", CandidArg.FromCandid());
			var signedContent = SignedContent<QueryRequest>.CreateAndSign(request, identity);

			var response = new QueryResponse(QueryResponseType.Replied,
				CandidArg.FromCandid("DIDL\0\01\01\x68\x65\x6c\x6c\x6f"), null, null, null);

			var responseBytes = SerializeQueryResponse(response);
			var httpResponse = CreateHttpResponse(HttpStatusCode.OK, responseBytes);

			httpClientMock.Setup(c => c.PostAsync(
				It.Is<string>(url => url.Contains("/api/v2/canister/") && url.Contains("/query")),
				It.IsAny<byte[]>(),
				It.IsAny<CancellationToken>()))
				.ReturnsAsync(httpResponse);

			// Act
			var result = await agent.QueryAsync(signedContent);

			// Assert
			Assert.NotNull(result);
			Assert.Equal("hello", result.Decode<string>());
		}

		[Fact]
		public async Task QueryAsync_RejectedResponse_ThrowsCallRejectedException()
		{
			// Arrange
			var httpClientMock = new Mock<IHttpClient>();
			var agent = new HttpAgent(httpClientMock.Object);

			var request = new QueryRequest(canisterId, "greet", CandidArg.FromCandid());
			var signedContent = SignedContent<QueryRequest>.CreateAndSign(request, identity);

			var response = new QueryResponse(QueryResponseType.Rejected,
				null, RejectCode.CanisterError, "Test error message", "TEST_ERROR");

			var responseBytes = SerializeQueryResponse(response);
			var httpResponse = CreateHttpResponse(HttpStatusCode.OK, responseBytes);

			httpClientMock.Setup(c => c.PostAsync(
				It.Is<string>(url => url.Contains("/api/v2/canister/") && url.Contains("/query")),
				It.IsAny<byte[]>(),
				It.IsAny<CancellationToken>()))
				.ReturnsAsync(httpResponse);

			// Act & Assert
			var exception = await Assert.ThrowsAsync<CallRejectedException>(
				async () => await agent.QueryAsync(signedContent));

			Assert.Equal(RejectCode.CanisterError, exception.Code);
			Assert.Equal("Test error message", exception.Message);
			Assert.Equal("TEST_ERROR", exception.ErrorCode);
		}

		[Fact]
		public async Task ReadStateAsync_SuccessfulResponse_ReturnsCertificate()
		{
			// Arrange
			var httpClientMock = new Mock<IHttpClient>();
			var agent = new HttpAgent(httpClientMock.Object, skipCertificateValidation: true);

			var requestId = RequestId.FromRandom();
			var request = new ReadStateRequest(new[] {
				StatePath.FromSegments("request_status", requestId.RawValue)
			});
			var signedContent = SignedContent<ReadStateRequest>.CreateAndSign(request, identity);

			var certificate = CreateCertificate(requestId, RequestStatus.CreateReplied(CandidArg.FromCandid("DIDL\0\01\01\x68\x65\x6c\x6c\x6f")));
			var response = new ReadStateResponse(certificate);
			var responseBytes = SerializeReadStateResponse(response);

			var httpResponse = CreateHttpResponse(HttpStatusCode.OK, responseBytes);

			httpClientMock.Setup(c => c.PostAsync(
				It.Is<string>(url => url.Contains("/api/v2/canister/") && url.Contains("/read_state")),
				It.IsAny<byte[]>(),
				It.IsAny<CancellationToken>()))
				.ReturnsAsync(httpResponse);

			// Act
			var result = await agent.ReadStateAsync(canisterId, signedContent);

			// Assert
			Assert.NotNull(result);
			Assert.NotNull(result.Certificate);

			var requestStatus = IAgentExtensions.ParseRequestStatus(
				result.Certificate.Tree.GetValueOrDefault(StatePath.FromSegments("request_status", requestId.RawValue))
			);

			Assert.Equal(RequestStatus.StatusType.Replied, requestStatus?.Type);
			Assert.Equal("hello", requestStatus?.AsReplied().Decode<string>());
		}

		[Fact]
		public async Task GetRootKeyAsync_StatusResponse_ReturnsRootKey()
		{
			// Arrange
			var httpClientMock = new Mock<IHttpClient>();
			var agent = new HttpAgent(httpClientMock.Object);

			// Mock status response with development root key
			var rootKeyBytes = new byte[] { 0x30, 0x82, 0x01, 0x22, 0x30, 0x0D, 0x06, 0x09, 0x2A, 0x86, 0x48, 0x86, 0xF7, 0x0D, 0x01, 0x01, 0x01, 0x05, 0x00 };
			var statusResponse = new StatusResponse(
				ApiVersion: "v2",
				RootKey: null,
				DevelopmentRootKey: rootKeyBytes,
				Implementations: Array.Empty<string>()
			);

			var responseBytes = SerializeStatusResponse(statusResponse);
			var httpResponse = CreateHttpResponse(HttpStatusCode.OK, responseBytes);

			httpClientMock.Setup(c => c.GetAsync(
				It.Is<string>(url => url.Contains("/api/v2/status")),
				It.IsAny<CancellationToken>()))
				.ReturnsAsync(httpResponse);

			// Act
			var result = await agent.GetRootKeyAsync();

			// Assert
			Assert.NotNull(result);
			// Compare DER encodings
			Assert.Equal(rootKeyBytes, result.GetDerEncoding());
		}

		[Fact]
		public async Task GetRootKeyAsync_NoDevRootKey_ReturnsMainNetKey()
		{
			// Arrange
			var httpClientMock = new Mock<IHttpClient>();
			var agent = new HttpAgent(httpClientMock.Object);

			// Mock status response without development root key
			var statusResponse = new StatusResponse(
				ApiVersion: "v2",
				RootKey: null,
				DevelopmentRootKey: null,
				Implementations: Array.Empty<string>()
			);

			var responseBytes = SerializeStatusResponse(statusResponse);
			var httpResponse = CreateHttpResponse(HttpStatusCode.OK, responseBytes);

			httpClientMock.Setup(c => c.GetAsync(
				It.Is<string>(url => url.Contains("/api/v2/status")),
				It.IsAny<CancellationToken>()))
				.ReturnsAsync(httpResponse);

			// Act
			var result = await agent.GetRootKeyAsync();

			// Assert
			Assert.NotNull(result);
			// Should be main net key
			Assert.Equal(SubjectPublicKeyInfo.MainNetRootPublicKey.GetDerEncoding(), result.GetDerEncoding());
		}

		[Fact]
		public async Task CallAsynchronousAsync_SuccessfulResponse_ReturnsRequestId()
		{
			// Arrange
			var httpClientMock = new Mock<IHttpClient>();
			var agent = new HttpAgent(httpClientMock.Object);

			var request = new CallRequest(canisterId, "greet", CandidArg.FromCandid());
			var signedContent = SignedContent<CallRequest>.CreateAndSign(request, identity);

			var httpResponse = CreateHttpResponse(HttpStatusCode.Accepted, Array.Empty<byte>());

			httpClientMock.Setup(c => c.PostAsync(
				It.Is<string>(url => url.Contains("/api/v2/canister/") && url.Contains("/call")),
				It.IsAny<byte[]>(),
				It.IsAny<CancellationToken>()))
				.ReturnsAsync(httpResponse);

			// Act
			var result = await agent.CallAsynchronousAsync(signedContent);

			// Assert
			Assert.Equal(signedContent.RequestId, result);
		}

		[Fact]
		public async Task CallAsynchronousAsync_RejectedResponse_ThrowsCallRejectedException()
		{
			// Arrange
			var httpClientMock = new Mock<IHttpClient>();
			var agent = new HttpAgent(httpClientMock.Object);

			var request = new CallRequest(canisterId, "greet", CandidArg.FromCandid());
			var signedContent = SignedContent<CallRequest>.CreateAndSign(request, identity);

			// Create rejected response
			var rejectResponse = new CallRejectedResponse(
				RejectCode.CanisterError,
				"Test error message",
				"TEST_ERROR"
			);

			var responseBytes = SerializeCallRejectedResponse(rejectResponse);
			var httpResponse = CreateHttpResponse(HttpStatusCode.OK, responseBytes);

			httpClientMock.Setup(c => c.PostAsync(
				It.Is<string>(url => url.Contains("/api/v2/canister/") && url.Contains("/call")),
				It.IsAny<byte[]>(),
				It.IsAny<CancellationToken>()))
				.ReturnsAsync(httpResponse);

			// Act & Assert
			var exception = await Assert.ThrowsAsync<CallRejectedException>(
				async () => await agent.CallAsynchronousAsync(signedContent));

			Assert.Equal(RejectCode.CanisterError, exception.Code);
			Assert.Equal("Test error message", exception.Message);
			Assert.Equal("TEST_ERROR", exception.ErrorCode);
		}

		#region Helper Methods

		private static HttpResponse CreateHttpResponse(HttpStatusCode statusCode, byte[] content)
		{
			return new HttpResponse(statusCode, content);
		}

		private static Certificate CreateCertificate(RequestId requestId, RequestStatus status)
		{
			var tree = HashTree.Empty();
			if (status != null)
			{
				tree = HashTree.Labeled(
					"request_status",
					HashTree.Labeled(
						requestId.RawValue,
						status.ToHashTree()
					)
				);
			}

			// Create a dummy signature for testing
			var signature = new byte[32];
			Array.Fill<byte>(signature, 1);

			return new Certificate(tree, signature);
		}

		private static byte[] SerializeV3CallResponse(V3CallResponse response)
		{
			var writer = new CborWriter();
			response.ToCbor(writer);
			return writer.Encode();
		}

		private static byte[] SerializeReadStateResponse(ReadStateResponse response)
		{
			var writer = new CborWriter();
			response.ToCbor(writer);
			return writer.Encode();
		}

		private static byte[] SerializeQueryResponse(QueryResponse response)
		{
			var writer = new CborWriter();
			response.ToCbor(writer);
			return writer.Encode();
		}

		private static byte[] SerializeStatusResponse(StatusResponse response)
		{
			var writer = new CborWriter();
			response.ToCbor(writer);
			return writer.Encode();
		}

		private static byte[] SerializeCallRejectedResponse(CallRejectedResponse response)
		{
			var writer = new CborWriter();
			response.ToCbor(writer);
			return writer.Encode();
		}

		#endregion
	}
}
