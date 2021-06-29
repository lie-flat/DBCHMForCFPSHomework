using DDTek.Oracle;
using MySql.Data.MySqlClient;
using Npgsql;
using Oracle.ManagedDataAccess.Client;
using MJTop.Data.Database;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using IBM.Data.DB2;
using System.Data.Common;

namespace MJTop.Data
{
    internal class DBFactory
    {
        internal static DB CreateInstance(DBType dbType, string connectionString, int cmdTimeOut)
        {
            switch (dbType)
            {
                case DBType.SqlServer:
                    return new SqlServerDB(dbType, SqlClientFactory.Instance, connectionString,cmdTimeOut);
                case DBType.MySql:
                    return new MySqlDB(dbType, MySqlClientFactory.Instance, connectionString, cmdTimeOut);
                case DBType.Oracle:
                    return new OracleDB(dbType, OracleClientFactory.Instance, connectionString, cmdTimeOut);
                case DBType.OracleDDTek:
                    return new OracleDDTekDB(dbType, OracleFactory.Instance, connectionString, cmdTimeOut);
                case DBType.PostgreSql:
                    return new PostgreSqlDB(dbType, NpgsqlFactory.Instance, connectionString, cmdTimeOut);
                case DBType.SQLite:
                    return new SQLiteDB(dbType, SQLiteFactory.Instance, connectionString, cmdTimeOut);
                case DBType.DB2:
                    return new DB2DDTekDB(dbType, DB2Factory.Instance, connectionString, cmdTimeOut);
                default:
                    throw new ArgumentException("未支持的数据库类型！");
            }
        }

        internal static void TryConnect(DBType dbType, string connectionString)
        {
            DbConnection conn = null;
            switch (dbType)
            {
                case DBType.SqlServer:
                    conn = SqlClientFactory.Instance.CreateConnection();
                    break;
                case DBType.MySql:
                    conn = MySqlClientFactory.Instance.CreateConnection();
                    break;
                case DBType.Oracle:
                    conn = OracleClientFactory.Instance.CreateConnection();
                    break;
                case DBType.OracleDDTek:
                    conn = OracleFactory.Instance.CreateConnection();
                    break;
                case DBType.PostgreSql:
                    conn = NpgsqlFactory.Instance.CreateConnection();
                    break;
                case DBType.SQLite:
                    conn = SQLiteFactory.Instance.CreateConnection();
                    break;
                case DBType.DB2:
                    conn = DB2Factory.Instance.CreateConnection();
                    break;
                default:
                    throw new ArgumentException("未支持的数据库类型！");
            }


            try
            {
                conn.ConnectionString = connectionString;
                conn.Open();
            }
            finally
            {
                conn?.Close();
            }
        }
    }
}
