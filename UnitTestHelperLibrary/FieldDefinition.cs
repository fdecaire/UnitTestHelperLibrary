﻿using System;

namespace UnitTestHelperLibrary
{
    public class FieldDefinition : IEquatable<string>
    {
        public string Name;
        public string Type;
        private string _value;
        public string Value
        {
            get => _value;
            set
            {
                _value = value;

                switch (Type)
                {
                    case "char":
                    case "text":
                    case "varchar":
                    case "nchar":
                    case "nvarchar":
                    case "xml":
                    case "ntext":
                    case "datetime":
                    case "datetime2":
                    case "smalldatetime":
                    case "date":
                    case "time":
                        _value = "'" + _value.Replace("'", "''") + "'";
                        break;
                    case "byte[]": //TODO: need to figure out the right type for this
                        _value = "0x" + _value.Replace("-", "");
                        break;
                    case "uniqueidentifier":
                        _value = "CAST('" + _value + "' AS UNIQUEIDENTIFIER)";
                        break;
                }
            }
        }

        public bool Equals(string Name)
        {
            return (this.Name.ToLower() == Name);
        }
    }

}
