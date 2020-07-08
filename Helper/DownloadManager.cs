using BExIS.IO.Transform.Output;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Web;
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

            if (value.IndexOfAny(AsciiHelper.GetAllSeperator().ToArray()) != -1)
            {
                value = "\"" + value.Replace("\"", "\"\"") + "\"";
            }
            return value;
        }


    }
}