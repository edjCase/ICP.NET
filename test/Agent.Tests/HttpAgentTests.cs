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
		private static readonly Uri boundaryNodeUrl = new("https://icp-api.io/");
		private static readonly SubjectPublicKeyInfo rootPublicKey = SubjectPublicKeyInfo.MainNetRootPublicKey;

		[Fact]
		public async Task CallAsync_SuccessfulResponse_ReturnsReply()
		{
			// Arrange
			Mock<IHttpClient> httpClientMock = new();
			HttpAgent agent = new(httpClientMock.Object, skipCertificateValidation: true);

			Principal sender = identity.GetPrincipal();
			ICTimestamp ingressExpiry = ICTimestamp.Future(TimeSpan.FromMinutes(1));
			CallRequest request = new(canisterId, "greet", CandidArg.FromCandid(), sender, ingressExpiry);
			SignedRequest<CallRequest> signedContent = identity.Sign(request);


			byte[] responseBytes = [];
			HttpResponse httpResponse = CreateHttpResponse(HttpStatusCode.OK, responseBytes);

			httpClientMock.Setup(c => c.PostAsync(
				It.Is<string>(url => url.Contains("/api/v3/canister/")),
				It.IsAny<byte[]>(),
				It.IsAny<CancellationToken>()))
				.ReturnsAsync(httpResponse);

			// Act
			CandidArg result = await agent.CallAsync(signedContent);

			// Assert
			Assert.NotNull(result);
			Assert.Equal("hello", result.ToObjects<string>());
		}

		[Fact]
		public async Task CallAsync_NotFoundResponse_FallsBackToV2()
		{
			// Arrange
			Mock<IHttpClient> httpClientMock = new();
			HttpAgent agent = new(httpClientMock.Object, skipCertificateValidation: true);

			Principal sender = identity.GetPrincipal();
			ICTimestamp ingressExpiry = ICTimestamp.Future(TimeSpan.FromMinutes(1));
			CallRequest request = new(canisterId, "greet", CandidArg.FromCandid(), sender, ingressExpiry);
			var signedContent = identity.Sign(request);

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

			byte[] readStateResponseBytes = [];

			httpClientMock.Setup(c => c.PostAsync(
				It.Is<string>(url => url.Contains("/api/v2/canister/") && url.Contains("/read_state")),
				It.IsAny<byte[]>(),
				It.IsAny<CancellationToken>()))
				.ReturnsAsync(CreateHttpResponse(HttpStatusCode.OK, readStateResponseBytes));

			// Act
			var result = await agent.CallAsync(signedContent);

			// Assert
			Assert.NotNull(result);
			Assert.Equal("hello", result.ToObjects<string>());
		}

		[Fact]
		public async Task CallAsync_AcceptedResponse_PollsUntilComplete()
		{
			// Arrange
			Mock<IHttpClient> httpClientMock = new();
			HttpAgent agent = new(httpClientMock.Object, skipCertificateValidation: true);

			Principal sender = identity.GetPrincipal();
			ICTimestamp ingressExpiry = ICTimestamp.Future(TimeSpan.FromMinutes(1));
			CallRequest request = new(canisterId, "greet", CandidArg.FromCandid(), sender, ingressExpiry);
			var signedContent = identity.Sign(request);

			// V3 returns 202 Accepted (requires polling)
			httpClientMock.Setup(c => c.PostAsync(
				It.Is<string>(url => url.Contains("/api/v3/canister/")),
				It.IsAny<byte[]>(),
				It.IsAny<CancellationToken>()))
				.ReturnsAsync(CreateHttpResponse(HttpStatusCode.Accepted, Array.Empty<byte>()));

			byte[] readStateResponseBytes = [];

			httpClientMock.Setup(c => c.PostAsync(
				It.Is<string>(url => url.Contains("/api/v2/canister/") && url.Contains("/read_state")),
				It.IsAny<byte[]>(),
				It.IsAny<CancellationToken>()))
				.ReturnsAsync(CreateHttpResponse(HttpStatusCode.OK, readStateResponseBytes));

			// Act
			var result = await agent.CallAsync(signedContent);

			// Assert
			Assert.NotNull(result);
			Assert.Equal("hello", result.ToObjects<string>());
		}

		[Fact]
		public async Task CallAsync_RejectedResponse_ThrowsCallRejectedException()
		{
			// Arrange
			Mock<IHttpClient> httpClientMock = new();
			HttpAgent agent = new(httpClientMock.Object, skipCertificateValidation: true);

			Principal sender = identity.GetPrincipal();
			ICTimestamp ingressExpiry = ICTimestamp.Future(TimeSpan.FromMinutes(1));
			CallRequest request = new(canisterId, "greet", CandidArg.FromCandid(), sender, ingressExpiry);
			var signedContent = identity.Sign(request);


			byte[] responseBytes = [];
			HttpResponse httpResponse = CreateHttpResponse(HttpStatusCode.OK, responseBytes);

			httpClientMock.Setup(c => c.PostAsync(
				It.Is<string>(url => url.Contains("/api/v3/canister/")),
				It.IsAny<byte[]>(),
				It.IsAny<CancellationToken>()))
				.ReturnsAsync(httpResponse);

			// Act & Assert
			var exception = await Assert.ThrowsAsync<CallRejectedException>(
				async () => await agent.CallAsync(signedContent));

			Assert.Equal(RejectCode.CanisterError, exception.RejectCode);
			Assert.Equal("Test error message", exception.Message);
			Assert.Equal("TEST_ERROR", exception.ErrorCode);
		}

		[Fact]
		public async Task QueryAsync_SuccessfulResponse_ReturnsReply()
		{
			// Arrange
			Mock<IHttpClient> httpClientMock = new();
			HttpAgent agent = new(httpClientMock.Object);

			Principal sender = identity.GetPrincipal();
			ICTimestamp ingressExpiry = ICTimestamp.Future(TimeSpan.FromMinutes(1));
			QueryRequest request = new(canisterId, "greet", CandidArg.FromCandid(), sender, ingressExpiry);
			var signedContent = identity.Sign(request);


			byte[] responseBytes = [];
			HttpResponse httpResponse = CreateHttpResponse(HttpStatusCode.OK, responseBytes);

			httpClientMock.Setup(c => c.PostAsync(
				It.Is<string>(url => url.Contains("/api/v2/canister/") && url.Contains("/query")),
				It.IsAny<byte[]>(),
				It.IsAny<CancellationToken>()))
				.ReturnsAsync(httpResponse);

			// Act
			var result = await agent.QueryAsync(signedContent);

			// Assert
			Assert.NotNull(result);
			Assert.Equal("hello", result.ToObjects<string>());
		}

		[Fact]
		public async Task QueryAsync_RejectedResponse_ThrowsCallRejectedException()
		{
			// Arrange
			Mock<IHttpClient> httpClientMock = new();
			HttpAgent agent = new(httpClientMock.Object);

			Principal sender = identity.GetPrincipal();
			ICTimestamp ingressExpiry = ICTimestamp.Future(TimeSpan.FromMinutes(1));

			QueryRequest request = new(canisterId, "greet", CandidArg.FromCandid(), sender, ingressExpiry);
			SignedRequest<QueryRequest> signedContent = identity.Sign(request);

			byte[] responseBytes = [];
			HttpResponse httpResponse = CreateHttpResponse(HttpStatusCode.OK, responseBytes);

			httpClientMock.Setup(c => c.PostAsync(
				It.Is<string>(url => url.Contains("/api/v2/canister/") && url.Contains("/query")),
				It.IsAny<byte[]>(),
				It.IsAny<CancellationToken>()))
				.ReturnsAsync(httpResponse);

			// Act & Assert
			var exception = await Assert.ThrowsAsync<CallRejectedException>(
				async () => await agent.QueryAsync(signedContent));

			Assert.Equal(RejectCode.CanisterError, exception.RejectCode);
			Assert.Equal("Test error message", exception.Message);
			Assert.Equal("TEST_ERROR", exception.ErrorCode);
		}

		[Fact]
		public async Task ReadStateAsync_SuccessfulResponse_ReturnsCertificate()
		{
			// Arrange
			Mock<IHttpClient> httpClientMock = new();
			HttpAgent agent = new(httpClientMock.Object, skipCertificateValidation: true);

			RequestId requestId = RequestId.FromBytes(new byte[32]);

			Principal sender = identity.GetPrincipal();
			ICTimestamp ingressExpiry = ICTimestamp.Future(TimeSpan.FromMinutes(1));
			List<StatePath> paths = [
				StatePath.FromSegments("request_status", requestId.RawValue)
			];
			ReadStateRequest request = new(paths, sender, ingressExpiry);
			var signedContent = identity.Sign(request);

			byte[] responseBytes = [];

			HttpResponse httpResponse = CreateHttpResponse(HttpStatusCode.OK, responseBytes);

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

			RequestStatus? requestStatus = IAgentExtensions.ParseRequestStatus(
				result.Certificate.Tree.GetValueOrDefault(StatePath.FromSegments("request_status", requestId.RawValue))
			);

			Assert.Equal(RequestStatus.StatusType.Replied, requestStatus?.Type);
			Assert.Equal("hello", requestStatus?.AsReplied().ToObjects<string>());
		}

		[Fact]
		public async Task GetRootKeyAsync_StatusResponse_ReturnsRootKey()
		{
			// Arrange
			Mock<IHttpClient> httpClientMock = new();
			HttpAgent agent = new(httpClientMock.Object);

			// Mock status response with development root key
			byte[] rootKeyBytes = new byte[] { 0x30, 0x82, 0x01, 0x22, 0x30, 0x0D, 0x06, 0x09, 0x2A, 0x86, 0x48, 0x86, 0xF7, 0x0D, 0x01, 0x01, 0x01, 0x05, 0x00 };


			byte[] responseBytes = [];
			HttpResponse httpResponse = CreateHttpResponse(HttpStatusCode.OK, responseBytes);

			httpClientMock.Setup(c => c.GetAsync(
				It.Is<string>(url => url.Contains("/api/v2/status")),
				It.IsAny<CancellationToken>()))
				.ReturnsAsync(httpResponse);

			// Act
			SubjectPublicKeyInfo result = await agent.GetRootKeyAsync();

			// Assert
			Assert.NotNull(result);
			// Compare DER encodings
			Assert.Equal(rootKeyBytes, result.ToDerEncoding());
		}

		[Fact]
		public async Task GetRootKeyAsync_NoDevRootKey_ReturnsMainNetKey()
		{
			// Arrange
			Mock<IHttpClient> httpClientMock = new();
			HttpAgent agent = new(httpClientMock.Object);

			// Mock status response without development root key
			StatusResponse statusResponse = new(
				icApiVersion: null,
				implementationSource: null,
				implementationVersion: null,
				implementationRevision: null,
				developmentRootKey: null
			);

			byte[] responseBytes = [];
			HttpResponse httpResponse = CreateHttpResponse(HttpStatusCode.OK, responseBytes);

			httpClientMock.Setup(c => c.GetAsync(
				It.Is<string>(url => url.Contains("/api/v2/status")),
				It.IsAny<CancellationToken>()))
				.ReturnsAsync(httpResponse);

			// Act
			SubjectPublicKeyInfo result = await agent.GetRootKeyAsync();

			// Assert
			Assert.NotNull(result);
			// Should be main net key
			Assert.Equal(SubjectPublicKeyInfo.MainNetRootPublicKey.ToDerEncoding(), result.ToDerEncoding());
		}

		[Fact]
		public async Task CallAsynchronousAsync_SuccessfulResponse_ReturnsRequestId()
		{
			// Arrange
			Mock<IHttpClient> httpClientMock = new();
			HttpAgent agent = new(httpClientMock.Object);

			Principal sender = identity.GetPrincipal();
			ICTimestamp ingressExpiry = ICTimestamp.Future(TimeSpan.FromMinutes(1));
			CallRequest request = new(canisterId, "greet", CandidArg.FromCandid(), sender, ingressExpiry);
			var signedContent = identity.Sign(request);
			RequestId requestId = signedContent.GetOrBuildRequestId();

			HttpResponse httpResponse = CreateHttpResponse(HttpStatusCode.Accepted, Array.Empty<byte>());

			httpClientMock.Setup(c => c.PostAsync(
				It.Is<string>(url => url.Contains("/api/v2/canister/") && url.Contains("/call")),
				It.IsAny<byte[]>(),
				It.IsAny<CancellationToken>()))
				.ReturnsAsync(httpResponse);

			// Act
			var result = await agent.CallAsynchronousAsync(signedContent);

			// Assert
			Assert.Equal(requestId, result);
		}

		[Fact]
		public async Task CallAsynchronousAsync_RejectedResponse_ThrowsCallRejectedException()
		{
			// Arrange
			Mock<IHttpClient> httpClientMock = new();
			HttpAgent agent = new(httpClientMock.Object);

			Principal sender = identity.GetPrincipal();
			ICTimestamp ingressExpiry = ICTimestamp.Future(TimeSpan.FromMinutes(1));
			CallRequest request = new(canisterId, "greet", CandidArg.FromCandid(), sender, ingressExpiry);
			var signedContent = identity.Sign(request);


			byte[] responseBytes = [];
			HttpResponse httpResponse = CreateHttpResponse(HttpStatusCode.OK, responseBytes);

			httpClientMock.Setup(c => c.PostAsync(
				It.Is<string>(url => url.Contains("/api/v2/canister/") && url.Contains("/call")),
				It.IsAny<byte[]>(),
				It.IsAny<CancellationToken>()))
				.ReturnsAsync(httpResponse);

			// Act & Assert
			var exception = await Assert.ThrowsAsync<CallRejectedException>(
				async () => await agent.CallAsynchronousAsync(signedContent));

			Assert.Equal(RejectCode.CanisterError, exception.RejectCode);
			Assert.Equal("Test error message", exception.Message);
			Assert.Equal("TEST_ERROR", exception.ErrorCode);
		}

		#region Helper Methods

		private static HttpResponse CreateHttpResponse(HttpStatusCode statusCode, byte[] content)
		{
			return new HttpResponse(statusCode, () => Task.FromResult(content));
		}

		#endregion
	}
}
