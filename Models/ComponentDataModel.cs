using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Web;

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