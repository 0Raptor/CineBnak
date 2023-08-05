﻿using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CineBank
{
    public class Database
    {
        public DatabaseConfig Config { get; private set; }
        public SQLiteConnection Connection { get; private set; }

        public Database() { }
        public Database(string path, string baseDir = "")
        {
            // check file path
            if (!File.Exists(path)) throw new Exception("ERROR: No DB: The supplied file does not exist.");

            // connect to db
            Connection = CreateConnection(path);

            // apply configuration
            if (baseDir != "") Config.BaseDir = baseDir;
        }

        private SQLiteConnection CreateConnection(string path)
        {

            SQLiteConnection sqlite_conn;

            // Create a new database connection:
            sqlite_conn = new SQLiteConnection(String.Format("Data Source={0}; Version = 3; FailIfMissing=True; Compress = True;", path));
            try
            {
                sqlite_conn.Open();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                throw new Exception("ERROR: DB Connection failed: Failed to connect to supplied database.");
            }

            // Read config from db:
            using (SQLiteCommand cmd = sqlite_conn.CreateCommand())
            {
                cmd.CommandText = "SELECT * FROM settings;";
                using (SQLiteDataReader reader = cmd.ExecuteReader())
                {
                    Dictionary<string, string> res = new Dictionary<string, string>();

                    while (reader.Read())
                    {
                        res.Add(reader.GetString(1), reader.GetString(2));
                    }

                    Config = new DatabaseConfig(res["version"], res["baseDir"]);
                }
            }

            return sqlite_conn;
        }

        /// <summary>
        /// Check if all entries in the file-table have an absolute path assigned
        /// </summary>
        /// <returns>true if all file entries have a absolute path</returns>
        public bool CheckFilesHaveAbsolutePath()
        {
            using (SQLiteCommand cmd = Connection.CreateCommand())
            {
                cmd.CommandText = "SELECT Path FROM files;";
                using (SQLiteDataReader reader = cmd.ExecuteReader())
                {
                    Dictionary<string, string> res = new Dictionary<string, string>();

                    while (reader.Read())
                    {
                        if (!Database.IsFullPath(reader.GetString(0)))
                            return false;
                    }
                }
            }
            return true;
        }

        /// <summary>
        /// Create a new SQLite-Database at the given location
        /// </summary>
        /// <param name="path">Path to the file storing the SQLite-Database</param>
        /// <param name="conf">(optional) Supply a configuration to change the defaults</param>
        public static void Init(string path, DatabaseConfig? conf = null)
        {
            if (conf == null)
                conf = new DatabaseConfig(""); // set defaults --> current scheme version and absolute paths

            // set path as connection string and create new connection with it - new file will be created if not present
            string cs = "URI=file:" + path;
            using var con = new SQLiteConnection(cs);
            con.Open();

            // create new command
            using var cmd = new SQLiteCommand(con);

            // add each table
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS movies (Id INTEGER PRIMARY KEY AUTOINCREMENT UNIQUE NOT NULL, Title TEXT (255) UNIQUE NOT NULL, Description TEXT NOT NULL, Duration TEXT (10) NOT NULL, Type INTEGER NOT NULL, Released TEXT (10), Cast TEXT, Director TEXT, Score TEXT, MaxResolution TEXT (10));";
            cmd.ExecuteNonQuery();

            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS files (Id INTEGER PRIMARY KEY AUTOINCREMENT UNIQUE NOT NULL, Movie INTEGER REFERENCES movies (Id) ON DELETE CASCADE NOT NULL, Type INTEGER NOT NULL, Open INTEGER NOT NULL, Path TEXT NOT NULL UNIQUE);";
            cmd.ExecuteNonQuery();

            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS genres (Id INTEGER PRIMARY KEY AUTOINCREMENT UNIQUE NOT NULL, Name TEXT (25) UNIQUE NOT NULL);";
            cmd.ExecuteNonQuery();

            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS languages (Id INTEGER PRIMARY KEY AUTOINCREMENT UNIQUE NOT NULL, Name TEXT (10) UNIQUE NOT NULL);";
            cmd.ExecuteNonQuery();

            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS movies2genres (Id INTEGER PRIMARY KEY AUTOINCREMENT UNIQUE NOT NULL, Genre INTEGER REFERENCES genres (Id) ON DELETE RESTRICT NOT NULL, Movie INTEGER REFERENCES movies (id) ON DELETE CASCADE NOT NULL);";
            cmd.ExecuteNonQuery();

            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS movies2languages (Id INTEGER PRIMARY KEY AUTOINCREMENT UNIQUE NOT NULL, Language INTEGER REFERENCES languages (Id) ON DELETE RESTRICT NOT NULL, Movie INTEGER REFERENCES movies (id) ON DELETE CASCADE NOT NULL, Type TEXT (1) NOT NULL);";
            cmd.ExecuteNonQuery();

            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS settings (Id INTEGER PRIMARY KEY AUTOINCREMENT UNIQUE NOT NULL, Key TEXT (25) UNIQUE NOT NULL, Value TEXT NOT NULL);";
            cmd.ExecuteNonQuery();

            // insert settings
            cmd.CommandText = "INSERT INTO settings(Key, Value) VALUES('version', @v)";
            cmd.Parameters.AddWithValue("@v", conf.Version);
            cmd.Prepare();
            cmd.ExecuteNonQuery();

            cmd.CommandText = "INSERT INTO settings(Key, Value) VALUES('baseDir', @dir)";
            cmd.Parameters.AddWithValue("@dir", conf.BaseDir);
            cmd.Prepare();
            cmd.ExecuteNonQuery();
        }

        /// <summary>
        /// Determins if a given path is an absolute path without throwing exceptions on invalid paths
        /// </summary>
        /// <param name="path">Path to validate</param>
        /// <returns>true if path is an absolute path</returns>
        public static bool IsFullPath(string path)
        {
            // https://stackoverflow.com/questions/5565029/check-if-full-path-given/35046453
            /*
            return !String.IsNullOrWhiteSpace(path)
                && path.IndexOfAny(System.IO.Path.GetInvalidPathChars().ToArray()) == -1
                && Path.IsPathRooted(path)
                && !Path.GetPathRoot(path).Equals(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal);
            */
            if (string.IsNullOrWhiteSpace(path) || path.IndexOfAny(Path.GetInvalidPathChars()) != -1 || !Path.IsPathRooted(path))
                return false;

            string pathRoot = Path.GetPathRoot(path);
            if (pathRoot.Length <= 2 && pathRoot != "/") // Accepts X:\ and \\UNC\PATH, rejects empty string, \ and X:, but accepts / to support Linux
                return false;

            if (pathRoot[0] != '\\' || pathRoot[1] != '\\')
                return true; // Rooted and not a UNC path

            return pathRoot.Trim('\\').IndexOf('\\') != -1; // A UNC server name without a share name (e.g "\\NAME" or "\\NAME\") is invalid
        }
    }

    public class DatabaseConfig
    {
        public string BaseDir { get; set; }
        public string Version { get; set; }

        public DatabaseConfig() { }
        public DatabaseConfig(string baseDir, string version = "1.0")
        {
            BaseDir = baseDir;
            Version = version;
        }
    }
}
