using MatthL.SqliteEF.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace MatthL.SqliteEF.Core.Models
{
    /// <summary>
    /// The base class for all entities inside a db 
    /// modelBuilder.Entity<TestEntity>(builder =>
    /// builder.ToTable("TestEntities");
    ///        builder.HasKey(e => e.Id);
    /// </summary>
    public interface IBaseEntity
    {
        public int Id { get; set; }
        void ConfigureEntity(ModelBuilder modelBuilder);

        /*            
        modelBuilder.Entity<TestEntity>(builder =>
            {
                builder.ToTable("TestEntities");
                builder.HasKey(e => e.Id);

            });
        */
    }
}
