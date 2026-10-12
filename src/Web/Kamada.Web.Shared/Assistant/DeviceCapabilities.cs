namespace Kamada.Web.Shared.Assistant;

/// <summary>What the phone's browser offers for running models (from <c>local-assistant.js</c>).</summary>
public sealed record DeviceCapabilities(
    bool WebGpu,
    bool ShaderF16,
    string? Adapter,
    double? DeviceMemoryGb,
    int? Cores,
    long? StorageQuotaBytes,
    long? StorageUsedBytes,
    bool? Persisted)
{
    public static DeviceCapabilities None { get; } = new(false, false, null, null, null, null, null, null);

    /// <summary>The language models are built for WebGPU with 16-bit shaders; without them there is nothing to run.</summary>
    public bool CanRunLanguageModel => WebGpu && ShaderF16;

    /// <summary>Roughly enough memory to keep both models loaded at once.</summary>
    public bool CanKeepBothModelsLoaded => DeviceMemoryGb >= 8;

    public long? StorageFreeBytes => StorageQuotaBytes is { } quota ? quota - (StorageUsedBytes ?? 0) : null;
}
