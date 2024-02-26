using BExIS.Modules.Lui.UI.Models;
using CsvHelper;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Web;
using System.Web.Script.Serialization;
using System.Xml;
using Vaiona.Utils.Cfg;
using Vaiona.Web.Mvc.Modularity;

namespace BExIS.Modules.Lui.UI.Helper
{
    public class DataAccess
    {

        /// <summary>
        /// Get metadata
        /// </summary>
        /// <param name="datasetId"></param>
        /// <returns>metadata from a dataset as xml document.</returns>
        public static XmlDocument GetMetadata(string datasetId, ServerInformation serverInformation)
        {
            string link = serverInformation.ServerName + "/api/metadata/" + datasetId;
            HttpWebRequest request = WebRequest.Create(link) as HttpWebRequest;
            request.Headers.Add("Authorization", "Bearer " + serverInformation.Token);

            XmlDocument document = new XmlDocument();

            using (HttpWebResponse response = request.GetResponse() as HttpWebResponse)
            {
                document.Load(response.GetResponseStream());

                response.Close();
            }

            return document;

        }

        /// <summary>
        /// Get lanu data
        /// 
        /// </summary>
        /// <param name="datasetId"></param>
        /// <returns>Data table with comp dataset depents on dataset id.</returns>
        public static DataTable GetData(string datasetId, long structureId, ServerInformation serverInformation)
        {
            string link = serverInformation.ServerName + "/api/data/" + datasetId;
            HttpWebRequest request = WebRequest.Create(link) as HttpWebRequest;
            request.Headers.Add("Authorization", "Bearer " + serverInformation.Token);

            List<ApiDataStatisticModel> statistics = GetStatistic(datasetId, serverInformation);

            DataStructureObject dataStructureObject = GetDataStructure(structureId, serverInformation);

            //create datatable using data structure info
            DataTable data = new DataTable();
            foreach (var variable in dataStructureObject.Variables)
            {
                DataColumn col = new DataColumn(variable.Label);
                col.DataType = System.Type.GetType("System." + variable.SystemType);
                col.AllowDBNull = true;
                data.Columns.Add(col);
            }

            try
            {
                // Get response  
                using (HttpWebResponse response = request.GetResponse() as HttpWebResponse)
                {
                    // Get the response stream  
                    using (StreamReader reader = new StreamReader(response.GetResponseStream()))
                    using (var csvReader = new CsvReader(reader, CultureInfo.InvariantCulture))
                    {
                        var records = csvReader.GetRecords<dynamic>();

                        foreach (var r in records)
                        {
                            var l = Enumerable.ToList(r);

                            DataRow dr = data.NewRow();
                            String[] row = new String[4];

                            for (int j = 0; j < data.Columns.Count; j++)
                            {
                                if (String.IsNullOrEmpty(l[j].Value))
                                    dr[data.Columns[j].ColumnName] = DBNull.Value;
                                else
                                {
                                    //get missing values from data structure to replace display name with placeholder
                                    DataTable missingValues = statistics.Where(a => a.VariableName == data.Columns[j].ColumnName).Select(a => a.missingValues).FirstOrDefault();
                                    dynamic value = null; 
                                    if (missingValues != null)
                                    {
                                        var displayNames = missingValues.AsEnumerable().Select(a => a.Field<string>("displayName")).ToList();
                                        if (displayNames.Contains(l[j].Value))
                                            value = missingValues.AsEnumerable().Where(a => a.Field<string>("displayName") == l[j].Value).Select(a => a.Field<string>("placeholder")).FirstOrDefault();
                                        else
                                            value = l[j].Value;

                                    }
                                    else
                                        value = l[j].Value;

                                    if (data.Columns[j].DataType == typeof(DateTime))
                                    {
                                            var format = dataStructureObject.Variables.Where(e => e.Label == data.Columns[j].ColumnName).FirstOrDefault().DataType;
                                            format = format.Split('-').ToArray()[1];
                                            dr[data.Columns[j].ColumnName] = ParseValue(data.Columns[j].DataType.ToString(), value, format);
                                    }
                                    else
                                        dr[data.Columns[j].ColumnName] = value;
                                }
                            }

                            data.Rows.Add(dr);
                        }

                        response.Close();
                    }
                }
            }
            catch (Exception e)
            {

            }


            return data;
        }

        public static List<ApiDataStatisticModel> GetStatistic(string datasetId, ServerInformation serverInformation)
        {
            string link = serverInformation.ServerName + "/api/datastatistic/" + datasetId;
            HttpWebRequest request = WebRequest.Create(link) as HttpWebRequest;
            request.Headers.Add("Authorization", "Bearer " + serverInformation.Token);

            List<ApiDataStatisticModel> data = new List<ApiDataStatisticModel>();

            try
            {
                // Get response  
                using (HttpWebResponse response = request.GetResponse() as HttpWebResponse)
                {
                    using (StreamReader reader = new StreamReader(response.GetResponseStream()))
                    {
                        var objText = reader.ReadToEnd();
                        data = JsonConvert.DeserializeObject<List<ApiDataStatisticModel>>(objText);
                    }
                }
            }
            catch (Exception e)
            {
                string error = "Not data" + e.InnerException;
            }

            return data;
        }

        /// <summary>
        /// Get missing comp data
        /// 
        /// </summary>
        /// <returns>List of missing component data. Years with missing ep plotids.</returns>
        public static List<MissingComponentData> GetMissingComponentData(ServerInformation serverInformation)
        {
            List<MissingComponentData> data = new List<MissingComponentData>();
            var settings = ModuleManager.GetModuleSettings("lui");

            string datasetId = settings.GetValueByKey("lui:datasetDefaultComponentsSet").ToString();
            long structureId = long.Parse(DataAccess.GetDatasetInfo(datasetId, serverInformation).DataStructureId, CultureInfo.InvariantCulture);
            DataTable compData = GetData(datasetId, structureId, serverInformation);

            //get all years where data rows less then 50, that means not all plots has data
            var years = compData.AsEnumerable().GroupBy(x => x.Field<DateTime>("Year")).Where(g => g.Count() < 150).ToList();

            foreach (var i in years)
            {
                MissingComponentData missingComponentData = new MissingComponentData();
                missingComponentData.Year = i.Select(a => a.Field<DateTime>("Year")).FirstOrDefault().ToString("yyyy");
                List<string> availablePlots = compData.AsEnumerable().Where(x => x.Field<DateTime>("Year").ToString("yyyy") == missingComponentData.Year).Select(a => a.Field<string>("EP_PlotID")).ToList();
                
                //
                int numberPlotsH = availablePlots.Where(a => a.Contains("H")).Count();
                int numberPlotsS = availablePlots.Where(a => a.Contains("S")).Count();
                int numberPlotsA = availablePlots.Where(a => a.Contains("A")).Count();

                double pHai = (numberPlotsH * 100) / 50;
                double pSch = (numberPlotsS * 100) / 50;
                double pAlb = (numberPlotsA * 100) / 50;

                missingComponentData.ExploPercentage.Add("ALB",pAlb.ToString() + "%");
                missingComponentData.ExploPercentage.Add("HAI", pHai.ToString() + "%");
                missingComponentData.ExploPercentage.Add("SCH", pSch.ToString() + "%");

                missingComponentData.PlotIds = getAllGrasslandPlots(serverInformation).Except(availablePlots).ToList();
                data.Add(missingComponentData);
            }

            return data;
        }

        /// <summary>
        /// Get information abbout dataset
        /// 
        /// </summary>
        /// <returns>Information like version, title etc</returns>
        public static DatasetObject GetDatasetInfo(string datasetId, ServerInformation serverInformation)
        {
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
        /// Get data structure
        /// 
        /// </summary>
        /// <returns> a data structure object</returns>
        public static DataStructureObject GetDataStructure(long structId, ServerInformation serverInformation)
        {
            string link = serverInformation.ServerName + "/api/structures/" + structId;
            HttpWebRequest request = WebRequest.Create(link) as HttpWebRequest;
            request.Headers.Add("Authorization", "Bearer " + serverInformation.Token);

            DataStructureObject dataStructureObject = new DataStructureObject();

            try
            {
                // Get response  
                using (HttpWebResponse response = request.GetResponse() as HttpWebResponse)
                {
                    using (StreamReader reader = new StreamReader(response.GetResponseStream()))
                    {
                        JavaScriptSerializer js = new JavaScriptSerializer();
                        var objText = reader.ReadToEnd();
                        dataStructureObject = (DataStructureObject)js.Deserialize(objText, typeof(DataStructureObject));
                    }
                }
            }
            catch (Exception e)
            {
                string error = "Not data" + e.InnerException;
            }

            return dataStructureObject;
        }

        /// <summary>
        /// get all ep plot ids from grasland plots
        /// 
        /// </summary>
        /// <returns>list of grasland ep plot ids</returns>
        public static List<string> getAllGrasslandPlots(ServerInformation serverInformation)
        {
            var settings = ModuleManager.GetModuleSettings("lui");

            string datasetId = settings.GetValueByKey("lui:epPlotsDataset").ToString();

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

        /// <summary>
        /// upload primary data via api
        /// 
        /// </summary>
        /// <returns>API response</returns>
        public static string Upload(DataApiModel data, ServerInformation serverInformation)
        {
            string link = serverInformation.ServerName + "/api/Data/";
            HttpWebRequest request = WebRequest.Create(link) as HttpWebRequest;
            //request.PreAuthenticate = true;
            request.Headers.Add("Authorization", "Bearer " + serverInformation.Token);
            request.Method = "PUT";
            request.ContentType = "application/json";

            using (var streamWriter = new StreamWriter(request.GetRequestStream()))
            {
                string json = Newtonsoft.Json.JsonConvert.SerializeObject(data);
                streamWriter.Write(json);
            }

            var httpResponse = (HttpWebResponse)request.GetResponse();
            string result;
            using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
            {
                result = streamReader.ReadToEnd();
            }

            return result;
        }

        /// <summary>
        /// convert string to given data type
        /// 
        /// </summary>
        /// <returns>type object</returns>
        private static object ParseValue(string type, string value, string format)
        {
            try
            {
                switch (type)
                {
                    case "System.DateTime":
                        DateTime.TryParseExact(value, format, new CultureInfo("en-US"), DateTimeStyles.None, out DateTime date);
                        return date;
                    default: throw new ArgumentException("DataType is not supported");
                }
            }
            catch(Exception e)
            {
                string text = String.Format("Type{0}, Value{1}, Format {2}", type, value, format);
                throw new ArgumentException(text, e);
            }
        }
    }
}



    