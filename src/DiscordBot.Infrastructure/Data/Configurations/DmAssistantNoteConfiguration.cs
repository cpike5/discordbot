using DiscordBot.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DiscordBot.Infrastructure.Data.Configurations;

public class DmAssistantNoteConfiguration : IEntityTypeConfiguration<DmAssistantNote>
{
    public void Configure(EntityTypeBuilder<DmAssistantNote> builder)
    {
        builder.ToTable("DmAssistantNotes");

        builder.HasKey(n => n.Id);
        builder.Property(n => n.Id).ValueGeneratedOnAdd();

        builder.Property(n => n.UserId)
            .HasConversion<long>()
            .IsRequired();

        builder.Property(n => n.Tag)
            .HasMaxLength(100);

        builder.Property(n => n.Content)
            .IsRequired()
            .HasMaxLength(4096);

        builder.Property(n => n.CreatedAt)
            .IsRequired();

        builder.Property(n => n.UpdatedAt)
            .IsRequired();

        builder.HasIndex(n => new { n.UserId, n.Tag })
            .HasDatabaseName("IX_DmAssistantNotes_UserId_Tag");

        builder.HasOne(n => n.User)
            .WithMany()
            .HasForeignKey(n => n.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
