using Kamada.Modules.Assistant.Domain.Settings;
using Microsoft.EntityFrameworkCore;

namespace Kamada.Modules.Assistant.Application.Abstractions;

internal interface IAssistantDbContext
{
    DbSet<AssistantSettings> Settings { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
