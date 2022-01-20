using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;

namespace BExIS.Modules.Lui.UI.Controllers
{
    public class LUIComponentCalculationController : Controller
    {
        // GET: LUIComponentCalculation
        public ActionResult Index()
        {
            return View();
        }

        public ActionResult CalculateCompontents()
        {

            return View();
        }













        private DataTable GetLanduseData()
        {
            string serverName = "";
            string datasetId= "";
            string token = "";

            string link = serverName + "/api/data/" + datasetId;
            HttpWebRequest request = WebRequest.Create(link) as HttpWebRequest;
            request.Headers.Add("Authorization", "Bearer " + token);

            DataTable landuseData = new DataTable();
            landuseData.Columns.Add("Exploratory");
            landuseData.Columns.Add("Year");
            landuseData.Columns.Add("EP_PlotID");
            landuseData.Columns.Add("SizeManagementUnit");

            landuseData.Columns.Add("Cuts");

            landuseData.Columns.Add("Manure_tha");
            landuseData.Columns.Add("TypeManure");
            landuseData.Columns.Add("Slurry_m3ha");
            landuseData.Columns.Add("TypeSlurry");
            landuseData.Columns.Add("Biogas_m3ha");
            landuseData.Columns.Add("ExactValOrg");
            landuseData.Columns.Add("NorgExact");
            landuseData.Columns.Add("minNitrogen_kgNha");

            landuseData.Columns.Add("LivestockUnits1");
            landuseData.Columns.Add("DayGrazing1");
            landuseData.Columns.Add("GrazingArea1");
            landuseData.Columns.Add("LivestockUnits2");
            landuseData.Columns.Add("DayGrazing2");
            landuseData.Columns.Add("GrazingArea2");
            landuseData.Columns.Add("LivestockUnits3");
            landuseData.Columns.Add("DayGrazing3");
            landuseData.Columns.Add("GrazingArea3");
            landuseData.Columns.Add("LivestockUnits4");
            landuseData.Columns.Add("DayGrazing4");
            landuseData.Columns.Add("GrazingArea4");


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
                                DataRow dr = landuseData.NewRow();

                                dr["Exploratory"] = row[0];
                                dr["Year"] = row[1];
                                dr["EP_PlotID"] = row[5];
                                dr["SizeManagementUnit"] = row[7];

                                dr["Cuts"] = row[89];

                                dr["Manure_tha"] = row[155];
                                dr["TypeManure"] = row[156];
                                dr["Slurry_m3ha"] = row[157];
                                dr["TypeSlurry"] = row[158];
                                dr["Biogas_m3ha"] = row[159];
                                dr["ExactValOrg"] = row[163];
                                dr["NorgExact"] = row[164];
                                dr["minNitrogen_kgNha"] = row[167];

                                dr["LivestockUnits1"] = row[44];
                                dr["DayGrazing1"] = row[46];
                                dr["GrazingArea1"] = row[47];
                                dr["LivestockUnits2"] = row[57];
                                dr["DayGrazing2"] = row[59];
                                dr["GrazingArea2"] = row[60];
                                dr["LivestockUnits3"] = row[70];
                                dr["DayGrazing3"] = row[72];
                                dr["GrazingArea3"] = row[73];
                                dr["LivestockUnits4"] = row[83];
                                dr["DayGrazing4"] = row[85];
                                dr["GrazingArea4"] = row[86];

                                landuseData.Rows.Add(dr);
                            }
                        }
                        response.Close();
                    }
                }
            }
            catch (Exception e)
            {

            }



            return landuseData;
        }
    

    }
}