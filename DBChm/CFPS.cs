using DocTools.Dtos;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Newtonsoft.Json.Linq;

namespace DBCHM {
	internal static class CFPS {
		internal const string CFPSRootDir = @"D:\BigHomework\2021-2022-1";
		internal static void ProcessDto(DBDto dto) {
			foreach (var table in dto.Tables) {
				string name = table.TableName;
				var (basename, year) = GetYearAndTableBaseNameByTableName(name);
				dynamic json = JObject.Parse(File.ReadAllText(GetJSONPath(year, basename)));
				table.Comment = $"{year}年的 {basename} 数据";
				foreach(var column in table.Columns) {
					column.Comment = json[column.ColumnName].key;
				}
			}
		}

		internal static (string, string) GetYearAndTableBaseNameByTableName(string tableName) {
			var li = tableName.Split('_');
			return (li[0], li[1]);
		}

		internal static string GetJSONPath(string year, string baseName) {
			var dir = new DirectoryInfo(Path.Combine(CFPSRootDir, $@"dataset\CFPS {year}\Data\Stata"));
			var files = dir.EnumerateFiles();
			foreach (var file in files) {
				if (file.Name.StartsWith($"cfps{year}{baseName}_") &&
					file.Name.EndsWith("dta.schemas.json")) {
					return file.FullName;
				}
			}
			throw new FileNotFoundException($"JSON for {year}, {baseName} not found!");
		}
	}
}
