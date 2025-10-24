using MatthL.SqliteEF.Core.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MatthL.SqliteEF.Demo.DBContexts
{
    public class PersonDBContext : RootDbContext
    {
        public PersonDBContext(string Path) : base(Path) { }

        public DbSet<Person> Persons { get; set; }
    }
}
