using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Web;

namespace BExIS.Modules.Lui.UI.Models
{
    public class ComponentDataModel
    {
         public DataTable ComponentData { get; set; }

        public ComponentDataModel()
        {
            ComponentData = new DataTable();
        }
    }
}