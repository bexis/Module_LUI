using System.Data;

namespace BExIS.Modules.Lui.UI.Models
{
    public class ComponentDataModel
    {
        public DataTable Data { get; set; }

        public ComponentDataModel()
        {
            Data = new DataTable();
        }
        public ComponentDataModel(DataTable data)
        {
            Data = data;
        }

    }
}