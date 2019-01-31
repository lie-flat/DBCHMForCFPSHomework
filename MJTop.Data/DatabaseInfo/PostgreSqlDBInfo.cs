﻿using MJTop.Data.SPI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MJTop.Data.DatabaseInfo
{
    public class PostgreSqlDBInfo : IDBInfo
    {
        internal DB Db { get; set; }

        /// <summary>
        /// 数据库工具
        /// </summary>
        public Tool Tools
        {
            get;
            private set;
        }
        public PostgreSqlDBInfo(DB db)
        {
            this.Db = db;
            Refresh();
            this.Tools = new Tool(db, this);
        }

        public string DBName
        {
            get { return (Db.ConnectionStringBuilder as Npgsql.NpgsqlConnectionStringBuilder).Database; }
        }

        public NameValueCollection TableComments { get; private set; } = new NameValueCollection();

        public List<string> TableNames { get; private set; } = new List<string>();
        
        public IgCaseDictionary<TableInfo> TableInfoDict { get; private set; } 

        public IgCaseDictionary<List<string>> TableColumnNameDict { get; private set; } 

        public IgCaseDictionary<List<ColumnInfo>> TableColumnInfoDict { get; private set; } 

        public IgCaseDictionary<NameValueCollection> TableColumnComments { get; private set; } 

        private IgCaseDictionary<ColumnInfo> DictColumnInfo { get; set; } 

        private IgCaseDictionary<string> Dict_Table_Identity_Column { get; set; } = new IgCaseDictionary<string>();

        public NameValueCollection Views { get; private set; }

        public NameValueCollection Procs { get; private set; }

        public List<string> DBNames { get; set; } = new List<string>();

        public ColumnInfo this[string tableName, string columnName]
        {
            get
            {
                ColumnInfo colInfo;
                var strKey = (tableName + "@" + columnName);
                DictColumnInfo.TryGetValue(strKey, out colInfo);
                return colInfo;
            }
        }

        public List<string> this[string tableName]
        {
            get
            {
                List<string> colNames;
                TableColumnNameDict.TryGetValue(tableName, out colNames);
                return colNames;
            }
        }


        public bool Refresh()
        {
            this.DictColumnInfo = new IgCaseDictionary<ColumnInfo>();
            this.TableInfoDict = new IgCaseDictionary<TableInfo>();
            this.TableColumnNameDict = new IgCaseDictionary<List<string>>();
            this.TableColumnInfoDict = new IgCaseDictionary<List<ColumnInfo>>();
            this.TableColumnComments = new IgCaseDictionary<NameValueCollection>();

            string dbSql = "select datname from pg_catalog.pg_database order by datname asc";
            string strSql = "select a.relname as name , b.description as value from pg_class a left join  (select * from pg_description where objsubid = 0 ) b on a.oid = b.objoid where a.relname in (select tablename from pg_tables where schemaname = 'public') order by a.relname asc";

            string viewSql = "SELECT viewname,definition FROM pg_views where schemaname='public' order by viewname asc";
            string procSql = "select proname,prosrc from  pg_proc where pronamespace=(SELECT pg_namespace.oid FROM pg_namespace WHERE nspname = 'public') order by proname asc";

            try
            {
               
                this.DBNames = Db.ReadList<string>(dbSql);
               
                this.TableComments = Db.ReadNameValues(strSql);

                //this.Views = Db.ReadNameValues(viewSql);

                //this.Procs = Db.ReadNameValues(procSql);

                if (this.TableComments != null && this.TableComments.Count > 0)
                {
                    this.TableNames = TableComments.AllKeys.ToList();

                    List<Task> lstTask = new List<Task>();

                    foreach (var tableName in this.TableNames)
                    {
                        Task task = Task.Run(() =>
                        {
                            TableInfo tabInfo = new TableInfo();
                            tabInfo.TableName = tableName;
                            tabInfo.TabComment = TableComments[tableName];

                            strSql = @"select ordinal_position as Colorder,column_name as ColumnName,data_type as TypeName,
coalesce(character_maximum_length,numeric_precision,-1) as Length,numeric_scale as Scale,
case is_nullable when 'NO' then 0 else 1 end as CanNull,column_default as DefaultVal,
case  when position('nextval' in column_default)>0 then 1 else 0 end as IsIdentity, 
case when b.pk_name is null then 0 else 1 end as IsPK,c.DeText
from information_schema.columns 
left join (
	select pg_attr.attname as colname,pg_constraint.conname as pk_name from pg_constraint  
	inner join pg_class on pg_constraint.conrelid = pg_class.oid 
	inner join pg_attribute pg_attr on pg_attr.attrelid = pg_class.oid and  pg_attr.attnum = pg_constraint.conkey[1] 
	inner join pg_type on pg_type.oid = pg_attr.atttypid
	where pg_class.relname =:tableName and pg_constraint.contype='p' 
) b on b.colname = information_schema.columns.column_name
left join (
	select attname,description as DeText from pg_class
	left join pg_attribute pg_attr on pg_attr.attrelid= pg_class.oid
	left join pg_description pg_desc on pg_desc.objoid = pg_attr.attrelid and pg_desc.objsubid=pg_attr.attnum
	where pg_attr.attnum>0 and pg_attr.attrelid=pg_class.oid and pg_class.relname=:tableName
)c on c.attname = information_schema.columns.column_name
where table_schema='public' and table_name=:tableName order by ordinal_position asc";
                            try
                            {
                                tabInfo.Colnumns = Db.GetDataTable(strSql, new { tableName = tableName }).ConvertToListObject<ColumnInfo>();
                                List<string> lstColName = new List<string>();
                                NameValueCollection nvcColDeText = new NameValueCollection();
                                foreach (ColumnInfo colInfo in tabInfo.Colnumns)
                                {
                                    lstColName.Add(colInfo.ColumnName);
                                    nvcColDeText.Add(colInfo.ColumnName, colInfo.DeText);

                                    var strKey = (tableName + "@" + colInfo.ColumnName);
                                    this.DictColumnInfo.Add(strKey, colInfo);
                                    if (colInfo.IsIdentity)
                                    {
                                        this.Dict_Table_Identity_Column[tableName] = colInfo.ColumnName;
                                    }

                                    if (colInfo.IsPK)
                                    {
                                        tabInfo.PriKeyColName = colInfo.ColumnName;
                                        if (colInfo.IsIdentity)
                                        {
                                            tabInfo.PriKeyType = PrimaryKeyType.AUTO;
                                        }
                                        else
                                        {
                                            tabInfo.PriKeyType = PrimaryKeyType.SET;
                                        }
                                    }

                                    Global.Dict_PostgreSql_DbType.TryGetValue(colInfo.TypeName, out DbType type);
                                    colInfo.DbType = type;
                                }

                                this.TableInfoDict.Add(tableName, tabInfo);
                                this.TableColumnNameDict.Add(tableName, lstColName);
                                this.TableColumnInfoDict.Add(tableName, tabInfo.Colnumns);
                                this.TableColumnComments.Add(tableName, nvcColDeText);
                            }
                            catch (Exception ex)
                            {
                                LogUtils.LogError("DB", Developer.SysDefault, ex);
                            }
                        });
                        lstTask.Add(task);
                        if (lstTask.Count(t => t.Status != TaskStatus.RanToCompletion) >= 50)
                        {
                            Task.WaitAny(lstTask.ToArray());
                            lstTask = lstTask.Where(t => t.Status != TaskStatus.RanToCompletion).ToList();
                        }
                    }
                    Task.WaitAll(lstTask.ToArray());
                }
            }
            catch (Exception ex)
            {
                LogUtils.LogError("DB", Developer.SysDefault, ex);
                return false;
            }
            return this.TableComments.Count == this.TableInfoDict.Count;
        }

        internal string IdentityColumnName(string tableName)
        {
            tableName = (tableName ?? string.Empty).ToLower();
            string colName;
            if (Dict_Table_Identity_Column.TryGetValue(tableName, out colName))
            {
                return colName;
            }
            return string.Empty;
        }

        public Dictionary<string, DateTime> GetTableStruct_Modify()
        {
            throw new Exception("暂未提供查询PostgreSql表结构修改时间的功能！");
        }

        public bool IsExistTable(string tableName)
        {
            return TableNames.Contains(tableName);
        }

        public bool IsExistColumn(string tableName, string columnName)
        {
            var strKey = (tableName + "@" + columnName).ToLower();
            return DictColumnInfo.ContainsKey(strKey);
        }

        public string GetColumnComment(string tableName, string columnName)
        {
            ColumnInfo colInfo = null;
            var strKey = (tableName + "@" + columnName).ToLower();
            DictColumnInfo.TryGetValue(strKey, out colInfo);
            return colInfo?.DeText;
        }

        public string GetTableComment(string tableName)
        {
            Db.CheckTabStuct(tableName);
            return TableComments[tableName];
        }

        public List<ColumnInfo> GetColumns(string tableName)
        {
            Db.CheckTabStuct(tableName);
            tableName = (tableName ?? string.Empty).ToLower();
            List<ColumnInfo> colInfos = null;
            TableColumnInfoDict.TryGetValue(tableName, out colInfos);
            return colInfos;
        }

        public bool SetTableComment(string tableName, string comment)
        {
            Db.CheckTabStuct(tableName);

            tableName = TableInfoDict[tableName].TableName;
           
            string upsert_sql = string.Empty;
            comment = (comment ?? string.Empty).Replace("'", "");
            try
            {
                upsert_sql = "comment on table \"" + tableName + "\" is '" + comment + "'";
                Db.ExecSql(upsert_sql);

                TableComments[tableName] = comment;

                var tabInfo = TableInfoDict[tableName];
                tabInfo.TabComment = comment;
                TableInfoDict[tableName] = tabInfo;
            }
            catch (Exception ex)
            {
                LogUtils.LogError("DB", Developer.SysDefault, ex, upsert_sql);
                return false;
            }
            return true;
        }

        public bool SetColumnComment(string tableName, string columnName, string comment)
        {
            Db.CheckTabStuct(tableName, columnName);

            tableName = TableInfoDict[tableName].TableName;
            columnName = this[tableName, columnName].ColumnName;
            
            string upsert_sql = string.Empty;
            comment = (comment ?? string.Empty).Replace("'", "");
            try
            {
                upsert_sql = "comment on column \"" + tableName + "\".\"" + columnName + "\" is '" + comment + "'";
                Db.ExecSql(upsert_sql);

                List<ColumnInfo> lstColInfo = TableColumnInfoDict[tableName];

                NameValueCollection nvcColDesc = new NameValueCollection();
                lstColInfo.ForEach(t =>
                {
                    if (t.ColumnName.Equals(columnName))
                    {
                        t.DeText = comment;
                    }
                    nvcColDesc.Add(t.ColumnName, t.DeText);
                });

                TableColumnInfoDict.Remove(tableName);
                TableColumnInfoDict.Add(tableName, lstColInfo);

                TableColumnComments.Remove(tableName);
                TableColumnComments.Add(tableName, nvcColDesc);

                var strKey = (tableName + "@" + columnName);
                ColumnInfo colInfo = DictColumnInfo[strKey];
                colInfo.DeText = comment;
                DictColumnInfo[strKey] = colInfo;
            }
            catch (Exception ex)
            {
                LogUtils.LogError("DB", Developer.SysDefault, ex, upsert_sql);
                return false;
            }
            return true;
        }

        public bool DropTable(string tableName)
        {
            Db.CheckTabStuct(tableName);
            tableName = (tableName ?? string.Empty).ToLower();
            string drop_sql = string.Empty;
            try
            {

                drop_sql = "drop table " + tableName;
                Db.ExecSql(drop_sql);

                this.TableComments.Remove(tableName);

                this.TableNames = TableComments.AllKeys.ToList();

                this.TableInfoDict.Remove(tableName);
                this.TableColumnInfoDict.Remove(tableName);
                this.TableColumnComments.Remove(tableName);

                var lstColName = TableColumnNameDict[tableName];

                foreach (var colName in lstColName)
                {
                    var strKey = (tableName + "@" + colName).ToLower();
                    this.DictColumnInfo.Remove(strKey);
                }

                this.TableColumnNameDict.Remove(tableName);

            }
            catch (Exception ex)
            {
                LogUtils.LogError("DB", Developer.SysDefault, ex, drop_sql);
                return false;
            }
            return true;
        }

        public bool DropColumn(string tableName, string columnName)
        {
            Db.CheckTabStuct(tableName, columnName);

            tableName = (tableName ?? string.Empty).ToLower();
            columnName = (columnName ?? string.Empty).ToLower();

            var strKey = (tableName + "@" + columnName);

            string drop_sql = "alter table {0} drop column {1}";
            try
            {               
                drop_sql = string.Format(drop_sql, tableName, columnName);
                Db.ExecSql(drop_sql);

                this.DictColumnInfo.Remove(strKey);

                var nvc = TableColumnComments[tableName];
                nvc.Remove(columnName);
                TableColumnNameDict[tableName] = nvc.AllKeys.ToList();

                var lstColInfo = TableColumnInfoDict[tableName];
                ColumnInfo curColInfo = null;
                lstColInfo.ForEach(t =>
                {
                    if (t.ColumnName.Equals(columnName))
                    {
                        curColInfo = t;

                        //tabInfo 对应的 主键类型和主键列 也需要 跟着修改。
                        if (curColInfo.IsPK)
                        {
                            var tabInfo = TableInfoDict[tableName];
                            tabInfo.PriKeyType = PrimaryKeyType.UNKNOWN;
                            tabInfo.PriKeyColName = null;
                            TableInfoDict[tableName] = tabInfo;
                        }
                        return;
                    }
                });
                lstColInfo.Remove(curColInfo);
                TableColumnInfoDict[tableName] = lstColInfo;

            }
            catch (Exception ex)
            {
                LogUtils.LogError("DB", Developer.SysDefault, ex, drop_sql);
                return false;
            }
            return true;
        }
    }
}