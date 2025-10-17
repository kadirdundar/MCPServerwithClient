using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Server;
using System.ComponentModel;
using Microsoft.Extensions.Logging;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Server.Utils;

var builder = Host.CreateApplicationBuilder(settings: null);

// Global IConfiguration değişkeni
GlobalConfig.Configuration = builder.Configuration;

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

await builder.Build().RunAsync();

[McpServerToolType]
public static class EchoTool
{

    [McpServerTool, Description("Echoes in number of rows in the table sent by the client")]
    public static string getTableRowCountEcho(string tableName)


    {
        // Tablo adını doğrulamak için bir beyaz liste kullanabilirsiniz
        var allowedTables = new List<string> { "dbo.Product", "Product", "Table3" }; // Geçerli tablo adlarını buraya ekleyin
        if (!allowedTables.Contains(tableName))
        {
            return $"Error: Table {tableName} is not allowed.";
        }

        var connectionString = Configuration.GetConnectionString();
        if (string.IsNullOrEmpty(connectionString))
        {
            return "Error: Connection string 'DefaultConnection' not found.";
        }

        try
        {
            using var connection = new SqlConnection(connectionString);
            connection.Open();

            using var command = new SqlCommand($"SELECT COUNT(*) FROM {tableName}", connection);
            object result = command.ExecuteScalar();
            int count;

            if (result == null || result == DBNull.Value)
            {
                // COUNT(*) sorgusu normalde null veya DBNull döndürmemelidir.
                // Boş bir tablo için 0 döndürmesi beklenir.
                // Bu durum oluşursa, bir uyarı loglayabilir ve count'u 0 olarak ayarlayabilirsiniz.
                Console.WriteLine($"[getTableRowCountEcho] UYARI: '{tableName}' tablosu için COUNT(*) sorgusu " +
                                  $"{(result == null ? "null" : "DBNull.Value")} döndürdü. Satır sayısı 0 olarak varsayılıyor.");
                count = 0;
                // Alternatif olarak, bu beklenmedik bir durumsa bir istisna fırlatabilirsiniz:
                // throw new InvalidOperationException($"'{tableName}' tablosu için satır sayısı sorgusu beklenmedik bir şekilde null/DBNull döndürdü.");
                return $"Error: COUNT(*) query returned null/DBNull for table '{tableName}'.";
            }
            else
            {
                try
                {
                    count = Convert.ToInt32(result);
                    return $"Table '{tableName}' has {count} rows.";
                }
                catch (FormatException ex)
                {
                    // Eğer result sayısal bir değere dönüştürülemiyorsa (COUNT(*) için beklenmez)
                    Console.WriteLine($"[getTableRowCountEcho] HATA: ExecuteScalar sonucu ('{result}') int tipine dönüştürülemedi. Tablo: '{tableName}'. İstisna: {ex.ToString()}");
                    throw; // Hatayı yeniden fırlat veya uygun bir hata mesajı döndür
                }
                catch (OverflowException ex)
                {
                    // Eğer result int sınırlarının dışındaysa (COUNT(*) için çok nadir)
                    Console.WriteLine($"[getTableRowCountEcho] HATA: ExecuteScalar sonucu ('{result}') int için çok büyük/küçük. Tablo: '{tableName}'. İstisna: {ex.ToString()}");
                    throw; // Hatayı yeniden fırlat veya uygun bir hata mesajı döndür
                }
            }
            // 'count' artık satır sayısını içeriyor.
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    [McpServerTool, Description("Tests if the Microsoft SQL Server Database connection is good and alive.")]
    public static string HealthCheck()
    {

        try
        {
            using var connection = new SqlConnection(Configuration.GetConnectionString());
            connection.Open();

            return "Connection is OK";
        }
        catch (Exception e)
        {
            return $"Connection failed: {e.Message}";
        }
    }

    [McpServerTool(), Description("Get a list of all tables with their respective schema, columns and types (SQL SERVER).")]
    public static string GetSchema()
    {
        try
        {
            using var connection = new SqlConnection(Configuration.GetConnectionString());
            connection.Open();

            using var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted);
            using var command = new SqlCommand(@"
                    SELECT 
                        t.table_schema,
                        t.table_name, 
                        c.column_name, 
                        c.data_type 
                    FROM 
                        information_schema.tables t
                    JOIN 
                        information_schema.columns c 
                        ON t.table_name = c.table_name
                        AND t.table_schema = c.table_schema
                    WHERE 
                        t.table_type = 'BASE TABLE';",
                connection,
                transaction
            );
            using var reader = command.ExecuteReader();

            var tables = new Dictionary<string, List<Dictionary<string, string>>>();

            while (reader.Read())
            {
                var schemaName = reader.GetString(0);
                var tableName = reader.GetString(1);
                var columnName = reader.GetString(2);
                var columnType = reader.GetString(3);

                if (!tables.ContainsKey(tableName))
                    tables[tableName] = new List<Dictionary<string, string>>();

                tables[tableName].Add(new Dictionary<string, string>
                    {
                        { "schema", schemaName },
                        { "name", columnName },
                        { "type", columnType }
                    });
            }

            return JsonSerializer.Serialize(tables, new JsonSerializerOptions { WriteIndented = false });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }

    [McpServerTool(), Description("Get and comment on staff's score(SQL SERVER).")]

    public static string GetPerRiskByPersonName(string personName)

    {
        try
        {
            using var connection = new SqlConnection(Configuration.GetConnectionString());
            connection.Open();

            using var command = new SqlCommand(@"
            SELECT ID, PersonName, Skor, PersonId
            FROM PerRisk
            WHERE LTRIM(RTRIM(PersonName)) = @PersonName;", connection);

            command.Parameters.AddWithValue("@PersonName", personName.Trim());

            using var reader = command.ExecuteReader();

            var results = new List<Dictionary<string, object>>();

            while (reader.Read())
            {
                var row = new Dictionary<string, object>
            {
                { "ID", reader["ID"] },
                { "PersonId", reader["PersonId"] },
                { "PersonName", reader["PersonName"] },
                { "Skor", reader["Skor"] }
            };
                results.Add(row);
            }

            // Eğer hiç sonuç yoksa, özel bir mesaj döndür
            if (results.Count == 0)
            {
                return JsonSerializer.Serialize(new { error = $"'{personName}' adına sahip bir kayıt bulunamadı." });
            }

            return JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = false });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }

    [McpServerTool(), Description("Get all staff's risc scores(SQL Server).")]
    public static string GetAllPerRisk()
    {
        try
        {
            using var connection = new SqlConnection(Configuration.GetConnectionString());
            connection.Open();

            using var command = new SqlCommand(@"
            SELECT ID, PersonName, Skor, PersonId
            FROM PerRisk;", connection);

            using var reader = command.ExecuteReader();

            var results = new List<Dictionary<string, object>>();

            while (reader.Read())
            {
                var row = new Dictionary<string, object>
                {
                    { "ID", reader["ID"] },
                    { "PersonId", reader["PersonId"] },
                    { "PersonName", reader["PersonName"] },
                    { "Skor", reader["Skor"] }
                };
                results.Add(row);
            }

            return JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = false });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }

    [McpServerTool(), Description("Get all staff's risc scores details (SQL Server).")]
    public static string GetPerRiskDetails(int PersonId)
    {
        try
        {
            using var connection = new SqlConnection(Configuration.GetConnectionString());
            connection.Open();

            using var command = new SqlCommand(@"
            SELECT ID,  RiskDetail ,PersonID
      
            FROM dbo.RiskDetail where PersonID =@PersonId  ;", connection);

        
            command.Parameters.AddWithValue("@PersonId", PersonId);

            using var reader = command.ExecuteReader();

            var results = new List<Dictionary<string, object>>();

            while (reader.Read())
            {
                var row = new Dictionary<string, object>
                {
                    { "ID", reader["ID"] },
                    { "RiskDetail", reader["RiskDetail"] },
                    { "PersonID", reader["PersonID"] }
                };
                results.Add(row);
            }

            return JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = false });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }
      [McpServerTool(), Description("Get money transfer details (SQL Server).")]
     public static string GetTransferDetails(int PersonId)
    {
        try
        {
            using var connection = new SqlConnection(Configuration.GetConnectionString());
            connection.Open();

            using var command = new SqlCommand(@"
            SELECT ID,  Date , GonderenUserId,AlanUserId,Miktar ,DövizCinsi,PersonelMi
            FROM dbo.ParaTransferleri where GonderenUserId =@PersonId  or AlanUserId = @PersonId;"
            , connection);


            command.Parameters.AddWithValue("@PersonId", PersonId);

            using var reader = command.ExecuteReader();

            var results = new List<Dictionary<string, object>>();

            while (reader.Read())
            {
                var row = new Dictionary<string, object>
                {
                    { "ID", reader["ID"] },
                    { "Date", reader["Date"] },
                    { "GonderenUserId", reader["GonderenUserId"] },
                    { "AlanUserId", reader["AlanUserId"] },
                    { "Miktar", reader["Miktar"] },
                    { "DövizCinsi", reader["DövizCinsi"] },
                    { "PersonelMi", reader["PersonelMi"] }
                };
                results.Add(row);
            }

            return JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = false });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }

}

