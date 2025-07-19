using DataReader.Datas.Operations;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataReader.Datas.SQLTable
{
    public class OperationConfiguration : IEntityTypeConfiguration<Operation>
    {
        public void Configure(EntityTypeBuilder<Operation> builder)
        {
            builder.ToTable("Operations");
            builder.HasKey(e => e.Id);
            builder.Property(e => e.Name).IsRequired();
            builder.Property(e => e.OperationType).IsRequired();

            // Configuration de la relation one-to-many avec OperationInput
            builder.HasMany(e => e.Inputs)
                   .WithOne(i => i.Operation)
                   .HasForeignKey(i => i.OperationId)
                   .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(e => e.OutputTo)
                   .WithOne()
                   .HasForeignKey(i => i.SourceId)
                   .HasPrincipalKey(e => e.Id)
                   .OnDelete(DeleteBehavior.Restrict);
        }
    }

    public class OperationInputConfiguration : IEntityTypeConfiguration<OperationInput>
    {
        public void Configure(EntityTypeBuilder<OperationInput> builder)
        {
            builder.ToTable("OperationInputs");
            builder.HasKey(e => e.Id);
            builder.Property(e => e.InputName).IsRequired();
            builder.Property(e => e.SourceType).IsRequired();

            // Ajout d'un index sur les colonnes souvent utilisées pour les recherches
            builder.HasIndex(e => new { e.OperationId, e.SourceId, e.SourceType });
        }
    }
}
