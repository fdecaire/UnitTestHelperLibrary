﻿using System;
using System.Data;

namespace UnitTestHelperLibrary
{
    public static class DataSetHelpers
    {
        public static void AddDataColumn(this DataTable dataTable, string fieldName, string dataType)
        {
            var dataColumn = new DataColumn
            {
                DataType = Type.GetType(dataType),
                ColumnName = fieldName
            };
            dataTable.Columns.Add(dataColumn);
        }

        public static object ConvertToDbNullDate(this string dateTimeString)
        {
            if (string.IsNullOrEmpty(dateTimeString)) return DBNull.Value;
            if (DateTime.TryParse(dateTimeString, out var tempDate))
            {
                return tempDate;
            }

            return DBNull.Value;
        }

        public static object ConvertToDbNullDouble(this string doubleString)
        {
            if (string.IsNullOrEmpty(doubleString)) return DBNull.Value;
            if (double.TryParse(doubleString, out var tempDouble))
            {
                return tempDouble;
            }

            return DBNull.Value;
        }

        /// <summary>
        /// convert from dataset to a nullable int data type
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public static int? DbNullToInt(this object data)
        {
            if (data != DBNull.Value)
            {
                return int.Parse(data.ToString());
            }

            return null;
        }

        /// <summary>
        /// convert from dataset to a nullable double data type
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public static double? DbNullToDouble(this object data)
        {
            if (data != DBNull.Value)
            {
                return double.Parse(data.ToString());
            }

            return null;
        }
    }

}
