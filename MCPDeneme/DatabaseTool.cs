// using System;
// using System.Collections.Generic;
// using System.ComponentModel;
// using System.Text.Json;
// using Microsoft.Data.SqlClient; // Veya System.Data.SqlClient
// using ModelContextProtocol.Server;


// // Bu sınıf, MCP tarafından discover edilmesi için [McpServerToolType] ile işaretlenir.
// [McpServerToolType]
// public static class DatabaseTools
// {
//     // Veritabanı bağlantı dizesini tutacak static alan.
//     // Bu alan uygulama başlatıldığında doldurulacaktır.
//     private static string _connectionString;

//     // Uygulama başlatıldığında bağlantı dizesini set etmek için kullanılacak internal method.
//     // MCP tool'ları static olduğu için doğrudan DI (Dependency Injection) ile configuration alamayız.
//     // Bu yöntem, host builder içinde configuration'ı alıp buraya set etmemizi sağlar.
//     internal static void SetConnectionString(string connectionString)
//     {
//         _connectionString = connectionString;
//     }

//     // Bu static method, MCP tarafından bir tool olarak discover edilecektir.
//     [McpServerTool, Description("Retrieves data from a specified table with an optional WHERE clause.")]
//     public static string GetDataFromTable(string tableName, string? condition = null)
//     {
//         if (string.IsNullOrEmpty(_connectionString))
//         {
//             // Bağlantı dizesi set edilmemişse hata fırlat.
//             throw new InvalidOperationException("Database connection string is not configured.");
//         }

//         if (string.IsNullOrEmpty(tableName))
//         {
//             throw new ArgumentException("Table name cannot be empty.", nameof(tableName));
//         }

       

//         // Sorguyu oluştur (DİKKAT: Güvenlik açığı içerebilir!)
//         string query = $"SELECT * FROM [{tableName}]"; // MSSQL'de tablo isimleri için köşeli parantez kullanımı yaygındır.
//         if (!string.IsNullOrEmpty(condition))
//         {
//             // Condition kısmını doğrudan ekliyoruz - BURASI GÜVENLİK ZAFİYETİDİR!
//             query += $" WHERE {condition}";
//         }

//         var results = new List<Dictionary<string, object>>();

//         try
//         {
//             // SqlConnection kullanarak veritabanına bağlan
//             using (var connection = new SqlConnection(_connectionString))
//             {
//                 connection.Open();

//                 // SqlCommand kullanarak sorguyu çalıştır
//                 using (var command = new SqlCommand(query, connection))
//                 {
//                     // Eğer condition'dan türetilen parametreler olsaydı, buraya eklenirdi:
//                     // command.Parameters.AddWithValue("@paramName", paramValue);

//                     // ExecuteReader ile veriyi oku
//                     using (var reader = command.ExecuteReader())
//                     {
//                         while (reader.Read())
//                         {
//                             var row = new Dictionary<string, object>();
//                             // Her satırdaki sütunları oku
//                             for (int i = 0; i < reader.FieldCount; i++)
//                             {
//                                 // DBNull.Value durumunu null olarak ele al
//                                 row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
//                             }
//                             results.Add(row);
//                         }
//                     }
//                 }
//             }

//             // Sonuçları JSON formatına serileştir
//             // JsonSerializerOptions ile çıktıyı daha okunur hale getirebilirsiniz.
//             var options = new JsonSerializerOptions { WriteIndented = true };
//             return JsonSerializer.Serialize(results, options);
//         }
//         catch (Exception ex)
//         {
//             // Hata durumunda hata mesajını logla veya döndür
//             Console.WriteLine($"Database Error: {ex.Message}");
//             // İstemciye hata mesajını döndür
//             return $"ERROR: {ex.Message}";
//         }
//     }
// }