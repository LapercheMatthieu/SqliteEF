using CommunityToolkit.Mvvm.ComponentModel;
using MatthL.SqliteEF.Core.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace MatthL.SqliteEF.Demo2
{

    public partial class Person : ObservableObject, IBaseEntity
    {
        [Key]
        public int Id { get; set; }
        [ObservableProperty] private string _FirstName;
        [ObservableProperty] private string _LastName;

        public virtual List<Address> Addresses { get; set; }
        public virtual List<Pet> Pets { get; set; }
        // CONFIGURATION
        public void ConfigureEntity(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Person>(builder =>
            {
                builder.ToTable("Persons");
                builder.HasKey(e => e.Id);


                // Configuration de la relation avec DataBlocks
                builder.HasMany(e => e.Addresses)
                       .WithOne(d => d.Person)
                       .HasForeignKey(d => d.PersonId)
                       .IsRequired(false)
                       .OnDelete(DeleteBehavior.Cascade);

                builder.HasMany(e => e.Pets)
                       .WithOne(d => d.Person)
                       .HasForeignKey(d => d.PersonId)
                       .IsRequired(false)
                       .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
    public partial class Address : ObservableObject, IBaseEntity
    {
        [Key]
        public int Id { get; set; }
        [ObservableProperty] private string _street;
        [ObservableProperty] private string _city;
        public int? PersonId { get; set; }
        [ForeignKey("PersonId")] public Person? Person { get; set; }


        // CONFIGURATION
        public void ConfigureEntity(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Address>(builder =>
            {
                builder.ToTable("Addresses");
                builder.HasKey(e => e.Id);


                // Configuration de la relation avec DataBlocks
                builder.HasOne(e => e.Person)
                       .WithMany()
                       .HasForeignKey(d => d.PersonId)
                       .IsRequired(false)
                       .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
    public partial class Pet : ObservableObject, IBaseEntity
    {
        [Key]
        public int Id { get; set; }
        [ObservableProperty] private string _FirstName;
        [ObservableProperty] private string _LastName;
        public int? PersonId { get; set; }
        [ForeignKey("PersonId")] public Person? Person { get; set; }

        // CONFIGURATION
        public void ConfigureEntity(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Pet>(builder =>
            {
                builder.ToTable("Pets");
                builder.HasKey(e => e.Id);


                // Configuration de la relation avec DataBlocks
                builder.HasOne(e => e.Person)
                       .WithMany()
                       .HasForeignKey(d => d.PersonId)
                       .IsRequired(false)
                       .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
