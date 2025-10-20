using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MatthL.SqliteEF.Core.Tools
{
    public static class FileSizeToString
    {
        /// <summary>
        /// return the long value as a string type "8KB"
        /// </summary>
        /// <param name="size"></param>
        /// <returns></returns>
        public static string ToFileSizeString(this long size)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = size;
            int order = 0;

            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }

            return $"{len:0.##} {sizes[order]}";
        }
    }
}
