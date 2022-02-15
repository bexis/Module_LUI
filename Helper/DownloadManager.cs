using BExIS.Modules.Lui.UI.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Text;

using Vaiona.Utils.Cfg;

namespace BExIS.Modules.Lui.UI.Helper
{
    public class DownloadManager
    {
        private StringBuilder data;

        public string GenerateAsciiFile(string ns, DataTable table, string title, string mimeType)
        {
            data = new StringBuilder();
            string path = Path.Combine(AppConfiguration.DataPath, title + ".txt");

            AddHeader(table.Columns);

            int rowIndex = 0;
            if (rowIndex == 0) rowIndex = 1;

            // iterate over all input rows
            foreach (DataRow row in table.Rows)
            {
                // add row and increment current index
                if (AddRow(row, rowIndex))
                {
                    rowIndex += 1;
                }
            }

            File.WriteAllText(path, data.ToString());
            return path;
        }

        public string GenerateHtmlFile(string data, string filename)
        {
            string path = Path.Combine(AppConfiguration.DataPath, filename + ".html");
            File.WriteAllText(path, data.ToString());
            return path;
        }

        public string GernateMissingDataFile(List<MissingComponentData> missingComponentData, string filename)
        {
            StringBuilder missingData = new StringBuilder();
            string path = Path.Combine(AppConfiguration.DataPath, filename + ".txt");
            foreach(var m in missingComponentData)
            {
                missingData.AppendLine(m.Year);
                foreach(var p in m.PlotIds)
                {
                    missingData.AppendLine(p);
                }
            }

            File.WriteAllText(path, missingData.ToString());
            return path;
        }

        protected bool AddRow(DataRow row, long rowIndex)
        {
            // number of columns
            int colCount = row.Table.Columns.Count;
            // content of one line
            string[] line = new string[colCount];

            // append contents
            for (int i = 0; i < colCount; i++)
            {
                // get value as string
                string value = row[i].ToString();
                // add value to row
                line[i] = escapeValue(value);
            }

            // Add to result
            data.AppendLine(String.Join(",", line));

            

            return true;
        }


        protected bool AddHeader(DataColumnCollection columns)
        {
            // number of columns
            int colCount = columns.Count;
            // content of one line
            string[] line = new string[colCount];

            // append header
            for (int i = 0; i < colCount; i++)
            {
                line[i] = escapeValue(columns[i].Caption);
            }
            data.AppendLine(String.Join(",", line));

            return true;
        }

        private string escapeValue(string value)
        {
            // modify if special characters are present

            if (value.IndexOfAny(GetAllSeperator().ToArray()) != -1)
            {
                value = "\"" + value.Replace("\"", "\"\"") + "\"";
            }
            return value;
        }

        /// <summary>
        /// Get all text seperators as char in a list
        /// </summary>
        /// <returns>List of char </returns>
        public static List<char> GetAllSeperator()
        {
            List<char> allSeperatorsAsChar = new List<char>();

            allSeperatorsAsChar.Add(',');
            allSeperatorsAsChar.Add(';');
            allSeperatorsAsChar.Add(' ');
            allSeperatorsAsChar.Add('\t');

            return allSeperatorsAsChar;
        }

    }
}