using Kamada.Shared.Kernel.Domain;

namespace Kamada.Modules.Workforce.Domain.Auth;

/// <summary>
/// One-time code that replaces an authenticator code when the phone is lost. Shown to the worker once; only a hash is
/// stored. A new set replaces the old one.
/// </summary>
internal sealed class RecoveryCode : Entity, IAuditable
{
    private RecoveryCode(Guid id, Guid workerId, string codeHash)
        : base(id)
    {
        WorkerId = workerId;
        CodeHash = codeHash;
    }

    public Guid WorkerId { get; private set; }

    public string CodeHash { get; private set; }

    public DateTimeOffset? UsedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    public static RecoveryCode Create(Guid workerId, string codeHash) => new(Guid.CreateVersion7(), workerId, codeHash);

    public void Use(DateTimeOffset now) => UsedAt ??= now;
}
