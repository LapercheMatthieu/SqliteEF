using MatthL.SqliteEF.Core.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MatthL.SqliteEF.Demo.DBContexts
{
    public class Person : IBaseEntity
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public void ConfigureEntity(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Person>(builder =>
            {
                builder.ToTable("Persons");
                builder.HasKey(e => e.Id);
            });
                
        }
    }
}
