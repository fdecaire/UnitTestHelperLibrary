﻿using System.Collections.Generic;
using System.Linq;
using System.Xml;

namespace UnitTestHelperLibrary
{
    public class InsertQueryGenerator
    {
        public List<FieldDefinition> Fields = new List<FieldDefinition>();
        private string _tableName;
        private readonly string _connectionString;
        private readonly string _databaseName;
        private readonly string _schemaName;
        private string _identityFieldName;
        private bool _dataContainsPrimaryKey;

        public InsertQueryGenerator(string connectionString, string databaseName, string schemaName)
        {
            _connectionString = connectionString;
            _databaseName = databaseName;
            _schemaName = schemaName;
        }

        public void InsertData(XmlNode e)
        {
            _tableName = e.Name;

            _identityFieldName = ReadTableIdentityFieldName();
            _dataContainsPrimaryKey = false;

            BuildFieldsForTable();

            // iterate through the fields and collect the data
            var children = e.ChildNodes;

            if (children.Count > 0)
            {
                foreach (XmlNode fields in children)
                {
                    var fieldName = fields.Name.ToLower();
                    var fieldData = fields.InnerText;

                    var record = Fields.FirstOrDefault(x => x.Name.ToLower() == fieldName);
                    if (record == null) continue;
                    record.Value = fieldData;

                    if (fieldName.ToLower() == _identityFieldName.ToLower())
                    {
                        _dataContainsPrimaryKey = true;
                    }
                }
            }
            else
            {
                // try to parse the xml attributes
                var childElements = e.Attributes;
                foreach (XmlAttribute fields in childElements)
                {
                    var fieldName = fields.Name.ToLower();
                    var fieldData = fields.Value;

                    var record = Fields.FirstOrDefault(x => x.Name.ToLower() == fieldName);
                    if (record == null) continue;
                    record.Value = fieldData;

                    if (fieldName.ToLower() == _identityFieldName.ToLower())
                    {
                        _dataContainsPrimaryKey = true;
                    }
                }
            }

            BuildQueryInsertData();
        }

        private void BuildQueryInsertData()
        {
            var query = "";

            foreach (var field in Fields)
            {
                if (field.Value != null)
                {
                    query += "[" + field.Name + "],";
                }
            }

            if (query[query.Length - 1] == ',')
            {
                query = query.Substring(0, query.Length - 1);
            }

            if (query == "")
            {
                return;
            }

            query = $"INSERT INTO [{_databaseName}].[{_schemaName}].[{_tableName}] ({query}) VALUES (";

            foreach (var field in Fields)
            {
                if (field.Value != null)
                {
                    query += field.Value + ",";
                }
            }

            if (query[query.Length - 1] == ',')
            {
                query = query.Substring(0, query.Length - 1);
            }

            query += ")";

            using (var db = new ADODatabaseContext(_connectionString))
            {
                if (_dataContainsPrimaryKey)
                {
                    db.ExecuteNonQuery("SET IDENTITY_INSERT [" + _databaseName + "].[" + _schemaName + "].[" + _tableName + "] ON");
                }
                db.ExecuteNonQuery(query);
                if (_dataContainsPrimaryKey)
                {
                    db.ExecuteNonQuery("SET IDENTITY_INSERT [" + _databaseName + "].[" + _schemaName + "].[" + _tableName + "] OFF");
                }
            }
        }

        public void InsertJsonData(dynamic jsonTableData)
        {
            // must be a list of tables
            _tableName = jsonTableData.Name;

            foreach (var tableItem in jsonTableData.Value)
            {
                _identityFieldName = ReadTableIdentityFieldName();
                _dataContainsPrimaryKey = false;

                BuildFieldsForTable();

                foreach (var fieldItem in tableItem)
                {
                    string fieldName = fieldItem.Name.ToLower();
                    string fieldData = fieldItem.Value;

                    var record = Fields.FirstOrDefault(x => x.Name.ToLower() == fieldName);
                    if (record == null) continue;
                    record.Value = fieldData;

                    if (fieldName.ToLower() == _identityFieldName.ToLower())
                    {
                        _dataContainsPrimaryKey = true;
                    }
                }

                BuildQueryInsertData();
            }
        }

        private void BuildFieldsForTable()
        {
            Fields.Clear();
            using (var db = new ADODatabaseContext(_connectionString))
            {
                var columnQuery = "SELECT * FROM [" + _databaseName + "].INFORMATION_SCHEMA.columns WHERE TABLE_NAME='" + _tableName + "'";
                using (var reader = db.ReadQuery(columnQuery))
                {
                    while (reader.Read())
                    {
                        Fields.Add(new FieldDefinition
                        {
                            Name = reader["COLUMN_NAME"].ToString(),
                            Type = reader["DATA_TYPE"].ToString()
                        });
                    }
                }
            }
        }

        private string ReadTableIdentityFieldName()
        {
            var query = @"
					SELECT * FROM [" + _databaseName + @"].sys.identity_columns AS a 
					INNER JOIN [" + _databaseName + @"].sys.objects AS b ON a.object_id=b.object_id 
					WHERE 
						LOWER(b.name)='" + _tableName + @"' AND 
						type='U'";

            using (var db = new ADODatabaseContext(_connectionString))
            {
                var reader = db.ReadQuery(query);
                while (reader.Read())
                {
                    return reader["name"].ToString();
                }
            }
            return "";
        }
    }

}
