using BExIS.IO.Transform.Output;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Web;

namespace BExIS.Modules.Lui.UI.Models
{
    /// <summary>
    /// Class to store server information to access data via API
    /// 
    /// </summary>
    /// <returns></returns>
    public class ServerInformation
    {
        public string ServerName { get; set; }
        public string UsernamePassword { get; set; }

    }

    /// <summary>
    /// Class to store dataset information receive via api
    /// 
    /// </summary>
    /// <returns></returns>
    public class DataStructureObject
    {
        public int id { get; set; }
        public string title { get; set; }
        public string desciption { get; set; }
        public bool inUse { get; set; }
        public List<Variable> variables { get; set; }
    }

    public class Constraint
    {
        public int id { get; set; }
        public string name { get; set; }
        public string type { get; set; }
        public string description { get; set; }
    }

    public class Unit
    {
        public int id { get; set; }
        public string name { get; set; }
        public string abbrevation { get; set; }
        public string description { get; set; }
        public Dimension dimension { get; set; }
        public string measurementSystem { get; set; }
    }

    public class Dimension
    {
        public string name { get; set; }
        public string description { get; set; }
        public string specification { get; set; }
    }

    public class Variable
    {
        public int id { get; set; }
        public string label { get; set; }
        public string description { get; set; }
        public bool isOptional { get; set; }
        public string dataType { get; set; }
        public string systemType { get; set; }
        public string displayPattern { get; set; }
        public Unit unit { get; set; }
        public List<object> missingValues { get; set; }
        public Template template { get; set; }
        public List<object> meanings { get; set; }
        public List<Constraint> constraints { get; set; }
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

    /// <summary>
    /// Class to store Data Statistic information receive via api
    /// </summary>
    /// <returns></returns>
    public class ApiDataStatisticModel
    {
        public long VariableId { get; set; }
        public string VariableName { get; set; }
        public string VariableDescription { get; set; }
        public string DataTypeName { get; set; }
        public string DataTypeSystemType { get; set; }
        public string DataTypeDisplayPattern { get; set; }
        public string Unit { get; set; }
        public DataTable uniqueValues { get; set; }
        public string count { get; set; }
        public string max { get; set; }
        public string min { get; set; }
        public string maxLength { get; set; }
        public string minLength { get; set; }
        public DataTable missingValues { get; set; }

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

    public enum DecimalCharacter
    {
        point,
        comma
    }

    public class DataApiModel
    {
        public string[] PrimaryKeys { get; set; }
        public long DatasetId { get; set; }
        public DecimalCharacter DecimalCharacter { get; set; }
        public string[] Columns { get; set; }
        public string[][] Data { get; set; }
    }
}
