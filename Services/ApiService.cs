using AgrolandApiSync.DTOs;
using AgrolandApiSync.Helpers;
using Newtonsoft.Json;
using Serilog;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace AgrolandApiSync.Services
{
    public class ApiService
    {
        private readonly AgrolandApiSettings _apiSettings;
        private readonly int _margin;
        private readonly string _connectionString;

        public ApiService(AgrolandApiSettings apiCredentials, int margin, string connectionString)
        {
            _apiSettings = apiCredentials;
            _margin = margin;
            _connectionString = connectionString;
        }

        public async Task SyncProducts()
        {
            using (var client = new HttpClient())
            {
                try
                {
                    int productInserted = 0;
                    int productUpdated = 0;

                    var url = $"{_apiSettings.BaseUrl}1/3/utf8/{_apiSettings.ApiKey}?stream=true";
                    Log.Information($"Sending request to {url}.");
                    var response = await client.GetAsync(url);

                    if (!response.IsSuccessStatusCode)
                    {
                        Log.Error($"API error while fetching products: {response.StatusCode}");
                        return;
                    }
                    var xml = await response.Content.ReadAsStringAsync();

                    Products apiResponse;
                    var serializer = new XmlSerializer(typeof(Products));
                    using (var reader = new StringReader(xml))
                    {
                        apiResponse = (Products)serializer.Deserialize(reader);
                    }
                    Log.Information($"Got response with {apiResponse.ProductList.Count()} products.");

                    using (SqlConnection connection = new SqlConnection(_connectionString))
                    {
                        await connection.OpenAsync();
                        Log.Information($"Attempting to update {apiResponse.ProductList.Count()} products in database.");
                        foreach (var apiProduct in apiResponse.ProductList)
                        {
                            try
                            {
                                // 1. Upsert main product data
                                using (SqlCommand cmd = new SqlCommand("dbo.UpsertProduct", connection))
                                {
                                    cmd.CommandType = CommandType.StoredProcedure;
                                    cmd.Parameters.AddWithValue("@NAZWA", apiProduct.Name ?? (object)DBNull.Value);
                                    cmd.Parameters.AddWithValue("@STAN", (object)apiProduct.Qty ?? 0);
                                    cmd.Parameters.AddWithValue("@INDEKS_KATALOGOWY", apiProduct.Ean ?? (object)DBNull.Value);
                                    cmd.Parameters.AddWithValue("@CENA_ZAKUPU_BRUTTO", (apiProduct.PriceAfterDiscountNet * 1.23m));
                                    cmd.Parameters.AddWithValue("@CENA_ZAKUPU_NETTO", (object)apiProduct.PriceAfterDiscountNet ?? 0);
                                    cmd.Parameters.AddWithValue("@CENA_SPRZEDAZY_BRUTTO", (apiProduct.PriceAfterDiscountNet * 1.23m) * ((_margin / 100m) + 1));
                                    cmd.Parameters.AddWithValue("@CENA_SPRZEDAZY_NETTO", apiProduct.PriceAfterDiscountNet * ((_margin / 100m) + 1));
                                    cmd.Parameters.AddWithValue("@VAT_ZAKUPU", apiProduct.Vat.ToString() ?? "23");
                                    cmd.Parameters.AddWithValue("@VAT_SPRZEDAZY", apiProduct.Vat.ToString() ?? "23");
                                    cmd.Parameters.AddWithValue("@KOD_KRESKOWY", apiProduct.Ean ?? (object)DBNull.Value);
                                    cmd.Parameters.AddWithValue("@WAGA", apiProduct.Weight ?? (object)DBNull.Value);
                                    cmd.Parameters.AddWithValue("@PRODUCENT", apiProduct.Brand?.Name ?? (object)DBNull.Value);
                                    cmd.Parameters.AddWithValue("@ID_PRODUCENTA", (object)apiProduct.Id ?? (object)DBNull.Value);
                                    cmd.Parameters.AddWithValue("@INTEGRATION_COMPANY", "AGROLAND");
                                    cmd.Parameters.AddWithValue("@UNIT", apiProduct.Unit ?? (object)DBNull.Value);

                                    var resultParam = cmd.Parameters.Add("@Result", SqlDbType.Int);
                                    resultParam.Direction = ParameterDirection.Output;

                                    await cmd.ExecuteNonQueryAsync();

                                    int result = (int)resultParam.Value;
                                    if (result == 1) productInserted++;
                                    else if (result == 2) productUpdated++;

                                    Log.Information($"Updated/inserted product: Product EAN = {apiProduct.Ean}, Name = {apiProduct.Name}");
                                }
                            }
                            catch (Exception ex)
                            {
                                Log.Error(ex, $"Failed to update/insert product: Product EAN = {apiProduct.Ean}, Name = {apiProduct.Name}");
                            }

                            try
                            {
                                // 2. Upsert product description
                                var opisBuilder = new StringBuilder();

                                opisBuilder.Append("<h2>Opis produktu</h2>");

                                if (!string.IsNullOrWhiteSpace(apiProduct.Desc))
                                    opisBuilder.Append($"<p>{apiProduct.Desc}</p>");

                                if (apiProduct.Attributes?.Any(a => !string.IsNullOrWhiteSpace(a)) == true)
                                    opisBuilder.Append("<p><b>Parametry: </b>")
                                               .Append(string.Join(", ", apiProduct.Attributes.Where(a => !string.IsNullOrWhiteSpace(a))))
                                               .Append("</p>");

                                string nowyOpis = TruncateHtml(opisBuilder.ToString(), 1000);

                                using (var descCmd = new SqlCommand("dbo.UpdateProductDescription", connection))
                                {
                                    descCmd.CommandType = CommandType.StoredProcedure;
                                    descCmd.Parameters.AddWithValue("@INDEKS_KATALOGOWY", apiProduct.Ean ?? (object)DBNull.Value);
                                    descCmd.Parameters.AddWithValue("@NowyOpis", nowyOpis ?? (object)DBNull.Value);
                                    await descCmd.ExecuteNonQueryAsync();
                                    Log.Information($"Updated/inserted product description: Product EAN = {apiProduct.Ean}, Name = {apiProduct.Name}");
                                }
                            }
                            catch (Exception ex)
                            {
                                Log.Error(ex, $"Failed to update/insert product description: Product EAN = {apiProduct.Ean}, Name = {apiProduct.Name}");
                            }

                            try
                            {
                                // 3. Upsert images
                                if (apiProduct.Photos != null)
                                {
                                    foreach (var img in apiProduct.Photos)
                                    {
                                        if (string.IsNullOrEmpty(img.Url)) continue;

                                        byte[] imageData = await client.GetByteArrayAsync(img.Url);

                                        using (var cmdImg = new SqlCommand("dbo.UpsertProductImage", connection))
                                        {
                                            cmdImg.CommandType = CommandType.StoredProcedure;
                                            cmdImg.Parameters.Add("@INDEKS", SqlDbType.VarChar, 20).Value = apiProduct.Ean ?? (object)DBNull.Value;
                                            cmdImg.Parameters.Add("@NAZWA_PLIKU", SqlDbType.VarChar, 100).Value = img.Id.ToString() ?? "image";
                                            cmdImg.Parameters.Add("@DANE", SqlDbType.VarBinary, -1).Value = imageData;

                                            await cmdImg.ExecuteNonQueryAsync();
                                            Log.Information($"Updated/inserted product image: Product EAN = {apiProduct.Ean}, Name = {apiProduct.Name}");
                                        }
                                    }
                                }
                            }
                            catch (HttpRequestException httpEx)
                            {
                                Log.Error(httpEx, $"Failed to download product image: Product EAN = {apiProduct.Ean}, Name = {apiProduct.Name}");
                            }
                            catch (Exception ex)
                            {
                                Log.Error(ex, $"Failed to update/insert product image: Product EAN = {apiProduct.Ean}, Name = {apiProduct.Name}");
                            }
                        }
                        Log.Information("Products imported: {Total} out of {ToUpdate}, Inserted: {Inserted}, Updated: {Updated}", productInserted + productUpdated, apiResponse.ProductList.Count(), productInserted, productUpdated);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, $"Error while fetching products");
                }
            }
        }

        private string TruncateHtml(string html, int maxLength)
        {
            if (string.IsNullOrEmpty(html) || html.Length <= maxLength)
                return html;

            // Find the last safe closing tag before the limit
            int lastLiClose = html.LastIndexOf("</li>", maxLength, StringComparison.OrdinalIgnoreCase);
            int lastPClose = html.LastIndexOf("</p>", maxLength, StringComparison.OrdinalIgnoreCase);
            int lastDivClose = html.LastIndexOf("</div>", maxLength, StringComparison.OrdinalIgnoreCase);

            // Choose the last valid cutoff point
            int cutoff = Math.Max(lastLiClose, Math.Max(lastPClose, lastDivClose));
            if (cutoff == -1) cutoff = maxLength;

            // Find actual closing tag length
            string tag = null;
            if (cutoff == lastLiClose) tag = "</li>";
            else if (cutoff == lastPClose) tag = "</p>";
            else if (cutoff == lastDivClose) tag = "</div>";

            int cutoffLength = tag?.Length ?? 0;
            if (cutoff + cutoffLength > html.Length)
                cutoffLength = 0;

            string truncated = html.Substring(0, Math.Min(cutoff + cutoffLength, html.Length));

            // Ensure all opened tags are closed properly
            var stack = new Stack<string>();
            var regex = new Regex(@"</?([a-zA-Z0-9]+)[^>]*>");
            foreach (Match match in regex.Matches(truncated))
            {
                if (!match.Value.StartsWith("</"))
                    stack.Push(match.Groups[1].Value);
                else if (stack.Count > 0 && stack.Peek().Equals(match.Groups[1].Value, StringComparison.OrdinalIgnoreCase))
                    stack.Pop();
            }

            // Close any still-open tags, but keep length <= maxLength
            while (stack.Count > 0)
            {
                string closeTag = $"</{stack.Pop()}>";
                if (truncated.Length + closeTag.Length > maxLength)
                    break; // stop if adding would exceed limit
                truncated += closeTag;
            }

            // Final safety: hard cut if still too long
            if (truncated.Length > maxLength)
                truncated = truncated.Substring(0, maxLength);

            return truncated;
        }
    }
}