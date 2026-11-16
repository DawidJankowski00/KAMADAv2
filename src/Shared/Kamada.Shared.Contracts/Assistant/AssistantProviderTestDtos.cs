namespace Kamada.Shared.Contracts.Assistant;

/// <summary>Result of <c>POST /api/assistant/settings/test</c>: one short call of each saved provider.</summary>
/// <param name="Speech">Null when dictation is off.</param>
public sealed record AssistantProviderTestDto(ProviderTestResultDto Text, ProviderTestResultDto? Speech);

/// <param name="Provider">E.g. <c>Ollama</c>, <c>Gemini</c>, <c>Whisper</c>.</param>
/// <param name="Model">Model asked, or <c>null</c> for Whisper (its model is set in the configuration).</param>
/// <param name="ErrorCode">Error code of a failed call (e.g. <c>Assistant.ModelUnavailable</c>), for a Polish message.</param>
public sealed record ProviderTestResultDto(string Provider, string? Model, bool Success, long ElapsedMs, string? ErrorCode);
