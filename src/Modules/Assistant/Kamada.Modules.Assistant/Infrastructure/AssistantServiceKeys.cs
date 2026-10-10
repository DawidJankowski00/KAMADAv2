namespace Kamada.Modules.Assistant.Infrastructure;

internal static class AssistantServiceKeys
{
    /// <summary>Keyed <c>IChatClient</c> of this module, so other features can register their own chat clients.</summary>
    public const string ChatClient = "assistant:chat-client";
}
