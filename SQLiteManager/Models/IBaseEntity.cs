using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace SQLiteManager.Models
{
    public interface IBaseEntity
    {
        public int Id { get; set; }
        void ConfigureEntity(ModelBuilder modelBuilder);
    }
}
