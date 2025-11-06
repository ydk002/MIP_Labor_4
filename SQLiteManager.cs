using System;
using System.Data;
using System.Data.SQLite;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Labor3
{
    /// <summary>
    /// Manages SQLite database operations using System.Data.SQLite
    /// </summary>
    public class SQLiteManager : IDisposable
    {
        private readonly string _databasePath;
        private readonly AsyncLogger _logger;
        private SQLiteConnection _connection;

        public SQLiteManager(string databasePath, AsyncLogger logger)
        {
            _databasePath = databasePath;
            _logger = logger;
        }

        /// <summary>
        /// Opens a connection to the SQLite database
        /// </summary>
        private async Task<SQLiteConnection> GetConnectionAsync()
        {
            if (_connection == null || _connection.State != ConnectionState.Open)
            {
                string connectionString = $"Data Source={_databasePath};Version=3;";
                _connection = new SQLiteConnection(connectionString);
                await Task.Run(() => _connection.Open());
                _logger?.LogEvent($"Database connection opened: {_databasePath}");
            }
            return _connection;
        }

        /// <summary>
        /// Executes a non-query SQL command (CREATE, INSERT, UPDATE, DELETE, DROP)
        /// </summary>
        private async Task<int> ExecuteNonQueryAsync(string sqlCommand)
        {
            try
            {
                _logger?.LogEvent($"Executing SQL command: {sqlCommand}");

                SQLiteConnection conn = await GetConnectionAsync();
                
                using (SQLiteCommand command = new SQLiteCommand(sqlCommand, conn))
                {
                    int rowsAffected = await Task.Run(() => command.ExecuteNonQuery());
                    _logger?.LogEvent($"SQL command completed. Rows affected: {rowsAffected}");
                    return rowsAffected;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogEvent($"ERROR executing SQL command: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Executes a SELECT query and returns the results as a formatted string
        /// </summary>
        private async Task<string> ExecuteQueryAsync(string sqlCommand)
        {
            try
            {
                _logger?.LogEvent($"Executing SQL query: {sqlCommand}");

                SQLiteConnection conn = await GetConnectionAsync();
                StringBuilder result = new StringBuilder();

                using (SQLiteCommand command = new SQLiteCommand(sqlCommand, conn))
                {
                    await Task.Run(() =>
                    {
                        using (SQLiteDataReader reader = command.ExecuteReader())
                        {
                            // Get column names
                            int columnCount = reader.FieldCount;
                            string[] columnNames = new string[columnCount];
                            for (int i = 0; i < columnCount; i++)
                            {
                                columnNames[i] = reader.GetName(i);
                            }

                            // Create header
                            result.AppendLine(string.Join(" | ", columnNames));
                            result.AppendLine(new string('-', 50));

                            // Read data rows
                            int rowCount = 0;
                            while (reader.Read())
                            {
                                string[] values = new string[columnCount];
                                for (int i = 0; i < columnCount; i++)
                                {
                                    values[i] = reader.GetValue(i)?.ToString() ?? "NULL";
                                }
                                result.AppendLine(string.Join(" | ", values));
                                rowCount++;
                            }

                            if (rowCount == 0)
                            {
                                result.AppendLine("(No rows found)");
                            }
                            else
                            {
                                result.AppendLine();
                                result.AppendLine($"Total rows: {rowCount}");
                            }
                        }
                    });
                }

                _logger?.LogEvent($"SQL query completed successfully");
                return result.ToString();
            }
            catch (Exception ex)
            {
                _logger?.LogEvent($"ERROR executing SQL query: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Performs all required database operations for the task
        /// </summary>
        public async Task<string> PerformDatabaseTasksAsync()
        {
            StringBuilder report = new StringBuilder();
            report.AppendLine("=== SQLite Database Operations (System.Data.SQLite) ===");
            report.AppendLine();

            try
            {
                // Ensure database file exists or will be created
                bool isNewDatabase = !File.Exists(_databasePath);
                if (isNewDatabase)
                {
                    _logger?.LogEvent("Creating new database file");
                    SQLiteConnection.CreateFile(_databasePath);
                    report.AppendLine("✓ New database file created");
                    report.AppendLine();
                }

                // Step 1: Create table with specific fields
                _logger?.LogEvent("Step 1: Creating Users table");
                report.AppendLine("Step 1: Creating Users table...");
                
                string createTableCommand = @"
                    CREATE TABLE IF NOT EXISTS Users (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        FirstName TEXT NOT NULL,
                        LastName TEXT NOT NULL,
                        CNP TEXT NOT NULL
                    );";
                
                await ExecuteNonQueryAsync(createTableCommand);
                
                report.AppendLine("✓ Users table created successfully");
                report.AppendLine("  Fields: Id (INTEGER), FirstName (TEXT), LastName (TEXT), CNP (TEXT)");
                report.AppendLine();

                // Step 2: Insert 3 users
                _logger?.LogEvent("Step 2: Inserting 3 users");
                report.AppendLine("Step 2: Inserting 3 users...");
                
                await ExecuteNonQueryAsync("INSERT INTO Users (FirstName, LastName, CNP) VALUES ('John', 'Doe', '1234567890123');");
                await ExecuteNonQueryAsync("INSERT INTO Users (FirstName, LastName, CNP) VALUES ('Jane', 'Smith', '9876543210987');");
                await ExecuteNonQueryAsync("INSERT INTO Users (FirstName, LastName, CNP) VALUES ('Robert', 'Johnson', '5555555555555');");
                
                report.AppendLine("✓ 3 users inserted successfully:");
                report.AppendLine("  - John Doe (CNP: 1234567890123)");
                report.AppendLine("  - Jane Smith (CNP: 9876543210987)");
                report.AppendLine("  - Robert Johnson (CNP: 5555555555555)");
                report.AppendLine();

                // Step 3: Show all data entries
                _logger?.LogEvent("Step 3: Displaying all users");
                report.AppendLine("Step 3: Showing all data entries...");
                report.AppendLine();
                
                string selectCommand = "SELECT Id, FirstName, LastName, CNP FROM Users;";
                string userData = await ExecuteQueryAsync(selectCommand);
                
                report.AppendLine(userData);
                report.AppendLine();

                // Step 4: Delete all data entries
                _logger?.LogEvent("Step 4: Deleting all data entries");
                report.AppendLine("Step 4: Deleting all data entries...");
                
                int deletedRows = await ExecuteNonQueryAsync("DELETE FROM Users;");
                
                report.AppendLine($"✓ All data entries deleted ({deletedRows} rows)");
                report.AppendLine();

                // Step 5: Drop the Users table
                _logger?.LogEvent("Step 5: Dropping Users table");
                report.AppendLine("Step 5: Dropping Users table...");
                
                await ExecuteNonQueryAsync("DROP TABLE IF EXISTS Users;");
                
                report.AppendLine("✓ Users table dropped successfully");
                report.AppendLine();

                // Close connection
                if (_connection != null && _connection.State == ConnectionState.Open)
                {
                    _connection.Close();
                    _logger?.LogEvent("Database connection closed");
                    report.AppendLine("✓ Database connection closed");
                    report.AppendLine();
                }

                report.AppendLine("=== All operations completed successfully ===");
                _logger?.LogEvent("All SQLite operations completed successfully");
            }
            catch (Exception ex)
            {
                string errorMsg = $"Error during database operations: {ex.Message}";
                report.AppendLine();
                report.AppendLine($"❌ ERROR: {errorMsg}");
                report.AppendLine();
                report.AppendLine($"Stack Trace: {ex.StackTrace}");
                _logger?.LogEvent($"ERROR: {errorMsg}");
            }

            return report.ToString();
        }

        /// <summary>
        /// Checks if the database file exists
        /// </summary>
        public bool DatabaseExists()
        {
            return File.Exists(_databasePath);
        }

        public void Dispose()
        {
            try
            {
                if (_connection != null)
                {
                    if (_connection.State == ConnectionState.Open)
                    {
                        _connection.Close();
                        _logger?.LogEvent("Database connection closed during disposal");
                    }
                    _connection.Dispose();
                    _connection = null;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogEvent($"ERROR disposing SQLiteManager: {ex.Message}");
            }
        }
    }
}