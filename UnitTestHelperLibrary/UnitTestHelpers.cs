using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml;
using Newtonsoft.Json;

namespace UnitTestHelperLibrary
{
    public class UnitTestHelpers
    {
        private static string[] _databaseList;
        public static string InstanceName { get; private set; }
        public static string UnitTestProjectInstance { get; private set; }

        /// <summary>
        /// This property will return true if the project that called the UnitTestHelpers.Start method is the currently running dll.
        /// </summary>
        public static bool IsInUnitTest => UnitTestProjectInstance == Assembly.GetExecutingAssembly().FullName;

        /// <summary>
        /// Use this method only in your unit test project.  You normally call this method once before starting your unit tests.
        /// This will setup the SQLLocalDB instance and create the databases that you'll use for all unit tests.  For MS Tests
        /// Include this call inside an AssemblyInitialize method.
        /// </summary>
        /// <param name="instanceName">Short name of your SQLLocalDB instance.  A GUID will be appended to the name to make it unique.</param>
        /// <param name="databaseList">Give a list of all databases you would like to create in the SQLLocalDB instance.</param>
        public static void Start(string instanceName, string[] databaseList)
        {
            UnitTestProjectInstance = Assembly.GetExecutingAssembly().FullName;

            _databaseList = databaseList;

            // make sure the instance name is unique.  This will allow unit tests to be run for two or more projects on a 
            // machine (like the build server).
            InstanceName = instanceName + Guid.NewGuid().ToString().Replace("-", "");

            // make sure any previous instances are shut down
            var startInfo = new ProcessStartInfo
            {
                WindowStyle = ProcessWindowStyle.Hidden,
                FileName = "cmd.exe",
                Arguments = "/c sqllocaldb stop \"" + InstanceName + "\""
            };

            var process = new Process { StartInfo = startInfo };
            process.Start();
            process.WaitForExit();

            // delete any previous instance
            startInfo.Arguments = "/c sqllocaldb delete \"" + InstanceName + "\"";
            process.Start();
            process.WaitForExit();

            // check to see if the database files exist, if so, then delete them
            foreach (var databaseName in databaseList)
            {
                DeleteDatabaseFiles(databaseName);
            }

            // create a new localdb sql server instance
            startInfo = new ProcessStartInfo
            {
                WindowStyle = ProcessWindowStyle.Hidden,
                FileName = "cmd.exe",
                Arguments = "/c sqllocaldb create \"" + InstanceName + "\" -s"
            };

            process = new Process { StartInfo = startInfo };
            process.Start();
            process.WaitForExit();

            foreach (var databaseName in databaseList)
            {
                CreateDatabase(databaseName);
            }
        }

        private static void DeleteDatabaseFiles(string databaseName)
        {
            if (File.Exists(databaseName + ".mdf"))
            {
                File.Delete(databaseName + ".mdf");
            }

            if (File.Exists(databaseName + "_log.ldf"))
            {
                File.Delete(databaseName + "_log.ldf");
            }
        }

        /// <summary>
        /// Call this method when all of your unit tests are completed.  For MS Test, include this
        /// call inside the AssemblyCleanup method.
        /// </summary>
        public static void End()
        {
            // shut down the instance
            var startInfo = new ProcessStartInfo
            {
                WindowStyle = ProcessWindowStyle.Hidden,
                FileName = "cmd.exe",
                Arguments = "/c sqllocaldb stop \"" + InstanceName + "\""
            };

            var process = new Process { StartInfo = startInfo };
            process.Start();
            process.WaitForExit();

            // delete the instance
            startInfo.Arguments = "/c sqllocaldb delete \"" + InstanceName + "\"";
            process.Start();
            process.WaitForExit();

            foreach (var databaseName in _databaseList)
            {
                DeleteDatabaseFiles(databaseName);
            }
        }

        /// <summary>
        /// Truncate all tables in the databases setup.
        /// This method will remove all constraints before truncating tables.  You must
        /// re-apply your constraints for each test, if you need them.
        /// </summary>
        public static void TruncateData()
        {
            var tableList = new List<string>();

            using (var db = new ADODatabaseContext("TEST"))
            {
                //_databaseList
                foreach (var database in _databaseList)
                {
                    ClearAllConstraints(database);

                    // generate a table list
                    using (var reader = db.ReadQuery(@"
						SELECT * 
						FROM " + database + @".INFORMATION_SCHEMA.tables 
						WHERE TABLE_TYPE = 'BASE TABLE'
						ORDER BY TABLE_CATALOG, TABLE_SCHEMA, TABLE_NAME"))
                    {
                        while (reader.Read())
                        {
                            var tableName = reader["table_name"].ToString();
                            var schemaName = reader["TABLE_SCHEMA"].ToString();

                            tableList.Add(database + "." + schemaName + "." + tableName);
                        }
                    }
                }
            }

            using (var db = new ADODatabaseContext("TEST"))
            {
                foreach (var item in tableList)
                {
                    db.ExecuteNonQuery(@"TRUNCATE TABLE " + item);
                }
            }
        }
        /// <summary>
        /// clear all constraints in database
        /// </summary>
        private static void ClearAllConstraints(string database)
        {
            using (var db = new ADODatabaseContext("TEST", database))
            {
                using (var reader = db.ReadQuery(@"
						SELECT 
							OBJECT_NAME(OBJECT_ID) AS ConstraintName,
							SCHEMA_NAME(schema_id) AS SchemaName,
							OBJECT_NAME(parent_object_id) AS TableName,
							type_desc AS ConstraintType
						FROM 
							sys.objects
						WHERE 
							type_desc LIKE '%CONSTRAINT' AND 
							OBJECT_NAME(OBJECT_ID) LIKE'fk_%'"))
                {
                    while (reader.Read())
                    {
                        var foreignKeyTableName = reader["TableName"].ToString();
                        var constraintName = reader["ConstraintName"].ToString();
                        var schemaName = reader["SchemaName"].ToString();

                        using (var dbExec = new ADODatabaseContext("TEST", database))
                        {
                            var query = "ALTER TABLE " + database + "." + schemaName + "." + foreignKeyTableName + " DROP CONSTRAINT " + constraintName;
                            dbExec.ExecuteNonQuery(query);
                        }
                    }
                }
            }
        }

        private static void CreateDatabase(string databaseName)
        {
            var databaseDirectory = Directory.GetCurrentDirectory();

            using (var db = new ADODatabaseContext("TEST"))
            {
                db.ExecuteNonQuery(@"CREATE DATABASE [" + databaseName + @"]
				  CONTAINMENT = NONE
				  ON  PRIMARY 
				  ( NAME = N'" + databaseName + @"', FILENAME = N'" + databaseDirectory + @"\" + databaseName +
                    @".mdf' , SIZE = 8096KB , FILEGROWTH = 1024KB )
				  LOG ON 
				  ( NAME = N'" + databaseName + @"_log', FILENAME = N'" + databaseDirectory + @"\" + databaseName +
                    @"_log.ldf' , SIZE = 8096KB , FILEGROWTH = 10%)
				  ");
            }
        }
        /// <summary>
        /// Use this method to create a stored procedure defined in the code generated by the Unit Test Generator utility.
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="databaseName"></param>
        /// <param name="spName"></param>
        public static void CreateStoredProcedure(Stream stream, string databaseName, string spName)
        {
            using (var db = new ADODatabaseContext("TEST", databaseName))
            {
                // first, drop the stored procedure if it already exists
                var sp = @"if exists (select * from sys.objects where name = N'" + spName + @"' and type = N'P') 
						  begin
							drop procedure " + spName + @"
						  end";
                db.ExecuteNonQuery(sp);

                // need to read the text file and create the stored procedure in the test database
                using (var reader = new StreamReader(stream))
                {
                    var storedProcText = reader.ReadToEnd();

                    var tsqLcommandList = Regex.Split(storedProcText, "GO");

                    foreach (var tsqlCommand in tsqLcommandList)
                    {
                        db.ExecuteNonQuery(tsqlCommand);
                    }
                }
            }
        }

        private static string LowerCaseTags(string xml)
        {
            return Regex.Replace(
                xml,
                @"<[^<>]+>",
                m => { return m.Value.ToLower(); },
                RegexOptions.Multiline | RegexOptions.Singleline);
        }

        /// <summary>
        /// Use this method to read xml or json data from an embedded file in your unit tests.  The data read
        /// will be inserted into your database to create seed data for your test.
        /// </summary>
        /// <param name="xmlJsonDataFile">Full namespace path to the embedded resource file</param>
        public static void ReadData(string xmlJsonDataFile)
        {
            var schemaName = "dbo";

            var assembly = Assembly.GetCallingAssembly();
            var resourceName = xmlJsonDataFile;
            using (var stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                {
                    throw new Exception("Cannot find XML data file, make sure it is set to Embedded Resource!");
                }

                var databaseName = "";
                if (xmlJsonDataFile.Substring(xmlJsonDataFile.Length - 3, 3).ToLower() == "xml")
                {
                    using (var reader = new StreamReader(stream))
                    {
                        var document = new XmlDocument();
                        document.LoadXml(reader.ReadToEnd());

                        foreach (XmlNode element in document.ChildNodes)
                        {
                            foreach (XmlNode subelement in element.ChildNodes)
                            {
                                if (subelement.Name.ToLower() == "database")
                                {
                                    databaseName = subelement.Attributes["name"].Value;
                                    schemaName = "dbo";

                                    if (subelement.Attributes["schema"] != null)
                                    {
                                        schemaName = subelement.Attributes["schema"].Value;
                                    }

                                    var insertQueryGenerator = new InsertQueryGenerator("TEST", databaseName, schemaName);

                                    var children = subelement.ChildNodes;
                                    foreach (XmlNode e in children)
                                    {
                                        insertQueryGenerator.InsertData(e);
                                    }
                                }
                            }
                        }
                    }
                }
                else if (xmlJsonDataFile.Substring(xmlJsonDataFile.Length - 4, 4).ToLower() == "json")
                {
                    using (var reader = new StreamReader(stream))
                    {
                        var jsonFile = reader.ReadToEnd();

                        dynamic temp = JsonConvert.DeserializeObject(jsonFile);
                        InsertQueryGenerator insertQueryGenerator = null;

                        foreach (var attr in temp)
                        {
                            if (attr.Name == "database")
                            {
                                databaseName = attr.Value;
                                insertQueryGenerator = new InsertQueryGenerator("TEST", databaseName, schemaName);
                            }
                            else if (insertQueryGenerator != null)
                            {
                                insertQueryGenerator.InsertJsonData(attr);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// This method will create a list of tables in the database provided.  You must call this method
        /// for each database you want to create tables inside of.
        /// </summary>
        /// <param name="tableList">List of table definitions provided by the code generated with the Unit Test Database Generator utility.</param>
        /// <param name="databaseName">The database name these tables will be created inside of.</param>
        public static void CreateAllTables(List<TableDefinition> tableList, string databaseName)
        {
            // create all non "dbo" schemas in database
            var schemaList = (from t in tableList where t.SchemaName != "dbo" select t.SchemaName).Distinct();
            foreach (var schemaName in schemaList)
            {
                if (!string.IsNullOrEmpty(schemaName))
                {
                    using (var db = new ADODatabaseContext("TEST", databaseName))
                    {
                        db.ExecuteNonQuery("CREATE SCHEMA [" + schemaName + "] AUTHORIZATION [dbo]");
                    }
                }
            }

            // generate all tables listed in the table name list
            foreach (var tableDefinition in tableList)
            {
                var query = tableDefinition.CreateScript;

                using (var db = new ADODatabaseContext("TEST", databaseName))
                {
                    db.ExecuteNonQuery(query);
                }
            }
        }

        /// <summary>
        /// Create one constraint between two tables.  Use this method to create a few constraints for the tables you use in one test.
        /// </summary>
        /// <param name="pConstraintList">This is the list of available constraints in the code created by the Unit Test Database Creator utility</param>
        /// <param name="table1">The first table that the constraint applies to</param>
        /// <param name="table2">The second table the constraint applies to</param>
        public static void CreateConstraint(List<ConstraintDefinition> pConstraintList, string table1, string table2)
        {
            var constraintList = pConstraintList.Where(x => x.PkTable.ToLower() == table1 && x.FkTable.ToLower() == table2).ToList();
            foreach (var constraint in constraintList)
            {
                var query = "ALTER TABLE " + constraint.FkTable + " ADD CONSTRAINT fk_" + constraint.FkTable + "_" + constraint.PkTable + " FOREIGN KEY (" + constraint.FkField + ") REFERENCES " + constraint.PkTable + "(" + constraint.PkField + ")";

                using (var db = new ADODatabaseContext("TEST"))
                {
                    db.ExecuteNonQuery(query);
                }
            }

            constraintList = pConstraintList.Where(x => x.PkTable.ToLower() == table2 && x.FkTable.ToLower() == table1).ToList();
            foreach (var constraint in constraintList)
            {
                var query = "ALTER TABLE " + constraint.FkTable + " ADD CONSTRAINT fk_" + constraint.FkTable + "_" + constraint.PkTable + " FOREIGN KEY (" + constraint.FkField + ") REFERENCES " + constraint.PkTable + "(" + constraint.PkField + ")";

                using (var db = new ADODatabaseContext("TEST", constraint.DatabaseName))
                {
                    db.ExecuteNonQuery(query);
                }
            }
        }

        /// <summary>
        /// Clear all the constraints in the database.
        /// </summary>
        /// <param name="pConstraintList">This is the list of available constraints in the code created by the Unit Test Database Creator utility</param>
        public static void ClearConstraints(List<ConstraintDefinition> pConstraintList)
        {
            var schemaName = "dbo";

            // delete all foreign constraints in all databases
            using (var db = new ADODatabaseContext("TEST"))
            {
                //_databaseList
                foreach (var database in _databaseList)
                {
                    var constraints = pConstraintList.Where(x => x.DatabaseName == database).ToList();

                    foreach (var constraint in constraints)
                    {
                        var constraintName = "fk_" + constraint.FkTable + "_" + constraint.PkTable;

                        if (!string.IsNullOrEmpty(constraint.SchemaName))
                        {
                            schemaName = constraint.SchemaName;
                        }

                        var query = "SELECT * FROM INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS WHERE CONSTRAINT_NAME ='" + constraintName + "' AND CONSTRAINT_SCHEMA='" + schemaName + "'";
                        using (var reader = db.ReadQuery(query))
                        {
                            while (reader.Read())
                            {
                                query = "ALTER TABLE " + constraint.DatabaseName + ".." + constraint.FkTable + " DROP CONSTRAINT " + constraintName;
                                db.ExecuteNonQuery(query);
                                break;
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Returns the total records in a table specified.  This can be used to verify how many records are created in your unit test.
        /// </summary>
        /// <param name="tableName">Table to check</param>
        /// <param name="database">Database that table resides in</param>
        /// <param name="schema">Optional schema name.  If not specified, then it will be set to "dbo"</param>
        /// <returns></returns>
        public static int TotalRecords(string tableName, string database, string schema = "dbo")
        {
            var results = 0;

            // make sure the schema name doesn't already contain a "."
            schema = schema.Replace(".", "");

            using (var db = new ADODatabaseContext("", schema + "." + database))
            {
                var query = "SELECT COUNT(*) AS total FROM " + tableName;
                using (var reader = db.ReadQuery(query))
                {
                    while (reader.Read())
                    {
                        results = int.Parse(reader["total"].ToString());
                        break;
                    }
                }
            }

            return results;
        }

        /// <summary>
        /// Execute any query against your SQLLocalDB instance.
        /// </summary>
        /// <param name="filePath">Full namespace path to the embedded resource file</param>
        /// <param name="database"></param>
        public static void ExecuteSQLCode(string filePath, string database)
        {
            using (var db = new ADODatabaseContext("TEST", database))
            {
                var assembly = Assembly.GetCallingAssembly();
                using (var stream = assembly.GetManifestResourceStream(filePath))
                {
                    using (var reader = new StreamReader(stream))
                    {
                        var code = reader.ReadToEnd();
                        var tsqlCommandList = Regex.Split(code, "GO");

                        foreach (var tsqlCommand in tsqlCommandList)
                        {
                            if (tsqlCommand.Trim() != "")
                            {
                                db.ExecuteNonQuery(tsqlCommand.Replace("\n", "").Replace("\r", ""));
                            }
                        }
                    }
                }
            }
        }
    }
}
