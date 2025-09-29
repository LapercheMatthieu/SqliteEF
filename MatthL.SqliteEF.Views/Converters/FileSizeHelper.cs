using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MatthL.SqliteEF.Views.Converters
{
    public class FileSizeHelper
    {
        private long _Size;

        public FileSizeHelper(long size)
        {
            _Size = size;
        }

        /// <summary>
        /// Retourne la taille du fichier formatée avec l'unité appropriée
        /// </summary>
        public string SizeString => GetFormattedSize(_Size);

        /// <summary>
        /// Convertit une taille en octets vers une chaîne formatée lisible
        /// </summary>
        /// <param name="bytes">Taille en octets</param>
        /// <returns>Chaîne formatée (ex: "1.5 Mo", "256 Ko")</returns>
        public static string GetFormattedSize(long bytes)
        {
            // Définition des unités et leurs seuils
            string[] units = { "o", "Ko", "Mo", "Go", "To", "Po" };
            double size = bytes;
            int unitIndex = 0;

            // Conversion vers l'unité appropriée
            while (size >= 1024 && unitIndex < units.Length - 1)
            {
                size /= 1024;
                unitIndex++;
            }

            // Formatage selon la taille
            if (unitIndex == 0) // Octets
            {
                return $"{bytes} {units[unitIndex]}";
            }
            else if (size >= 100) // Grands nombres : 0 décimale
            {
                return $"{size:F0} {units[unitIndex]}";
            }
            else if (size >= 10) // Nombres moyens : 1 décimale
            {
                return $"{size:F1} {units[unitIndex]}";
            }
            else // Petits nombres : 2 décimales
            {
                return $"{size:F2} {units[unitIndex]}";
            }
        }

        /// <summary>
        /// Version alternative avec IEC (Kio, Mio, Gio)
        /// </summary>
        public static string GetFormattedSizeIEC(long bytes)
        {
            string[] units = { "o", "Kio", "Mio", "Gio", "Tio", "Pio" };
            double size = bytes;
            int unitIndex = 0;

            while (size >= 1024 && unitIndex < units.Length - 1)
            {
                size /= 1024;
                unitIndex++;
            }

            if (unitIndex == 0)
            {
                return $"{bytes} {units[unitIndex]}";
            }
            else if (size >= 100)
            {
                return $"{size:F0} {units[unitIndex]}";
            }
            else if (size >= 10)
            {
                return $"{size:F1} {units[unitIndex]}";
            }
            else
            {
                return $"{size:F2} {units[unitIndex]}";
            }
        }

        /// <summary>
        /// Version avec culture française (virgule comme séparateur décimal)
        /// </summary>
        public static string GetFormattedSizeFR(long bytes)
        {
            var culture = new System.Globalization.CultureInfo("fr-FR");
            string[] units = { "o", "Ko", "Mo", "Go", "To", "Po" };
            double size = bytes;
            int unitIndex = 0;

            while (size >= 1024 && unitIndex < units.Length - 1)
            {
                size /= 1024;
                unitIndex++;
            }

            if (unitIndex == 0)
            {
                return $"{bytes} {units[unitIndex]}";
            }
            else if (size >= 100)
            {
                return string.Format(culture, "{0:F0} {1}", size, units[unitIndex]);
            }
            else if (size >= 10)
            {
                return string.Format(culture, "{0:F1} {1}", size, units[unitIndex]);
            }
            else
            {
                return string.Format(culture, "{0:F2} {1}", size, units[unitIndex]);
            }
        }
    }
}
