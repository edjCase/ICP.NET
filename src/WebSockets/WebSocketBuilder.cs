using EdjCase.ICP.Agent;
using EdjCase.ICP.Agent.Identities;
using EdjCase.ICP.BLS;
using EdjCase.ICP.Candid;
using EdjCase.ICP.Candid.Models;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace EdjCase.ICP.WebSockets
{
	/// <summary>
	/// Represents a builder class for creating WebSocket agents that communicate with a specified canister using the Internet Computer Protocol (ICP).
	/// </summary>
	/// <typeparam name="TMessage">The type of messages exchanged with the WebSocket agent</typeparam>
	public class WebSocketBuilder<TMessage>
			where TMessage : notnull
	{
		private Principal canisterId { get; }
		private Uri gatewayUri { get; }
		private IIdentity? identity { get; set; }
		private IWebSocketClient? client { get; set; }
		private SubjectPublicKeyInfo? rootPublicKey { get; set; }
		private CandidConverter? customConverter { get; set; }
		private Action<TMessage>? onMessage { get; set; }
		private Action? onOpen { get; set; }
		private Action? onClose { get; set; }
		private Action<Exception>? onError { get; set; }


		/// <summary>
		/// Initializes a new instance of the WebSocketBuilder class.
		/// </summary>
		/// <param name="canisterId">The principal canister id to send messages to</param>
		/// <param name="gatewayUri">The URI of the websocket gateway</param>
		public WebSocketBuilder(
			Principal canisterId,
			Uri gatewayUri
		)
		{
			this.canisterId = canisterId;
			this.gatewayUri = gatewayUri;
		}

		/// <summary>
		/// Sets the action to be executed when the WebSocket connection is opened.
		/// </summary>
		/// <param name="onOpen">The action to be executed.</param>
		/// <returns>The WebSocketBuilder instance.</returns>
		public WebSocketBuilder<TMessage> OnOpen(Action onOpen)
		{
			this.onOpen = onOpen;
			return this;
		}

		/// <summary>
		/// Sets the callback action to be executed when a message is received.
		/// </summary>
		/// <param name="onMessage">The callback action to be executed.</param>
		/// <returns>The WebSocketBuilder instance.</returns>
		public WebSocketBuilder<TMessage> OnMessage(Action<TMessage> onMessage)
		{
			this.onMessage = onMessage;
			return this;
		}

		/// <summary>
		/// Sets the error handler for the WebSocketBuilder.
		/// </summary>
		/// <param name="onError">The action to be executed when an error occurs.</param>
		/// <returns>The WebSocketBuilder instance.</returns>
		public WebSocketBuilder<TMessage> OnError(Action<Exception> onError)
		{
			this.onError = onError;
			return this;
		}

		/// <summary>
		/// Sets the action to be executed when the WebSocket connection is closed.
		/// </summary>
		/// <param name="onClose">The action to be executed.</param>
		/// <returns>The WebSocketBuilder instance.</returns>
		public WebSocketBuilder<TMessage> OnClose(Action onClose)
		{
			this.onClose = onClose;
			return this;
		}


		/// <summary>
		/// Sets the network root key for signature verification. Development networks have different
		/// root keys than mainnet. If not specified, the mainnet root key is used.
		/// </summary>
		/// <param name="derEncodedRootKey">The DER-encoded root key.</param>
		/// <returns>The WebSocketBuilder instance.</returns>
		public WebSocketBuilder<TMessage> WithRootKey(byte[] derEncodedRootKey)
		{
			this.rootPublicKey = SubjectPublicKeyInfo.FromDerEncoding(derEncodedRootKey);
			return this;
		}

		/// <summary>
		/// Sets the network root key for signature verification. Development networks have different
		/// root keys than mainnet. If not specified, the mainnet root key is used.
		/// </summary>
		/// <param name="subjectPublicKeyInfo">The key info of the root key.</param>
		/// <returns>The WebSocketBuilder instance.</returns>
		public WebSocketBuilder<TMessage> WithRootKey(SubjectPublicKeyInfo subjectPublicKeyInfo)
		{
			this.rootPublicKey = subjectPublicKeyInfo;
			return this;
		}

		/// <summary>
		/// Sets the identity for the WebSocket connection.
		/// </summary>
		/// <param name="identity">The identity to set.</param>
		/// <returns>The WebSocketBuilder instance.</returns>
		public WebSocketBuilder<TMessage> WithIdentity(IIdentity identity)
		{
			this.identity = identity;
			return this;
		}

		/// <summary>
		/// Sets a custom CandidConverter for the `TMessage` candid conversion to override the
		/// default implemenation.
		/// </summary>
		/// <param name="customConverter">The custom CandidConverter to use.</param>
		/// <returns>The WebSocketBuilder instance.</returns>
		public WebSocketBuilder<TMessage> WithCustomCandidConverter(CandidConverter customConverter)
		{
			this.customConverter = customConverter;
			return this;
		}

		/// <summary>
		/// Sets a custom websocket client implementation to override the default.
		/// </summary>
		/// <param name="client">The custom websocket client implementation.</param>
		/// <returns>The WebSocketBuilder instance.</returns>
		public WebSocketBuilder<TMessage> WithCustomClient(IWebSocketClient client)
		{
			this.client = client;
			return this;
		}

		/// <summary>
		/// Builds the WebSocket agent from the specified configuration.
		/// Will NOT connect the agent to the WebSocket gateway
		/// </summary>
		/// <returns>The WebSocket agent.</returns>
		/// <exception cref="InvalidOperationException">Thrown if the OnMessage action is not specified.</exception>
		public IWebSocketAgent<TMessage> Build()
		{
			if (this.onMessage == null)
			{
				throw new InvalidOperationException("Web socket requires an OnMessage action");
			}
			if (this.identity == null)
			{
				// Generate ephemral identity if not specified
				this.identity = Ed25519Identity.Generate();
			}
			if (this.rootPublicKey == null)
			{
				this.rootPublicKey = SubjectPublicKeyInfo.MainNetRootPublicKey;
			}
			if (this.client == null)
			{
				this.client = new WebSocketClient();
			}
			return new WebSocketAgent<TMessage>(
				this.canisterId,
				this.gatewayUri,
				this.rootPublicKey,
				this.identity,
				this.client,
				this.onMessage,
				this.onOpen,
				this.onError,
				this.onClose,
				this.customConverter
			);
		}

		/// <summary>
		/// Builds the WebSocket agent from the specified configuration and then connects the agent to the WebSocket gateway.
		/// </summary>
		/// <returns>The WebSocket agent.</returns>
		/// <exception cref="InvalidOperationException">Thrown if the OnMessage action is not specified.</exception>
		public async Task<IWebSocketAgent<TMessage>> BuildAndConnectAsync(CancellationToken? cancellationToken = null)
		{
			IWebSocketAgent<TMessage> agent = this.Build();
			await agent.ConnectAsync(cancellationToken);
			return agent;
		}

	}
}
