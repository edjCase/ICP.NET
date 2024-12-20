

using System;

namespace EdjCase.ICP.PocketIC.Tests;

public class PocketIcServerFixture : IDisposable
{
	public PocketIcServer Server { get; private set; }

	public PocketIcServerFixture()
	{
		// Start the server for all tests
		this.Server = PocketIcServer.StartAsync(runtimeLogLevel: LogLevel.Debug, showErrorLogs: true).GetAwaiter().GetResult();
	}

	public void Dispose()
	{
		// Stop the server after all tests
		if (this.Server != null)
		{
			this.Server.StopAsync().GetAwaiter().GetResult();
			this.Server.DisposeAsync().GetAwaiter().GetResult();
		}
	}
}