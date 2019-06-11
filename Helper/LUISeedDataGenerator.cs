using BExIS.Security.Entities.Objects;
using BExIS.Security.Services.Objects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Vaiona.Web.Mvc.Modularity;

namespace BExIS.Modules.Lui.UI.Helper
{
    public class LUISeedDataGenerator : IModuleSeedDataGenerator
    {

        public void GenerateSeedData()
        {
            FeatureManager featureManager = new FeatureManager();
            OperationManager operationManager = new OperationManager();

            try                 
            {
                Feature rootDataToolsFeature = featureManager.FeatureRepository.Get().FirstOrDefault(f => f.Name.Equals("Data Tools"));
                if (rootDataToolsFeature == null) rootDataToolsFeature = featureManager.Create("Data Tools", "Data Tools");

                Feature luiFeature = featureManager.FeatureRepository.Get().FirstOrDefault(f => f.Name.Equals("LUI Tool"));
                if (luiFeature == null) luiFeature = featureManager.Create("LUI Tool", "LUI Tool", rootDataToolsFeature);

                operationManager.Create("LUI", "Main", "*", luiFeature);


            }
            catch (Exception ex)
            {
                throw ex;
            }
            finally
            {
                featureManager.Dispose();
                operationManager.Dispose();
            }

        }
    
        public void Dispose()
        {
            // nothing to do for now...
        }
    }

}