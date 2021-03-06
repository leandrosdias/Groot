﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Groot
 {  
     public static class Groot
     {
         private static List<TOut> CsvReaderIters<TOut>(string filePath, Func<IEnumerable<(string, string)>, TOut> fn)
         {
             var csvlines = File.ReadAllLines(filePath);
             var header = csvlines[0].Split(',').Select(s => s.Trim()).ToList();

             return (from line in csvlines.Skip(1)
                 let data = new Dictionary<string, string>()
                 let enumRange = Enumerable.Range(start: 0, count: header.Count)
                 select fn(line.Split(',')
                     .ToList()
                     .Zip(enumRange, (value, idx) => (header[idx], value)))).ToList();
         }
         
         public static IEnumerable<Dictionary<string, string>> GetDictFromCsv(string filePath)
         {
             return CsvReaderIters(filePath, xs => xs.ToDictionary(x => x.Item1.Trim(), x => x.Item2.Trim()));
         }

         public static IEnumerable<T> GetObjectFromCsv<T>(string filePath, bool autoMapForNoCustomAttr = true)
         {
             var type = typeof(T);
             var propCollection = type.GetProperties();
             return GetDictFromCsv(filePath).Select(elem =>
             {
                 var tElem = (T) Activator.CreateInstance(typeof(T), new object[] { });
                 foreach (var x in elem)
                 {

                     IEnumerable<PropertyInfo> propertyEnumerable;
                     
                     var matchedCustomAttrProp = propCollection
                         .Where(property => property.GetCustomAttributes<GrootFieldAttribute>()
                             .Any(grootAttr => grootAttr.GetGrootFields() == x.Key)
                         );
                     
                     if (autoMapForNoCustomAttr)
                     {
                         var matchedFieldNamePropWithNoCustomAttr = propCollection
                             .Where(property => !property.GetCustomAttributes<GrootFieldAttribute>().Any())
                             .Where(property => property.Name == x.Key);
                         propertyEnumerable = matchedCustomAttrProp.Concat(matchedFieldNamePropWithNoCustomAttr);
                     }
                     else
                     {
                         propertyEnumerable = matchedCustomAttrProp;
                     }
                     
                     propertyEnumerable
                         .ToList()
                         .ForEach(prop =>
                         {
                             prop.SetValue(tElem, ChangeType(prop, x.Value), null);
                         });

                 }
                 return tElem;
             }).ToList();
             
         }
         
         private static object ChangeType(PropertyInfo prop, string value)
         {
            if (prop.PropertyType.IsEnum)
              return Enum.Parse(prop.PropertyType, value);

            return Convert.ChangeType(value, prop.PropertyType);
         }

        public static string CreateCsvFromList<T>(List<T> objList, string filePath)
        {

            var csv = new StringBuilder();
            var props = typeof(T).GetProperties();

            foreach (var prop in props)
            {

                if (prop.GetCustomAttributes(true).Length > 0)
                {
                    var grootAttr = (GrootFieldAttribute)prop.GetCustomAttributes(true).GetValue(0);
                    csv.Append(grootAttr.GetGrootFields() + ", ");
                }
                else
                {
                    csv.Append(prop.Name + ", ");
                }
            }
            csv = csv.Remove(csv.Length - 2, 2);
            csv.AppendLine();

            foreach (var obj in objList)
            {
                foreach (var prop in props)
                {
                    if (prop.PropertyType.IsEnum)
                    {
                        var name = Enum.GetName(prop.PropertyType, prop.GetValue(obj));
                        csv.Append(name + ", ");
                    }
                    else
                    {
                        csv.Append(prop.GetValue(obj) + ", ");
                    }
                }
                csv = csv.Remove(csv.Length - 2, 2);
                csv.AppendLine();
            }

            File.WriteAllText(filePath, csv.ToString());
            return csv.ToString();
        }

    }
}