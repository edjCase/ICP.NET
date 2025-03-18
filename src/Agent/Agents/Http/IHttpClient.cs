using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace EdjCase.ICP.Agent.Agents.Http
{
	/// <summary>
	/// A simple http request interface for sending messages
	/// </summary>
	public interface IHttpClient
	{
		/// <summary>
		/// Sends a GET http request and awaits the response
		/// </summary>
		/// <param name="url">The url to send the GET request to</param>
		/// <param name="cancellationToken">Optional. Token to cancel waiting on the response</param>
		/// <returns>The http response</returns>
		Task<HttpResponse> GetAsync(string url, CancellationToken? cancellationToken = null);

		/// <summary>
		/// Sends a POST http request and awaits a response
		/// </summary>
		/// <param name="url">The url to send the POST request to</param>
		/// <param name="cborBody">The CBOR encoded body to send</param>
		/// <param name="cancellationToken">Optional. Token to cancel waiting on the response</param>
		/// <returns>The http response</returns>
		Task<HttpResponse> PostAsync(string url, byte[] cborBody, CancellationToken? cancellationToken = null);
	}

	/// <summary>
	/// A model holding the HTTP response info
	/// </summary>
	public class HttpResponse
	{
		private readonly Func<Task<byte[]>> getContentFunc;

		/// <summary>
		/// The HTTP status code
		/// </summary>
		public HttpStatusCode StatusCode { get; }

		/// <summary>
		/// Default constructor
		/// </summary>
		/// <param name="statusCode">The status code from the http response</param>
		/// <param name="getContentFunc">A func that returns the http response content</param>
		public HttpResponse(HttpStatusCode statusCode, Func<Task<byte[]>> getContentFunc)
		{
			this.StatusCode = statusCode;
			this.getContentFunc = getContentFunc ?? throw new ArgumentNullException(nameof(getContentFunc));
		}

		/// <summary>
		/// Returns the response content bytes
		/// </summary>
		/// <returns>Content bytes</returns>
		public async Task<byte[]> GetContentAsync()
		{
			return await this.getContentFunc();
		}

		/// <summary>
		/// Throws an exception if the status code is not 200-299, otherwise does nothing
		/// </summary>
		/// <returns></returns>
		public async ValueTask ThrowIfErrorAsync()
		{
			bool non2XXStatusCode = this.StatusCode >= HttpStatusCode.Ambiguous || this.StatusCode < HttpStatusCode.OK;
			if (non2XXStatusCode)
			{
				byte[] bytes = await this.GetContentAsync();
				string message = Encoding.UTF8.GetString(bytes);
				throw new Exception($"Response returned a failed status. HttpStatusCode={this.StatusCode} Message={message}");
			}
		}
	}
}
