﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Vita.Common;
using Vita.Data.Model;
using Vita.Data.Driver;
using Vita.Entities.Services;

namespace Vita.Data {

  public class DbSettings {
    public readonly string DataSourceName;
    public readonly DbModelConfig ModelConfig;
    public readonly DbUpgradeMode UpgradeMode;
    public DbUpgradeOptions UpgradeOptions;

    /// <summary>A DB server role to grant execute permissions for SELECT stored procedures (MS SQL only).</summary>
    public string GrantExecReadToRole = "public";
    /// <summary>A DB server role to grant execute permissions for Insert, Update, Delete stored procedures (MS SQL only).</summary>
    public string GrantExecWriteToRole = "public";

    private HashSet<string> _schemas = new StringSet();

    // Connection strings
    public readonly string ConnectionString;
    public readonly string SchemaManagementConnectionString; //optional, admin-privilege conn string

    public DbSettings(DbDriver driver, DbOptions options, 
                      string connectionString, 
                      string schemaManagementConnectionString = null, 
                      DbUpgradeMode upgradeMode = DbUpgradeMode.NonProductionOnly, 
                      DbUpgradeOptions upgradeOptions = DbUpgradeOptions.Default,
                      string dataSourceName = null, 
                      DbNamingPolicy namingPolicy = null) 
       : this (new DbModelConfig(driver, options, namingPolicy), connectionString, schemaManagementConnectionString, 
              upgradeMode, upgradeOptions, dataSourceName)   {  }

    // Use this constructor for shared db model (multi-tenant app aganst multiple identical databases)
    public DbSettings(DbModelConfig modelConfig, 
                      string connectionString, 
                      string schemaManagementConnectionString = null, 
                      DbUpgradeMode upgradeMode = DbUpgradeMode.NonProductionOnly, 
                      DbUpgradeOptions upgradeOptions = DbUpgradeOptions.Default,
                      string dataSourceName = null) {
      ModelConfig = modelConfig;
      ConnectionString = connectionString;
      SchemaManagementConnectionString = schemaManagementConnectionString ?? connectionString;
      UpgradeMode = upgradeMode;
      UpgradeOptions = upgradeOptions;
      DataSourceName = dataSourceName ?? DataSource.DefaultName;
    }

    public DbDriver Driver {
      get { return ModelConfig.Driver; }
    }

    public IEnumerable<string> GetSchemas() {
      return _schemas; 
    }
    //Used by DbModelLoader in DbFirst scenario, to limit list of schemas to load from
    public void SetSchemas(IEnumerable<string> schemas) {
      _schemas.Clear();
      _schemas.UnionWith(schemas); 
    }


  }//class


}
