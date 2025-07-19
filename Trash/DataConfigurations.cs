using DataReader.Datas.Acquisitions;
using DataReader.Datas.Base;
using DataReader.Datas.DataBlocks;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataReader.Datas.SQLTable
{
    public class SensorConfiguration : IEntityTypeConfiguration<Sensor>
    {
        public void Configure(EntityTypeBuilder<Sensor> builder)
        {
            builder.ToTable("Sensors");
            builder.HasKey(e => e.Id);
            builder.Property(e => e.Name).IsRequired();

            // Relation avec DataGroup
            builder.HasOne(e => e.Datas)
                   .WithMany()
                   .HasForeignKey(e => e.DataGroupId)
                   .OnDelete(DeleteBehavior.Cascade);

            // Relation avec Timeline
            builder.HasOne(e => e.Timeline)
                   .WithMany()
                   .HasForeignKey(e => e.TimelineId)
                   .OnDelete(DeleteBehavior.SetNull);

            // Relations avec InputOutputSignal
            builder.HasOne(e => e.PhysicalSignal)
                   .WithMany()
                   .HasForeignKey(e => e.PhysicalSignalId)
                   .OnDelete(DeleteBehavior.SetNull);

            builder.HasOne(e => e.ElectricalSignal)
                   .WithMany()
                   .HasForeignKey(e => e.ElectricalSignalId)
                   .OnDelete(DeleteBehavior.SetNull);
        }
    }

    public class TimelineConfiguration : IEntityTypeConfiguration<Timeline>
    {
        public void Configure(EntityTypeBuilder<Timeline> builder)
        {
            builder.ToTable("Timelines");
            builder.HasKey(e => e.Id);
            builder.Property(e => e.Name).IsRequired();
        }
    }

    public class DataBlockConfiguration : IEntityTypeConfiguration<DataBlock>
    {
        public void Configure(EntityTypeBuilder<DataBlock> builder)
        {
            builder.ToTable("DataBlocks");
            builder.HasKey(e => e.Id);
            builder.Property(e => e.Name).IsRequired();

            // Relation avec DataGroup
            builder.HasOne(e => e.DataGroup)
                   .WithMany(g => g.DataBlocks)
                   .HasForeignKey(e => e.DataGroupId)
                   .OnDelete(DeleteBehavior.Cascade);

            // Relation avec SourceFile
            builder.HasOne(e => e.SourceFile)
                   .WithMany()
                   .HasForeignKey(e => e.SourceFileId)
                   .OnDelete(DeleteBehavior.SetNull);
        }
    }

    public class DataGroupConfiguration : IEntityTypeConfiguration<DataGroup>
    {
        public void Configure(EntityTypeBuilder<DataGroup> builder)
        {
            builder.ToTable("DataGroups");
            builder.HasKey(e => e.Id);
            builder.Property(e => e.Name).IsRequired();
        }
    }

    public class SourceFileInfoConfiguration : IEntityTypeConfiguration<SourceFileInfo>
    {
        public void Configure(EntityTypeBuilder<SourceFileInfo> builder)
        {
            builder.ToTable("SourceFiles");
            builder.HasKey(e => e.Id);
            builder.Property(e => e.FileName).IsRequired();
        }
    }

    public class InputOutputSignalConfiguration : IEntityTypeConfiguration<InputOutputSignal>
    {
        public void Configure(EntityTypeBuilder<InputOutputSignal> builder)
        {
            builder.ToTable("InputOutputSignals");
            builder.HasKey(e => e.Id);

            // Configuration pour gérer BaseUnit
            builder.Ignore(e => e.PhysicalUnit);
        }
    }

    public class AcquisitionGroupConfiguration : IEntityTypeConfiguration<AcquisitionGroup>
    {
        public void Configure(EntityTypeBuilder<AcquisitionGroup> builder)
        {
            builder.ToTable("AcquisitionGroups");
            builder.HasKey(e => e.Id);
            builder.Property(e => e.Name).IsRequired();
            builder.Property(e => e.Description).IsRequired(false);

            // Relation avec Sensors (Many-to-Many)
            builder.HasMany(e => e.Sensors)
                   .WithMany()
                   .UsingEntity(j => j.ToTable("AcquisitionGroupSensors"));

            // Relation avec Timeline (Many-to-Many)
            builder.HasMany(e => e.Timeline)
                   .WithMany()
                   .UsingEntity(j => j.ToTable("AcquisitionGroupTimelines"));

            // Relation avec SourceFiles (Many-to-Many)
            builder.HasMany(e => e.SourceFiles)
                   .WithMany()
                   .UsingEntity(j => j.ToTable("AcquisitionGroupSourceFiles"));
        }
    }
}
