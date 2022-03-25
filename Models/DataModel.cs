using System;
using System.Collections.Generic;
using System.Data;


namespace BExIS.Modules.Lui.UI.Models
{
    public class DataModel
    {
         public DataTable Data { get; set; }

        public DataModel()
        {
            Data = new DataTable();
        }
    }
}