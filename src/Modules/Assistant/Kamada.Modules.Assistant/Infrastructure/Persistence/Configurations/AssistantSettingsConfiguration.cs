using Kamada.Modules.Assistant.Domain.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kamada.Modules.Assistant.Infrastructure.Persistence.Configurations;

internal sealed class AssistantSettingsConfiguration : IEntityTypeConfiguration<AssistantSettings>
{
    public void Configure(EntityTypeBuilder<AssistantSettings> builder)
    {
        builder.ToTable("settings", t => t.HasCheckConstraint(
            "ck_settings_single_row", $"id = '{AssistantSettings.SingletonId}'"));
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.Property(s => s.TextProvider).HasConversion<string>().HasMaxLength(20);
        builder.Property(s => s.SpeechProvider).HasConversion<string>().HasMaxLength(20);
        builder.Property(s => s.OllamaModel).HasMaxLength(AssistantSettings.MaxModelLength);
        builder.Property(s => s.GeminiModel).HasMaxLength(AssistantSettings.MaxModelLength);
        builder.Ignore(s => s.DomainEvents);
    }
}
