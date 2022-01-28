using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using Vaiona.Utils.Cfg;

namespace BExIS.Modules.Lui.UI.Helper
{
    public class DataAccessHelper
    {
        public static DataAccess ReadFile()
        {
            string filePath = Path.Combine(AppConfiguration.GetModuleWorkspacePath("LUI"), "Credentials.json");
            string text = System.IO.File.ReadAllText(filePath);
            DataAccess dataAccess = Newtonsoft.Json.JsonConvert.DeserializeObject<DataAccess>(text);

            return dataAccess;
        }
    }

    public class DataAccess
    {
        public string ServerName { get; set; }
        public string Token { get; set; }

    }

    public class DatasetObject
    {
        public string Id { get; set; }
        public string Version { get; set; }
        public string VersionId { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string DataStructureId { get; set; }
        public string MetadataStructureId { get; set; }
        public string AdditionalInformations { get; set; }
    }
}