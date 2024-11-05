using EdjCase.ICP.Candid.Mapping;
using EdjCase.ICP.Candid.Models;

namespace EdjCase.ICP.PocketIC.Models;

/// <summary>
/// Request model for creating a new canister with optional settings
/// </summary>
internal class CreateCanisterRequest
{
	/// <summary>
	/// Optional canister settings to configure the new canister with
	/// </summary>
	[CandidName("settings")]
	public OptionalValue<CanisterSettings> Settings { get; set; } = OptionalValue<CanisterSettings>.NoValue();

	/// <summary>
	/// Optional amount of cycles to add to the new canister
	/// </summary>
	[CandidName("amount")]
	public OptionalValue<UnboundedUInt> Amount { get; set; } = OptionalValue<UnboundedUInt>.NoValue();

	/// <summary>
	/// Optional specific canister ID to create the canister with
	/// </summary>
	[CandidName("specified_id")]
	public OptionalValue<Principal> SpecifiedId { get; set; } = OptionalValue<Principal>.NoValue();
}

/// <summary>
/// Configuration settings for a canister
/// </summary>
public class CanisterSettings
{
	/// <summary>
	/// Optional list of principal IDs that can control this canister
	/// </summary>
	[CandidName("controllers")]
	public OptionalValue<List<Principal>> Controllers { get; set; } = OptionalValue<List<Principal>>.NoValue();

	/// <summary>
	/// Optional compute allocation in percentage of subnet capacity
	/// </summary>
	[CandidName("compute_allocation")]
	public OptionalValue<UnboundedUInt> ComputeAllocation { get; set; } = OptionalValue<UnboundedUInt>.NoValue();

	/// <summary>
	/// Optional memory allocation in bytes
	/// </summary>
	[CandidName("memory_allocation")]
	public OptionalValue<UnboundedUInt> MemoryAllocation { get; set; } = OptionalValue<UnboundedUInt>.NoValue();

	/// <summary>
	/// Optional freezing threshold in seconds
	/// </summary>
	[CandidName("freezing_threshold")]
	public OptionalValue<UnboundedUInt> FreezingThreshold { get; set; } = OptionalValue<UnboundedUInt>.NoValue();

	/// <summary>
	/// Optional reserved cycles limit in cycles
	/// </summary>
	[CandidName("reserved_cycles_limit")]
	public OptionalValue<UnboundedUInt> ReservedCyclesLimit { get; set; } = OptionalValue<UnboundedUInt>.NoValue();
}

/// <summary>
/// Request model for starting a canister
/// </summary>
internal class StartCanisterRequest
{
	/// <summary>
	/// The ID of the canister to start
	/// </summary>
	[CandidName("canister_id")]
	public required Principal CanisterId { get; set; }
}

/// <summary>
/// Request model for stopping a canister
/// </summary>
internal class StopCanisterRequest
{
	/// <summary>
	/// The ID of the canister to stop
	/// </summary>
	[CandidName("canister_id")]
	public required Principal CanisterId { get; set; }
}

/// <summary>
/// Request model for installing code on a canister
/// </summary>
internal class InstallCodeRequest
{
	/// <summary>
	/// The ID of the target canister 
	/// </summary>
	[CandidName("canister_id")]
	public required Principal CanisterId { get; set; }

	/// <summary>
	/// The initialization/upgrade arguments in raw bytes
	/// </summary>
	[CandidName("arg")]
	public required byte[] Arg { get; set; }

	/// <summary>
	/// The WASM module bytes to install
	/// </summary>
	[CandidName("wasm_module")]
	public required byte[] WasmModule { get; set; }

	/// <summary>
	/// The installation mode (install, reinstall, or upgrade)
	/// </summary>
	[CandidName("mode")]
	public required InstallCodeMode Mode { get; set; }
}

/// <summary>
/// The mode for installing code on a canister
/// </summary>
public enum InstallCodeMode
{
	/// <summary>
	/// Install new code on an empty canister
	/// </summary>
	[CandidName("install")]
	Install,

	/// <summary>
	/// Replace existing code and clear state
	/// </summary>
	[CandidName("reinstall")]
	Reinstall,

	/// <summary>
	/// Upgrade existing code while preserving state
	/// </summary>
	[CandidName("upgrade")]
	Upgrade
}

/// <summary>
/// Request model for updating canister settings
/// </summary>
internal class UpdateCanisterSettingsRequest
{
	/// <summary>
	/// The ID of the canister to update
	/// </summary>
	[CandidName("canister_id")]
	public required Principal CanisterId { get; set; }

	/// <summary>
	/// The new settings to apply to the canister
	/// </summary>
	[CandidName("settings")]
	public required CanisterSettings Settings { get; set; }
}