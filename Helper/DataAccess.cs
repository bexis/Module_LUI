using BExIS.Modules.Lui.UI.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Script.Serialization;
using Vaiona.Utils.Cfg;

namespace BExIS.Modules.Lui.UI.Helper
{
    public class DataAccess
    {
        /// <summary>
        /// Get server information form json file in workspace
        /// </summary>
        /// <returns></returns>
        public static ServerInformation GetServerInformation()
        {
            string filePath = Path.Combine(AppConfiguration.GetModuleWorkspacePath("LUI"), "Credentials.json");
            string text = System.IO.File.ReadAllText(filePath);
            ServerInformation serverInformation = Newtonsoft.Json.JsonConvert.DeserializeObject<ServerInformation>(text);

            return serverInformation;
        }

        /// <summary>
        /// Get comp data
        /// 
        /// </summary>
        /// <param name="datasetId"></param>
        /// <returns>Data table with comp dataset depents on dataset id.</returns>
        public static DataTable GetComponentData(string datasetId)
        {
            ServerInformation serverInformation = GetServerInformation();

            string link = serverInformation.ServerName + "/api/data/" + datasetId;
            HttpWebRequest request = WebRequest.Create(link) as HttpWebRequest;
            request.Headers.Add("Authorization", "Bearer " + serverInformation.Token);

            DataTable compData = new DataTable();
            DataColumn year = new DataColumn("Year");
            year.DataType = System.Type.GetType("System.DateTime");
            compData.Columns.Add(year);
            compData.Columns.Add("Exploratory");
            compData.Columns.Add("EP_PlotID");
            compData.Columns.Add("isVIP");
            compData.Columns.Add("isMIP");
            DataColumn totalGrazing = new DataColumn("TotalGrazing");
            totalGrazing.DataType = System.Type.GetType("System.Decimal");
            compData.Columns.Add(totalGrazing);
            DataColumn totalMowing = new DataColumn("TotalMowing");
            totalMowing.DataType = System.Type.GetType("System.Decimal");
            compData.Columns.Add(totalMowing);
            DataColumn totalFertilization = new DataColumn("TotalFertilization");
            totalFertilization.DataType = System.Type.GetType("System.Decimal");
            compData.Columns.Add(totalFertilization);

            try
            {
                // Get response  
                using (HttpWebResponse response = request.GetResponse() as HttpWebResponse)
                {
                    // Get the response stream  
                    using (StreamReader reader = new StreamReader(response.GetResponseStream()))
                    {
                        string line = String.Empty;
                        string sep = "\t";
                        String[] row = new String[4];
                        int count = 0;
                        while ((line = reader.ReadLine()) != null)
                        {
                            count++;
                            if (count > 1)
                            {
                                row = line.Split(',');
                                DataRow dr = compData.NewRow();
                                //dr["Year"] = DateTime.Parse(row[0]).ToString("yyyy");
                                dr["Year"] = row[0];
                                dr["Exploratory"] = row[1];
                                dr["EP_PlotID"] = row[2];
                                dr["isVIP"] = row[3];
                                dr["isMIP"] = row[4];
                                dr["TotalGrazing"] = row[5];
                                dr["TotalMowing"] = row[6];
                                dr["TotalFertilization"] = row[7];
                                compData.Rows.Add(dr);
                            }
                        }
                        response.Close();
                    }
                }
            }
            catch (Exception e)
            {

            }

            return compData;
        }

        /// <summary>
        /// Get missing comp data
        /// 
        /// </summary>
        /// <returns>List of missing component data. Years with missing ep plotids.</returns>
        public static List<MissingComponentData> GetMissingComponentData()
        {
            List<MissingComponentData> data = new List<MissingComponentData>();
            string datasetId = Models.Settings.get("lui:datasetNewComponentsSet").ToString();
            DataTable compData = GetComponentData(datasetId);

            //get all years where data rows less then 50, that means not all plots has data
            var years = compData.AsEnumerable().GroupBy(x => x.Field<DateTime>("Year")).Where(g => g.Count() < 50).ToList();

            foreach (var i in years)
            {
                MissingComponentData missingComponentData = new MissingComponentData();
                missingComponentData.Year = i.Select(a => a.Field<DateTime>("Year")).FirstOrDefault().ToString("yyyy");
                List<string> availablePlots = compData.AsEnumerable().Where(x => x.Field<DateTime>("Year").ToString("yyyy") == missingComponentData.Year).Select(a => a.Field<string>("EP_PlotID")).ToList();
                missingComponentData.PlotIds = getAllGrasslandPlots().Except(availablePlots).ToList();
                data.Add(missingComponentData);
            }

            return data;
        }

        /// <summary>
        /// Get information abbout dataset
        /// 
        /// </summary>
        /// <returns>Information like version, title etc</returns>
        public static DatasetObject GetDatasetInfo(string datasetId)
        {
            ServerInformation serverInformation =  GetServerInformation();
            string link = serverInformation.ServerName + "/api/dataset/" + datasetId;
            HttpWebRequest request = WebRequest.Create(link) as HttpWebRequest;
            request.Headers.Add("Authorization", "Bearer " + serverInformation.Token);

            DatasetObject datasetObject = new DatasetObject();

            try
            {
                // Get response  
                using (HttpWebResponse response = request.GetResponse() as HttpWebResponse)
                {
                    using (StreamReader reader = new StreamReader(response.GetResponseStream()))
                    {
                        JavaScriptSerializer js = new JavaScriptSerializer();
                        var objText = reader.ReadToEnd();
                        datasetObject = (DatasetObject)js.Deserialize(objText, typeof(DatasetObject));
                    }
                }
            }
            catch (Exception e)
            {
                string error = "Not data" + e.InnerException;
            }

            return datasetObject;
        }


        /// <summary>
        /// get all ep plot ids from grasland plots
        /// 
        /// </summary>
        /// <returns>list of grasland ep plot ids</returns>
        public static List<string> getAllGrasslandPlots()
        {
            ServerInformation serverInformation = GetServerInformation();
            string datasetId = Models.Settings.get("lui:epPlotsDataset").ToString();

            string link = serverInformation.ServerName + "/api/data/" + datasetId;
            HttpWebRequest request = WebRequest.Create(link) as HttpWebRequest;
            //request.PreAuthenticate = true;
            request.Headers.Add("Authorization", "Bearer " + serverInformation.Token);

            DataTable epPlotTable = new DataTable();
            epPlotTable.Columns.Add("EP_Plotid");
            epPlotTable.Columns.Add("LANDUSE");

            try
            {
                // Get response  
                using (HttpWebResponse response = request.GetResponse() as HttpWebResponse)
                {
                    // Get the response stream  
                    using (StreamReader reader = new StreamReader(response.GetResponseStream()))
                    {
                        string line = String.Empty;
                        string sep = "\t";
                        String[] row = new String[4];
                        int count = 0;
                        while ((line = reader.ReadLine()) != null)
                        {
                            count++;
                            if (count > 1)
                            {
                                row = line.Split(',');
                                DataRow dr = epPlotTable.NewRow();
                                dr["EP_Plotid"] = row[0];
                                dr["LANDUSE"] = row[3];
                                epPlotTable.Rows.Add(dr);
                            }
                        }

                        response.Close();
                    }
                }
            }

            catch (Exception e)
            {

            }

            //get all grasland plots
            List<string> graslandPlots = epPlotTable.AsEnumerable().Where((x => x.Field<string>("LANDUSE") == "G")).Select(a => a.Field<string>("EP_PlotID")).ToList();

            return graslandPlots;
        }
    }
    /// <summary>
    /// Class to store server information to access data via API
    /// 
    /// </summary>
    /// <returns></returns>
    public class ServerInformation
    {
        public string ServerName { get; set; }
        public string Token { get; set; }

    }

    /// <summary>
    /// Class to store dataset information receive via api
    /// 
    /// </summary>
    /// <returns></returns>
    public class DatasetObject
    {
        public string Id { get; set; }
        public string Version { get; set; }
        public string VersionId { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string DataStructureId { get; set; }
        public string MetadataStructureId { get; set; }
        public AdditionalInformations AdditionalInformations { get; set; }
        public DatasetObject()
        {
            AdditionalInformations = new AdditionalInformations();
        }
    }
   
}

/// <summary>
/// Store AdditionalInformations for Dataset Object
/// 
/// </summary>
/// <returns></returns>
public class AdditionalInformations
{
    public string Title { get; set; }

}