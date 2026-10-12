namespace Kamada.Web.Shared.Assistant;

/// <summary>Which assistant the worker wants (the chip over the assistant panel).</summary>
public enum AssistantMode
{
    /// <summary>The server when the connection is good and it answers, the phone otherwise.</summary>
    Auto,

    /// <summary>Always the assistant on the server.</summary>
    Server,

    /// <summary>Always the model on the phone (nothing leaves the device).</summary>
    Device,
}

/// <summary>Where a draft was or will be made.</summary>
public enum AssistantLocation
{
    Server,
    Device,
}
