using CommunityToolkit.Mvvm.DependencyInjection;
using NexusAIO.Core.Database.Tools;
using NexusAIO.Features.DatabaseGroup.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;

namespace MatthL.SqliteEF.Views.Databases.DetailViews
{
    public class StatistiquesView : Grid
    {
        private DatabaseInfoCollector _collector ;
        private DatabaseCoordinator _coordinator ;

        public TextBox MyRichTextBox;


        public StatistiquesView()
        {
            _collector = Ioc.Default.GetService<DatabaseInfoCollector>();
            _coordinator = Ioc.Default.GetService<DatabaseCoordinator>();

            MyRichTextBox = new TextBox();
            Children.Add(MyRichTextBox);

           //WriteStatistiques();
        }


        public void WriteStatistiques()
        {
            foreach (var tableinfo in _collector.TableInformations)
            {
                string newline = tableinfo.Name.ToString();
                newline += tableinfo.Dependancies.ToString();
                newline += tableinfo.DependancyCount.ToString();
                newline += tableinfo.Schema.ToString();
                AddLigne(newline);
            }
        }
        private void AddLigne(string line)
        {
            MyRichTextBox.Text += line + "/n";
        }


    }
}
