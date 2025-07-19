using DataReader.Datas.Acquisitions;
using DataReader.Datas.Base;
using DataReader.Datas.DataBlocks;
using DataReader.Datas.Operations;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataReader.Datas.SQLTable
{
    /// <summary>
    /// DbContext unique pour la création du schéma de la base de données
    /// </summary>
    public class DataReaderDbContext : DbContext
    {
        private readonly string _dbPath;

        public DataReaderDbContext(string dbPath)
        {
            _dbPath = dbPath;
        }

        // DbSets pour les entités de données
        public DbSet<AcquisitionGroup> AcquisitionGroups { get; set; }
        public DbSet<Sensor> Sensors { get; set; }
        public DbSet<Timeline> Timelines { get; set; }
        public DbSet<DataBlock> DataBlocks { get; set; }
        public DbSet<DataGroup> DataGroups { get; set; }
        public DbSet<SourceFileInfo> SourceFiles { get; set; }
        public DbSet<InputOutputSignal> InputOutputSignals { get; set; }

        // DbSets pour les entités d'opérations
        public DbSet<Operation> Operations { get; set; }
        public DbSet<OperationInput> OperationInputs { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite($"Data Source={_dbPath}");
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Appliquer toutes les configurations définies dans les classes dédiées
            modelBuilder.ApplyConfiguration(new SensorConfiguration());
            modelBuilder.ApplyConfiguration(new TimelineConfiguration());
            modelBuilder.ApplyConfiguration(new DataBlockConfiguration());
            modelBuilder.ApplyConfiguration(new DataGroupConfiguration());
            modelBuilder.ApplyConfiguration(new SourceFileInfoConfiguration());
            modelBuilder.ApplyConfiguration(new InputOutputSignalConfiguration());
            modelBuilder.ApplyConfiguration(new OperationConfiguration());
            modelBuilder.ApplyConfiguration(new OperationInputConfiguration());
            modelBuilder.ApplyConfiguration(new AcquisitionGroupConfiguration());

            base.OnModelCreating(modelBuilder);
        }

        public void EnsureDbCreated()
        {
            Database.EnsureDeleted();
            Database.EnsureCreated();
        }
    }
}
