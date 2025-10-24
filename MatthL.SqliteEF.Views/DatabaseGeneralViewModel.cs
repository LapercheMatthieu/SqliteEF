using MatthL.SqliteEF.Core.Managers;
using MatthL.SqliteEF.Views.DatabaseCompactViews;
using MatthL.SqliteEF.Views.DatabaseDetailViews;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MatthL.SqliteEF.Views
{
    public class DatabaseGeneralViewModel : IDisposable
    {
        private readonly SQLManager _manager;
        
        public DatabaseGeneralViewModel GeneralVM { get; set; }
        public DatabaseDetailViewModel DetailVM { get; set; }
        public DatabaseCompactViewModel CompactVM { get; set; }
        public DatabaseGeneralViewModel(SQLManager Manager)
        {
            _manager = Manager;
            GeneralVM = new DatabaseGeneralViewModel(Manager);
            DetailVM = new DatabaseDetailViewModel(Manager);
            CompactVM = new DatabaseCompactViewModel(Manager);
        }
        public void Dispose()
        {
            GeneralVM.Dispose();
            DetailVM.Dispose();
            CompactVM.Dispose();
        }
    }
}
