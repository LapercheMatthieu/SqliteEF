using MatthL.SqliteEF.Core.Managers;
using MatthL.SqliteEF.Core.Repositories;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MatthL.SqliteEF.Demo2
{
    public class SpecialStorage : RootSqliteEFRepository
    {
        public SpecialStorage(SQLManager manager) : base(manager) { }

        public ObservableCollection<Pet> Pets { get; set; }
        public ObservableCollection<Address> Addresses { get; set; }
        public ObservableCollection<Person> Persons { get; set; }
    }
}
