using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Text;
using System.Linq;
using System.Configuration;
using System.Web;

using MySql.Data.MySqlClient;

namespace LT
{
    public class DBConnection : IDisposable
    {
        public static string DBConnectionString
        {
            get
            {
                return GetConnectionString();
            }
        }

        public static string GetConnectionString(string siteName = null)
        {
            var result = ConfigurationManager.AppSettings["DatabaseConnection"];

            if (siteName == null)
                siteName = ConfigurationManager.AppSettings["SiteName"];

            if (result == null)
            {
                var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey("Software\\" + siteName);
                if (key != null)
                    result = (String)key.GetValue("Database Connection");
            }

            if (result == null)
            {
                var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey("Software\\LT");
                if (key != null)
                    result = (String)key.GetValue(siteName);
            }

            if (result == null)
                throw new ConfigurationErrorsException(String.Format("Database connection string for sitename {0} not found in app settings or registry. {1}", siteName, Microsoft.Win32.Registry.CurrentUser.Name));

            return result;
        }

        public bool IsDebug { get; set; }
        //{
        //    get
        //    {
        //        //if (HttpContext.Current != null && HttpContext.Current.Request != null)
        //        //    return HttpContext.Current.Request.IsLocal;
        //        return false;
        //    }
        //}

        String _connectionString;

        public DBConnection()
        {
            _connectionString = DBConnectionString;
        }

        public DBConnection(string connectionString)
        {
            _connectionString = connectionString;
        }

        public void Dispose()
        {
            try
            {
                if (database != null)
                {
                    database.Close();
                    database.Dispose();
                }
            }
            finally
            {
                GC.SuppressFinalize(this);
            }
        }

        private IDbConnection database;
        public IDbConnection Database
        {
            get
            {
                if (database == null)
                {
                    database = new MySqlConnection(_connectionString);
                    database.Open();
                }
                return database;
            }
        }

        public int QueryCount { get; set; }
        public string LastQuery { get; set; }

        /// <summary>
        /// Uses dapper to execute a statement.
        /// var result = connection.ExecuteWithParams("insert into Dog values (@Age, @Id)", new { Age = (int?)null, Id = guid });
        /// Since this query is cached by dapper, be sure to use the parameterized querys and do not use String.Format to encode the parameters or you will leak memory.
        /// </summary>
        public int ExecuteWithParams(string command, object param)
        {
            return Dapper.SqlMapper.Execute(Database, command, param);
        }

        public int Execute(string command, params object[] arguments)
        {
            return Execute(String.Format(command, arguments));
        }

        const int defaultCommandTimeout = 30;

        public int Execute(string command)
        {
            return ExecuteWithTimeout(command, defaultCommandTimeout);
        }

        public int ExecuteWithTimeout(string command, int timeout)
        {
            QueryCount++;
            LastQuery = command;
            using (var _command = Database.CreateCommand())
            {
                if (IsDebug)
                    System.Diagnostics.Debug.Print("Execute: {0}", command);
                _command.CommandText = command;
                _command.CommandTimeout = timeout;
                return _command.ExecuteNonQuery();
            }
        }

        public object Evaluate(string command, params object[] arguments)
        {
            return Evaluate(String.Format(command, arguments));
        }

        public object Evaluate(string command)
        {
            QueryCount++;
            LastQuery = command;
            using (var _command = Database.CreateCommand())
            {
                _command.CommandText = command;
                var result = _command.ExecuteScalar();
                if (IsDebug)
                    System.Diagnostics.Debug.Print("Evaluate: {0} = {1}", command, result);
                return result;
            }
        }

        public IDataReader OpenQuery(string command, params object[] arguments)
        {
            return OpenQuery(String.Format(command, arguments));
        }

        public IDataReader OpenQuery(string command)
        {
            QueryCount++;
            LastQuery = command;
            var _command = Database.CreateCommand();
            _command.CommandText = command;
            if (IsDebug)
                System.Diagnostics.Debug.Print("OpenQuery: {0}", command);
            return _command.ExecuteReader();
        }

        public IDataReader OpenQuery(string command, int timeout)
        {
            QueryCount++;
            LastQuery = command;
            var _command = Database.CreateCommand();
            if (IsDebug)
                System.Diagnostics.Debug.Print("OpenQuery: {0}", command);
            _command.CommandText = command;
            _command.CommandTimeout = timeout;
            return _command.ExecuteReader();
        }

        public static Hashtable GetSingleRow(IDataRecord query)
        {
            var result = new Hashtable(query.FieldCount);
            for (int count = 0; count < query.FieldCount; count++)
            {
                if (query[count] is byte || query[count] is sbyte || query[count] is Int16 || query[count] is UInt16 || query[count] is UInt32 || query[count] is bool)
                    result[query.GetName(count)] = Convert.ToInt32(query[count]);
                else
                    result[query.GetName(count)] = query[count];
            }
            return result;
        }

        public Hashtable EvaluateRow(string command, params object[] arguments)
        {
            return EvaluateRow(String.Format(command, arguments));
        }

        public Hashtable EvaluateRow(string command)
        {
            using (var row = OpenQuery(command))
            {
                if (row.Read())
                    return GetSingleRow(row);
                else
                    return null;
            }
        }

        /// <summary>
        /// Uses dapper to create a single strongly typed object from a query.
        /// var dog = connection.Evaluate&lt;Dog&gt;("select Age = @Age, Id = @Id", new { Age = (int?)null, Id = guid });
        /// Since this query is cached by dapper, be sure to use the parameterized querys and do not use String.Format to encode the parameters or you will leak memory.
        /// </summary>
        public T Evaluate<T>(string sql, object param = null)
        {
            return Dapper.SqlMapper.Query<T>(Database, sql, param).First();
        }

        public List<Hashtable> EvaluateTable(string command, params object[] arguments)
        {
            return EvaluateTable(String.Format(command, arguments));
        }

        public List<Hashtable> EvaluateTable(string command)
        {
            using (var query = OpenQuery(command))
            {
                var result = new List<Hashtable>();
                while (query.Read())
                    result.Add(GetSingleRow(query));
                return result;
            }
        }

        /// <summary>
        /// Uses dapper to create a set of strongly typed objects from a query.
        /// var dog = connection.Query&lt;Dog&gt;("select Age = @Age, Id = @Id", new { Age = (int?)null, Id = guid });
        /// Since this query is cached by dapper, be sure to use the parameterized querys and do not use String.Format to encode the parameters or you will leak memory.
        /// </summary>
        public IEnumerable<T> Query<T>(string command)
        {
            return Dapper.SqlMapper.Query<T>(Database, command, null);
        }

        public List<Hashtable> EvaluateTableWithTimeout(int timeout, string command, params object[] arguments)
        {
            command = String.Format(command, arguments);
            using (var query = OpenQuery(command, timeout))
            {
                var result = new List<Hashtable>();
                while (query.Read())
                    result.Add(GetSingleRow(query));
                return result;
            }
        }

        public long LastInsertID
        {
            get { return (long)Evaluate("select LAST_INSERT_ID()"); }
        }

        public static object IfNull(object nullable, object value)
        {
            if (nullable == null)
                return value;
            else
                return nullable;
        }

        /// <summary>
        /// Quotes the specified string with backslashes. 
        /// The characters to be quoted are: quote('), double quote("), backslash(\) and null(0).
        /// </summary>
        /// <param name="text">The string to be quoted with backslashes.</param>
        /// <returns>Returns the quoted string.</returns>
        public static string AddSlashes(string text)
        {
            text = text.Replace("\\", "\\\\");
            text = text.Replace("\'", "\\\'");
            text = text.Replace("’", "\\’"); // unicode 8217 "Closing Single Quote" http://www.cs.sfu.ca/~ggbaker/reference/characters/
            text = text.Replace("‘", "\\‘"); // unicode 8216 "Opening Single Quote"
            text = text.Replace("′", "\\′"); // unicode 8242 "Prime"
            text = text.Replace("ʹ", "\\ʹ"); // unicode 697 #02b9 "MODIFIER LETTER PRIME"
            text = text.Replace("\"", "\\\"");
            text = text.Replace("\0", "\\0");

            return text;
        }

        public static string FormatDateTime(DateTime value)
        {
            return value.ToString("yyyy-MM-dd HH:mm:ss");
        }
    }
}