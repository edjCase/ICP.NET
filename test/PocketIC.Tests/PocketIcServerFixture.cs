

using EdjCase.ICP.BLS;
using System;

namespace EdjCase.ICP.PocketIC.Tests;

public class PocketIcServerFixture : IDisposable
{
	public PocketIcServer Server { get; private set; }

	public PocketIcServerFixture()
	{
		// Start the server for all tests
		this.Server = PocketIcServer.StartAsync(runtimeLogLevel: LogLevel.Debug, showErrorLogs: true).GetAwaiter().GetResult();
		DefaultBlsCryptograhy.Bypass = true;
	}

	public void Dispose()
	{
		DefaultBlsCryptograhy.Bypass = false;
		// Stop the server after all tests
		if (this.Server != null)
		{
			this.Server.StopAsync().GetAwaiter().GetResult();
			this.Server.DisposeAsync().GetAwaiter().GetResult();
		}
	}
}
