using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Xml;
using HtmlDocument = HtmlAgilityPack.HtmlDocument;
using MessageBox = System.Windows.Forms.MessageBox;

namespace ITCubeRG
{
    internal class Program
    {
        private readonly string Pattern = @"^--- \/ OR\/\d{4}\/\d{2}\/\d{5}$|OF\/\d{4}\/\d{2}\/\d{5} \/ OR\/\d{4}\/\d{2}\/\d{5}$";
        public string Login { get; set; }
        public string Password { get; set; }
        public string Month { get; set; }
        public int Year { get; set; }
        public string PathToSave { get; set; }
        private string SessionId = "";
        private DateTimeFormatInfo dtfi = new CultureInfo("en-US").DateTimeFormat;
        public event Action<int> ProgressChanged;
        private int startId;
        private int endId;

        public Program()
        {
        }

        //public Program(string login, string password, string month, int year, string path)
        //{
        //    Login = login;
        //    Password = password;
        //    Month = month;
        //    Year = year;
        //    PathToSave = path;
        //}

        public async Task StartAsync()
        {
            Logger.Logger.Log.Info($"The program started working with the following parameters Login = {Login}, Year = {Year}, Month = {Month}, Path = {PathToSave}");
            var sw = new Stopwatch();
            //await Console.Out.WriteLineAsync("The program starts creating report with following parameters:");
            //SessionId = ExtractJSessionId(ConfigurationManager.AppSettings["Token"]);
            //await Console.Out.WriteLineAsync($"Year of report: {ConfigurationManager.AppSettings["Year"]}");
            //await Console.Out.WriteLineAsync($"Month of report: {ConfigurationManager.AppSettings["Month"]}");
            //await Console.Out.WriteLineAsync($"Place to save report : {ConfigurationManager.AppSettings["PathToSaveReport"]}");
            switch (Year)
            {
                case 2022:
                    {
                        startId = 12500;
                        endId = 16000;
                        break;
                    }
                case 2023:
                    {
                        startId = 15000;
                        endId = 19000;
                        break;
                    }
                case 2024:
                    {
                        startId = 17000;
                        endId = 21000;
                        break;
                    }
                default:
                    {
                        startId = 0;
                        endId = 20000;
                        break;
                    }

            }
            SessionId = await getToken();
            sw.Start();
            List<string> resultList = await Task.Run(async () => await Generate(17000, 17500));
            sw.Stop();
            if (resultList.Count != 0)
            {
                Logger.Logger.Log.Info($"Report was generated for {sw.ElapsedMilliseconds} ms");
                await WriteToFile(resultList);
                //await Console.Out.WriteLineAsync($"Success!\nTime spent : {sw.Elapsed} ");
            }
            else
            {
                Logger.Logger.Log.Info("There were no data that meet the parameters");
                throw new Exception("There is no data meet the parameters");
            }
        }

        private async Task<List<string>> Request(int id)
        {
            List<string> resultOfOneId = new List<string>();
            string numberOfOrder = "";
            DateTime dateOfOrder;
            string url = $"http://crmlog.ewabis.com.pl:8080/crm/Jsp/viewOrder.jsp;jsessionid={SessionId}?command=viewOrder&nextPage=viewOrder.jsp&mode=view&OrderId={id}";
            using (HttpClient client = new HttpClient())
            {
                try
                {
                    HttpResponseMessage response = await client.GetAsync(url);
                    if (response.IsSuccessStatusCode)
                    {
                        Logger.Logger.Log.Info("Status Code is Success");
                        string htmlContent = await response.Content.ReadAsStringAsync();
                        HtmlDocument htmlDoc = new HtmlDocument();
                        htmlDoc.LoadHtml(htmlContent);
                        HtmlNode bodyNode = htmlDoc.DocumentNode.SelectSingleNode("//body");
                        string text = bodyNode.InnerText.Trim();
                        if (text.Length != 0)
                        {
                            // Find the number of order
                            HtmlNodeCollection paragraphs = htmlDoc.DocumentNode.SelectNodes("//td[contains(b, 'Numer oferty/zam.')]/following-sibling::td");
                            if (paragraphs != null)
                            {
                                foreach (HtmlNode paragraph in paragraphs)
                                {
                                    numberOfOrder = paragraph.InnerText.ToString().Trim();
                                }
                                // Find date of the order
                                HtmlNodeCollection paragraphs2 = htmlDoc.DocumentNode.SelectNodes("//td[contains(b, 'Data zł./sp.: ')]/following-sibling::td");
                                if (Regex.IsMatch(numberOfOrder, Pattern) && paragraphs2 != null)
                                {
                                    foreach (HtmlNode paragraph in paragraphs2)
                                    {
                                        string dateTimeString = paragraph.InnerText.ToString().Trim().Substring(0, 10).Trim();
                                        if (DateTime.TryParseExact(dateTimeString.ToString(), "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out dateOfOrder))
                                        {
                                            DateTime date = DateTime.ParseExact(Month, "MMMM", dtfi);
                                            if (dateOfOrder.Year == Year && dateOfOrder.Month == date.Month)
                                            {
                                                //Going to lines
                                                List<string> nameOfProducts = GetNameOfProductList(htmlDoc);
                                                List<string> nettoPrices = GetNettoPriceList(htmlDoc);
                                                List<string> currencyList = GetCurrencyList(htmlDoc);
                                                List<string> category = GetCategoryList(nameOfProducts);

                                                if (nameOfProducts.Count == nettoPrices.Count && nettoPrices.Count == currencyList.Count)
                                                {
                                                    int count = nameOfProducts.Count;
                                                    StringBuilder stringBuilder = new StringBuilder();
                                                    for (int i = 0; i < count; i++)
                                                    {
                                                        stringBuilder.Append(numberOfOrder).Append(";").Append(dateOfOrder.ToString("yyyy-MM-dd")).Append(";").Append(nameOfProducts[i]).Append(";").Append(nettoPrices[i]).Append(";").Append(currencyList[i]).Append(";").Append(category[i]).Append(";").Append(id);
                                                        resultOfOneId.Add(stringBuilder.ToString());
                                                        stringBuilder.Clear();
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        else
                        {
                            //System.Windows.Forms.MessageBox.Show("Access token is not correct", "ERROR", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            Logger.Logger.Log.Info("Access token is not correct");
                            throw new Exception("Access token is not correct");
                        }
                    }
                    else
                    {
                        //System.Windows.Forms.MessageBox.Show($"ERROR: {response.StatusCode} - {response.ReasonPhrase}", "ERROR", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        Logger.Logger.Log.Info($"Response code is not success: {response.StatusCode} - {response.ReasonPhrase}");
                        throw new Exception($"Response code is not success: {response.StatusCode} - {response.ReasonPhrase}");
                    }
                }
                catch (Exception ex)
                {
                    //System.Windows.Forms.MessageBox.Show("Error during internet connection. ", "ERROR", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    Logger.Logger.Log.Info("Error during internet connection"+ex.ToString());
                    throw new Exception("Error during internet connection");
                }
            }
            return resultOfOneId;
        }

        private List<string> GetCategoryList(List<string> nameOfProducts)
        {
            List<string> result = new List<string>();
            foreach (var item in nameOfProducts)
            {
                if (item.StartsWith("Etykiet") || item.StartsWith("Taśma") || item.StartsWith("Winietka") || item.StartsWith("Kalka") || item.StartsWith("Tasma") || item.StartsWith("TTR"))
                {
                    result.Add("Consumables");
                }
                else if (item.StartsWith("Midmeki") || item.StartsWith("Logopak") || item.StartsWith("Drukarka") || item.StartsWith("Meki") || item.Contains("Logomatic") || item.Contains("Maszyna") || item.StartsWith("Detektor"))
                {
                    result.Add("Machine");
                }
                else if (item.StartsWith("Print") || item.StartsWith("Moduł") || item.StartsWith("Usługa") || item.StartsWith("Roboczogodzina") || item.StartsWith("Dojazd") || item.StartsWith("Płyt")
                     || item.StartsWith("dojazd") || item.StartsWith("Wizyta") || item.StartsWith("wizyta") || item.StartsWith("Płyn") || item.StartsWith("Nocleg") || item.Contains("Filter") || item.Contains("Belt") || StartsWithDigit(item)
                     || item.StartsWith("dostawa") || item.StartsWith("Dostawa") || item.StartsWith("Diet") || item.StartsWith("Hotel") || item.StartsWith("Kamera") || item.StartsWith("opakowanie") || item.StartsWith("Transport")
                     || item.StartsWith("Travel") || item.StartsWith("usługa") || item.StartsWith("Work") || item.StartsWith("Zestaw") || item.StartsWith("Tooth") || item.StartsWith("Tester") || item.StartsWith("S") || item.StartsWith("s") || item.Contains("motor")
                     || item.StartsWith("instalacja") || item.StartsWith("Bulb") || item.StartsWith("Bateria") || item.Contains("Rubber") || item.Contains("Rolka") || item.StartsWith("Cost") || item.Contains("switch") || item.Contains("gumowa") || item.Contains("Belt") || item.Contains("traveling") || item.Contains("Pasek")
                     || item.Contains("socket") || item.Contains("roller") || item.Contains("Bearing") || item.Contains("Łożysko") || item.Contains("Fotokomórka") || item.StartsWith("Delivery") || item.Contains("Głowica") || item.Contains("EPROM")
                     || item.Contains("Patyczki") || item.Contains("Roundbelt") || item.Contains("LogoCare") || item.Contains("LogoClean") || item.Contains("electronic") || item.StartsWith("Mod")
                     || item.Contains("Rurka") || item.Contains("Terminal") || item.Contains("Instalacja") || item.Contains("travel") || item.Contains("Sensor") || item.Contains("BATTERY") || item.Contains("mocujący") || item.Contains("Battery") || item.Contains("adapter") || item.Contains("adapter") || item.Contains("Supply") || item.Contains("CPU") || item.Contains("Motor"))
                {
                    result.Add("Service");
                }
                else
                {
                    result.Add("");
                }
            }
            return result;
        }

        private bool StartsWithDigit(string item)
        {
            // Проверяем, не пустая ли строка и начинается ли она с цифры
            return !string.IsNullOrEmpty(item) && char.IsDigit(item.FirstOrDefault());
        }

        private async Task<List<string>> Generate(int startId, int endId)
        {
            try
            {
                Random random = new Random();
                Stopwatch sw = new Stopwatch();
                Logger.Logger.Log.Info("The program started generating report");
                List<string> list = new List<string>();
                for (int i = startId; i <= endId; i++)
                {
                    sw.Start();
                    //if (i % 100 == 0)
                    //{
                    //    sw.Stop();
                    //    int timeLeft = ((int)sw.Elapsed.TotalSeconds * ((endId - i) / 100));
                    //    //Console.SetCursorPosition(0, Console.CursorTop - 1);
                    //    //Console.WriteLine($"Completed {i} iterations from {endId}, time left = " + (timeLeft == 0 ? ".." : timeLeft.ToString()) + " seconds ");
                    //    sw.Restart();
                    //}
                    OnProgressChanged(((i - startId) * 100) / (endId - startId));
                    List<string> tempList = await Request(i);
                    list.AddRange(tempList);
                }
                //await Console.Out.WriteLineAsync("The program ended generating report");
                return list;
            }
            catch (Exception ex)
            {
                //Console.WriteLine($"Exception in method Generate: {ex.Message}");
                //throw new Exception($"Exception in method Generate: {ex.Message}");
                throw new Exception($"Exception in method Generate. ");
            }
        }

        private List<string> GetNameOfProductList(HtmlDocument htmlDoc)
        {
            //var tdNodes = htmlDoc.DocumentNode.SelectNodes($"//tr[@class='tablelistitem']//td[@valign='top']//a");
            var tdNodes = htmlDoc.DocumentNode.SelectNodes($"//tr[@class='tablelistitem']//td[@class='maindata']//a");
            List<string> values = new List<string>();
            if (tdNodes != null)
            {
                foreach (var tdNode in tdNodes)
                {
                    values.Add(RemoveParagraphs(tdNode.InnerText.Trim()).Replace(';', ','));
                }
            }
            return values;
        }

        private List<string> GetNettoPriceList(HtmlDocument htmlDoc)
        {
            var tdNodes = htmlDoc.DocumentNode.SelectNodes("//table[@class='tablelist itchistory']//td[@valign='top'][position() = 11]");
            List<string> values = new List<string>();
            if (tdNodes != null)
            {
                foreach (var tdNode in tdNodes)
                {
                    string str = tdNode.InnerText.Trim();
                    values.Add(str.Replace(',', ' ').Replace('.', ','));
                }
            }
            return values;
        }

        private List<string> GetCurrencyList(HtmlDocument htmlDoc)
        {
            var tdNodes = htmlDoc.DocumentNode.SelectNodes("//table[@class='tablelist itchistory']//td[@valign='top'][position() = 14]");
            List<string> values = new List<string>();
            if (tdNodes != null)
            {
                foreach (var tdNode in tdNodes)
                {
                    string str = tdNode.InnerText.Trim();
                    values.Add(str);
                }
            }
            return values;
        }

        private async Task WriteToFile(List<string> lines)
        {
            //int length = str.Length;
            //string pathToSave = @"C:\folderToDel\strContent.txt";
            //using (FileStream stream = new FileStream(pathToSave, FileMode.OpenOrCreate))
            //{
            //    await stream.WriteAsync(Encoding.Default.GetBytes(str), 0, Encoding.Default.GetByteCount(str));
            //    await stream.FlushAsync();
            //    await Console.Out.WriteLineAsync("Successfuly");
            //}
            //await Console.Out.WriteLineAsync($"Length of saved string is = {File.ReadAllBytes(pathToSave).Length} ");
            string fileName = $"{DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss")}.txt";
            //string pathToSave = ConfigurationManager.AppSettings["PathToSaveReport"] + fileName;
            if (string.IsNullOrEmpty(PathToSave))
            {
                PathToSave = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName);
            }
            else
            {
                PathToSave = Path.Combine(Path.GetDirectoryName(PathToSave), fileName);

            }
            using (FileStream stream = new FileStream(PathToSave, FileMode.OpenOrCreate))
            {
                foreach (string line in lines)
                {
                    await stream.WriteAsync(Encoding.Default.GetBytes(line), 0, Encoding.Default.GetByteCount(line));
                    await stream.WriteAsync(Encoding.Default.GetBytes("\n"), 0, 1);
                }
            }
            System.Windows.Forms.MessageBox.Show($"Done! The report successfuly saved to {PathToSave}");
            // await Console.Out.WriteLineAsync($"Report is successfuly saved to {pathToSave}");
        }
        private string RemoveParagraphs(string input)
        {
            // Замена символов новой строки на пустую строку
            string result = input.Replace("\n", "");
            result = result.Replace("\r", "");
            result = result.Replace("\t", "");
            return result;
        }

        private async Task<string> getToken()
        {
            string token = "";
            string url = "http://crmlog.ewabis.com.pl:8080/crm/Jsp/commandCenterAction.jsp";
            using (HttpClient client = new HttpClient())
            {
                var formContent = new FormUrlEncodedContent(new[]
                {
                new KeyValuePair<string, string>("command", "login"),
                new KeyValuePair<string, string>("nextPage", "welcomeFrame.jsp"),
                new KeyValuePair<string, string>("login", Login),
                new KeyValuePair<string, string>("password", Password),
                new KeyValuePair<string, string>("isExplorer", "0"),
                new KeyValuePair<string, string>("isExplorer10up", "0"),
                new KeyValuePair<string, string>("isFirefox", "0"),
                new KeyValuePair<string, string>("isSafari", "1"),
                new KeyValuePair<string, string>("isMobile", "0"),
                new KeyValuePair<string, string>("isiPad", "0"),
                new KeyValuePair<string, string>("submited.x", "40"),
                new KeyValuePair<string, string>("submited.y", "8"),
                new KeyValuePair<string, string>("LoginInternalConnection", "1"),
                new KeyValuePair<string, string>("LoginReload", "1"),
            });
                HttpResponseMessage response;
                try
                {
                    response = await client.PostAsync(url, formContent);
                    // Печать статуса ответа
                    //Console.WriteLine($"Статус код: {response.StatusCode}");
                    // Печать тела ответа
                    string responseBody = await response.Content.ReadAsStringAsync();
                    //Console.WriteLine($"Тело ответа: {responseBody}");
                    int startIndex = responseBody.IndexOf("jsessionid=") + "jsessionid=".Length;
                    int endIndex = responseBody.IndexOf("?Param=True", startIndex);
                    if (startIndex >= 0 && endIndex >= 0)
                    {
                        token = responseBody.Substring(startIndex, endIndex - startIndex);
                        //await Console.Out.WriteLineAsync("Log in successfuly");
                    }
                    else
                    {
                        //System.Windows.Forms.MessageBox.Show("Session id is not found. Login or password are not correct !!! ", "ERROR", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        throw new Exception("Session id is not found. Login or password are not correct !!! ");
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception("ERROR during internet connection. ");
                }


            }


            return token;
        }
        protected virtual void OnProgressChanged(int value)
        {
            // Вызов события в основном потоке UI
            App.Current.Dispatcher.Invoke(() => ProgressChanged?.Invoke(value));
        }
    }
}
