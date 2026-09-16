using ComposeLab.Api.Domain;
using ComposeLab.Api.Domain.Topology.Document;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ComposeLab.Api.Infrastructure.Persistence.Configurations;

internal sealed class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.ToTable("projects");

        builder.HasKey(project => project.Id);

        builder.Property(project => project.Name)
            .IsRequired()
            .HasMaxLength(120);

        // The document is one opaque jsonb value. A converter keeps the domain contract intact instead of
        // reshaping it so EF can model every nested property.
        builder.Property(project => project.Topology)
            .HasColumnType("jsonb")
            .IsRequired()
            .HasConversion(
                document => TopologyDocumentSerializer.Serialize(document),
                json => TopologyDocumentSerializer.Deserialize(json),
                new ValueComparer<TopologyDocument>(
                    (left, right) => Equivalent(left, right),
                    document => TopologyDocumentSerializer.Serialize(document).GetHashCode(
                        StringComparison.Ordinal),
                    document => TopologyDocumentSerializer.Deserialize(
                        TopologyDocumentSerializer.Serialize(document))));

        // A normal column beside the document, so a read path can check the contract version before trusting
        // the shape rather than after misreading it.
        builder.Property(project => project.TopologySchemaVersion)
            .IsRequired();

        builder.Property(project => project.CreatedAt).IsRequired();
        builder.Property(project => project.UpdatedAt).IsRequired();

        // Listing projects orders by most recently changed, and never reads the documents.
        builder.HasIndex(project => project.UpdatedAt);
    }

    private static bool Equivalent(TopologyDocument? left, TopologyDocument? right)
    {
        if (left is null || right is null)
        {
            return ReferenceEquals(left, right);
        }

        return string.Equals(
            TopologyDocumentSerializer.Serialize(left),
            TopologyDocumentSerializer.Serialize(right),
            StringComparison.Ordinal);
    }
}
